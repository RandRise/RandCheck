"""Exercise redaction and scope with synthetic credentials in an isolated repository."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import phase6_secret_review as scanner


def runtime_environment():
    # Keep executable lookup on Windows while excluding private caller values.
    return {key: value for key, value in os.environ.items() if key.upper() in {'PATH', 'SYSTEMROOT'}}


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
            cache = root / 'cache.tsbuildinfo'
            cache.write_text(json.dumps({'synthetic': key}), encoding='utf-8')
            diagram = root / 'diagram.svg'
            diagram.write_text('<svg><desc>' + key + '</desc></svg>', encoding='utf-8')
            git('add', 'source.py', 'cache.tsbuildinfo', 'diagram.svg')
            git('-c', 'user.name=Synthetic', '-c', 'user.email=synthetic@example.invalid', 'commit', '-m', 'fixture')
            source.write_text('value = 1', encoding='utf-8')
            cache.unlink()
            diagram.unlink()
            logs = root / 'logs'
            logs.mkdir()
            (logs / 'service.log').write_text(key, encoding='utf-16')
            bundle = root / 'bundle'
            (bundle / 'static').mkdir(parents=True)
            (bundle / 'BUILD_ID').write_text('synthetic', encoding='utf-8')
            marker = 'unpublished-' + 'a' * 24
            (bundle / 'static/client.js').write_text(marker, encoding='utf-8')
            (root / '.gitignore').write_text('logs/\nbundle/\n', encoding='utf-8')
            with patch.dict(os.environ, {**runtime_environment(), 'TEST_PRIVATE_TOKEN': marker}, clear=True):
                result = scanner.review(root, bundle, [logs])
            self.assertTrue(result['complete'])
            self.assertEqual({'history', 'logs', 'browser'}, {item['scope'] for item in result['findings']})
            self.assertTrue(any(item['scope'] == 'history' and item['path'].endswith(':cache.tsbuildinfo')
                                for item in result['findings']))
            self.assertTrue(any(item['scope'] == 'history' and item['path'].endswith(':diagram.svg')
                                for item in result['findings']))
            serialized = json.dumps(result)
            self.assertNotIn(key, serialized)
            self.assertNotIn(marker, serialized)

    def test_missing_build_and_unreadable_encoding_fail_closed(self):
        with tempfile.TemporaryDirectory(prefix='phase6-secret-review-') as temporary:
            root = Path(temporary)
            (root / 'broken.txt').write_bytes(b'\x80\x81')
            with patch.object(scanner, 'git', side_effect=[b'broken.txt\0', b'', b'']):
                result = scanner.review(root, root / 'missing', [root / 'missing'])
            self.assertFalse(result['complete'])
            self.assertEqual({'unsupported-encoding', 'missing-root', 'missing-production-build'},
                             {item['reason'] for item in result['errors']})

    def test_pending_deletion_keeps_historical_credential_detection(self):
        with tempfile.TemporaryDirectory(prefix='phase6-deleted-review-') as temporary:
            root = Path(temporary)
            def git(*args):
                subprocess.run(['git', '-C', str(root), *args], check=True, capture_output=True)
            git('init')
            key = 'ghp_' + 'Q' * 36
            source = root / 'obsolete.py'
            source.write_text('value = "' + key + '"', encoding='utf-8')
            git('add', 'obsolete.py')
            git('-c', 'user.name=Synthetic', '-c', 'user.email=synthetic@example.invalid', 'commit', '-m', 'fixture')
            source.unlink()
            bundle = root / 'bundle'
            (bundle / 'static').mkdir(parents=True)
            (bundle / 'BUILD_ID').write_text('synthetic', encoding='utf-8')
            (bundle / 'static/client.js').write_text('const safe = 1;', encoding='utf-8')
            (root / '.gitignore').write_text('bundle/\n', encoding='utf-8')
            with patch.dict(os.environ, runtime_environment(), clear=True):
                result = scanner.review(root, bundle, [])
            self.assertTrue(result['complete'])
            self.assertEqual(['obsolete.py'], result['pendingTrackedDeletions'])
            self.assertEqual({'history'}, {item['scope'] for item in result['findings']})
            self.assertNotIn(key, json.dumps(result))

    def test_unexplained_missing_candidate_still_fails_closed(self):
        with tempfile.TemporaryDirectory(prefix='phase6-missing-review-') as temporary:
            root = Path(temporary)
            with patch.object(scanner, 'git', side_effect=[b'missing.py\0', b'', b'']):
                result = scanner.review(root, root / 'missing-build', [])
            self.assertFalse(result['complete'])
            self.assertIn('unreadable-or-outside-root', {item['reason'] for item in result['errors']})

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
