# Project file inventory

Reviewed 2026-08-22 after the approved cleanup and release verification.
“Runtime” below means required to build the application from source; a
published end-user copy needs only the published app files and
`Ai/dinov2_service.py`.

## Runtime flow

| Flow | Files involved | Description |
|---|---|---|
| Startup/UI | `App.xaml`, `App.xaml.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs` | Starts WPF, owns the controls, initializes diagnostics, and coordinates rendering, rating, re-rendering, and AI actions. |
| Generate | `Generation/DeterministicRandom.cs`, `FlameGenerator.cs`, `FlameValidator.cs`; `Models/FlameGenome.cs`, `VariationRegistry.cs` | Produces and validates seeded genomes and supported variation data. |
| Render | `Rendering/CpuFlameRenderer.cs`, `ToneMapper.cs`; `Pipeline/BoundedRenderQueue.cs`, `ContinuousRenderService.cs` | Runs bounded CPU rendering, tone mapping, progress, pause/resume, cancellation, and session limits. |
| Save/rate/browse | `Serialization/FlameXmlSerializer.cs`; `Storage/SourceArchive.cs`, `RatingStore.cs`; `Pipeline/CandidateCatalog.cs` | Reads/writes `.flame`, publishes matched PNG/FLAME pairs, moves ratings safely, and orders candidates. |
| Re-render | `Rendering/ArtifactRerenderer.cs` | Replaces a PNG only after a successful cancellable render while preserving its source `.flame`. |
| Optional AI | `Models/PreferenceScoringModels.cs`; `Ai/PreferenceDataset.cs`, `PreferenceScoringBackend.cs`, `dinov2_service.py`; `Pipeline/ContinuousAiScoringService.cs` | Builds ordinal datasets, talks to the CUDA-only Python/DINOv2 worker, watches candidates, scores, trains, and renames scored pairs. |

## Tracked files

| Group / file | Needed to run or build? | Description and recommendation |
|---|---:|---|
| `FractalFlameCurator.sln` | Build | Solution entry point for app and tests. Keep. |
| `global.json` | Build | Pins .NET SDK 8.0.424. Keep while that version is supported. |
| `src/FractalFlameCurator/FractalFlameCurator.csproj` | Build | WPF project definition, release version, and Python-worker publishing rule. Keep. |
| `src/FractalFlameCurator/App.xaml` | Runtime source | Shared WPF theme/control templates. Keep. |
| `src/FractalFlameCurator/App.xaml.cs` | Runtime source | WPF application type required by XAML compilation. Keep even though it is intentionally small. |
| `src/FractalFlameCurator/MainWindow.xaml` | Runtime source | Main native UI layout and event bindings. Keep. |
| `src/FractalFlameCurator/MainWindow.xaml.cs` | Runtime source | UI orchestration and lifecycle cleanup. Keep; it is large, but splitting it without a behavior goal would be a high-risk structural rewrite. |
| `src/FractalFlameCurator/GlobalUsings.cs` | Runtime source | Common namespace imports used throughout the project. Keep. |
| `src/FractalFlameCurator/Generation/*` | Runtime source | Deterministic RNG, generator, and validation. All three files are referenced. Keep. |
| `src/FractalFlameCurator/Models/*` | Runtime source | Flame/render and AI data contracts plus variation registry. All three files are referenced. Keep. |
| `src/FractalFlameCurator/Rendering/*` | Runtime source | CPU renderer, tone mapper, and safe re-render helper. All three files are referenced. Keep. |
| `src/FractalFlameCurator/Serialization/FlameXmlSerializer.cs` | Runtime source | Apophysis-compatible XML persistence and legacy reader. Keep. |
| `src/FractalFlameCurator/Storage/*` | Runtime source | Source archive and rating-pair transactions. Both files are referenced. Keep. |
| `src/FractalFlameCurator/Pipeline/*` | Runtime source | Render queue/session, candidate catalog, and AI scoring session. All four files are referenced. Keep. |
| `src/FractalFlameCurator/Ai/*` except `__pycache__` | Runtime source | Dataset builder, C# worker client, and published Python worker. Keep if optional AI remains a product feature. |
| `tests/FractalFlameCurator.Tests/FractalFlameCurator.Tests.csproj` | Development | Test project definition. Keep. |
| `tests/FractalFlameCurator.Tests/PhaseOneTests.cs` | Development | Phase 1 acceptance/regression tests. Keep. |
| `tests/FractalFlameCurator.Tests/PhaseTwoTests.cs` | Development | Optional AI/data-pipeline tests. Keep while Phase 2 remains. |
| `agents.md` | Development contract | Product constraints and acceptance behavior. Keep. |
| `features.md` | Development contract | Required chronological behavior log. Keep. |
| `README.md` | Development/user help | Build, architecture, renderer, and AI overview. Keep. |
| `.gitignore` | Development | Excludes generated SDK, build, release, scratch, data, and cache files. Keep. |
| `.gitattributes` | Development | Repository text normalization. Keep. |
| `native-fractal-flame-curator-user-manual.pdf` | Optional documentation | User-facing manual included in releases; not required to build or run the executable. |

