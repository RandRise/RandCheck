"""Read package license metadata without installing, changing or approving it.

npm records come from current lockfiles. NuGet records come from available
restore assets and their package metadata. Python records describe the interpreter
running this command, including its development extras. This is an inventory, not
a legal conclusion or an OS/container-image license audit.
"""
import argparse
import importlib.metadata
import json
from pathlib import Path
import platform
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def npm_metadata(root):
    records = []
    for relative in ("apps/web/package-lock.json", "docs/package-lock.json"):
        lock = json.loads((root / relative).read_text("utf-8"))
        for name, package in lock["packages"].items():
            if not name:
                continue
            records.append({"lockfile": relative, "path": name,
                            "version": package.get("version"), "license": package.get("license")})
    return records


def nuget_metadata(root):
    records = {}
    for asset in sorted((root / "apps/backend").glob("**/obj/project.assets.json")):
        document = json.loads(asset.read_text("utf-8-sig"))
        for identity, library in document["libraries"].items():
            if library["type"] != "package":
                continue
            record = records.setdefault(identity, {"identity": identity, "license": None,
                                                   "licenseType": None, "licenseUrl": None})
            for folder in document.get("packageFolders", {}):
                directory = Path(folder) / library["path"]
                for nuspec in directory.glob("*.nuspec"):
                    metadata = ET.parse(nuspec).getroot().find("{*}metadata")
                    if metadata is None:
                        continue
                    license_node = metadata.find("{*}license")
                    if license_node is not None:
                        record["license"] = license_node.text
                        record["licenseType"] = license_node.get("type")
                    record["licenseUrl"] = metadata.findtext("{*}licenseUrl")
    return sorted(records.values(), key=lambda row: row["identity"].lower())


def python_metadata():
    records = []
    for distribution in importlib.metadata.distributions():
        metadata = distribution.metadata
        records.append({"name": metadata["Name"], "version": distribution.version,
                        "expression": metadata.get("License-Expression"),
                        "license": metadata.get("License"),
                        "classifiers": [value for value in metadata.get_all("Classifier", []) if value.startswith("License ::")],
                        "licenseFiles": metadata.get_all("License-File", [])})
    return sorted(records, key=lambda row: row["name"].lower())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--group", choices=("all", "npm", "nuget", "python"), default="all")
    args = parser.parse_args()
    if args.output.exists():
        raise SystemExit("Choose a new output; existing evidence is preserved")
    report = {"scope": __doc__, "pythonVersion": platform.python_version(),
              "group": args.group,
              "npm": npm_metadata(args.root) if args.group in ("all", "npm") else [],
              "nuget": nuget_metadata(args.root) if args.group in ("all", "nuget") else [],
              "python": python_metadata() if args.group in ("all", "python") else []}
    report["missing"] = {
        "npm": [row["path"] for row in report["npm"] if not row["license"]],
        "nuget": [row["identity"] for row in report["nuget"] if not row["license"] and not row["licenseUrl"]],
        "python": [row["name"] for row in report["python"] if not any(row[key] for key in ("expression", "license", "classifiers", "licenseFiles"))],
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"packages": {key: len(report[key]) for key in ("npm", "nuget", "python")},
                      "missing": report["missing"]}))


if __name__ == "__main__":
    main()
