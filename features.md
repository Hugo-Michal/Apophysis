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
