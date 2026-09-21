"""Fail on missing projects, zero-selected tests, skipped cases or failed TRX results."""
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

root, destination = map(Path, sys.argv[1:])
expected = {'Domain', 'Architecture', 'Application', 'Infrastructure', 'Api', 'Persistence'}
assert {p.stem for p in root.glob('*.trx')} == expected, 'Missing/unexpected test project results'
counts = {}
for path in sorted(root.glob('*.trx')):
    tree = ET.parse(path)
    counter = tree.find('.//{*}Counters')
    assert counter is not None, f'Missing counters: {path.name}'
    values = {key: int(value) for key, value in counter.attrib.items()}
    assert values['total'] > 0 and values['passed'] == values['total'], path.name
    assert values.get('notExecuted', 0) == 0 and values.get('failed', 0) == 0, path.name
    counts[path.stem] = values['passed']
destination.write_text(json.dumps({'passed': sum(counts.values()), 'projects': counts}, indent=2) + '\n')
print(json.dumps(counts))
