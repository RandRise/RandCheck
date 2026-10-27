"""Create a redistributable fictional geometry fixture using PDF core fonts only.

The historical mixed-language PDF remains untouched. This separate one-page
fixture carries its English table layout into clean-checkout regression tests.
Refuse overwrites; source truth remains the frozen S1 rows, not parser output.
"""
from hashlib import sha256
import json
from pathlib import Path

from reportlab.lib.pagesizes import A4
from reportlab.pdfgen import canvas


def main():
    root = Path(__file__).parent
    pdf = root / "native-table.pdf"
    manifest = root / "manifest.json"
    if pdf.exists() or manifest.exists():
        raise SystemExit("Refusing to overwrite a frozen fixture")
    document = canvas.Canvas(str(pdf), pagesize=A4, invariant=1)
    document.setTitle("Fictional native table geometry fixture")
    document.setAuthor("UAE Document Intelligence development fixtures")
    document.setFont("Helvetica", 11)
    document.drawString(40, 760, "SYNTHETIC TEST DOCUMENT - NOT A REAL INVOICE")
    document.drawString(40, 730, "Two fictional services. Values fixed before extraction.")
    document.setFont("Helvetica", 10)
    for x, label in [(42, "Description"), (320, "Qty"), (378, "Unit price"), (485, "Net AED")]:
        document.drawString(x, 475, label)
    for y, description, quantity, price, amount in [
        (435, "Document review training", "2", "400.00", "800.00"),
        (395, "Workflow configuration session", "1", "400.00", "400.00"),
    ]:
        for x, value in [(42, description), (325, quantity), (390, price), (486, amount)]:
            document.drawString(x, y, value)
    document.drawString(40, 335, "Amounts are in AED. No supplier or banking information.")
    document.save()
    manifest.write_text(json.dumps({
        "fixture": "native-table-v0.1.0", "file": pdf.name,
        "sha256": sha256(pdf.read_bytes()).hexdigest(), "pages": 1,
        "rights": "Project-authored fictional fixture, CC0-1.0. PDF core Helvetica; no embedded font program.",
        "truth": "../v0.1.0/source-truth.json#rows",
        "historicalLayoutSource": "Phase 7 mixed-native-two-page.pdf, first-page English table",
        "historicalSourceSha256": "e6dc0255c374659712e66b04da33bc6333712148fab1aef1ec63e62bee60a990",
    }, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
