import base64
import io
import math
import random
import time
import xml.etree.ElementTree as ET
from dataclasses import dataclass

from PIL import Image, ImageDraw


SCHEMES = {
    "ember": [(255, 52, 20), (255, 153, 38), (255, 229, 122), (82, 10, 8)],
    "aurora": [(26, 255, 180), (74, 151, 255), (164, 93, 255), (10, 30, 58)],
    "ocean": [(15, 55, 100), (25, 174, 221), (160, 245, 238), (2, 15, 30)],
    "monochrome": [(245, 245, 230), (160, 165, 155), (70, 75, 70), (8, 9, 8)],
}


@dataclass
class Flame:
    name: str
    transforms: list[dict]
    palette: list[tuple[int, int, int]]
    novelty_score: int

    def to_xml(self) -> str:
        root = ET.Element("flames")
        flame = ET.SubElement(root, "flame", {
            "name": self.name,
            "version": "Apophysis 7X",
            "size": "800 600",
            "center": "0 0",
            "scale": "100",
            "rotate": "0",
            "oversample": "1",
            "filter": "0.01",
            "quality": "200",
            "background": "0 0 0",
            "brightness": "4",
            "gamma": "4",
            "gamma_threshold": "0.01",
            "vibrancy": "1",
            "hue": "0",
        })
        for transform in self.transforms:
            a, b, c, d = transform["coefs"]
            e, f = transform["post"]
            attrs = {
                "weight": _number(transform["weight"]),
                "color": _number(transform["color"]),
                "a": _number(a), "b": _number(b), "c": _number(c), "d": _number(d),
                "e": _number(e), "f": _number(f),
            }
            for variation in ("linear", "swirl", "horseshoe", "sinusoidal", "polar"):
                attrs[f"var_{variation}"] = "1" if variation == transform["variation"] else "0"
            ET.SubElement(flame, "xform", attrs)
        palette = ET.SubElement(flame, "palette", {"count": "256", "format": "RGB"})
        palette.text = "".join(f"{r:02x}{g:02x}{b:02x}" for r, g, b in _expand_palette(self.palette, 256))
        return ET.tostring(root, encoding="unicode")

    def xml_bytes(self) -> io.BytesIO:
        return io.BytesIO(self.to_xml().encode("utf-8"))

    def preview_data_uri(self) -> str:
        image = Image.new("RGB", (800, 600), (3, 4, 7))
        pixels = image.load()
        for transform in self.transforms:
            x, y = 0.0, 0.0
            color = transform["color"]
            for _ in range(30000):
                x, y = _iterate(x, y, transform)
                if abs(x) > 4 or abs(y) > 4:
                    x, y = 0.0, 0.0
                    continue
                px = int(400 + x * 125)
                py = int(300 - y * 125)
                if 0 <= px < 800 and 0 <= py < 600:
                    old = pixels[px, py]
                    target = self.palette[int((color * 3) % len(self.palette))]
                    pixels[px, py] = tuple(min(255, int(old[i] * 0.82 + target[i] * 0.18)) for i in range(3))
        output = io.BytesIO()
        image.save(output, format="PNG", optimize=True)
        return "data:image/png;base64," + base64.b64encode(output.getvalue()).decode("ascii")


class FlameGenerator:
    scheme_names = list(SCHEMES)

    def __init__(self, seed=None):
        self.random = random.Random(seed)
        self.recent_fingerprints = []

    def generate(self, scheme="ember") -> Flame:
        if scheme not in SCHEMES:
            raise ValueError(f"Unknown color scheme: {scheme}")
        best = None
        for _ in range(30):
            transforms = self._random_transforms()
            fingerprint = self._fingerprint(transforms)
            distance = min((self._distance(fingerprint, old) for old in self.recent_fingerprints), default=1.0)
            if best is None or distance > best[0]:
                best = (distance, fingerprint, transforms)
            if distance >= 0.22:
                break
        distance, fingerprint, transforms = best
        self.recent_fingerprints.append(fingerprint)
        self.recent_fingerprints = self.recent_fingerprints[-64:]
        return Flame(
            name=f"apophysis_{time.strftime('%Y%m%d_%H%M%S')}_{self.random.randrange(1000, 9999)}",
            transforms=transforms,
            palette=SCHEMES[scheme],
            novelty_score=max(1, min(10, round(distance * 28))),
        )

    def _random_transforms(self):
        transforms = []
        variations = ["linear", "swirl", "horseshoe", "sinusoidal", "polar"]
        for index in range(self.random.randint(3, 5)):
            angle = self.random.uniform(-math.pi, math.pi)
            scale = self.random.uniform(0.42, 0.92)
            transform = {
                "weight": self.random.uniform(0.2, 1.0),
                "color": index / 4,
                "coefs": (math.cos(angle) * scale, -math.sin(angle) * scale,
                          math.sin(angle) * scale, math.cos(angle) * scale),
                "post": (self.random.uniform(-0.7, 0.7), self.random.uniform(-0.7, 0.7)),
                "variation": self.random.choice(variations),
            }
            transforms.append(transform)
        return transforms

    @staticmethod
    def _fingerprint(transforms):
        return tuple(round(value, 2) for transform in transforms for value in (
            transform["weight"], *transform["coefs"], *transform["post"],
        )) + tuple(transform["variation"] for transform in transforms)

    @staticmethod
    def _distance(first, second):
        if len(first) != len(second):
            return 1.0
        numeric = sum(abs(a - b) for a, b in zip(first, second) if isinstance(a, float))
        return min(1.0, numeric / (len(first) * 1.5))


def _iterate(x, y, transform):
    a, b, c, d = transform["coefs"]
    nx, ny = a * x + b * y, c * x + d * y
    variation = transform["variation"]
    if variation == "swirl":
        radius = nx * nx + ny * ny
        nx, ny = nx * math.sin(radius) - ny * math.cos(radius), nx * math.cos(radius) + ny * math.sin(radius)
    elif variation == "horseshoe":
        radius = math.hypot(nx, ny) + 1e-6
        nx, ny = (nx - ny) * (nx + ny) / radius, 2 * nx * ny / radius
    elif variation == "sinusoidal":
        nx, ny = math.sin(nx), math.sin(ny)
    elif variation == "polar":
        nx, ny = math.atan2(nx, ny) / math.pi, math.hypot(nx, ny) - 1
    px, py = transform["post"]
    return nx + px, ny + py


def _expand_palette(colors, size):
    result = []
    for index in range(size):
        position = index / (size - 1) * (len(colors) - 1)
        left = min(len(colors) - 2, int(position))
        amount = position - left
        result.append(tuple(int(colors[left][channel] * (1 - amount) + colors[left + 1][channel] * amount) for channel in range(3)))
    return result


def _number(value):
    if isinstance(value, tuple):
        return " ".join(_number(item) for item in value)
    return f"{value:.6f}" if isinstance(value, float) else str(value)
