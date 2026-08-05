# Features

This file is the shared feature log for the project.

Every agent must update this file whenever a feature is added, changed, removed, or materially affected. Add the newest entry at the top and keep the description short.

## Entry format

```text
### YYYY-MM-DD - Feature name
- Change: What was added, changed, or removed.
- Files: Main files affected.
- Notes: User-visible behavior or follow-up work, if relevant.
```

## Current features

### 2026-08-05 - Initial project baseline
- Change: Flask app with fractal-flame generation, palette selection, previews, novelty scoring, and `.flame` XML downloads.
- Files: `app.py`, `flame_generator.py`, `templates/`, `static/`.
- Notes: The renderer is lightweight and intentionally does not match Apophysis pixel-for-pixel.
