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

### 2026-08-20 - Viewport refresh after render and image replacement
- Change: Existing candidates now appear immediately when a render session starts, and replaced PNGs are loaded from a fresh stream so re-rendered images refresh reliably in the viewport.
- Files: `src/FractalFlameCurator/MainWindow.xaml.cs`.
- Notes: The current image remains visible while background rendering continues; users can rate it or choose Next to inspect another completed candidate.

### 2026-08-20 - Image tone-control drawer and safe current-flame re-render
- Change: Added an expandable Image settings drawer with palette, sample budget, oversample, filter radius, gamma, brightness, vibrancy, white/black points, contrast curve, and low-density cutoff. Added an explicit current-flame re-render action with cancellation and temporary-file replacement.
- Files: `src/FractalFlameCurator/MainWindow.xaml`, `src/FractalFlameCurator/MainWindow.xaml.cs`, `src/FractalFlameCurator/Models/FlameGenome.cs`, `src/FractalFlameCurator/Rendering/ToneMapper.cs`, `src/FractalFlameCurator/Rendering/ArtifactRerenderer.cs`, `tests/FractalFlameCurator.Tests/PhaseOneTests.cs`.
- Notes: Changing Image values does not alter the viewport until Re-render current flame is pressed. Re-rendering preserves the source `.flame` file and replaces the PNG only after a successful render; cancellation/failure leaves the old image in place. Any cached AI score for the changed image is invalidated. Monochrome remains the explicit default.

### 2026-08-20 - Paired PNG and FLAME candidate storage
- Change: Rating now moves the rendered PNG and matching `.flame` together, AI rescoring renames both files with the same fixed-width score prefix, and undo/re-rating operate on complete pairs.
- Files: `src/FractalFlameCurator/Storage/SourceArchive.cs`, `src/FractalFlameCurator/Storage/RatingStore.cs`, `src/FractalFlameCurator/Pipeline/ContinuousAiScoringService.cs`, `src/FractalFlameCurator/MainWindow.xaml.cs`, `tests/FractalFlameCurator.Tests`.
- Notes: Rating folders now contain only matched PNG/`.flame` pairs. Existing workspace files were repaired separately; unmatched orphan `.flame` files were removed after the audit.

### 2026-08-19 - Render-session source ID collision fix
- Change: Added a unique render-session suffix to generated source IDs and made candidate catalog refresh tolerate duplicate legacy IDs without crashing.
- Files: `src/FractalFlameCurator/Storage/SourceArchive.cs`, `src/FractalFlameCurator/Pipeline/ContinuousRenderService.cs`, `src/FractalFlameCurator/Pipeline/CandidateCatalog.cs`, `tests/FractalFlameCurator.Tests/PhaseTwoTests.cs`.
- Notes: Existing rendered files are not deleted or renamed by this fix. When a legacy duplicate is encountered, the newest complete image is selected deterministically.

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
