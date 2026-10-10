"""Ensure the local art-review server cannot expose the rest of the repository."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location("serve", Path(__file__).parents[1] / "serve.py")
serve = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(serve)


class PublicFileTests(unittest.TestCase):
    def test_sprite_route_prefers_overlay_and_serves_only_texture_images_and_metadata(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            resources = [root / "cmu", root / "upstream"]
            for directory in resources:
                (directory / "Textures/example.rsi").mkdir(parents=True)
                (directory / "Textures/example.rsi/on.png").write_bytes(b"image")
                (directory / "Textures/example.rsi/meta.json").write_text("{}")
            self.assertEqual(serve.public_file(root, "/resources/Textures/example.rsi/on.png", resources),
                             (resources[0] / "Textures/example.rsi/on.png").resolve())
            self.assertEqual(serve.public_file(root, "/resources/Textures/example.rsi/meta.json", resources),
                             (resources[0] / "Textures/example.rsi/meta.json").resolve())
            for path in ("/resources/Maps/map.png", "/resources/Textures/example.rsi/private.json",
                         "/resources/Textures/../../private.png"):
                self.assertIsNone(serve.public_file(root, path, resources))

    def test_review_routes_resolve_but_repository_and_traversal_do_not(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "viewer").mkdir()
            index = root / "viewer/index.html"
            index.write_text("review")
            (root / "private.json").write_text("private")
            (root / "generated").mkdir()
            chunk = root / "generated/scene.json.gz"
            chunk.write_bytes(b"compressed scene")
            (root / "generated/private.tar.gz").write_bytes(b"private")
            (root / "viewer/private.json.gz").write_bytes(b"private")
            self.assertEqual(serve.public_file(root, "/generated/scene.json.gz"), chunk.resolve())
            (root / "viewer/private.py").write_text("private source")
            self.assertEqual(serve.public_file(root, "/viewer/"), index.resolve())
            self.assertEqual(serve.public_file(root, "/viewer/index.html?refresh=1"), index.resolve())
            for path in ("/private.json", "/viewer/../private.json", "/viewer/%2e%2e/private.json",
                         "/viewer/..%5cprivate.json", "/viewer/private.py", "/viewer/missing.json",
                         "/viewer/%00index.html", "/generated/", "/generated/private.tar.gz", "/viewer/private.json.gz"):
                with self.subTest(path=path):
                    self.assertIsNone(serve.public_file(root, path))


if __name__ == "__main__":
    unittest.main()
