"""Filesystem preservation checks for the synthetic fixture materializer (Linux CI)."""
import importlib.util
import os
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('fixture_setup', Path(__file__).with_name('prepare-python-fixtures.py'))
setup = importlib.util.module_from_spec(spec)
spec.loader.exec_module(setup)


class FixturePreservationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.parent = Path(self.temporary.name).resolve()
        self.root = self.parent / 'workspace'
        self.root.mkdir()
        self.old_root = setup.ROOT
        setup.ROOT = self.root

    def tearDown(self):
        setup.ROOT = self.old_root
        self.temporary.cleanup()

    def test_matching_existing_file_is_not_rewritten(self):
        path = self.root / 'output' / 'fixture.json'
        setup.preserve_or_create(path, b'synthetic')
        os.utime(path, ns=(1000000000, 1000000000))
        before = path.stat().st_mtime_ns
        setup.preserve_or_create(path, b'synthetic')
        self.assertEqual(before, path.stat().st_mtime_ns)
        self.assertEqual(b'synthetic', path.read_bytes())

    def test_different_existing_file_is_preserved(self):
        path = self.root / 'fixture.json'
        path.write_bytes(b'existing')
        with self.assertRaises(ValueError):
            setup.preserve_or_create(path, b'replacement')
        self.assertEqual(b'existing', path.read_bytes())

    def test_redirected_directory_cannot_write_outside_workspace(self):
        outside = self.parent / 'outside'
        outside.mkdir()
        (self.root / 'output').symlink_to(outside, target_is_directory=True)
        with self.assertRaises(ValueError):
            setup.preserve_or_create(self.root / 'output' / 'fixture.json', b'synthetic')
        self.assertEqual([], list(outside.iterdir()))

    def test_explicit_outside_path_is_refused(self):
        path = self.parent / 'foreign.json'
        with self.assertRaises(ValueError):
            setup.preserve_or_create(path, b'synthetic')
        self.assertFalse(path.exists())


if __name__ == '__main__':
    unittest.main()
