"""Exercise CDN upload recovery over real local HTTP connections."""
import contextlib
import io
import json
import socket
import sys
import tempfile
import threading
import unittest
import zipfile
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
with patch.dict("os.environ", {"PUBLISH_TOKEN": "local-test-token", "GITHUB_SHA": "test-version"}):
    import publish_multi_request as publisher


class PublishTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.expected = {}
        for name in ("SS14.Client.zip", "SS14.Server_linux-x64.zip", "SS14.Server_win-x64.zip"):
            with zipfile.ZipFile(self.root / name, "w") as archive:
                archive.writestr("fixture.bin", b"fixture" * 100000)
            self.expected[name] = (self.root / name).read_bytes()
        self.starts = 0
        self.finishes = 0
        self.uploads = {}
        self.upload_attempts = []
        self.disconnects = 0
        self.upload_status = 204
        self.published = False
        self.redirect_start = False
        self.redirect_followed = False
        owner = self

        class Handler(BaseHTTPRequestHandler):
            protocol_version = "HTTP/1.1"

            def log_message(self, *_args):
                pass

            def respond(self, status, body=b""):
                self.send_response(status)
                self.send_header("Content-Length", str(len(body)))
                self.send_header("Connection", "close")
                self.end_headers()
                self.wfile.write(body)
                self.close_connection = True

            def handle_expect_100(self):
                name = self.headers.get("Robust-Cdn-Publish-File")
                if name in owner.uploads:
                    self.respond(409, b"File already published")
                    return False
                return super().handle_expect_100()

            def do_GET(self):
                if self.path == "/redirect-target":
                    owner.redirect_followed = True
                builds = {"test-version": {}} if owner.published else {}
                self.respond(200, json.dumps({"builds": builds}).encode())

            def do_POST(self):
                if self.headers.get("Authorization") != "Bearer local-test-token":
                    self.respond(403)
                    return
                length = int(self.headers.get("Content-Length", 0))
                if self.path.endswith("/start"):
                    self.rfile.read(length)
                    if owner.redirect_start:
                        self.send_response(302)
                        self.send_header("Location", "/redirect-target")
                        self.send_header("Content-Length", "0")
                        self.end_headers()
                        return
                    if owner.published:
                        self.respond(409, b"Version already exists")
                        return
                    owner.starts += 1
                    owner.uploads.clear()
                    self.respond(204)
                elif self.path.endswith("/file"):
                    name = self.headers["Robust-Cdn-Publish-File"]
                    owner.upload_attempts.append(name)
                    if owner.upload_status != 204:
                        self.respond(owner.upload_status, b"Upload rejected")
                        return
                    if name == "SS14.Server_win-x64.zip" and owner.disconnects:
                        owner.disconnects -= 1
                        owner.uploads[name] = self.rfile.read(16)
                        self.connection.shutdown(socket.SHUT_RDWR)
                        self.close_connection = True
                        return
                    owner.uploads[name] = self.rfile.read(length)
                    self.respond(204)
                elif self.path.endswith("/finish"):
                    self.rfile.read(length)
                    if owner.uploads != owner.expected:
                        self.respond(422, b"Incomplete release")
                        return
                    owner.finishes += 1
                    owner.published = True
                    self.respond(204)
                else:
                    self.respond(404)

        self.server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        self.worker = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.worker.start()
        self.addCleanup(self.stop_server)
        self.settings = patch.multiple(publisher, RELEASE_DIR=str(self.root), VERSION="test-version",
                                       PUBLISH_TOKEN="local-test-token",
                                       ROBUST_CDN_URL=f"http://127.0.0.1:{self.server.server_port}/")
        self.settings.start()
        self.addCleanup(self.settings.stop)
        engine = patch.object(publisher, "get_engine_version", return_value="295.0.2")
        engine.start()
        self.addCleanup(engine.stop)

    def stop_server(self):
        self.server.shutdown()
        self.server.server_close()
        self.worker.join()

    def run_publish(self, *args):
        with patch.object(sys, "argv", ["publish", *args]), contextlib.redirect_stdout(io.StringIO()):
            publisher.main()

    def test_interrupted_upload_restarts_transaction_and_reuses_exact_packages(self):
        self.disconnects = 1
        self.run_publish()
        self.assertEqual(self.starts, 2)
        self.assertEqual(self.finishes, 1)
        self.assertEqual(self.uploads, self.expected)
        self.assertEqual(self.upload_attempts.count("SS14.Client.zip"), 2)
        self.assertEqual({p.name: p.read_bytes() for p in self.root.glob("*.zip")}, self.expected)

    def test_permanent_upload_failure_never_restarts_or_finalizes(self):
        self.upload_status = 403
        with self.assertRaisesRegex(publisher.PublishError, "HTTP 403"):
            self.run_publish()
        self.assertEqual(self.starts, 1)
        self.assertEqual(self.finishes, 0)

    def test_retry_limit_never_finalizes_partial_release(self):
        self.upload_status = 503
        with self.assertRaisesRegex(publisher.PublishError, "HTTP 503"):
            self.run_publish()
        self.assertEqual(self.starts, 3)
        self.assertEqual(self.finishes, 0)
        self.assertFalse(self.published)

    def test_missing_platform_is_rejected_before_starting_publish(self):
        (self.root / "SS14.Server_linux-x64.zip").unlink()
        with self.assertRaisesRegex(ValueError, "requires exactly"):
            self.run_publish()
        self.assertEqual(self.starts, 0)
        self.assertEqual(self.upload_attempts, [])

    def test_existing_release_is_not_replaced(self):
        self.published = True
        self.run_publish()
        self.assertEqual(self.starts, 0)
        self.assertEqual(self.upload_attempts, [])
        self.assertEqual(self.finishes, 0)

    def test_check_reports_status_and_engine_without_mutation(self):
        output = self.root / "github-output"
        for existing in (False, True):
            with self.subTest(published=existing):
                self.published = existing
                output.write_text("")
                with patch.dict("os.environ", {"GITHUB_OUTPUT": str(output)}):
                    self.run_publish("check")
                self.assertIn(f"already-published={str(existing).lower()}", output.read_text())
                if not existing:
                    self.assertIn("engine-version=295.0.2", output.read_text())
        self.assertEqual(self.starts, 0)
        self.assertEqual(self.upload_attempts, [])

    def test_verification_only_never_mutates_cdn(self):
        self.run_publish("verify")
        self.assertEqual(self.starts, 0)
        self.assertEqual(self.upload_attempts, [])
        self.assertEqual(self.finishes, 0)

    def test_publish_credentials_are_not_forwarded_through_redirects(self):
        self.redirect_start = True
        with self.assertRaisesRegex(publisher.PublishError, "HTTP 302"):
            self.run_publish()
        self.assertFalse(self.redirect_followed)
        self.assertEqual(self.starts, 0)
        self.assertEqual(self.upload_attempts, [])


if __name__ == "__main__":
    unittest.main()
