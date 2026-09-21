"""Independent frozen-corpus/value checks for the provider-free typed-fact trial."""
import argparse
import hashlib
import json
from decimal import Decimal
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ROLES = {0: "1981923e-0baa-4976-bfe8-01c23dcd543a", 1: "44526ce3-5451-47a1-a442-252b8ae98c63", 2: "f6f4dc55-6d36-420f-98a0-3b5f465e5e81"}
MONEY = {"total", "subtotal", "vat", "line_unit_price", "line_tax", "line_total"}
# Expected facts are authored from the pre-existing synthetic truth, not returned answers.
EXPECTED = {
    1: [(0, "quotation_number", 0, "Q-260801")], 2: [(0, "total", 0, "8820.00")],
    3: [(0, "vat", 0, "420.00")], 4: [(1, "purchase_order_number", 0, "PO-260801")],
    5: [(1, "total", 0, "8925.00")], 6: [(1, "line_unit_price", 2, "1250.00")],
    7: [(2, "invoice_number", 0, "SYN-STD-260801")], 8: [(2, "invoice_date", 0, "2026-08-01")],
    9: [(2, "due_date", 0, "2026-08-31")], 10: [(2, "supplier_trn", 0, "000000000000701")],
    11: [(2, "total", 0, "8820.00")], 12: [(2, "payment_terms", 0, "Net 30")],
    13: [(r, "supplier_trn", 0, "000000000000701") for r in (0, 1, 2)],
    14: [(0, "total", 0, "8820.00"), (1, "total", 0, "8925.00"), (2, "total", 0, "8820.00")],
    15: [(0, "total", 0, "8820.00"), (1, "total", 0, "8925.00")],
    16: [(1, "total", 0, "8925.00"), (2, "total", 0, "8820.00")],
    17: [(0, "line_unit_price", 2, "1200.00"), (1, "line_unit_price", 2, "1250.00"), (2, "line_unit_price", 2, "1200.00")],
    18: [(1, "vat", 0, "425.00"), (2, "vat", 0, "420.00")],
    19: [(2, "line_quantity", 2, "2"), (2, "line_unit_price", 2, "1200.00"), (2, "line_tax", 2, "120.00"), (2, "line_total", 2, "2520.00")],
    20: [(2, "total", 0, "8820.00")], 21: [(1, "total", 0, "8925.00")],
    22: [(2, "supplier_trn", 0, "000000000000701")], 23: [(0, "quotation_number", 0, "Q-260801")],
    24: [(0, "line_total", 2, "2520.00"), (1, "line_total", 2, "2625.00"), (2, "line_total", 2, "2520.00")],
}
CALCULATIONS = {15: ("Difference", "105"), 16: ("Difference", "105"), 18: ("Difference", "5"), 24: ("Maximum", "2625")}


