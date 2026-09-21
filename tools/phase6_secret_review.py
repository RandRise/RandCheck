"""Offline, redacted credential-pattern review. Findings are never secret values.

Scans current Git candidates, reachable historical text blobs, selected log roots,
and a required fresh browser build. This is evidence for review, not a guarantee
against all secrets, encodings, private data, or unexamined files.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess

TEXT = {'.cs', '.py', '.ts', '.tsx', '.js', '.mjs', '.json', '.yaml', '.yml', '.toml',
        '.md', '.ps1', '.sql', '.txt', '.log', '.trx', '.xml', '.html', '.css', '.sh',
        '.props', '.csproj', '.sln', '.config', '.conf', '.example', '.map', '.env'}
RULES = {
    'private-key': re.compile(r'-----BEGIN (?:RSA |EC |OPENSSH |ENCRYPTED )?PRIVATE KEY-----'),
    'provider-key': re.compile(r'\bsk-(?:proj-|svcacct-)?[A-Za-z0-9_-]{24,}\b'),
    'github-token': re.compile(r'\b(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,})\b'),
    'aws-access-key': re.compile(r'\b(?:AKIA|ASIA)[A-Z0-9]{16}\b'),
    'slack-token': re.compile(r'\bxox[baprs]-[A-Za-z0-9-]{20,}\b'),
    'credential-literal': re.compile(r'''(?i)\b(?:password|client[_-]?secret|api[_-]?key|access[_-]?token|worker[_-]?token|secret|token)\s*["']?\s*[:=]\s*["']([^"'\r\n]{8,})["']'''),
}
SENSITIVE_NAME = re.compile(r'(?i)(?:secret|password|(?:^|_)token(?:$|_)|api_?key|connection)')
MAX_BYTES = 32 * 1024 * 1024


def matches(text, known=()):
    found = set()
    for rule, pattern in RULES.items():
        for match in pattern.finditer(text):
            found.add((rule, text.count('\n', 0, match.start()) + 1))
    for value in known:
        if value in text:
            found.add(('private-environment-value', text.count('\n', 0, text.index(value)) + 1))
    return [{'rule': rule, 'line': line} for rule, line in sorted(found)]


def known_values(root):
    values = set()
    bindings = dict(os.environ)
    # Deliberately explicit local files; do not traverse private key stores.
    for path in (root / '.env', root / '.env.local', root / 'apps/web/.env.local', root / 'apps/api/.env'):
        if path.is_file() and not path.is_symlink():
            for line in path.read_text(encoding='utf-8-sig').splitlines():
                key, sep, value = line.partition('=')
                if sep and not key.lstrip().startswith('#'):
                    bindings[key.strip()] = value.strip().strip('"\'')
    for key, value in bindings.items():
        if not SENSITIVE_NAME.search(key):
            continue
        if 'connection' in key.lower():
            match = re.search(r'(?i)(?:^|;)Password=([^;]+)', value)
            value = match[1] if match else ''
        if len(value) >= 16 and value != 'local_development_only' and not value.startswith(('YOUR_', 'replace-', '${')):
            values.add(value)
    return values


def git(root, *args):
    return subprocess.check_output(['git', '-C', str(root), *args], stderr=subprocess.DEVNULL)


def text_path(path):
    return path.suffix.lower() in TEXT or path.name in {'.gitignore', '.dockerignore', '.nvmrc', 'Dockerfile'} or path.name.startswith('.env')


def review(root, bundle, log_roots):
    root = root.resolve()
    known = known_values(root)
    report = {'schema': 1, 'knownPrivateValues': len(known), 'files': [], 'findings': [], 'excluded': [], 'errors': []}

    def inspect(label, data, scope):
        if len(data) > MAX_BYTES:
            report['errors'].append({'path': label, 'scope': scope, 'reason': 'oversized'})
            return
        try:
            contents = data.decode('utf-16' if data.startswith((b'\xff\xfe', b'\xfe\xff')) else 'utf-8-sig')
        except UnicodeDecodeError:
            report['errors'].append({'path': label, 'scope': scope, 'reason': 'unsupported-encoding'})
            return
        report['files'].append({'path': label, 'scope': scope, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()})
        report['findings'].extend({'path': label, 'scope': scope, **match} for match in matches(contents, known))

    def inspect_file(path, scope):
        try:
            label = path.relative_to(root).as_posix()
            if path.is_symlink() or any(p.is_symlink() for p in path.parents if p != root.parent):
                raise ValueError()
            if path.stat().st_size > MAX_BYTES:
                report['errors'].append({'path': label, 'scope': scope, 'reason': 'oversized'})
                return
            inspect(label, path.read_bytes(), scope)
        except (OSError, ValueError):
            report['errors'].append({'scope': scope, 'reason': 'unreadable-or-outside-root'})

    candidates = set(git(root, 'ls-files', '-z', '--cached', '--others', '--exclude-standard').decode().split('\0')) - {''}
    for name in sorted(candidates):
        path = root / name
        if text_path(path):
            inspect_file(path, 'checkout')
        else:
            report['excluded'].append({'path': name, 'scope': 'checkout', 'reason': 'non-text-extension'})
    # Inspect each reachable historical text blob once, recording its Git object ID.
    entries = [entry.partition(' ') for entry in git(root, 'rev-list', '--objects', '--all').decode().splitlines()]
    names = {oid: name for oid, sep, name in entries if sep and text_path(Path(name))}
    if names:
        metadata = subprocess.run(['git', '-C', str(root), 'cat-file', '--batch-check'],
                                  input=('\n'.join(names) + '\n').encode(), check=True,
                                  stdout=subprocess.PIPE, stderr=subprocess.DEVNULL).stdout
        with subprocess.Popen(['git', '-C', str(root), 'cat-file', '--batch'],
                              stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL) as objects:
            for entry in metadata.decode().splitlines():
                oid, kind, size = entry.split()
                if kind != 'blob':
                    continue
                if int(size) > MAX_BYTES:
                    report['errors'].append({'path': oid + ':' + names[oid], 'scope': 'history', 'reason': 'oversized'})
                    continue
                objects.stdin.write((oid + '\n').encode())
                objects.stdin.flush()
                header = objects.stdout.readline().decode().split()
                if header != [oid, 'blob', size]:
                    raise ValueError('Unexpected Git object header')
                data = objects.stdout.read(int(size))
                if len(data) != int(size) or objects.stdout.read(1) != b'\n':
                    raise ValueError('Incomplete Git object')
                inspect(oid + ':' + names[oid], data, 'history')
            objects.stdin.close()
            if objects.wait() != 0:
                raise ValueError('Git object reader failed')
    for directory in log_roots:
        if not directory.is_dir():
            report['errors'].append({'scope': 'logs', 'reason': 'missing-root'})
            continue
        for path in sorted(directory.rglob('*')):
            if path.is_file() and path.suffix.lower() in {'.log', '.trx'}:
                inspect_file(path, 'logs')
    if not (bundle / 'static').is_dir() or not (bundle / 'BUILD_ID').is_file():
        report['errors'].append({'scope': 'browser', 'reason': 'missing-production-build'})
    else:
        browser_files = list((bundle / 'static').rglob('*.js')) + list((bundle / 'static').rglob('*.map'))
        # Include prerendered HTML/RSC payloads that can be sent to a browser.
        browser_files += [p for p in (bundle / 'server/app').rglob('*') if p.is_file() and p.suffix in {'.html', '.rsc'}]
        if not browser_files:
            report['errors'].append({'scope': 'browser', 'reason': 'empty-build'})
        for path in sorted(browser_files):
            inspect_file(path, 'browser')
    report['complete'] = not report['errors']
    return report


def apply_review(report, decisions):
    """Accept only individually reviewed, unchanged findings; never expose values."""
    hashes = {(item['scope'], item['path']): item['sha256'] for item in report['files']}
    approved = {(item['scope'], item['path'], item['sha256'], item['rule'], item['line']): item['reason']
                for item in decisions if item.get('reason')}
    report['reviewedFindings'] = []
    unresolved = []
    for item in report['findings']:
        key = (item['scope'], item['path'], hashes[(item['scope'], item['path'])], item['rule'], item['line'])
        if key in approved:
            report['reviewedFindings'].append({**item, 'reason': approved[key]})
        else:
            unresolved.append(item)
    report['findings'] = unresolved
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--bundle', type=Path, required=True)
    parser.add_argument('--logs', type=Path, action='append', default=[])
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--reviewed', type=Path, help='Exact path/hash/rule/line decisions from human review')
    args = parser.parse_args()
    try:
        root = Path(__file__).resolve().parents[1]
        result = review(root, args.bundle.resolve(), [p.resolve() for p in args.logs])
        if args.reviewed:
            apply_review(result, json.loads(args.reviewed.read_text(encoding='utf-8-sig')))
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, indent=2), encoding='utf-8')
        print(json.dumps({'complete': result['complete'], 'files': len(result['files']),
                          'findings': len(result['findings']), 'errors': len(result['errors'])}))
        return 0 if result['complete'] and not result['findings'] else 1
    except Exception:
        print('Secret review failed; no diagnostic values are printed.')
        return 2


if __name__ == '__main__':
    raise SystemExit(main())
