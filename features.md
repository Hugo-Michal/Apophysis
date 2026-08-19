# Features

This is the shared feature log for the new native Windows fractal-flame curator.
Every agent must add the newest entry at the top whenever behavior changes.

## Entry format

```text
### YYYY-MM-DD - Feature name
- Change: What was added, changed, or removed.
- Files: Main files affected.
- Notes: User-visible behavior or follow-up work.
```

## Current features

### 2026-08-19 - CUDA runtime verification and Python version pin
- Change: Pin the Windows Python launcher used by the DINOv2 worker to Python 3.12, which is supported by the selected PyTorch Windows runtime, and verified PyTorch 2.5.1+cu118 plus DINOv2 ViT-B/14 on `cuda:0` with the installed GTX 1060 6GB.
- Files: `src/FractalFlameCurator/Ai/PreferenceScoringBackend.cs`.
- Notes: The app now uses the installed CUDA-capable runtime rather than the machine’s default Python 3.13 interpreter.

### 2026-08-19 - Optional DINOv2 preference scorer
- Change: Added an optional CUDA-only PyTorch DINOv2 ViT-B/14 preference scorer with a frozen backbone, four-threshold ordinal head, expected-rating/0–1 score conversion, stable fixed-width score prefixes, model replacement, candidate rescoring, and independent background scoring controls.
- Files: `src/FractalFlameCurator/Ai`, `src/FractalFlameCurator/Models/PreferenceScoringModels.cs`, `src/FractalFlameCurator/Pipeline/ContinuousAiScoringService.cs`, `src/FractalFlameCurator/Pipeline/CandidateCatalog.cs`, `src/FractalFlameCurator/Storage`, `src/FractalFlameCurator/MainWindow.xaml`, `src/FractalFlameCurator/MainWindow.xaml.cs`, `tests/FractalFlameCurator.Tests/PhaseTwoTests.cs`.
- Notes: Human rating folders remain the source of truth and are never changed by AI. Training is available for small or empty corpora with explicit unreliable-metrics warnings. CUDA, GPU, PyTorch version, active device, model status, dataset readiness bars, validation metrics, and evaluation-only controls are reported. The bundled Python worker refuses CPU AI execution; manual rendering/rating remain operational when Python/PyTorch/CUDA is unavailable.

### 2026-08-19 - Default quality and undo navigation correction
- Change: Set the default sample budget to 20,000,000 points and fixed Undo navigation so the image that was current before Undo is retained and shown after the undone image is rated or skipped.
- Files: `src/FractalFlameCurator/Models/FlameGenome.cs`, `src/FractalFlameCurator/MainWindow.xaml.cs`, `src/FractalFlameCurator/Generation/FlameGenerator.cs`, `src/FractalFlameCurator/Serialization/FlameXmlSerializer.cs`.
- Notes: The explicit maximum remains 500,000,000 points.

### 2026-08-19 - Higher sample-budget quality control
- Change: Raised the practical default sample budget to 20,000,000 points, allowed explicit budgets up to 500,000,000, displayed live sample progress, and stored the selected quality/tone settings in each source `.flame` file.
- Files: `src/FractalFlameCurator/Models/FlameGenome.cs`, `src/FractalFlameCurator/Pipeline/ContinuousRenderService.cs`, `src/FractalFlameCurator/MainWindow.xaml.cs`, `tests/FractalFlameCurator.Tests/PhaseOneTests.cs`.
- Notes: 500,000,000 points is intentionally a long-running CPU render; the UI no longer silently reduces that value to 20,000,000.

### 2026-08-19 - Viewport-fit and pointer zoom
- Change: The preview now fits each square render to the available viewport, refits when the viewport changes, and supports mouse-wheel zoom anchored to the pointer location. Actual size and Zoom to fit remain available.
- Files: `src/FractalFlameCurator/MainWindow.xaml`, `src/FractalFlameCurator/MainWindow.xaml.cs`.
- Notes: Zooming uses the rendered image dimensions and keeps the pointed-to detail under the cursor when scrolling is available.

### 2026-08-19 - Higher-resolution inverted monochrome output
- Change: Raised the default square render from 1024×1024 to 2048×2048, changed the default monochrome output to black fractal ink on a white background, and fixed dropdown controls to use black text on a white popup/background.
- Files: `src/FractalFlameCurator/Models/FlameGenome.cs`, `src/FractalFlameCurator/Rendering/CpuFlameRenderer.cs`, `src/FractalFlameCurator/App.xaml`, `src/FractalFlameCurator/MainWindow.xaml`, `tests/FractalFlameCurator.Tests/PhaseOneTests.cs`.
- Notes: Oversample remains an additional render-time multiplier above the new 2048×2048 base.

### 2026-08-19 - Phase 1 native curator
- Change: Added a WPF/.NET 8 desktop application with deterministic seeded flame generation, Apophysis 7X-style XML serialization, a broad audited variation registry, a bounded background render queue, truthful CPU backend reporting, 2048x2048 default rendering, and manual five-star rating with skip, undo, and safe re-rating.
- Files: `src/FractalFlameCurator`, `tests/FractalFlameCurator.Tests`, `README.md`.
- Notes: The built-in renderer is a deterministic managed CPU implementation because no flam3/Apophysis executable is installed. It exposes real sample-budget, oversample, filter-radius, gamma, brightness, vibrancy, and palette controls. GPU rendering and Phase 2 AI scoring are not implemented.

### 2026-08-19 - Previous implementation removed
- Change: Removed the old Flask application, heuristic evaluator, genetic algorithm, source scripts, UI files, and dependency files as the starting point for a clean rebuild.
- Files: Previous implementation source files.
- Notes: The destructive shell guard prevented removal of generated binary/data directories; they are not part of the new design and must be cleared before implementation. Preserve only this feature log and `agents.md` as project guidance. The first implementation is manual curation; AI scoring is a separate second phase.
