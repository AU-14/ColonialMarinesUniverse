#!/usr/bin/env python3
"""Serve the local Garrison 3D art review tool on loopback, without a web dependency.

Run with --prepare after changing maps or assets. This serves exported art only;
it does not connect to a game server or change gameplay.
"""
from __future__ import annotations

import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import subprocess
import sys
from urllib.parse import unquote, urlsplit
import webbrowser

ROOT = Path(__file__).resolve().parent
RESOURCE_ROOTS = (ROOT.parents[1] / "Content.CMU/Resources", ROOT.parents[1] / "Resources")
PUBLIC_DIRECTORIES = {"viewer", "generated"}
PUBLIC_SUFFIXES = {".html", ".css", ".js", ".json", ".png", ".svg", ".glb"}


def public_file(root: Path, request: str, resource_roots=RESOURCE_ROOTS) -> Path | None:
    """Expose only review outputs, including after resolving symlinks and escapes."""
    path = unquote(urlsplit(request).path)
    if "\\" in path or "\0" in path:
        return None
    parts = path.strip("/").split("/")
    if not parts or any(part in ("..", ".") for part in parts):
        return None
    if parts[:2] == ["resources", "Textures"]:
        if not (parts[-1].lower().endswith(".png") or parts[-1] == "meta.json"):
            return None
        for resources in resource_roots:
            base = (resources / "Textures").resolve()
            target = resources.joinpath(*parts[1:]).resolve()
            if base.is_relative_to(resources.resolve()) and target.is_relative_to(base) and target.is_file():
                return target
        return None
    if parts[0] not in PUBLIC_DIRECTORIES:
        return None
    if path.endswith("/"):
        parts.append("index.html")
    base = (root / parts[0]).resolve()
    target = root.joinpath(*parts).resolve()
    compressed_scene = parts[0] == 'generated' and target.name.lower().endswith('.json.gz')
    if (not base.is_relative_to(root.resolve()) or not target.is_relative_to(base) or
            target.suffix.lower() not in PUBLIC_SUFFIXES and not compressed_scene):
        return None
    return target if target.is_file() else None


class ReviewHandler(SimpleHTTPRequestHandler):
    def __init__(self, *args, root=ROOT, **kwargs):
        self.root = root
        super().__init__(*args, directory=str(root), **kwargs)

    def send_head(self):
        if urlsplit(self.path).path in ("", "/", "/viewer"):
            self.send_response(302)
            self.send_header("Location", "/viewer/")
            self.send_header("Content-Length", "0")
            self.end_headers()
            return None
        target = public_file(self.root, self.path)
        if target is None:
            self.send_error(404, "Review file not found")
            return None
        self.review_target = target
        return super().send_head()

    def translate_path(self, path):
        return str(self.review_target)

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        super().end_headers()


def prepare():
    for script, args in (("build_models.py", ["--no-review"]),
                         ("scene.py", ["--variant", "redux", "--all-levels", "--output", str(ROOT / "generated/redux-scene.json")])):
        subprocess.run([sys.executable, str(ROOT / script), *args], check=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=8766)
    parser.add_argument("--prepare", action="store_true", help="Refresh model data and the default saved-map scene first")
    parser.add_argument("--open", action="store_true", help="Open the viewer in the default browser")
    args = parser.parse_args()
    if not 0 <= args.port <= 65535:
        parser.error("port must be between 0 and 65535")
    if args.prepare:
        prepare()
    missing = [str(path.relative_to(ROOT)) for path in
               (ROOT / "viewer/index.html", ROOT / "generated/redux-scene.json", ROOT / "generated/models.json")
               if not path.is_file()]
    if missing:
        parser.error("Missing " + ", ".join(missing) + "; run with --prepare to generate review data")
    with ThreadingHTTPServer(("127.0.0.1", args.port), partial(ReviewHandler, root=ROOT)) as server:
        url = f"http://127.0.0.1:{server.server_port}/viewer/"
        print(f"Garrison 3D map review: {url}\nOffline saved-map art review. Press Ctrl+C to stop.", flush=True)
        if args.open:
            webbrowser.open(url)
        try:
            server.serve_forever()
        except KeyboardInterrupt:
            pass


if __name__ == "__main__":
    main()
