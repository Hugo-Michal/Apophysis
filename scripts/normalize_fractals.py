"""Normalize fractal artwork to contrast-preserving white-background grayscale.

The transformation is deterministic for a given input image:

1. Apply EXIF orientation and composite any alpha onto white.
2. Convert RGB to perceived luminance instead of averaging channels.
3. Detect whether the image border is dark; invert only when needed so the
   detected background becomes light.
4. Stretch robust foreground percentiles instead of using min/max, which
   avoids a few hot pixels flattening the rest of the fractal.
5. Apply a mild adaptive gamma curve and snap near-background pixels to white.

The output is an 8-bit, single-channel PNG with the original pixel dimensions.
"""

from __future__ import annotations

import argparse
import json
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Iterable

import numpy as np
from PIL import Image, ImageOps


SUPPORTED_EXTENSIONS = {".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff", ".webp"}


@dataclass
class ImageReport:
    input_file: str
    output_file: str
    width: int
    height: int
    inverted: bool
    border_luminance: float
    global_median_luminance: float
    low_percentile: float
    high_percentile: float
    gamma: float
    input_mean: float
    output_mean: float


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, required=True, help="Folder containing source fractal images")
    parser.add_argument("--output", type=Path, required=True, help="New folder for normalized PNG images")
    parser.add_argument("--limit", type=int, default=0, help="Process only the first N files; 0 means all")
    parser.add_argument("--overwrite", action="store_true", help="Replace existing output PNGs")
    return parser.parse_args()


def image_files(folder: Path) -> list[Path]:
    return sorted(
        path for path in folder.iterdir()
        if path.is_file() and path.suffix.lower() in SUPPORTED_EXTENSIONS
    )


def load_rgb(path: Path) -> np.ndarray:
    with Image.open(path) as source:
        image = ImageOps.exif_transpose(source)
        if "A" in image.getbands() or "transparency" in image.info:
            rgba = image.convert("RGBA")
            background = Image.new("RGBA", rgba.size, (255, 255, 255, 255))
            background.alpha_composite(rgba)
            image = background.convert("RGB")
        else:
            image = image.convert("RGB")
        return np.asarray(image, dtype=np.float32)


def luminance(rgb: np.ndarray) -> np.ndarray:
    # Rec. 709 luma: a perceived-brightness mapping that preserves more useful
    # structure than an unweighted RGB average for saturated fractal colors.
    return (
        0.2126 * rgb[..., 0]
        + 0.7152 * rgb[..., 1]
        + 0.0722 * rgb[..., 2]
    )


def border_pixels(values: np.ndarray, fraction: float = 0.05) -> np.ndarray:
    height, width = values.shape
    border_height = max(1, int(round(height * fraction)))
    border_width = max(1, int(round(width * fraction)))
    parts = (
        values[:border_height, :].ravel(),
        values[-border_height:, :].ravel(),
        values[:, :border_width].ravel(),
        values[:, -border_width:].ravel(),
    )
    return np.concatenate(parts)


def normalize(values: np.ndarray) -> tuple[np.ndarray, dict[str, float | bool]]:
    border = border_pixels(values)
    border_median = float(np.median(border))
    global_median = float(np.median(values))

    # A dark border is the strongest available signal that the source uses a
    # black canvas. A bright border is kept bright; this avoids turning a
    # source image that already has a white background black.
    inverted = border_median < 128.0
    tone = 255.0 - values if inverted else values.copy()
    tone_border = 255.0 - border if inverted else border
    background_level = float(np.median(tone_border))

    spread = float(np.percentile(tone, 99.5) - np.percentile(tone, 0.5))
    foreground_threshold = background_level - max(6.0, spread * 0.025)
    foreground = tone[tone < foreground_threshold]
    if foreground.size < max(256, tone.size // 100):
        foreground = tone.ravel()

    low = float(np.percentile(foreground, 0.5))
    high = float(np.percentile(foreground, 99.5))
    if high - low < 8.0:
        low = float(np.percentile(tone, 0.5))
        high = float(np.percentile(tone, 99.5))
    if high - low < 1.0:
        high = low + 1.0

    normalized = np.clip((tone - low) / (high - low), 0.0, 1.0)
    foreground_mean = float(np.mean(normalized[normalized < 0.98])) if np.any(normalized < 0.98) else 0.5
    gamma = float(np.clip(1.30 + max(0.0, foreground_mean - 0.48) * 0.75, 1.30, 1.70))
    normalized = np.power(normalized, gamma)

    # Make the estimated canvas genuinely white while retaining darker lines
    # and gradients that are clearly below the background level.
    white_threshold = background_level - max(5.0, (high - low) * 0.02)
    normalized[tone >= white_threshold] = 1.0
    result = np.rint(np.clip(normalized * 255.0, 0.0, 255.0)).astype(np.uint8)

    metadata: dict[str, float | bool] = {
        "inverted": inverted,
        "border_luminance": border_median,
        "global_median_luminance": global_median,
        "low_percentile": low,
        "high_percentile": high,
        "gamma": gamma,
    }
    return result, metadata


def process_one(input_path: Path, output_path: Path) -> ImageReport:
    rgb = load_rgb(input_path)
    gray = luminance(rgb)
    normalized, metadata = normalize(gray)
    Image.fromarray(normalized, mode="L").save(output_path, format="PNG", optimize=True)
    return ImageReport(
        input_file=input_path.name,
        output_file=output_path.name,
        width=int(normalized.shape[1]),
        height=int(normalized.shape[0]),
        input_mean=float(np.mean(gray)),
        output_mean=float(np.mean(normalized)),
        **metadata,
    )


def main() -> int:
    args = parse_args()
    input_dir = args.input.resolve()
    output_dir = args.output.resolve()
    if not input_dir.is_dir():
        raise SystemExit(f"Input folder does not exist: {input_dir}")
    output_dir.mkdir(parents=True, exist_ok=True)

    paths = image_files(input_dir)
    if args.limit > 0:
        paths = paths[:args.limit]
    if not paths:
        raise SystemExit(f"No supported images found in {input_dir}")

    reports: list[ImageReport] = []
    failures: list[dict[str, str]] = []
    for index, input_path in enumerate(paths, start=1):
        output_path = output_dir / f"{input_path.stem}.png"
        if output_path.exists() and not args.overwrite:
            continue
        try:
            reports.append(process_one(input_path, output_path))
            print(f"[{index}/{len(paths)}] {input_path.name}")
        except Exception as exc:  # keep one malformed source from stopping a batch
            failures.append({"input_file": input_path.name, "error": str(exc)})
            print(f"[{index}/{len(paths)}] FAILED {input_path.name}: {exc}")

    report_path = output_dir / "normalization_report.json"
    report_path.write_text(
        json.dumps(
            {
                "input_folder": str(input_dir),
                "output_folder": str(output_dir),
                "method": "rec709_luminance -> adaptive border inversion -> percentile contrast -> gamma -> white background",
                "processed": len(reports),
                "failures": failures,
                "images": [asdict(report) for report in reports],
            },
            indent=2,
        ),
        encoding="utf-8",
    )
    print(f"Processed {len(reports)} images; failures: {len(failures)}")
    print(f"Report: {report_path}")
    return 0 if not failures else 2


if __name__ == "__main__":
    raise SystemExit(main())
