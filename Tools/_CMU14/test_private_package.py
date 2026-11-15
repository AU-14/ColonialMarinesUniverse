"""CMU14: prevent mismatched or tampered private packages from reaching publishing."""
import hashlib
import io
import json
import sys
import tempfile
import unittest
from types import SimpleNamespace
from unittest.mock import patch
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from request_private_package import PackageError, receive_package, request_package


class PrivatePackageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.expected = {
            "schema": 1, "request_id": "request-one", "source_sha": "a" * 40,
            "build_sha": "b" * 40, "platform": "linux-x64", "patch_sha256": "", "audio": True,
        }
        package = io.BytesIO()
        with zipfile.ZipFile(package, "w") as archive:
            archive.writestr("Content.Server.dll", b"server assembly fixture")
        self.package = package.getvalue()
        self.name = "SS14.Server_linux-x64.zip"
        self.receipt = {**self.expected, "package": self.name, "size": len(self.package),
                        "sha256": hashlib.sha256(self.package).hexdigest()}

    def artifact(self, *, receipt=None, package=None, extra=None):
        path = self.root / "artifact.zip"
        with zipfile.ZipFile(path, "w") as archive:
            archive.writestr("manifest.json", json.dumps(receipt or self.receipt))
            archive.writestr(self.name, self.package if package is None else package)
            if extra:
                archive.writestr(extra, b"unexpected source or log")
        return path

    def test_accepts_matching_package_without_publishing_the_receipt(self):
        output = self.root / "release"
        result = receive_package(self.artifact(), output, self.expected)
        self.assertEqual(result.read_bytes(), self.package)
        self.assertEqual([p.name for p in output.iterdir()], [self.name])

    def test_rejects_replayed_or_different_build_receipts(self):
        for key, value in (("request_id", "earlier-request"), ("source_sha", "c" * 40),
                           ("build_sha", "d" * 40), ("audio", False),
                           ("patch_sha256", "e" * 64), ("platform", "win-x64")):
            with self.subTest(field=key):
                receipt = {**self.receipt, key: value}
                output = self.root / "release"
                with self.assertRaisesRegex(PackageError, "does not match"):
                    receive_package(self.artifact(receipt=receipt), output, self.expected)
                self.assertFalse((output / self.name).exists())

    def test_client_receipt_cannot_be_substituted_for_server_package(self):
        self.name = "SS14.Client.zip"
        self.expected["platform"] = "client"
        self.receipt = {**self.receipt, **self.expected, "package": self.name}
        artifact = self.artifact()
        server_request = {**self.expected, "platform": "linux-x64"}
        with self.assertRaisesRegex(PackageError, "Unexpected private artifact"):
            receive_package(artifact, self.root / "rejected", server_request)
        result = receive_package(artifact, self.root / "accepted", self.expected)
        self.assertEqual(result.name, "SS14.Client.zip")
        self.assertEqual(result.read_bytes(), self.package)

    def test_mid_publish_revision_change_stops_before_dispatch(self):
        with patch.dict("os.environ", {"CMU_PRIVATE_BUILD_TOKEN": "test-token", "CMU_PRIVATE_BUILD_SHA": "b" * 40}, clear=True), \
                patch("request_private_package.api", return_value={"object": {"sha": "c" * 40}}) as api:
            with self.assertRaisesRegex(PackageError, "changed since publish preparation"):
                request_package(SimpleNamespace(source_sha="a" * 40, patch=None))
            api.assert_called_once_with("/git/ref/heads/main", "test-token")

    def test_rejects_tampered_bytes_without_leaving_a_publishable_file(self):
        damaged = bytearray(self.package)
        damaged[35] ^= 1
        output = self.root / "release"
        with self.assertRaisesRegex(PackageError, "integrity"):
            receive_package(self.artifact(package=damaged), output, self.expected)
        self.assertEqual(list(output.iterdir()), [])

    def test_rejects_source_logs_and_traversal_entries(self):
        for extra in ("build.log", "source/private.cs", "../escaped.txt"):
            with self.subTest(extra=extra):
                output = self.root / "release"
                with self.assertRaisesRegex(PackageError, "Unexpected private artifact"):
                    receive_package(self.artifact(extra=extra), output, self.expected)
                self.assertFalse(output.exists())
                self.assertFalse((self.root / "escaped.txt").exists())


if __name__ == "__main__":
    unittest.main()
