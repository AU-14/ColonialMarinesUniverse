"""CMU14: prevent mismatched or tampered private packages from reaching publishing."""
import hashlib
import io
import json
import sys
import tempfile
import unittest
import urllib.error
from types import SimpleNamespace
from unittest.mock import patch
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from request_private_package import PackageError, receive_package, request_package
from request_private_package import download_release_asset, fetch_package


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

    def test_selects_package_asset_without_downloading_private_diagnostics(self):
        asset = {"id": 42, "name": "package.zip", "state": "uploaded", "size": 500}
        release = {"draft": False, "prerelease": True, "target_commitish": "b" * 40,
                   "assets": [{"id": 43, "name": "diagnostics.zip"}, asset]}
        archive = self.root / "download.zip"
        with patch("request_private_package.api", return_value=release) as api, \
                patch("request_private_package.download_release_asset") as download:
            fetch_package(100, "request-one", "b" * 40, "token", archive)
        download.assert_called_once_with(42, "token", archive)
        api.assert_called_once_with("/releases/tags/cmu-ci-100-package", "token", allow_not_found=True)

    def test_invalid_release_does_not_fall_back_to_artifacts(self):
        release = {"draft": False, "prerelease": True, "target_commitish": "b" * 40,
                   "assets": [{"id": 42, "name": "package.zip", "state": "uploaded", "size": 500}]}
        for change in ({"target_commitish": "c" * 40}, {"draft": True}, {"assets": []}):
            with self.subTest(change=change), \
                    patch("request_private_package.api", return_value={**release, **change}) as api, \
                    patch("request_private_package.download_release_asset") as download:
                with self.assertRaises(PackageError):
                    fetch_package(100, "request-one", "b" * 40, "token", self.root / "download.zip")
                self.assertEqual(api.call_count, 1)
                download.assert_not_called()

    def test_old_workflow_remains_downloadable_during_rollout(self):
        with patch("request_private_package.api", side_effect=[None, {"artifacts": [
                {"id": 77, "name": "server-request-one", "expired": False}]},
                "https://signed.blob.core.windows.net/package"]) as api, \
                patch("request_private_package.download_artifact") as download:
            destination = self.root / "download.zip"
            fetch_package(100, "request-one", "b" * 40, "token", destination)
        download.assert_called_once_with("https://signed.blob.core.windows.net/package", destination)
        self.assertEqual(api.call_args.args, ("/actions/artifacts/77/zip", "token"))

    def test_release_asset_supports_direct_response_and_credential_free_redirect(self):
        for redirect in (False, True):
            with self.subTest(redirect=redirect), patch("request_private_package.urllib.request.build_opener") as factory:
                destination = self.root / "download.zip"
                if redirect:
                    location = "https://release-assets.githubusercontent.com/package"
                    factory.return_value.open.side_effect = [urllib.error.HTTPError(
                        "https://api.github.com/asset", 302, "Found", {"Location": location}, None),
                        io.BytesIO(self.package)]
                else:
                    factory.return_value.open.return_value = io.BytesIO(self.package)
                download_release_asset(42, "private-token", destination)
                self.assertEqual(destination.read_bytes(), self.package)
                calls = factory.return_value.open.call_args_list
                self.assertEqual(calls[0].args[0].get_header("Authorization"), "Bearer private-token")
                if redirect:
                    self.assertEqual(calls[1].args, (location,))
                    self.assertEqual(calls[1].kwargs, {"timeout": 120})

    def test_untrusted_asset_redirect_is_rejected_before_download(self):
        with patch("request_private_package.urllib.request.build_opener") as factory:
            factory.return_value.open.side_effect = urllib.error.HTTPError(
                "https://api.github.com/asset", 302, "Found", {"Location": "https://example.com/leak"}, None)
            with self.assertRaisesRegex(PackageError, "Unexpected GitHub artifact download host"):
                download_release_asset(42, "private-token", self.root / "download.zip")
            self.assertEqual(factory.return_value.open.call_count, 1)

    def test_failed_regression_run_never_downloads_a_package(self):
        run = {"id": 100, "head_sha": "b" * 40, "event": "workflow_dispatch",
               "path": ".github/workflows/package-server.yml", "display_title": "package-request-one",
               "status": "completed", "conclusion": "failure"}
        args = SimpleNamespace(source_sha="a" * 40, patch=None, platform="linux-x64",
                               require_audio=True, timeout_minutes=1, output=self.root / "release")
        with patch.dict("os.environ", {"CMU_PRIVATE_BUILD_TOKEN": "token"}, clear=True), \
                patch("request_private_package.uuid.uuid4", return_value=SimpleNamespace(hex="request-one")), \
                patch("request_private_package.api", side_effect=[{"object": {"sha": "b" * 40}},
                                                                  None, {"workflow_runs": [run]}]), \
                patch("request_private_package.fetch_package") as fetch:
            with self.assertRaisesRegex(PackageError, "Private build failed"):
                request_package(args)
            fetch.assert_not_called()


if __name__ == "__main__":
    unittest.main()
