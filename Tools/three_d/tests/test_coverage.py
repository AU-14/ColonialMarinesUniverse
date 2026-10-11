import importlib.util
from pathlib import Path
import unittest


SPEC = importlib.util.spec_from_file_location("asset_coverage", Path(__file__).resolve().parents[1] / "coverage.py")
coverage = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(coverage)


def fixture():
    prototypes = [
        {"id": "Chair", "classification": "visual", "ancestors": ["Seat"], "instanceCount": 8,
         "instanceCounts": {"classic": 3, "redux": 5}},
        {"id": "RedChair", "classification": "visual", "ancestors": ["Chair", "Seat"], "instanceCount": 4,
         "instanceCounts": {"classic": 0, "redux": 4}},
        {"id": "Marker", "classification": "helper", "ancestors": [], "instanceCount": 1000,
         "instanceCounts": {"classic": 1000, "redux": 0}},
    ]
    return {"knownEntityPrototypeIds": ["Seat", "Chair", "RedChair", "OffMapItem", "Marker"],
            "prototypes": prototypes, "summary": {"variants": {"classic": {}, "redux": {}}},
            "families": [{"id": "chairs.rsi", "resources": ["chairs.rsi"],
                          "prototypeIds": ["Chair", "RedChair"]}]}


class CoverageTests(unittest.TestCase):
    def test_physical_queue_keeps_ui_actors_and_fog_in_inventory_without_modeling_their_icons(self):
        data = fixture()
        rows = [('Toggle', ['Action']), ('Mob', ['MobState']), ('Foam', ['Sprite', 'Occluder']),
                ('Clothes', ['Sprite', 'Clothing']), ('CMUFogWallLamentWaterfallMist', ['Sprite', 'Occluder'])]
        for uid, components in rows:
            data['knownEntityPrototypeIds'].append(uid)
            data['prototypes'].append(dict(id=uid, classification='visual', ancestors=[], components=components,
                instanceCount=7, instanceCounts={'redux': 7, 'classic': 0}))
            data['families'].append(dict(id=uid, resources=[uid + '.rsi'], prototypeIds=[uid]))
        result = coverage.calculate_coverage(data, [])
        self.assertEqual(result['summary']['redux']['visualPrototypes'], 7)
        self.assertEqual(result['summary']['redux']['categories']['unmapped']['instances'], 44)
        queue = {uid for family in result['physicalModelingFamilies'] for uid in family['prototypeIds']}
        self.assertEqual(queue, {'Chair', 'RedChair', 'Foam', 'Clothes'})
        retained = {p['id']: p['presentationTarget'] for p in result['retainedSpriteOrUiPrototypes']}
        self.assertEqual(retained, {'Toggle': 'existing-ui', 'Mob': 'sprite-character',
                                   'CMUFogWallLamentWaterfallMist': 'sprite-atmosphere'})

    def test_directional_assemblies_do_not_multiply_prototype_coverage(self):
        names = ['South', 'North', 'East', 'West']
        models = [{'id': name, 'status': 'draft', 'sourcePrototypes': ['Chair'] if i == 0 else [],
                   'referenceDirection': i, 'directionalModels': names} for i, name in enumerate(names)]
        result = coverage.calculate_coverage(fixture(), models)
        self.assertEqual(result['modelCount'], 4)
        self.assertEqual(result['summary']['classic']['categories']['exact_draft'], {'prototypes': 1, 'instances': 3})
        self.assertEqual(len(result['referenceMappings']), 1)
        self.assertEqual(result['directionalModelGroups'], [{'baseModelId': 'South', 'sourcePrototypes': ['Chair'],
                                                           'modelsInRsiOrder': names}])

    def test_reviewed_exact_does_not_accept_inherited_variants(self):
        result = coverage.calculate_coverage(fixture(), [{"id": "Model", "status": "reviewed", "sourcePrototypes": ["Chair"]}])
        self.assertEqual([item["category"] for item in result["prototypes"]],
                         ["exact_reviewed", "inherited_reviewed_candidate"])
        self.assertEqual(result["unreviewedFamilies"][0]["prototypeIds"], ["RedChair"])
        self.assertEqual(result["summary"]["combined"]["categories"]["exact_reviewed"]["instances"], 8)

    def test_draft_exact_is_pending_and_beats_inherited_reviewed_candidate(self):
        models = [{"id": "Base", "status": "reviewed", "sourcePrototypes": ["Seat"]},
                  {"id": "Specific", "status": "draft", "sourcePrototypes": ["Chair", "Chair"]}]
        result = coverage.calculate_coverage(fixture(), models)
        self.assertEqual(result["prototypes"][0]["category"], "exact_draft")
        self.assertEqual(result["summary"]["combined"]["categories"]["exact_reviewed"]["prototypes"], 0)
        self.assertEqual(result["unreviewedFamilies"][0]["instanceCount"], 12)

    def test_existing_off_map_references_are_not_reported_missing(self):
        result = coverage.calculate_coverage(fixture(), [{"id": "Model", "sourcePrototypes": ["OffMapItem", "Typo", "Seat"]}])
        self.assertEqual(result["unresolvedSourceReferences"], ["Typo"])
        locations = {entry["sourcePrototype"]: entry["location"] for entry in result["referenceMappings"]}
        self.assertEqual(locations["OffMapItem"], "existing_off_map")
        self.assertEqual(locations["Seat"], "ancestor_on_map")

    def test_map_counts_are_weighted_and_helpers_excluded(self):
        result = coverage.calculate_coverage(fixture(), [])
        self.assertEqual(result["summary"]["combined"]["visualPrototypes"], 2)
        self.assertEqual(result["summary"]["classic"]["visualPrototypes"], 1)
        self.assertEqual(result["summary"]["redux"]["visualInstances"], 9)
        self.assertEqual(result["unmappedFamilies"][0]["instanceCount"], 12)

    def test_bad_status_and_missing_registry_fail_visibly(self):
        with self.assertRaisesRegex(ValueError, "Invalid review status"):
            coverage.calculate_coverage(fixture(), [{"id": "Model", "status": "done"}])
        old = fixture()
        del old["knownEntityPrototypeIds"]
        with self.assertRaisesRegex(ValueError, "Regenerate inventory"):
            coverage.calculate_coverage(old, [])


if __name__ == "__main__":
    unittest.main()
