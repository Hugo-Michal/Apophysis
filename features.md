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

### 2026-08-22 - Idle CPU, pause, and integrity audit
- Change: Removed render-frame and repeated full-workspace polling from the idle UI path, changed catalog/rating lookups to indexed linear scans, slowed the AI watcher fallback scan, and made Pause stop active CPU sampling. Also preserved undo history when reusing a workspace, made rating moves roll back both files on publication failure, validated pairs within each star folder, rendered imported final transforms, retained legacy total-sample quality values, and corrected ordinal calibration/Spearman calculations.
- Files: `src/FractalFlameCurator/MainWindow.xaml.cs`, `src/FractalFlameCurator/Pipeline`, `src/FractalFlameCurator/Storage/RatingStore.cs`, `src/FractalFlameCurator/Rendering/CpuFlameRenderer.cs`, `src/FractalFlameCurator/Generation/FlameValidator.cs`, `src/FractalFlameCurator/Serialization/FlameXmlSerializer.cs`, `src/FractalFlameCurator/Ai`, `tests/FractalFlameCurator.Tests`.
- Notes: With the existing 2,199-image workspace initialized and rendering stopped, the final WPF build used 0.031 CPU seconds during an 8.01-second steady-state sample (about 0.03% of the 12-logical-processor machine). Workspace refresh remains event-driven; active render progress updates four times per second without disk enumeration.

### 2026-08-20 - Apophysis-compatible flame serialization
- Change: Corrected saved `.flame` files to use declaration-free UTF-8 XML, native `coefs` affine attributes, direct variation attributes, `hue_rotation`, and Apophysis samples-per-pixel quality derived from the curator's total sample budget.
- Files: `src/FractalFlameCurator/Serialization/FlameXmlSerializer.cs`, `tests/FractalFlameCurator.Tests/PhaseOneTests.cs`.
- Notes: The reader remains backward-compatible with the previously emitted `a`–`f` and `var_*` attributes, while new files follow the Apophysis 7X/AV-compatible dialect. The curator's internal sample budget remains unchanged.

### 2026-08-20 - Rated dataset and existing-render AI rescoring
- Change: Rated rescoring now processes every rated PNG, including legacy PNG-only entries, updates the fixed-width score prefix, and renames a matching `.flame` together with its image when available. Starting AI scoring clears the prior candidate-suppression cache so existing rendered files are rescored on every new session.
- Files: `src/FractalFlameCurator/Storage/RatingStore.cs`, `src/FractalFlameCurator/Pipeline/ContinuousAiScoringService.cs`, `src/FractalFlameCurator/MainWindow.xaml`, `src/FractalFlameCurator/MainWindow.xaml.cs`, `tests/FractalFlameCurator.Tests/PhaseTwoTests.cs`.
- Notes: Rating folders are preserved; rescoring changes only score prefixes and never deletes or moves an image between rating folders. Unpaired legacy PNGs remain unpaired.

### 2026-08-20 - Disabled render-toggle contrast
- Change: Added an explicit dark disabled-button style so the Pause/Resume render toggle remains gray and readable instead of switching to the WPF white disabled appearance.
- Files: `src/FractalFlameCurator/App.xaml`.
- Notes: The style applies consistently to disabled action buttons, including while rated-frame re-rendering locks the Rendering controls.

### 2026-08-20 - Parallel rated re-render and render-session toggles
- Change: Rated-frame replacement now runs with the configured Rendering Workers count, reports the active worker count, and disables the Rendering controls while the batch is active. Consolidated Start/Stop and Pause/Resume into two stateful toggle buttons.
- Files: `src/FractalFlameCurator/MainWindow.xaml`, `src/FractalFlameCurator/MainWindow.xaml.cs`.
- Notes: The Image drawer’s render settings still control every rated render; cancellation preserves already-completed replacements. Keyboard P and Esc continue to toggle pause and stop the render session.

