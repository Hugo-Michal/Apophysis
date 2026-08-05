from flask import Flask, jsonify, render_template, request, send_file

from flame_generator import FlameGenerator


app = Flask(__name__)
generator = FlameGenerator()


@app.get("/")
def index():
    return render_template("index.html", schemes=generator.scheme_names)


@app.post("/api/generate")
def generate():
    payload = request.get_json(silent=True) or {}
    scheme = payload.get("scheme", "ember")
    try:
        flame = generator.generate(scheme=scheme)
    except ValueError as error:
        return jsonify({"error": str(error)}), 400

    return jsonify({
        "name": flame.name,
        "xml": flame.to_xml(),
        "preview": flame.preview_data_uri(),
        "novelty": flame.novelty_score,
    })


@app.post("/api/download")
def download():
    payload = request.get_json(silent=True) or {}
    scheme = payload.get("scheme", "ember")
    flame = generator.generate(scheme=scheme)
    response = send_file(
        flame.xml_bytes(),
        mimetype="application/xml",
        as_attachment=True,
        download_name=f"{flame.name}.flame",
    )
    return response


if __name__ == "__main__":
    app.run(debug=True)
