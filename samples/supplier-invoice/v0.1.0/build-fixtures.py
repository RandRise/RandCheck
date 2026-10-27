"""Create the frozen fictional English native PDFs and four text helper inputs.

Truth is authored in source-truth.json, never derived from extractor output.
Existing files must not be replaced. No OCR, provider or application import.
"""
from hashlib import sha256
import json
from pathlib import Path

from reportlab.lib.pagesizes import A4
from reportlab.pdfgen import canvas


def main():
    root = Path(__file__).resolve().parent
    truth = json.loads((root / "source-truth.json").read_text(encoding="utf-8"))
    targets = [root / (doc["id"] + ".txt") for doc in truth["documents"]]
    targets += [root / (doc["id"] + ".pdf") for doc in truth["documents"] if "native-pdf" in doc["representation"]]
    targets.append(root / "manifest.json")
    if any(path.exists() for path in targets):
        raise SystemExit("Refusing to replace frozen fixture files")
    entries = []
    for doc in truth["documents"]:
        rows = []
        for row in truth["rows"]:
            index = row["occurrence"]
            rows.extend([f'Line {index} Description: {row["description"]}', f'Line {index} Quantity: {row["quantity"]}', f'Line {index} Unit Price: {row["unitPrice"]}'])
        text = "\n".join([doc["title"], *doc["headers"], *rows]) + "\n"
        text_path = root / (doc["id"] + ".txt")
        text_path.write_text(text, encoding="utf-8")
        entries.append({"name": text_path.name, "sha256": sha256(text_path.read_bytes()).hexdigest(), "representation": "text-helper", "pages": 1})
        if "native-pdf" not in doc["representation"]:
            continue
        pdf_path = root / (doc["id"] + ".pdf")
        pdf = canvas.Canvas(str(pdf_path), pagesize=A4, invariant=1)
        pdf.setTitle("Fictional supplier invoice S1 fixture " + doc["id"])
        pdf.setAuthor("Synthetic local development fixture - CC0")
        pdf.setFont("Helvetica", 9)
        pdf.drawString(42, 806, "SYNTHETIC DEVELOPMENT DOCUMENT - NOT A REAL TAX INVOICE")
        pdf.setFont("Helvetica-Bold", 18)
        pdf.drawString(42, 770, doc["title"])
        pdf.setFont("Helvetica", 10)
        pdf.drawString(42, 746, "Desert Demo Services (fictional)")
        for index, header in enumerate(doc["headers"]):
            pdf.drawString(42, 720 - index * 21, header)
        geometry = truth["geometry"]
        pdf.setFont("Helvetica-Bold", 10)
        for x, header in zip(geometry["headerX"], geometry["headers"]):
            pdf.drawString(x, geometry["headerY"], header)
        pdf.line(42, 464, 553, 464)
        pdf.setFont("Helvetica", 10)
        for y, row in zip(geometry["rowY"], truth["rows"]):
            values = [row["description"], row["quantity"], row["unitPrice"], row["netAmount"]]
            for x, value in zip(geometry["cellX"], values):
                pdf.drawString(x, y, value)
        pdf.setFont("Helvetica", 9)
        pdf.drawString(42, 340, "Row amounts are NET AED; VAT is stated at document level.")
        pdf.drawString(42, 320, "Unit and price basis require review; this is not a full agreement fixture.")
        pdf.drawString(42, 60, "Fictional parties and identifier. Local development only. Page 1 of 1.")
        pdf.showPage()
        pdf.save()
        entries.append({"name": pdf_path.name, "sha256": sha256(pdf_path.read_bytes()).hexdigest(), "representation": "native-pdf", "pages": 1})
    (root / "manifest.json").write_text(json.dumps({"dataset": truth["dataset"], "truthSha256": sha256((root / "source-truth.json").read_bytes()).hexdigest(), "files": entries}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Created four text helpers and two fictional native PDFs; truth frozen independently.")


if __name__ == "__main__":
    main()
