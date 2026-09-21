"""Read-only publication candidate inventory; does not stage, commit or approve files."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    names = subprocess.check_output(
        ['git', '-C', str(root), 'ls-files', '-z', '--cached', '--others', '--exclude-standard']
    ).decode().split('\0')
    files, errors = [], []
    forbidden = re.compile(r'(^|/)(outputs?|tmp|work|node_modules|bin|obj|\.git|\.next[^/]*)(/|$)|\.(db|dump|pfx|p12|pem|key|log|trx)$', re.I)
    for name in sorted(set(names) - {''}):
        path = root / name
        if path.resolve() != path.absolute() or not path.is_file():
            errors.append({'path': name, 'reason': 'missing-or-redirected'})
            continue
        if forbidden.search(name) or (path.name.startswith('.env') and not path.name.endswith('.example')):
            errors.append({'path': name, 'reason': 'excluded-artifact-is-git-candidate'})
            continue
        data = path.read_bytes()
        files.append({'path': name, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest(),
                      'review': 'binary-provenance-required' if path.suffix.lower() in {'.pdf', '.png', '.jpg', '.jpeg', '.gif', '.woff', '.woff2'} else 'source-or-documentation'})
    report = {'publicationApproved': False, 'projectLicensePresent': (root / 'LICENSE').is_file(),
              'scope': 'Current Git candidates, including uncommitted files; hash inventory only, not a credential/PII or license verdict.',
              'fileCount': len(files), 'errors': errors, 'files': files}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'files': len(files), 'errors': len(errors), 'publicationApproved': False}))
    return 1 if errors else 0


if __name__ == '__main__':
    raise SystemExit(main())
