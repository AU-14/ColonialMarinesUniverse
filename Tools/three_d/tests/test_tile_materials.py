import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest

from PIL import Image
import yaml

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
SPEC = importlib.util.spec_from_file_location("build_tile_materials", TOOLS / "build_tile_materials.py")
materials = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(materials)


class TileMaterialTests(unittest.TestCase):
    def test_variant_order_source_metadata_and_deterministic_yaml(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            folder = root / "Resources/Textures/Tiles"
            folder.mkdir(parents=True)
            image = Image.new("RGBA", (64, 32), (120, 80, 40, 255))
            image.paste((10, 20, 30, 255), (32, 0, 64, 32))
            image.save(folder / "floor.png")
            (folder / "meta.json").write_text(json.dumps({"license": "Existing source terms"}), encoding="utf-8")
            inventory = {"tiles": [{"id": "Space"}, {"id": "FloorB", "sprite": "/Textures/Tiles/floor.png", "variants": 2},
                                   {"id": "FloorA", "sprite": "/Textures/Tiles/floor.png", "variants": 2}]}
            records, report = materials.build_materials(root, inventory)
            encoded = materials.render_yaml(records)
            reversed_records, _ = materials.build_materials(root, {"tiles": list(reversed(inventory["tiles"]))})
        self.assertEqual(encoded, materials.render_yaml(reversed_records))
        self.assertEqual([record["tile"] for record in records], ["FloorA", "FloorB"])
        self.assertEqual(records[0]["colors"], ["#785028", "#0A141E"])
        self.assertEqual(records[0]["sourceMetadata"], ["Resources/Textures/Tiles/meta.json"])
        self.assertEqual(yaml.safe_load(encoded)[0]["type"], "cmu3DTileMaterial")
        self.assertEqual(report["skippedTilesWithoutSprite"], ["Space"])

    def test_missing_source_texture_fails_instead_of_silent_gray_material(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaisesRegex(ValueError, "Missing tile texture"):
                materials.build_materials(Path(directory), {"tiles": [{"id": "Missing", "sprite": "/Textures/missing.png"}]})

    def test_check_mode_detects_stale_bytes_without_writing(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "materials.yml"
            materials.write_or_check(output, b"expected\n", False)
            materials.write_or_check(output, b"expected\n", True)
            output.write_bytes(b"local change\n")
            with self.assertRaisesRegex(ValueError, "Stale or missing"):
                materials.write_or_check(output, b"expected\n", True)
            self.assertEqual(output.read_bytes(), b"local change\n")


if __name__ == "__main__":
    unittest.main()
