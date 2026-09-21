"""Exercise redaction and scope with synthetic credentials in an isolated repository."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import phase6_secret_review as scanner


class SecretReviewTests(unittest.TestCase):
    def test_detects_all_scopes_without_echoing_credentials(self):
        with tempfile.TemporaryDirectory(prefix='phase6-secret-review-') as temporary:
            root = Path(temporary)
            def git(*args):
                subprocess.run(['git', '-C', str(root), *args], check=True, capture_output=True)
            git('init')
            key = 'ghp_' + 'Z' * 36
            source = root / 'source.py'
            source.write_text('value = "' + key + '"', encoding='utf-8')
            git('add', 'source.py')
            git('-c', 'user.name=Synthetic', '-c', 'user.email=synthetic@example.invalid', 'commit', '-m', 'fixture')
            source.write_text('value = 1', encoding='utf-8')
            logs = root / 'logs'
            logs.mkdir()
            (logs / 'service.log').write_text(key, encoding='utf-16')
            bundle = root / 'bundle'
            (bundle / 'static').mkdir(parents=True)
            (bundle / 'BUILD_ID').write_text('synthetic', encoding='utf-8')
            marker = 'unpublished-' + 'a' * 24
            (bundle / 'static/client.js').write_text(marker, encoding='utf-8')
            (root / '.gitignore').write_text('logs/\nbundle/\n', encoding='utf-8')
            with patch.dict(os.environ, {'TEST_PRIVATE_TOKEN': marker}, clear=True):
                result = scanner.review(root, bundle, [logs])
            self.assertTrue(result['complete'])
            self.assertEqual({'history', 'logs', 'browser'}, {item['scope'] for item in result['findings']})
            serialized = json.dumps(result)
            self.assertNotIn(key, serialized)
            self.assertNotIn(marker, serialized)

    def test_missing_build_and_unreadable_encoding_fail_closed(self):
        with tempfile.TemporaryDirectory(prefix='phase6-secret-review-') as temporary:
            root = Path(temporary)
            (root / 'broken.txt').write_bytes(b'\x80\x81')
            with patch.object(scanner, 'git', side_effect=[b'broken.txt\0', b'']):
                result = scanner.review(root, root / 'missing', [root / 'missing'])
            self.assertFalse(result['complete'])
            self.assertEqual({'unsupported-encoding', 'missing-root', 'missing-production-build'},
                             {item['reason'] for item in result['errors']})

    def test_review_decision_does_not_accept_changed_content_or_other_findings(self):
        finding = {'scope': 'checkout', 'path': 'test.py', 'rule': 'credential-literal', 'line': 2}
        decision = {**finding, 'sha256': 'original', 'reason': 'Synthetic fixture reviewed'}
        for digest, expected in [('original', 0), ('changed', 1)]:
            report = {'files': [{'scope': 'checkout', 'path': 'test.py', 'sha256': digest}],
                      'findings': [finding]}
            scanner.apply_review(report, [decision])
            self.assertEqual(expected, len(report['findings']))
        report['findings'] = [{**finding, 'line': 3}]
        scanner.apply_review(report, [decision])
        self.assertEqual(1, len(report['findings']))

    def test_private_env_values_are_read_but_never_reported(self):
        with tempfile.TemporaryDirectory(prefix='phase6-secret-review-') as temporary:
            root = Path(temporary)
            marker = 'private-value-' + 'b' * 24
            (root / '.env').write_text('API_KEY=' + marker, encoding='utf-8')
            with patch.dict(os.environ, {}, clear=True):
                values = scanner.known_values(root)
            self.assertEqual({marker}, values)
            found = scanner.matches(marker, values)
            self.assertEqual([{'rule': 'private-environment-value', 'line': 1}], found)
            self.assertNotIn(marker, json.dumps(found))


if __name__ == '__main__':
    unittest.main()