def check(case, data, chunks):
    answer = data["result"]["Answer"]
    issues = []
    if data["outcome"] != "typed-unpublished" or data["result"]["RetrievalRunId"] != case["runId"]:
        raise ValueError("Wrong trial output")
    if answer["PublicationAllowed"] or answer["ModelInvoked"] or answer["GeneralSemanticSupportEstablished"]:
        raise ValueError("Candidate boundary changed")
    if answer["Status"] != case["expected"]["status"] or answer["ReasonCode"] != case["expected"]["reason_code"]:
        issues.append("Answer status/reason differs from the unchanged reference.")
    number = int(case["caseId"].rsplit("-", 1)[1])
    expected = set(EXPECTED.get(number, []))
    got = {(x["Role"], x["FieldKey"], x["Occurrence"], x["Value"]) for x in answer["Operands"]}
    if expected != got or len(got) != len(answer["Operands"]):
        issues.append("Requested source/field/value coverage is incomplete or contains extra operands.")
    unique = {}
    for claim in answer["Claims"]:
        if not claim["Citations"]:
            raise ValueError("Uncited claim")
        for citation in claim["Citations"]:
            chunk = chunks[(citation["IndexId"], citation["ChunkId"])]
            if (citation["IndexId"] not in case["indexIds"] or citation["SupportText"] != chunk["text"] or
                    citation["ContentSha256"] != chunk["sha256"] or
                    hashlib.sha256(citation["SupportText"].encode()).hexdigest() != chunk["sha256"] or
                    citation["SupportText"] != f'{citation["FieldKey"]}[{citation["Occurrence"]}]: {citation["ReviewedValue"]}'):
                raise ValueError("Unbound reviewed fact citation")
            actual_spans = {(s["PageNumber"], s["ByteStart"], s["ByteEnd"], s["Quote"]) for s in citation["OriginalPageEvidence"]}
            old_spans = {(s["pageNumber"], s["byteStart"], s["byteEnd"], s["quote"]) for s in chunk["spans"]}
            if actual_spans != old_spans:
                raise ValueError("Changed original page evidence")
            if citation["ChunkId"] in unique and unique[citation["ChunkId"]] != citation:
                raise ValueError("Inconsistent citation metadata")
            unique[citation["ChunkId"]] = citation
    for operand in answer["Operands"]:
        if operand["IndexId"] != ROLES[operand["Role"]]:
            raise ValueError("Wrong document role")
        if any(unique.get(c["ChunkId"]) != c for c in operand["Citations"]):
            raise ValueError("Operand citation was not verified in the claims")
        numeric = operand["FieldKey"] in MONEY or operand["FieldKey"] == "line_quantity"
        if (numeric and (operand["Number"] is None or Decimal(str(operand["Number"])) != Decimal(operand["Value"])) or
                not numeric and operand["Number"] is not None):
            raise ValueError("Typed numeric value does not match its reviewed field")
        if operand["Currency"] != ("AED" if operand["FieldKey"] in MONEY else None):
            issues.append("A monetary amount lacks currency, or currency was attached to a nonmonetary field.")
        if not any(c["FieldKey"] == operand["FieldKey"] and c["Occurrence"] == operand["Occurrence"] and
                   c["ReviewedValue"] == operand["Value"] and c["IndexId"] == operand["IndexId"] for c in operand["Citations"]):
            raise ValueError("Operand lacks its own reviewed source")
    if data["evidenceReferencesVerified"] != len(unique):
        raise ValueError("Navigation verification incomplete")
    calculation = answer["Calculation"]
    if number in CALCULATIONS:
        op, value = CALCULATIONS[number]
        if (calculation is None or calculation["Operation"] != op or Decimal(str(calculation["Value"])) != Decimal(value) or
                calculation["Currency"] != "AED" or set(calculation["OperandIndexIds"]) != {ROLES[x[0]] for x in expected} or
                number == 24 and calculation["MaximumIndexIds"] != [ROLES[1]]):
            issues.append("Calculation or complete operand attribution differs from the reference.")
    elif calculation is not None:
        issues.append("Unexpected calculation.")
    return dict(caseId=case["caseId"], passes=not issues, status=answer["Status"], issues=issues,
                missingParts=answer["MissingParts"], citationCount=len(unique), claimCount=len(answer["Claims"]))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--plan", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    plan_bytes = (ROOT / args.plan).read_bytes()
    plan = json.loads(plan_bytes)
    # The original frozen-plan hash excludes the file's terminal newline.
    corpus_bytes = (ROOT / "samples/phase-5/retrieval-evaluation-v0.1.0/corpus.json").read_bytes().rstrip(b"\r\n")
    if hashlib.sha256(corpus_bytes).hexdigest() != plan["corpusSha256"]:
        raise ValueError("Changed frozen corpus")
    chunks = {(d["indexId"], c["id"]): c for d in json.loads(corpus_bytes) for c in d["chunks"]}
    directory = ROOT / args.output
    rows = []
    for case in plan["cases"]:
        raw = (directory / (case["caseId"] + ".json")).read_bytes()
        data = json.loads(raw)
        if (data["result"]["Answer"]["PolicyVersion"] != plan["answerPolicy"] or
                data["result"]["Answer"]["QuestionPolicyVersion"] != plan["questionPolicy"] or
                data["result"]["SourcePolicyVersion"] != plan["sourcePolicy"]):
            raise ValueError("Wrong implementation policy")
        row = check(case, data, chunks)
        row["outputSha256"] = hashlib.sha256(raw).hexdigest()
        rows.append(row)
    report = dict(version="typed-fact-evaluation-v1", trial=plan["version"], synthetic=True, independentHoldout=False,
        claimEligible=False, modelCalls=0, newCostUsd="0", planSha256=hashlib.sha256(plan_bytes).hexdigest(),
        caseCount=len(rows), passes=sum(x["passes"] for x in rows), failedCases=[x["caseId"] for x in rows if not x["passes"]],
        citationReferencesVerified=sum(x["citationCount"] for x in rows), results=rows)
    with (directory / "evaluation.json").open("x") as stream:
        json.dump(report, stream, indent=2)
    print(json.dumps({k: v for k, v in report.items() if k != "results"}, indent=2))


if __name__ == "__main__":
    main()
