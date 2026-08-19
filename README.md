# Native Fractal-Flame Curator

This repository contains the Phase 1 native Windows desktop application for
generating Apophysis-compatible flame genomes and rating rendered images by
hand. Phase 2 AI scoring is intentionally not present.

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
  does not stop rendering.
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
sample budget controls the number of histogram samples; the default is 5 million
and the UI permits up to 500 million for long CPU renders. Gamma, brightness, and
vibrancy affect the tone map. The GUI displays this backend and does not label
the CPU fallback as GPU acceleration.
