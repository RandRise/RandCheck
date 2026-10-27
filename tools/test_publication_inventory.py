"""Inventory behavior on disposable repositories, without touching this index."""
from pathlib import Path
import subprocess
import tempfile
import unittest

from publication_inventory import inventory


class PublicationInventoryTests(unittest.TestCase):
    def setUp(self):
        base = Path(__file__).resolve().parents[1] / 'tmp'
        base.mkdir(exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(prefix='publication-test-', dir=base)
        self.root = Path(self.temporary.name).resolve()
        self.root.relative_to(base.resolve())
        self.git('init')

    def tearDown(self):
        self.temporary.cleanup()

    def git(self, *args):
        subprocess.run(['git', '-C', str(self.root), *args], check=True, capture_output=True)

    def test_tracked_deletion_is_explicit_and_not_a_missing_source_error(self):
        source = self.root / 'obsolete.py'
        source.write_text('value = 1', encoding='utf-8')
        self.git('add', 'obsolete.py')
        source.unlink()
        result = inventory(self.root)
        self.assertEqual(['obsolete.py'], result['pendingTrackedDeletions'])
        self.assertEqual([], result['errors'])
        self.assertEqual(0, result['fileCount'])
        self.assertFalse(result['publicationApproved'])

    def test_tracked_build_cache_is_flagged_even_when_ignore_rule_exists(self):
        cache = self.root / 'tsconfig.tsbuildinfo'
        cache.write_text('generated', encoding='utf-8')
        self.git('add', cache.name)
        (self.root / '.gitignore').write_text('*.tsbuildinfo\n', encoding='utf-8')
        result = inventory(self.root)
        self.assertEqual([{'path': cache.name, 'reason': 'excluded-artifact-is-git-candidate'}], result['errors'])
        self.assertEqual('generated', cache.read_text('utf-8'))

    def test_untracking_cache_preserves_file_and_excludes_it_from_candidates(self):
        cache = self.root / 'tsconfig.tsbuildinfo'
        cache.write_text('generated', encoding='utf-8')
        self.git('add', cache.name)
        self.git('rm', '--cached', '--', cache.name)
        (self.root / '.gitignore').write_text('*.tsbuildinfo\n', encoding='utf-8')
        result = inventory(self.root)
        self.assertEqual([], result['errors'])
        self.assertNotIn(cache.name, [row['path'] for row in result['files']])
        self.assertEqual('generated', cache.read_text('utf-8'))
