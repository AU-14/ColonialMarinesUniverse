import importlib.util
import math
from pathlib import Path
import unittest

from test_models import sample, unpack

SPEC = importlib.util.spec_from_file_location("export_scene", Path(__file__).parents[1] / "export_scene.py")
exporter = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(exporter)


class SceneExportTests(unittest.TestCase):
    def test_elevated_threshold_exports_a_closed_wedge_in_the_grid_direction(self):
        scene = {'map': {'path': 'test.yml'}, 'instances': [], 'tiles': [
            {'x': 10, 'y': 20, 'z': -2, 'yaw': math.pi/2, 'palette': 1, 'elevation': .39,
             'foundationDepth': .07, 'elevationRamp': {'direction': [1, 0], 'bottom': .39, 'top': .78}}]}
        document, _ = unpack(exporter.export_region(scene, [], [9.5,20.5,-2], radius=1)[0])
        tile, foundation, ramp = document['nodes']
        self.assertEqual(tile['translation'][::2], [10,-20])
        self.assertAlmostEqual(tile['translation'][1], -1.61)
        self.assertEqual(tile['children'], [1,2])
        self.assertEqual(ramp['scale'], [1,.39,1])
        self.assertAlmostEqual(ramp['rotation'][1], -math.sqrt(.5))
        self.assertAlmostEqual(ramp['translation'][1]+ramp['scale'][1]/2,.38)

    def test_yaml_string_variant_bounds_export_as_numeric_geometry_without_mutation(self):
        scene = self.scene()
        scene['instances'][0]['geometryKey'] = 'TestModel:fit'
        part = {'min': '-0.125, -0.5, 0.25', 'max': '0.375, 0.5, 2.75',
                'color': '#FFFFFF', 'label': 'fitted panel'}
        scene['geometryVariants'] = {'TestModel:fit': [part]}
        blob, report = exporter.export_region(scene, [sample()], [10, 20, 0], floors=False)
        document, _ = unpack(blob)
        self.assertEqual(report['solidParts'], 1)
        self.assertEqual(document['nodes'][1]['translation'], [.125, 1.5, 0])
        self.assertEqual(document['nodes'][1]['scale'], [.5, 2.5, 1])
        self.assertEqual(part['min'], '-0.125, -0.5, 0.25')
        self.assertEqual(part['max'], '0.375, 0.5, 2.75')

    def test_source_rotation_and_connected_variant_survive_export(self):
        scene = self.scene()
        scene['instances'][0].update(renderYaw=0, geometryKey='TestModel:3')
        scene['geometryVariants'] = {'TestModel:3': [{'min': [-.1,-.5,0], 'max': [.1,.5,2],
                                                     'color': '#FFFFFF', 'label': 'vertical panel'}]}
        blob, _ = exporter.export_region(scene, [sample()], [10,20,0], floors=False)
        document, _ = unpack(blob)
        self.assertEqual(document['nodes'][0]['rotation'], [0,0,0,1])
        self.assertEqual(document['nodes'][1]['scale'], [.2,2,1])
        self.assertEqual(scene['instances'][0]['yaw'], math.pi/2)

    def test_surface_lift_changes_render_translation_without_changing_map_plane_selection(self):
        scene = self.scene()
        scene['instances'][0]['renderOffset'] = [0, 0, .862]
        blob, report = exporter.export_region(scene, [sample()], [10, 20, 0], floors=False)
        document, _ = unpack(blob)
        self.assertEqual(report['exportedEntities'], 1)
        self.assertEqual(document['nodes'][0]['translation'], [10, .862, -20])
        self.assertEqual(scene['instances'][0]['position'], [10, 20, 0])

    def scene(self):
        return {"map": {"path": "test.yml"}, "instances": [
            {"id": 1, "prototype": "Chair", "position": [10, 20, 0], "yaw": math.pi / 2,
             "modelId": "TestModel", "matchKind": "exact"},
            {"id": 2, "prototype": "FancyChair", "position": [11, 20, 0], "yaw": 0,
             "modelId": "TestModel", "matchKind": "inherited"},
            {"id": 3, "prototype": "Chair", "position": [10, 20, 1], "yaw": 0,
             "modelId": "TestModel", "matchKind": "exact"}], "tiles": [], "tilePalette": {}}

    def test_hierarchy_rotation_and_plane_selection_survive_glb_export(self):
        blob, report = exporter.export_region(self.scene(), [sample()], [10, 20, 0], floors=False)
        document, _ = unpack(blob)
        entity = document["nodes"][0]
        self.assertEqual(entity["translation"], [10, 0, -20])
        self.assertAlmostEqual(entity["rotation"][1], math.sqrt(.5))
        self.assertAlmostEqual(entity["rotation"][3], math.sqrt(.5))
        self.assertEqual(entity["children"], [1, 2])
        self.assertEqual(document["nodes"][1]["translation"], [1, 3, -1])
        self.assertEqual(report["exportedEntities"], 1)
        self.assertEqual(report["omittedEntities"], 1)

    def test_inherited_candidates_are_opt_in_and_meshes_are_shared(self):
        blob, report = exporter.export_region(self.scene(), [sample()], [10, 20, 0], inherited=True)
        document, _ = unpack(blob)
        self.assertEqual(report["exportedEntities"], 2)
        self.assertEqual(len(document["meshes"]), 2)
        self.assertEqual(document["nodes"][1]["mesh"], document["nodes"][4]["mesh"])
        self.assertEqual(document["nodes"][3]["extras"]["matchKind"], "inherited")
        self.assertEqual(document["nodes"][3]["extras"]["status"], "draft")

    def test_invalid_or_empty_regions_are_reported(self):
        for center, radius in (([10, 20, 0], 0), ([math.nan, 0, 0], 10), ([100, 100, 0], 1)):
            with self.assertRaises(ValueError):
                exporter.export_region(self.scene(), [sample()], center, radius)

    def test_rotated_tile_uses_its_center_for_selection_and_actual_palette_color(self):
        scene = {"map": {"path": "test.yml"}, "instances": [],
                 "tiles": [{"x": 2, "y": 2, "z": 0, "yaw": math.pi, "palette": 1}],
                 "tilePalette": {"1": {"color": "#00FF00"}}}
        blob, report = exporter.export_region(scene, [], [1.5, 1.5, 0], radius=.1)
        document, _ = unpack(blob)
        self.assertEqual(report["floorTiles"], 1)
        self.assertEqual(document["nodes"][0]["translation"], [2, 0, -2])
        self.assertEqual(document["nodes"][1]["translation"], [.5, -.04, -.5])
        self.assertEqual(document["materials"][0]["pbrMetallicRoughness"]["baseColorFactor"], [0, 1, 0, 1])

    def test_floor_variants_keep_their_sampled_colors(self):
        scene = {"map": {"path": "test.yml"}, "instances": [],
                 "tiles": [{"x": i, "y": 0, "z": 0, "palette": 1, "variant": i} for i in range(2)],
                 "tilePalette": {"1": {"color": "#FF0000", "variantColors": ["#FF0000", "#00FF00"]}}}
        blob, _ = exporter.export_region(scene, [], [1, 0, 0])
        document, _ = unpack(blob)
        colors = [material["pbrMetallicRoughness"]["baseColorFactor"] for material in document["materials"]]
        self.assertEqual(colors, [[1, 0, 0, 1], [0, 1, 0, 1]])
        self.assertNotEqual(document["nodes"][1]["mesh"], document["nodes"][3]["mesh"])


if __name__ == "__main__":
    unittest.main()
