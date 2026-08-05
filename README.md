# Flame Foundry

Small Flask-based starting point for generating and previewing Apophysis-style fractal flames.

## Run

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r requirements.txt
python app.py
```

Open <http://127.0.0.1:5000>. Generate a flame, choose a palette, and download the resulting `.flame` XML file.

The renderer is intentionally lightweight and is not intended to match Apophysis pixel-for-pixel yet. The generator uses multiple variations, randomized affine transforms, and a bounded fingerprint archive with rejection sampling to keep recent outputs distinct.
