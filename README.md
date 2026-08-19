# Native Fractal-Flame Curator

This repository contains the native Windows desktop application for generating
Apophysis-compatible flame genomes, rating rendered images by hand, and
optionally ranking candidates according to those human preferences. Manual
ratings remain the source of truth; AI never accepts, deletes, hides, or edits
human rating folders.

## Build and run

The repository pins the SDK in `global.json`. On a machine with the .NET 8 SDK:

```powershell
dotnet build .\FractalFlameCurator.sln
dotnet test .\FractalFlameCurator.sln
dotnet run --project .\src\FractalFlameCurator\FractalFlameCurator.csproj
```

The application writes `rendered/` and `ratings/1` through `ratings/5` below
the selected output directory. Each source genome and its rendered PNG share a
stable basename. Rating folders contain copied PNG files only; the source
archive is never removed by rating or undo.

## Architecture

- `Generation` creates seeded genomes, validates bounded affine/variation
  parameters, and exposes the complete variation registry sampled by the
  generator.
- `Serialization` reads and writes deterministic Apophysis 7X-style XML,
  including `seed`, affine coefficients, `var_*` weights, post transforms, and
  a 256-color palette.
- `Rendering` contains the built-in deterministic CPU flame renderer. It uses
  a bounded managed worker pool at the session layer and reports CPU honestly;
  it does not claim GPU support.
- `Pipeline` provides a bounded producer queue, pause/resume/stop cancellation,
  batch limits, completion/failure counts, and a ready-image queue so rating
  does not stop rendering. The independent AI scoring service watches complete
  rendered PNG/`.flame` pairs, preserves low-scoring candidates, and renames
  only rendered candidates with fixed-width score prefixes.
- `Ai` snapshots the five human rating folders, creates stable-source grouped
  train/validation/test splits, runs a frozen DINOv2 ViT-B/14 backbone through
  the bundled PyTorch worker, and trains a four-threshold ordinal preference
  head. AI is CUDA-only and reports truthful Python/PyTorch/CUDA/device
  diagnostics; manual rendering and rating remain available without it.
- `Storage` keeps source PNG/`.flame` pairs separate from image-only rating
  folders and implements safe re-rating plus one-step undo.
- `MainWindow.xaml` is the WPF manual-curation shell. Quality controls are
  visible and truthful: sample budget, actual oversample, filter radius, gamma,
  brightness, vibrancy, and palette. The preview fits the viewport by default;
  mouse-wheel zoom is anchored to the pointer, with explicit fit and actual-size
  controls.

## Renderer setup and controls

No flam3 or Apophysis executable is assumed to be installed. The selected
renderer is therefore the bundled managed CPU renderer. Its `oversample`
setting renders at an integer multiple of the requested dimensions and applies
the configured filter radius while reducing to the final 2048x2048 image. The
sample budget controls the number of histogram samples; the default is 20 million
and the UI permits up to 500 million for long CPU renders. Gamma, brightness, and
vibrancy affect the tone map. The GUI displays this backend and does not label
the CPU fallback as GPU acceleration.

## Optional AI scorer

AI scoring requires a Windows Python installation with PyTorch, CUDA, and the
usual image dependencies (`Pillow`). The first DINOv2 use loads the official
`dinov2_vitb14` pretrained backbone through PyTorch Hub. The application refuses
to run AI inference or training on CPU and reports the reason in Diagnostics.
The score is a learned preference estimate: four cumulative probabilities are
converted to an expected rating and then to `(expected_rating - 1) / 4` in the
range 0–1. Evaluation-only controls may be placed under `controls/<name>/*.png`;
they are never added to human training labels.
