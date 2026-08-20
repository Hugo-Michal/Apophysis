# Agent Instructions: Native Fractal-Flame Curator

This project is being rebuilt from scratch. Do not restore the old Flask app,
genetic algorithm, heuristic evaluator, or old dataset. Work in two explicit
phases and do not implement Phase 2 until Phase 1 is complete and accepted.

## Phase 1: generator and manual scoring

Build a native Windows desktop application. A WPF/.NET desktop application is
the default choice unless investigation shows a better native Windows option.
The application must:

- Generate valid, reproducible, Apophysis-compatible `.flame` genomes.
- Explore the complete supported variation space, transform counts, affine
  coefficients, weights, post transforms, symmetry, camera, and palettes while
  rejecting invalid or non-renderable genomes. Use seeded randomness and save
  the seed with every source genome.
- Use a 2048x2048 square render by default.
- Make renderer quality controls visible: quality/sample budget, supersampling
  or oversample, filter radius, and any equivalent density/tone controls exposed
  by the chosen Apophysis-compatible renderer. Do not pretend that a control is
  supersampling if it is not.
- Default to a black-and-white palette. Keep additional palettes available in
  Settings, but never silently change the default monochrome mode.
- Show one image at a time at a usable zoom. Render the selected image at the
  configured quality before rating it.
- Support configurable continuous rendering for overnight use. Rendering must
  continue in background workers while the user rates the current image.
  Include Start, Pause, Resume, Stop, queue depth, progress, and a configurable
  batch/session limit.
- Detect and report GPU rendering explicitly. Use GPU rendering when the
  selected renderer genuinely supports it; otherwise use bounded multithreaded
  CPU rendering with cancellation and no frozen UI.
- Save every generated source image and matching `.flame` file in a source
  archive. The `.flame` file must remain available after rating.
- Provide five rating buttons, one through five stars. Rating an image moves
  the rendered PNG and its matching `.flame` genome together into
  `ratings/1` through `ratings/5`, using stable matching basenames. Provide
  undo and safe re-rating so mistakes do not lose the source pair.
- Keep each rated PNG and `.flame` together in its rating folder. Unrated source
  pairs remain in `rendered/`; the rating folders contain only matched PNG and
  `.flame` pairs, with no other metadata.
- Provide an expandable Image settings drawer with truthful palette, quality,
  tone, and density controls. Apply changes to the current viewport only through
  an explicit cancellable re-render that preserves the source `.flame` pair.
- Keep the Image drawer compact and accessible without reducing font sizes or
  removing controls. The current-flame re-render action must toggle to
  cancellation while active, and the rated-flame batch action may replace
  rated PNGs in place while preserving rating folders and source `.flame` pairs.

## Phase 1 acceptance checks

- A fixed seed regenerates the same `.flame` and same image when render settings
  are unchanged.
- Generated files open as valid Apophysis-compatible XML and render correctly.
- The GUI remains responsive while continuous rendering is active.
- GPU/CPU backend and render settings are visible and truthful.
- The default output is monochrome and 2048x2048.
- Rating and undo move and restore complete PNG/`.flame` pairs in the five
  rating folders.
- Add automated tests for XML validity, seed reproducibility, rating moves,
  cancellation, and queue behavior. Update this file and `features.md`.

## Phase 2: AI scoring addition

Only after Phase 1 has collected a meaningful human-rated corpus, add automatic
scoring. Keep the manual workflow intact and make AI mode optional.

- Use a pretrained DINOv2 backbone as a frozen feature extractor first. Do not
  retrain the full DINOv2 backbone on a small local corpus unless a measured
  experiment proves it necessary.
- Human ratings 1–5 are ordinal, not five unrelated categories. Train an
  ordinal head with four thresholds for `rating >= 2`, `>= 3`, `>= 4`, and
  `>= 5`. Convert the cumulative probabilities to an expected rating and map
  it to a continuous 0–1 score: `(expected_rating - 1) / 4`.
- Create train/validation/test splits only in Phase 2. Split by source genome or
  generation family where possible, not by near-identical rendered variants,
  to prevent leakage. Keep the original star folders unchanged as the raw
  labels.
- Measure held-out ordinal accuracy, mean absolute rating error, Spearman
  correlation, calibration, and rank correlation. Also test black, sparse,
  clipped, low-detail, and non-fractal controls.
- Use the DINOv2 image preprocessing required by the selected model. DINOv2
  ViT models use patch size 14 and commonly evaluate 224x224 inputs; larger
  source renders are still valuable because they preserve detail before the
  model preprocessing, but do not claim that enlarging a low-resolution image
  creates information.
- Keep the AI scorer separate from the generator and manual scorer. It must be
  possible to compare human rating, predicted 0–1 score, and rendered image in
  the GUI before enabling automatic selection.
- Record every feature addition here and in `features.md`.

## Phase 1 implementation note

The Phase 1 implementation is in `src/FractalFlameCurator` as a WPF/.NET 8
desktop application. Its automated acceptance coverage is in
`tests/FractalFlameCurator.Tests`; the tests cover XML validity, deterministic
seed output, variation coverage, CPU backend reporting, renderer cancellation
and failure handling, queue bounds, finite-session completion, paired rating-folder
behavior, undo/re-rating, source-pair preservation, tone mapping, and safe
current-flame re-render. Phase 2
AI scoring is implemented separately after the Phase 1 manual workflow; see the Phase 2 implementation note below.

## Phase 2 implementation note

Phase 2 is implemented as an optional CUDA-only DINOv2 preference scorer in
`src/FractalFlameCurator/Ai` and `src/FractalFlameCurator/Pipeline`. The WPF
application keeps the Phase 1 manual workflow as the source of truth. The
bundled Python worker uses a frozen DINOv2 ViT-B/14 backbone and a small four-
threshold ordinal head; it refuses CPU inference/training and reports the
Python/PyTorch/CUDA/device state. Human rating folders are snapshotted but never
modified by AI. Phase 2 automated coverage is in
`tests/FractalFlameCurator.Tests/PhaseTwoTests.cs`.

## Product constraints

Do not reintroduce the previous genetic algorithm, web server, old datasets, or
extra pipeline stages during Phase 1. Prefer a small, testable native desktop
application with a real renderer over a large speculative architecture.

## Technical references

- DINOv2 model card: https://github.com/facebookresearch/dinov2/blob/main/MODEL_CARD.md
- DINOv2 repository and CUDA/PyTorch setup: https://github.com/facebookresearch/dinov2
- DINOv2 paper: https://arxiv.org/abs/2304.07193