### 2026-08-20 - AI rated-dataset rescoring
- Change: Reduced AI Scoring actions to Start, Stop, Train Model, and Rescore rated. Added a cancellable action that scores complete rated PNG/.flame pairs with the current AI model without renaming, moving, deleting, or changing their human ratings.
- Files: `src/FractalFlameCurator/MainWindow.xaml`, `src/FractalFlameCurator/MainWindow.xaml.cs`, `src/FractalFlameCurator/Pipeline/ContinuousAiScoringService.cs`, `tests/FractalFlameCurator.Tests/PhaseTwoTests.cs`.
- Notes: Rated rescoring uses the existing CUDA/DINOv2 backend and leaves the five human-rating folders intact. Pause/Resume remain available internally but are no longer exposed as menu actions.

### 2026-08-20 - Simplified AI training warning
- Change: Removed the “Do not show the small-data warning again” checkbox and the modal training warning dialog from the AI Scoring drawer workflow.
- Files: `src/FractalFlameCurator/MainWindow.xaml`, `src/FractalFlameCurator/MainWindow.xaml.cs`.
- Notes: The persistent small/imbalanced-corpus warning remains visible in the menu and training still proceeds without an extra confirmation dialog.

### 2026-08-20 - Closed dropdown contrast and startup drawer defaults
- Change: Added an explicit dark ComboBox field template so the selected Oversample and Palette values remain visible in their closed boxes, changed default Gamma to `1` and Black point to `0.85`, and collapsed Image, Dataset Statistics, and Diagnostics drawers at startup.
- Files: `src/FractalFlameCurator/App.xaml`, `src/FractalFlameCurator/MainWindow.xaml`, `src/FractalFlameCurator/MainWindow.xaml.cs`.
- Notes: Rendering and AI Scoring remain expanded by default; dropdown popup and tooltip contrast remain dark with light text.

### 2026-08-20 - Palette control alignment
- Change: Matched the Palette dropdown to the other Image controls by using the same stacked label/input layout, width, spacing, and shared ComboBox style.
- Files: `src/FractalFlameCurator/MainWindow.xaml`.
- Notes: Palette names and selection behavior are unchanged.

### 2026-08-20 - Explicit palette labels
- Change: Replaced the Palette dropdown display-member lookup with an explicit text template so palette names render clearly in both the selected value and popup list.
- Files: `src/FractalFlameCurator/MainWindow.xaml`.
- Notes: Palette behavior and the monochrome default are unchanged.

### 2026-08-20 - Dark dropdown and tooltip presentation
- Change: Set the Image drawer dropdowns and tooltips to a dark background with light text so their contrast remains visible under the WPF theme.
- Files: `src/FractalFlameCurator/App.xaml`.
- Notes: Oversample and Palette keep their existing labels and behavior; this change affects presentation only.

### 2026-08-20 - Explicit dropdown contrast and compact drawer layout
- Change: Replaced the default ComboBox item and ToolTip presentation with explicit black-on-white templates, widened the settings drawer, tightened shared control spacing, and reorganized Rendering and Image settings into denser grids.
- Files: `src/FractalFlameCurator/App.xaml`, `src/FractalFlameCurator/MainWindow.xaml`.
- Notes: Font sizes and controls were retained. The Image drawer keeps all settings and actions while reducing vertical space; the left panel can show more content before its vertical scrollbar is needed.

### 2026-08-20 - Compact image actions and rated-flame replacement
- Change: Fixed Image drawer combo-box text contrast, compacted action rows without reducing font sizes, merged current re-render cancellation into the same toggle button, and added a cancellable batch action for re-rendering all complete rated PNG/.flame pairs.
- Files: `src/FractalFlameCurator/App.xaml`, `src/FractalFlameCurator/MainWindow.xaml`, `src/FractalFlameCurator/MainWindow.xaml.cs`, `src/FractalFlameCurator/Storage/RatingStore.cs`, `tests/FractalFlameCurator.Tests/PhaseOneTests.cs`.
- Notes: The batch action replaces rated PNGs in their existing `ratings/1` through `ratings/5` folders, preserves star assignments and source `.flame` files, and does not save selected image settings back into source genomes. The current and batch buttons both switch to cancellation while active.

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
