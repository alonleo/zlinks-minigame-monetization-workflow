import json
import os
from pathlib import Path
import tempfile
import threading
import unittest
from unittest.mock import patch

import credentials
import upload


class FakeRemote:
    def __init__(self, objects=None, receipt=True, persist=True):
        self.objects = objects or {}
        self.receipt, self.persist, self.calls = receipt, persist, 0

    def stat(self, key):
        return self.objects.get(key)

    def put(self, item, overwrite):
        self.calls += 1
        if self.persist:
            self.objects[item['key']] = {'hash': item['etag'], 'fsize': item['bytes']}
        return self.receipt


class UploadTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        (self.root / 'StreamingAssets').mkdir()
        (self.root / 'StreamingAssets' / '你好.json').write_text('{"ok":true}', encoding='utf-8')
        (self.root / 'data.bin.br').write_bytes(b'compressed-test-bytes')

    def tearDown(self):
        self.temp.cleanup()

    def records(self):
        return upload.collect(self.root, 'game/v1/')[0]

    def test_relative_paths_secrets_and_unicode(self):
        (self.root / '.env').write_text('fake-secret')
        (self.root / 'private.pem').write_text('fake-key')
        (self.root / '.git').mkdir()
        (self.root / '.git' / 'config').write_text('test')
        records, skipped = upload.collect(self.root, 'game/v1/', domain='https://cdn.example.com')
        self.assertEqual(len(records), 2)
        self.assertEqual(len(skipped), 3)
        item = next(r for r in records if r['relative'].endswith('.json'))
        self.assertEqual(item['key'], 'game/v1/StreamingAssets/你好.json')
        self.assertIn('%E4%BD%A0%E5%A5%BD', item['url'])

    def test_prefix_rejects_traversal_and_urls(self):
        for prefix in ('a/../b', 'a//b', 'https://example.com/a', 'a\n'):
            with self.assertRaises(upload.UploadError):
                upload.normalize_prefix(prefix)
        self.assertEqual(upload.normalize_prefix('/game\\v1/'), 'game/v1/')

    def test_unity_project_root_is_rejected(self):
        (self.root / 'Assets').mkdir()
        (self.root / 'ProjectSettings').mkdir()
        with self.assertRaises(upload.UploadError):
            self.records()

    def test_manifest_mismatch_blocks_plan(self):
        records = self.records()
        manifest = {'cdn_prefix': 'https://cdn.example.com/game/v1/', 'files': [
            {'path': r['relative'], 'bytes': r['bytes'], 'sha256': r['sha256']} for r in records]}
        with tempfile.TemporaryDirectory() as outside:
            path = Path(outside) / 'manifest.json'
            path.write_text(json.dumps(manifest))
            self.assertEqual(len(upload.collect(self.root, 'game/v1/', manifest=path)[0]), 2)
            (self.root / 'data.bin.br').write_bytes(b'changed')
            with self.assertRaises(upload.UploadError):
                upload.collect(self.root, 'game/v1/', manifest=path)

    def test_no_writes_when_any_preflight_conflict(self):
        records = self.records()
        remote = FakeRemote({records[-1]['key']: {'hash': 'different', 'fsize': 1}})
        with patch('sys.argv', ['upload.py', '--source', str(self.root), '--bucket', 'test-bucket', '--prefix=game/v1/', '--upload']), patch('upload.Remote', return_value=remote), patch('upload.write_report', return_value=self.root / 'report.json'), patch('builtins.print'):
            self.assertEqual(upload.main(), 1)
        self.assertEqual(remote.calls, 0)

    def test_identical_objects_are_skipped(self):
        item = self.records()[0]
        remote = FakeRemote({item['key']: {'hash': item['etag'], 'fsize': item['bytes']}})
        upload.classify(remote, item, False)
        upload.transfer(remote, item, False, threading.Event())
        self.assertEqual(item['status'], 'unchanged')
        self.assertEqual(remote.calls, 0)

    def test_unknown_receipt_is_reconciled_without_reupload(self):
        item = self.records()[0]
        remote = FakeRemote(receipt=False)
        upload.classify(remote, item, False)
        upload.transfer(remote, item, False, threading.Event())
        self.assertEqual(item['status'], 'verified_after_uncertain_receipt')
        self.assertEqual(remote.calls, 1)

    def test_failed_verification_stops_following_uploads(self):
        first, second = self.records()
        remote, stop = FakeRemote(receipt=False, persist=False), threading.Event()
        for item in (first, second):
            upload.classify(remote, item, False)
            upload.transfer(remote, item, False, stop)
        self.assertEqual(first['status'], 'failed')
        self.assertEqual(second['status'], 'not_started')
        self.assertEqual(remote.calls, 1)

    def test_changed_source_is_not_uploaded(self):
        item = self.records()[0]
        remote = FakeRemote()
        upload.classify(remote, item, False)
        Path(item['path']).write_bytes(b'new-build')
        upload.transfer(remote, item, False, threading.Event())
        self.assertEqual(item['status'], 'failed')
        self.assertEqual(remote.calls, 0)

    @unittest.skipUnless(os.name == 'nt', 'DPAPI requires Windows')
    def test_dpapi_round_trip_has_no_plaintext(self):
        fake = {'access_key': 'A' * 40, 'secret_key': 'B' * 40}
        with patch('credentials.state_dir', return_value=self.root / 'state'):
            credentials.save(fake)
            encrypted = credentials.credential_path().read_bytes()
            self.assertNotIn(fake['secret_key'].encode(), encrypted)
            with patch.dict(os.environ, {}, clear=True):
                self.assertEqual(credentials.load(), fake)


if __name__ == '__main__':
    unittest.main()
