"""Regression checks for art inheritance and streamed format-7 map counting."""

import importlib.util
from pathlib import Path
import tempfile
import unittest


SPEC = importlib.util.spec_from_file_location("inventory", Path(__file__).resolve().parents[1] / "inventory.py")
inventory = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(inventory)


class InventoryTests(unittest.TestCase):
    def test_child_sprite_fields_and_layer_list_override_parents(self):
        resolver = inventory.Resolver({
            "base": {"id": "base", "abstract": True, "components": [
                {"type": "Sprite", "sprite": "base.rsi", "state": "base", "layers": [{"state": "old"}]}]},
            "child": {"id": "child", "parent": "base", "components": [
                {"type": "Sprite", "state": "child", "layers": [{"state": "new", "visible": False}]}]},
        })
        child = resolver.resolve("child")
        sprite = inventory.component_map(child)["Sprite"]
        self.assertEqual(sprite["sprite"], "base.rsi")
        self.assertEqual(sprite["state"], "child")
        self.assertEqual(sprite["layers"], [{"state": "new", "visible": False}])
        self.assertFalse(child["abstract"])
        self.assertEqual(resolver.resolve("base")["components"][0]["state"], "base")

    def test_first_parent_wins_conflicts_and_later_parent_fills_missing_fields(self):
        resolver = inventory.Resolver({
            "first": {"id": "first", "components": [
                {"type": "Sprite", "sprite": "first.rsi", "color": "red"}]},
            "second": {"id": "second", "components": [
                {"type": "Sprite", "sprite": "second.rsi", "state": "second"}, {"type": "Marker"}]},
            "child": {"id": "child", "parent": ["first", "second"], "components": [
                {"type": "Sprite", "color": "green"}]},
        })
        components = inventory.component_map(resolver.resolve("child"))
        self.assertEqual(components["Sprite"],
                         {"type": "Sprite", "sprite": "first.rsi", "color": "green", "state": "second"})
        self.assertIn("Marker", components)

    def test_cycle_and_missing_parent_report_full_path(self):
        resolver = inventory.Resolver({"a": {"parent": "b"}, "b": {"parent": "a"}, "c": {"parent": "lost"}})
        with self.assertRaisesRegex(inventory.ResolutionError, "a -> b -> a"):
            resolver.resolve("a")
        with self.assertRaisesRegex(inventory.ResolutionError, "c -> lost"):
            resolver.resolve("c")

    def test_robust_type_tags_are_inert_and_preserve_fields(self):
        value = inventory.load_yaml("behavior: !type:SpawnEntitiesBehavior\n  spawn: {Chair: {min: 1}}\n")
        self.assertEqual(value["behavior"]["__type_tag__"], "SpawnEntitiesBehavior")
        self.assertEqual(value["behavior"]["spawn"]["Chair"]["min"], 1)

    def test_unquoted_on_off_are_sprite_names_but_true_false_are_boolean(self):
        value = inventory.load_yaml("state: off\nother: on\nvisible: false\nenabled: True\n")
        self.assertEqual(value["state"], "off")
        self.assertEqual(value["other"], "on")
        self.assertIs(value["visible"], False)
        self.assertIs(value["enabled"], True)

    def test_stream_map_counts_anonymous_entities_and_sprite_overrides(self):
        text = ("meta:\n  format: 7\n  entityCount: 3\ntilemap:\n  0: Space\n  1: Floor\nentities:\n"
                '- proto: ""\n  entities:\n  - uid: 1\n- proto: Chair\n  entities:\n  - uid: 2\n'
                "    components:\n    - type: Sprite\n      color: red\n  - uid: 3\n")
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "map.yml"
            path.write_text(text, encoding="utf-8")
            result = inventory.scan_map(path)
            self.assertEqual(result["prototypeCounts"], {"": 1, "Chair": 2})
            self.assertEqual(result["spriteOverrideCounts"], {"Chair": 1})
            self.assertEqual(result["tilePalette"], ["Floor", "Space"])
            path.write_text(text.replace("entityCount: 3", "entityCount: 4"), encoding="utf-8")
            stale = inventory.scan_map(path)
            self.assertFalse(stale["metadataCountMatches"])
            self.assertEqual(stale["entityCount"], 3)
            self.assertEqual(stale["metadataEntityCount"], 4)
            path.write_text(text.replace("  - uid: 3", "   - uid: 3"), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Unsupported entity indentation"):
                inventory.scan_map(path)

    def test_helper_hidden_and_dynamic_sprite_classification(self):
        self.assertEqual(inventory.classification({"Marker": {}, "Sprite": {}}), "helper")
        self.assertEqual(inventory.classification({"Sprite": {"visible": False}}), "hidden")
        self.assertEqual(inventory.classification({"Sprite": {}}), "visual")
        self.assertEqual(inventory.classification({"Icon": {"sprite": "item.rsi"}}), "no_sprite")
        self.assertEqual(inventory.sprite_resources({"sprite": "base.rsi", "layers": [
            {"rsi": "/Textures/overlay.rsi", "state": "overlay"}]}),
            ["/Textures/base.rsi", "/Textures/overlay.rsi"])


if __name__ == "__main__":
    unittest.main()
