import base64
import importlib.util
import json
import math
from pathlib import Path
import struct
import sys
import tempfile
import unittest

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
SPEC = importlib.util.spec_from_file_location("scene", TOOLS / "scene.py")
scene = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(scene)

def entity(uid, prototype="Chair", parent=1, pos="0,0", rotation=0, extra=None):
    return {"id": uid, "prototype": prototype, "components": {
        "Transform": {"parent": parent, "pos": pos, "rot": rotation}, **(extra or {})},
        "componentTypes": ["Transform", *(extra or {})]}

def fixture_inventory():
    return {"prototypes": [
        {"id": "Chair", "ancestors": ["Seat"], "classification": "visual"},
        {"id": "Marker", "ancestors": [], "classification": "helper"},
    ], "tiles": [{"id": "Floor", "sprite": "/Textures/floor.png", "variants": 3}], "resources": {}}

class SceneTests(unittest.TestCase):

    def test_nested_parent_translation_rotation_and_prototype_defaults(self):
        records = {1: entity(1, "", 0, "10,20", 90), 2: entity(2, parent=1, pos="2,0", rotation="0.5 rad"),
                   3: entity(3, parent=2, pos="1,0", rotation=0)}
        transform = scene.WorldTransforms(records)
        x, y, yaw, root = transform.resolve(3)
        self.assertAlmostEqual(x, 10 - math.sin(.5))
        self.assertAlmostEqual(y, 22 + math.cos(.5))
        self.assertAlmostEqual(yaw, math.pi / 2 + .5)
        self.assertEqual(root, 1)
        records[4] = {"id": 4, "prototype": "DefaultOffset", "components": {"Transform": {"parent": 1}}, "componentTypes": []}
        transform = scene.WorldTransforms(records, {"DefaultOffset": {"Transform": {"pos": "3,0", "rot": 90}}})
        self.assertEqual(tuple(round(value, 6) for value in transform.resolve(4)[:2]), (10, 23))
        self.assertAlmostEqual(transform.resolve(4)[2], math.pi)

    def test_missing_parent_cycles_and_nonfinite_transforms_fail(self):
        records = {1: entity(1, parent=2), 2: entity(2, parent=1), 3: entity(3, parent=88),
                   4: entity(4, parent=0, pos="nan,0")}
        resolver = scene.WorldTransforms(records)
        with self.assertRaisesRegex(scene.TransformError, "cycle"):
            resolver.resolve(1)
        with self.assertRaisesRegex(scene.TransformError, "Missing transform parent"):
            resolver.resolve(3)
        with self.assertRaisesRegex(scene.TransformError, "finite"):
            resolver.resolve(4)

    def test_tile_decoder_is_little_endian_y_major_and_keeps_flags_variant(self):
        values = [(2, 1, 3, 4), (9, 5, 6, 7), (11, 8, 9, 10), (0, 0, 0, 0)]
        blob = b"".join(struct.pack("<iBBB", *value) for value in values)
        chunk = {"ind": "-1,2", "size": 2, "version": 7, "tiles": base64.b64encode(blob).decode()}
        tiles = scene.decode_chunk(chunk)
        self.assertEqual([(tile["x"], tile["y"], tile["palette"]) for tile in tiles],
                         [(-2, 4, 2), (-1, 4, 9), (-2, 5, 11), (-1, 5, 0)])
        self.assertEqual((tiles[1]["flags"], tiles[1]["variant"], tiles[1]["rotationMirroring"]), (5, 6, 7))
        with self.assertRaisesRegex(ValueError, "length"):
            scene.decode_chunk({**chunk, "tiles": base64.b64encode(blob[:-1]).decode()})

    def test_stream_reader_skips_unneeded_component_and_keeps_nested_data(self):
        text = ("meta:\n  format: 7\n  entityCount: 2\nmaps: [1]\ntilemap: {0: Space}\nentities:\n"
                '- proto: ""\n  entities:\n  - uid: 1\n    components:\n    - type: Map\n'
                "    - type: Transform\n- proto: Chair\n  entities:\n  - uid: 2\n    components:\n"
                "    - type: Irrelevant\n      deep: {do: not_read}\n    - type: Transform\n"
                "      parent: 1\n      pos: 2,3\n    - type: ContainerContainer\n      containers:\n"
                "        slot: !type:ContainerSlot\n          ent: 8\n          showEnts: false\n...\n")
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "map.yml"
            path.write_text(text, encoding="utf-8")
            header, records = scene.read_map(path)
        self.assertEqual(header["maps"], [1])
        self.assertEqual(records[2]["components"]["Transform"]["pos"], "2,3")
        self.assertNotIn("Irrelevant", records[2]["components"])
        self.assertIn("Irrelevant", records[2]["componentTypes"])
        self.assertEqual(scene.hidden_container_entities(records), {8})

    def test_export_filters_helpers_containers_orphans_and_regions(self):
        records = {1: entity(1, "", 0, extra={"Map": {}}), 2: entity(2, pos="4,5"),
                   3: entity(3, "Marker"), 4: entity(4, parent=0),
                   5: entity(5, extra={"ContainerContainer": {"containers": {"slot": {"ent": 6}}}}),
                   6: entity(6, parent=5), 7: entity(7, parent=6), 8: entity(8, pos="90,90")}
        header = {"maps": [1], "tilemap": {}, "meta": {"entityCount": 8}}
        result = scene.build_scene(header, records, fixture_inventory(), [], region=(-1, -1, 10, 10))
        self.assertEqual([instance["id"] for instance in result["instances"]], [2, 5])
        excluded = result["diagnostics"]["excluded"]
        self.assertEqual(excluded["hidden_container"], 2)
        self.assertEqual(excluded["nullspace_or_orphan"], 1)
        self.assertEqual(excluded["helper"], 1)
        self.assertEqual(excluded["outside_region"], 1)

    def test_rotated_grid_tiles_keep_correct_origin_and_space_is_omitted(self):
        blob = b"".join(struct.pack("<iBBB", tile, 0, 0, 0) for tile in [1, 0, 0, 0])
        grid = {"chunks": {"0,0": {"ind": "0,0", "size": 2, "version": 7,
                                     "tiles": base64.b64encode(blob).decode()}}}
        records = {1: entity(1, "", 0, "10,20", 90, {"Map": {}, "MapGrid": grid})}
        result = scene.build_scene({"maps": [1], "tilemap": {0: "Space", 1: "Floor"}}, records, fixture_inventory(), [])
        self.assertEqual(len(result["tiles"]), 1)
        self.assertEqual((result["tiles"][0]["x"], result["tiles"][0]["y"]), (10, 20))
        self.assertAlmostEqual(result["tiles"][0]["yaw"], math.pi / 2)
        self.assertEqual(result["map"]["bounds"], [9, 20, 10, 21])

    def test_generated_scene_contract_feeds_region_glb_exporter(self):
        import export_scene

        blob = struct.pack("<iBBB", 1, 0, 1, 0)
        grid = {"chunks": {"0,0": {"ind": "0,0", "size": 1, "version": 7,
                                     "tiles": base64.b64encode(blob).decode()}}}
        records = {1: entity(1, "", 0, "10,20", 90, {"Map": {}, "MapGrid": grid}),
                   2: entity(2, pos="2,0")}
        model = {"id": "ChairModel", "label": "Test chair", "status": "draft", "sourcePrototypes": ["Chair"],
                 "parts": [{"min": [-.2, -.2, 0], "max": [.2, .2, .5], "color": "#FF0000", "label": "seat"}]}
        exported = scene.build_scene({"maps": [1], "tilemap": {1: "Floor"}}, records, fixture_inventory(), [model])
        exported["map"]["path"] = "synthetic-map.yml"
        exported["tilePalette"]["1"].update({"color": "#0000FF", "variantColors": ["#0000FF", "#00FF00"]})
        glb, report = export_scene.export_region(exported, [model], [10, 21, 0], radius=4)
        self.assertEqual((report["exportedEntities"], report["floorTiles"]), (1, 1))
        json_length = struct.unpack_from("<I", glb, 12)[0]
        document = json.loads(glb[20:20 + json_length])
        root = document["nodes"][document["scenes"][0]["nodes"][0]]
        self.assertEqual(root["translation"], [10, 0, -22])
        self.assertEqual(root["extras"]["savedUid"], 2)
        colors = [material["pbrMetallicRoughness"]["baseColorFactor"] for material in document["materials"]]
        self.assertIn([0, 1, 0, 1], colors)

if __name__ == "__main__":
    unittest.main()
