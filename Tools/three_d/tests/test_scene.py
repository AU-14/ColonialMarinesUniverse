import base64
import importlib.util
import json
import math
from pathlib import Path
import struct
import sys
import tempfile
import unittest

from PIL import Image


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
    def test_connected_wall_export_has_numeric_geometry_after_yaml_vector_input(self):
        parts = [{'min': f'{x}, {y}, 0', 'max': f'{x+.5}, {y+.5}, 2.8', 'color': '#FFFFFF'}
                 for x, y in ((0, -.5), (0, 0), (-.5, 0), (-.5, -.5))]
        models = [{'id': 'Wall', 'sourcePrototypes': ['Wall'], 'parts': parts,
                   'cornerSurfaces': [f'Surface{i}' for i in range(32)]}]
        records = {1: entity(1, '', 0, extra={'Map': {}, 'MapGrid': {}}),
                   2: entity(2, 'Wall', pos='.5,.5'), 3: entity(3, 'Wall', pos='1.5,.5')}
        defaults = {'Wall': {'Transform': {'anchored': True}, 'IconSmooth': {'key': 'wall'}}}
        inventory = {'prototypes': [{'id': 'Wall', 'classification': 'visual', 'ancestors': []}],
                     'tiles': [], 'resources': {}}
        result = json.loads(json.dumps(scene.build_scene({'maps': [1], 'tilemap': {}},
            records, inventory, models, defaults)))
        for instance in result['instances']:
            geometry = result['geometryVariants'][instance['geometryKey']]
            self.assertEqual(len(geometry), 4)
            for original, exported in zip(parts, geometry):
                self.assertEqual(exported['min'], [float(v) for v in original['min'].split(',')])
                self.assertEqual(exported['max'], [float(v) for v in original['max'].split(',')])
                self.assertEqual(exported['surfaceAxis'], 'XY')
        self.assertEqual([e['connectionMask'] for e in result['instances']], [4, 8])

    def test_missing_fold_geometry_still_compares_the_actual_visible_sprite_layer(self):
        components={'Sprite':{'layers':[{'state':'down','map':['unfoldedLayer']},
                                        {'state':'packed','map':['foldedLayer'],'visible':False}]},
                    'GenericVisualizer':{'visuals':{'enum.FoldedVisuals.State':{
                        'unfoldedLayer':{False:{'visible':True},True:{'visible':False}},
                        'foldedLayer':{'true':{'visible':True},'false':{'visible':False}}}}}}
        self.assertEqual(scene.fold_reference_state(components,True),'packed')
        self.assertEqual(scene.fold_reference_state(components,False),'down')
        self.assertIsNone(scene.fold_reference_state({},True))
        self.assertIsNone(scene.fold_reference_state(components,'true'))

    def test_saved_fold_state_overrides_initial_pose_without_moving_simulation_coordinates(self):
        inventory=fixture_inventory()
        records={1:entity(1,'',0,extra={'Map':{}}),2:entity(2),
                 3:entity(3,pos='2,3',rotation=90,extra={'Foldable':{'folded':False}})}
        models=[{'id':'Flat','sourcePrototypes':['Chair'],'parts':[],'folded':True,
                 'alternateFoldModel':'Standing','referenceState':'chair_folded'},
                {'id':'Standing','sourcePrototypes':[],'parts':[],'folded':False,
                 'alternateFoldModel':'Flat','referenceState':'chair','sourceDirections':4}]
        result=scene.build_scene({'maps':[1],'tilemap':{}},records,inventory,models,
                                  {'Chair':{'Foldable':{'folded':True},'Sprite':{'noRot':True}}})
        flat,standing=result['instances']
        self.assertEqual((flat['modelId'],flat['referenceState'],flat['folded']),('Flat','chair_folded',True))
        self.assertEqual((standing['modelId'],standing['referenceState'],standing['folded']),('Standing','chair',False))
        self.assertEqual(standing['position'],[2,3,0]);self.assertAlmostEqual(standing['yaw'],math.pi/2)
        self.assertEqual(standing['unappliedStateComponents'],[])
        self.assertEqual(result['diagnostics']['foldPoses'],{'modeledFolded':1,'modeledUnfolded':1})

    def test_initial_folded_model_cannot_show_its_folded_shape_when_saved_unfolded_pose_is_missing(self):
        library={'Flat':{'id':'Flat','folded':True}}
        instance={'modelId':'Flat','matchKind':'exact'}
        self.assertFalse(scene.resolve_fold_pose(instance,{'folded':False},library))
        self.assertEqual((instance['modelId'],instance['baseModelId'],instance['unsupportedState']), (None,'Flat','Unfolded'))
        self.assertEqual(instance['matchKind'],'unmapped')

    def test_fold_switch_retains_inherited_provenance_and_requires_reciprocal_opposite_pose(self):
        library={'A':{'id':'A','alternateFoldModel':'B'},'B':{'id':'B','folded':True,'alternateFoldModel':'A'}}
        instance={'modelId':'A','matchKind':'inherited','matchedPrototype':'Parent'}
        self.assertTrue(scene.resolve_fold_pose(instance,{'folded':True},library))
        self.assertEqual((instance['modelId'],instance['matchKind'],instance['matchedPrototype']),('B','inherited','Parent'))
        self.assertTrue(scene.resolve_fold_pose(instance,{'folded':False},library))
        self.assertEqual(instance['modelId'],'A')
        for bad in ({'id':'B','folded':False,'alternateFoldModel':'A'}, {'id':'B','folded':True}, {}):
            entry={'modelId':'A','matchKind':'exact'}
            self.assertFalse(scene.resolve_fold_pose(entry,{'folded':True},{**library,'B':bad}))
            self.assertIsNone(entry['modelId'])
        self.assertFalse(scene.resolve_fold_pose({'modelId':'A','matchKind':'exact'},{'folded':'false'},library))

    def test_unknown_unfolded_objects_remain_missing_art_and_unknown_folded_objects_have_state_markers(self):
        unknown={'modelId':None,'matchKind':'unmapped'}
        self.assertFalse(scene.resolve_fold_pose(unknown,{},{}));self.assertNotIn('unsupportedState',unknown)
        self.assertFalse(scene.resolve_fold_pose(unknown,{'folded':True},{}));self.assertEqual(unknown['unsupportedState'],'Folded')

    def test_saved_door_state_overrides_prototype_and_keeps_open_passage_clear(self):
        inventory = fixture_inventory()
        inventory['prototypes'][0]['id'] = 'Door'
        records = {1: entity(1, '', 0, extra={'Map': {}}),
                   2: entity(2, 'Door'),
                   3: entity(3, 'Door', extra={'Door': {'state': 'Closed'}}),
                   4: entity(4, 'Door', extra={'Door': {'state': 'Opening'}})}
        models = [{'id': 'Shut', 'sourcePrototypes': ['Door'], 'parts': [], 'alternateDoorModel': 'Clear'},
                  {'id': 'Clear', 'sourcePrototypes': [], 'parts': [], 'doorState': 'Open', 'alternateDoorModel': 'Shut'}]
        result = scene.build_scene({'maps': [1], 'tilemap': {}}, records, inventory, models,
                                   {'Door': {'Door': {'state': 'Open', 'openSpriteState': 'door_open'}}})
        opened, closed, moving = result['instances']
        self.assertEqual((opened['modelId'], opened['referenceState']), ('Clear', 'door_open'))
        self.assertEqual((closed['modelId'], closed['referenceState']), ('Shut', 'closed'))
        self.assertEqual(closed['unappliedStateComponents'], [])
        self.assertIsNone(moving['modelId'])
        self.assertEqual(moving['unsupportedState'], 'Door Opening')
        self.assertEqual(result['diagnostics']['unappliedStateComponentCounts'], {'Door': 1})

    def test_open_prototype_can_close_and_missing_pose_does_not_reuse_wrong_geometry(self):
        library = {'Open': {'id': 'Open', 'doorState': 'Open', 'alternateDoorModel': 'Closed'},
                   'Closed': {'id': 'Closed'}}
        instance = {'modelId': 'Open', 'matchKind': 'inherited'}
        self.assertTrue(scene.resolve_door_pose(instance, {'state': 'Closed'}, library))
        self.assertEqual((instance['modelId'], instance['matchKind']), ('Closed', 'inherited'))
        self.assertFalse(scene.resolve_door_pose(instance, {'state': 'Open'}, library))
        self.assertIsNone(instance['modelId'])
        self.assertEqual(instance['baseModelId'], 'Closed')
        missing = {'modelId': None}
        self.assertFalse(scene.resolve_door_pose(missing, {'state': 'Open'}, library))
        self.assertEqual(missing['unsupportedState'], 'Door Open')

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

    def test_select_map_uses_configured_above_and_below_order(self):
        definition = {"mapPath": "/surface", "mapsAbove": ["/one", "/two"], "mapsBelow": ["/minus-one"]}
        self.assertEqual(scene.select_map(definition, 0), "/surface")
        self.assertEqual(scene.select_map(definition, -1), "/minus-one")
        self.assertEqual(scene.select_map(definition, 2), "/two")
        with self.assertRaisesRegex(ValueError, "no level"):
            scene.select_map(definition, -2)

    def test_model_exact_and_inherited_matches_are_distinct(self):
        index = scene.model_index([{"id": "Parent", "sourcePrototypes": ["Seat"], "status": "reviewed"},
                                   {"id": "Exact", "sourcePrototypes": ["Chair"], "status": "draft"}])
        result = scene.match_model({"id": "Chair", "ancestors": ["Seat"]}, index)
        self.assertEqual((result["modelId"], result["matchKind"], result["modelStatus"]), ("Exact", "exact", "draft"))
        inherited = scene.match_model({"id": "Other", "ancestors": ["Seat"]}, index)
        self.assertEqual((inherited["modelId"], inherited["matchKind"]), ("Parent", "inherited"))
        self.assertEqual(scene.match_model({"id": "Unrelated"}, index)["matchKind"], "unmapped")

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

    def test_sampled_color_excludes_transparent_pixels_and_applies_tint(self):
        image = Image.new("RGBA", (2, 1), (255, 0, 0, 0))
        image.putpixel((1, 0), (200, 100, 40, 255))
        self.assertEqual(scene.sampled_color(image), "#C86428")
        self.assertEqual(scene.sampled_color(image, ("#80FFFF",)), "#646428")

    def test_invisible_tile_definition_does_not_fill_a_shaft_with_fallback_floor(self):
        blob = b"".join(struct.pack("<iBBB", tile, 0, 0, 0) for tile in [1, 2, 0, 0])
        grid = {"chunks": {"0,0": {"ind": "0,0", "size": 2, "version": 7,
                                     "tiles": base64.b64encode(blob).decode()}}}
        records = {1: entity(1, "", 0, extra={"Map": {}, "MapGrid": grid})}
        inventory = fixture_inventory()
        inventory['tiles'].append({'id': 'Shaft', 'sprite': None})
        result = scene.build_scene({'maps': [1], 'tilemap': {0: 'Space', 1: 'Floor', 2: 'Shaft'}},
                                   records, inventory, [])
        self.assertEqual([(tile['x'], tile['y']) for tile in result['tiles']], [(0, 0)])

    def test_tint_normalization_matches_engine_names_and_preserves_alpha(self):
        expected = {"pink": "#FFC0CB", "PiNk": "#FFC0CB", "green": "#008000",
                    "BetterViolet": "#7E03A8", "ruber": "#CC4778", "seablue": "#004299",
                    "vividgamboge": "#FF9900", "transparent": "#FFFFFF00",
                    "#abc": "#AABBCC", "#AbC8": "#AABBCC88", "#ff80c080": "#FF80C080",
                    "#FFFFFFFF": "#FFFFFF"}
        for source, color in expected.items():
            with self.subTest(source=source):
                self.assertEqual(scene.normalize_tint(source), color)
        for invalid in ("not-a-color", "rgb(1,2,3)", "#12345", None):
            with self.subTest(invalid=invalid), self.assertRaises(ValueError):
                scene.normalize_tint(invalid)

    def test_source_preview_exports_named_sprite_and_layer_tints_as_hex(self):
        prototype = {"sprite": {"sprite": "pie.rsi", "color": "BetterViolet",
                                "layers": [{"state": "pie", "color": "pink"}]}}
        resources = {"/Textures/pie.rsi": {"size": {"x": 32, "y": 32},
                                            "states": [{"name": "pie", "imageExists": True}]}}
        preview = scene.source_reference(prototype, resources)["preview"]
        self.assertEqual(preview["spriteTint"], "#7E03A8")
        self.assertEqual(preview["layerTint"], "#FFC0CB")
        # The sampled fallback and viewer reference now use exactly the same normalized tints.
        white = Image.new("RGBA", (1, 1), (255, 255, 255, 255))
        self.assertEqual(scene.sampled_color(white, (preview["spriteTint"], preview["layerTint"])), "#7E0286")

    def test_explicit_layers_replace_inherited_single_state(self):
        prototype = {"sprite": {"sprite": "hydro.rsi", "state": "soil",
                                "layers": [{"state": "tray"}, {"state": "warning", "visible": False}]}}
        resources = {"/Textures/hydro.rsi": {"states": [
            {"name": name, "imageExists": True} for name in ("soil", "tray", "warning")]}}
        self.assertEqual(scene.source_reference(prototype, resources)["preview"]["state"], "tray")
        prototype["sprite"]["layers"] = []
        self.assertEqual(scene.source_reference(prototype, resources)["preview"]["state"], "soil")

    def test_tile_material_sampling_preserves_variant_order(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            folder = root / "Resources/Textures/Tiles"
            folder.mkdir(parents=True)
            image = Image.new("RGBA", (64, 32), (200, 0, 0, 255))
            image.paste((0, 150, 0, 255), (32, 0, 64, 32))
            image.save(folder / "floor.png")
            result = {"tilePalette": {"1": {"prototype": "Floor", "sprite": "/Textures/Tiles/floor.png", "variants": 2}},
                      "sourceReferences": {}, "diagnostics": {"limitations": []}}
            scene.enrich_materials(result, root)
        palette = result["tilePalette"]["1"]
        self.assertEqual(palette["variantColors"], ["#C80000", "#009600"])
        self.assertEqual(palette["color"], "#C80000")
        self.assertEqual(result["diagnostics"]["materialErrors"], [])

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
