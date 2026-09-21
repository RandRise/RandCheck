"""Materialize pinned synthetic inputs without importing or overwriting local outputs."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[2]
PACK = Path('output/pdf/uae-procurement-reconciliation-v0.1.0')
FIXTURES = ROOT / 'samples/phase-4/procurement-v0.1.0'
NAMES = {
    'truth-set.json',
    'synthetic-quotation-Q-260801.pdf',
    'synthetic-purchase-order-PO-260801.pdf',
    'synthetic-tax-invoice-SYN-STD-260801.pdf',
}


def preserve_or_create(path: Path, content: bytes) -> None:
    # Refuse redirected paths, including Windows junctions, and changed outputs.
    path.absolute().relative_to(ROOT)
    if path.resolve() != path.absolute():
        raise ValueError('Refusing a redirected fixture destination')
    if path.exists():
        if not path.is_file() or path.read_bytes() != content:
            raise ValueError(f'Refusing to overwrite existing output: {path.relative_to(ROOT)}')
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('xb') as stream:
        stream.write(content)


def main() -> None:
    dataset = json.loads((ROOT / 'samples/phase-5/cited-questions-v0.1.0/question-set.json').read_text('utf-8'))
    pack = dataset['source_pack']
    refs = [{'path': pack['truth_set_path'], 'sha256': pack['truth_set_sha256']}, *dataset['sources']]
    inputs = {}
    for ref in refs:
        relative = Path(ref['path'])
        if relative.parent != PACK or relative.name not in NAMES:
            raise ValueError('Unexpected synthetic fixture reference')
        content = (FIXTURES / relative.name).read_bytes()
        if hashlib.sha256(content).hexdigest() != ref['sha256']:
            raise ValueError(f'Synthetic fixture hash mismatch: {relative.name}')
        inputs[relative] = content
    if {path.name for path in inputs} != NAMES:
        raise ValueError('Incomplete synthetic fixture pack')
    native_root = ROOT / 'samples/phase-5/due-date-v0.1.0'
    native = json.loads((native_root / 'manifest.json').read_text('utf-8'))
    relative = Path('output/pdf/uae-invoice-development-v0.2.0/fta-standard-services-native.pdf')
    if Path(native['sourcePath']) != relative or native['synthetic'] is not True:
        raise ValueError('Unexpected native fixture reference')
    content = (native_root / relative.name).read_bytes()
    if hashlib.sha256(content).hexdigest() != native['sourceSha256']:
        raise ValueError('Native fixture hash mismatch')
    inputs[relative] = content
    for path, content in inputs.items():
        preserve_or_create(ROOT / path, content)

    smoke = ROOT / 'samples/phase-3/smoke-v0.1.0'
    with tempfile.TemporaryDirectory(prefix='ci-smoke-') as temporary:
        report = Path(temporary) / 'report.json'
        subprocess.run([
            sys.executable, str(ROOT / 'tools/phase3_evaluate.py'),
            '--dataset', str(smoke / 'dataset.json'),
            '--predictions', str(smoke / 'local-predictions.json'),
            '--output', str(report),
        ], check=True, cwd=ROOT)
        preserve_or_create(ROOT / 'outputs/phase-3/smoke-v0.1.0-report.json', report.read_bytes())
    print('Prepared five hash-pinned synthetic inputs and the deterministic smoke report.')


if __name__ == '__main__':
    main()