## Ignored/generated folders

| Path | Current size / files | Required? | Recommendation |
|---|---:|---:|---|
| `src/FractalFlameCurator/bin/` | 169.92 MB / 476 | No | Build/publish outputs. Safe to regenerate with `dotnet build`/`publish`; candidate for cleanup. |
| `src/FractalFlameCurator/obj/` | 10.93 MB / 74 | No | Intermediate compiler output. Safe to regenerate; candidate for cleanup. |
| `tests/FractalFlameCurator.Tests/bin/` | 9.79 MB / 188 | No | Test output. Safe to regenerate; candidate for cleanup. |
| `tests/FractalFlameCurator.Tests/obj/` | 0.41 MB / 37 | No | Test intermediate output. Safe to regenerate; candidate for cleanup. |
| `src/FractalFlameCurator/Ai/__pycache__/` | 0.02 MB / 1 | No | Python bytecode cache. Safe to regenerate; candidate for cleanup. |
| `artifacts/releases/` | Generated | Distribution-only | Contains unpacked self-contained apps and ZIP archives prepared for GitHub Releases. Safe to remove locally after upload and checksum verification. |
| `artifacts/verification/` | 4.91 MB / 93 | No | One-off test/publish verification output. Safe to regenerate; candidate for cleanup. |
| `tmp/` | 4.48 MB / 44 | No | Rendered manual pages and extracted research text. Scratch output; candidate for cleanup. |
| `output/` | empty | No | Empty generated-output folder; candidate for cleanup. |
| `.git/` | about 569.55 MB | Version control only | Not needed by the executable but required for this working repository. Do not delete individual objects. A fresh clone or deliberate Git maintenance can reduce it; `git count-objects` also reports 14.55 MB of temporary garbage. |

## Items requiring a product decision

| Item | Evidence | Decision |
|---|---|---|
| Generator transform-count range | Validation supports 2–12 transforms, but the seeded generator currently produces 2–5. Expanding it directly would change every affected seed and break cross-version reproducibility. | Keep the stable generator or introduce a versioned generator before expanding the range. |
| Existing user corpus integrity | `C:\Users\Hugo\Documents\ApophysisCurator` currently has 1,099 rated PNGs but 996 rated `.flame` files, so 103 legacy images are unpaired. The application data is outside this repository and was not changed. | Preserve legacy PNG-only ratings or run a separately approved repair/archive pass. |
| Idle AI memory | Startup diagnostics leaves the Python/PyTorch worker waiting with about 338 MB resident memory, but the measured worker and WPF process use 0 sustained CPU while idle. | Keep fast AI readiness, or redesign diagnostics as a short-lived process if memory—not CPU—is a concern. |
