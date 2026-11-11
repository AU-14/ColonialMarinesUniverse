"""Exporter regressions: orientation, materials, binary layout, and invalid geometry."""
import importlib.util
import json
from pathlib import Path
import struct
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).parents[1]))
SPEC = importlib.util.spec_from_file_location("build_models", Path(__file__).parents[1] / "build_models.py")
models = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(models)

def sample():
    return models.validate_model({
        "id": "TestModel", "label": "An asymmetric model", "sourcePrototypes": ["TestChair"],
        "parts": [{"min": "-1, -2, 0", "max": "3, 4, 6", "color": "#808080", "label": "body"},
                  {"min": "0, -3, 1", "max": "1, -2, 2", "color": "#3388CC80", "label": "glass"}]})

def unpack(blob):
    magic, version, total = struct.unpack_from("<III", blob)
    assert (magic, version, total) == (0x46546C67, 2, len(blob))
    length, kind = struct.unpack_from("<II", blob, 12)
    assert kind == 0x4E4F534A and length % 4 == 0
    document = json.loads(blob[20:20 + length])
    offset = 20 + length
    binary_length, kind = struct.unpack_from("<II", blob, offset)
    assert kind == 0x004E4942 and binary_length % 4 == 0
    assert offset + 8 + binary_length == len(blob)
    return document, blob[offset + 8:]

class ExportTests(unittest.TestCase):

    def test_cylinders_have_closed_caps_outward_winding_and_distinct_axis_meshes(self):
        np = models.np
        model = sample()
        for axis in 'XYZ':
            points,normals,indices = models.solid_geometry('Cylinder'+axis)
            ai = 'XYZ'.index(axis)
            edges = {}
            self.assertEqual(len(indices),64*3)
            for i in range(0,len(indices),3):
                vertices=[np.array(points[j]) for j in indices[i:i+3]]
                a,b,c=vertices
                self.assertGreater(np.dot(np.cross(b-a,c-a),(a+b+c)/3),0)
                for j,k in ((0,1),(1,2),(2,0)):
                    key=tuple(sorted((tuple(np.round(vertices[j],9)),tuple(np.round(vertices[k],9)))))
                    edges[key]=edges.get(key,0)+1
            self.assertTrue(all(n==2 for n in edges.values()))
            self.assertTrue(any(abs(n[ai])==1 for n in normals))
            self.assertTrue(any(abs(n[ai])==0 for n in normals))
            for p,n in zip(points,normals):
                self.assertAlmostEqual(np.linalg.norm(n),1)
                self.assertLessEqual(max(abs(v) for v in p),.500001)
            model['parts'].append(dict(model['parts'][0],shape='Cylinder'+axis))
        model=models.validate_model(model)
        document,_=models.glb_document(model)
        self.assertEqual(len({n['mesh'] for n in document['nodes'][2:]}),3)
        self.assertEqual(models.triangle_count(model['parts']),216)
        for node in document['nodes'][2:]:
            primitive=document['meshes'][node['mesh']]['primitives'][0]
            self.assertEqual(document['accessors'][primitive['indices']]['count'],192)

    def test_ellipsoid_mesh_is_closed_outward_and_inscribed_in_its_bounds(self):
        points, normals, indices = models.ellipsoid_geometry()
        np = models.np
        self.assertEqual(len(indices) // 3, 224)
        edges = {}
        for i in range(0, len(indices), 3):
            a, b, c = [np.array(points[index]) for index in indices[i:i+3]]
            self.assertGreater(float(np.dot(np.cross(b-a, c-a), (a+b+c)/3)), 0)
            for x, y in ((indices[i], indices[i+1]), (indices[i+1], indices[i+2]), (indices[i+2], indices[i])):
                key = tuple(sorted((x, y))); edges[key] = edges.get(key, 0) + 1
        self.assertTrue(all(count == 2 for count in edges.values()))
        for point, normal in zip(points, normals):
            self.assertAlmostEqual(float(np.linalg.norm(point)), .5)
            self.assertAlmostEqual(float(np.linalg.norm(normal)), 1)

    def test_mixed_shapes_share_material_but_not_mesh_and_keep_triangle_counts(self):
        model = sample()
        model['parts'][1].update(shape='Ellipsoid', color=model['parts'][0]['color'])
        document, binary = models.glb_document(model)
        self.assertEqual(len(document['materials']), 1)
        self.assertNotEqual(document['nodes'][0]['mesh'], document['nodes'][1]['mesh'])
        self.assertEqual(document['accessors'][5]['count'], 224 * 3)
        self.assertEqual(models.triangle_count(model['parts']), 236)
        self.assertEqual(document['buffers'][0]['byteLength'], len(binary))
        model['parts'][1]['shape'] = 'Unknown'
        with self.assertRaisesRegex(ValueError, 'unsupported shape'):
            models.validate_model(model)

    def test_alternate_door_links_require_reciprocal_opposite_poses(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            closed = {'type': 'cmu3DModel', 'id': 'Closed', 'label': 'Closed',
                      'alternateDoorModel': 'Open', 'parts': [{'min': '0,0,0', 'max': '1,1,1'}]}
            opened = {**closed, 'id': 'Open', 'doorState': 'Open', 'alternateDoorModel': 'Closed'}
            for bad in ({**opened, 'doorState': 'Closed'}, {**opened, 'alternateDoorModel': 'Missing'}):
                (root / 'poses.yml').write_text(models.yaml.safe_dump([closed, bad]))
                with self.assertRaisesRegex(ValueError, 'reciprocal opposite pose'):
                    models.load_models(root)
            (root / 'poses.yml').write_text(models.yaml.safe_dump([closed, opened]))
            self.assertEqual(len(models.load_models(root)), 2)

    def test_two_models_cannot_silently_claim_the_same_source(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name in ('First', 'Second'):
                entry = {"type": "cmu3DModel", "id": name, "label": name,
                         "sourcePrototypes": ["SameMedicalCase"],
                         "parts": [{"min": "0, 0, 0", "max": "1, 1, 1"}]}
                (root / f'{name}.yml').write_text(models.yaml.safe_dump([entry]))
            with self.assertRaisesRegex(ValueError, 'Duplicate source prototype SameMedicalCase'):
                models.load_models(root)

    def test_y_up_conversion_preserves_asymmetric_bounds_and_front(self):
        document, _ = unpack(models.glb_bytes(sample()))
        self.assertEqual(document["nodes"][0]["translation"], [1, 3, -1])
        self.assertEqual(document["nodes"][0]["scale"], [4, 6, 6])
        # The detail on game south (-Y) must be on glTF positive Z.
        self.assertEqual(document["nodes"][1]["translation"], [.5, 1.5, 2.5])

    def test_cube_triangles_wind_outward_and_normals_match(self):
        positions, normals, indices = models.cube_geometry()
        for i in range(0, len(indices), 3):
            a, b, c = [positions[index] for index in indices[i:i + 3]]
            ab, ac = [y - x for x, y in zip(a, b)], [y - x for x, y in zip(a, c)]
            cross = (ab[1] * ac[2] - ab[2] * ac[1], ab[2] * ac[0] - ab[0] * ac[2], ab[0] * ac[1] - ab[1] * ac[0])
            self.assertGreater(models.dot(cross, a), 0)
            self.assertEqual(cross, normals[indices[i]])

    def test_materials_convert_srgb_to_linear_and_preserve_alpha(self):
        document, _ = unpack(models.glb_bytes(sample()))
        solid, glass = document["materials"]
        self.assertAlmostEqual(solid["pbrMetallicRoughness"]["baseColorFactor"][0], .2158605, places=6)
        self.assertNotIn("alphaMode", solid)
        self.assertEqual(glass["alphaMode"], "BLEND")
        self.assertAlmostEqual(glass["pbrMetallicRoughness"]["baseColorFactor"][3], 128 / 255)

    def test_binary_accessors_are_aligned_in_bounds_and_nonempty(self):
        document, binary = unpack(models.glb_bytes(sample()))
        self.assertEqual(document["buffers"][0]["byteLength"], len(binary))
        for view in document["bufferViews"]:
            self.assertEqual(view["byteOffset"] % 4, 0)
            self.assertLessEqual(view["byteOffset"] + view["byteLength"], len(binary))
        indices = struct.unpack_from("<36H", binary, document["bufferViews"][2]["byteOffset"])
        self.assertEqual(len(indices), 36)
        self.assertLess(max(indices), document["accessors"][0]["count"])

    def test_generation_is_deterministic_and_keeps_review_status(self):
        first, second = models.glb_bytes(sample()), models.glb_bytes(sample())
        self.assertEqual(first, second)
        document, _ = unpack(first)
        self.assertEqual(document["extras"]["status"], "draft")
        self.assertEqual(document["extras"]["sourcePrototypes"], ["TestChair"])

    def test_nan_flat_and_reversed_boxes_are_rejected(self):
        for low, high in (("nan, 0, 0", "1, 1, 1"), ("0, 0, 0", "0, 1, 1"), ("2, 0, 0", "1, 1, 1")):
            with self.subTest(low=low, high=high), self.assertRaises(ValueError):
                models.validate_model({"id": "Bad", "label": "Bad", "parts": [{"min": low, "max": high}]})

    def test_unsafe_file_names_and_unsupported_status_are_rejected(self):
        for field, value in (("id", "../escape"), ("status", "automatically-perfect")):
            model = sample()
            model[field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                models.validate_model(model)

    def test_renaming_removes_only_unchanged_manifest_owned_exports(self):
        with tempfile.TemporaryDirectory() as folder:
            destination = Path(folder)
            old = models.glb_bytes(sample())
            (destination / "Old.glb").write_bytes(old)
            (destination / "HandAuthored.glb").write_bytes(b"leave me alone")
            previous = {"models": [{"file": "Old.glb", "sha256": models.hashlib.sha256(old).hexdigest()}]}
            (destination / "manifest.json").write_text(json.dumps(previous))
            models.write_outputs({"New.glb": old, "manifest.json": b'{"models": []}'}, destination)
            self.assertFalse((destination / "Old.glb").exists())
            self.assertEqual((destination / "New.glb").read_bytes(), old)
            self.assertEqual((destination / "HandAuthored.glb").read_bytes(), b"leave me alone")

    def test_modified_or_unsafe_previous_exports_are_never_deleted(self):
        for name in ("Old.glb", "../Outside.glb"):
            with self.subTest(name=name), tempfile.TemporaryDirectory() as folder:
                destination = Path(folder)
                old = destination / "Old.glb"
                old.write_bytes(b"local changes")
                (destination / "manifest.json").write_text(json.dumps({"models": [{"file": name, "sha256": "outdated"}]}))
                with self.assertRaises(ValueError):
                    models.write_outputs({}, destination)
                self.assertEqual(old.read_bytes(), b"local changes")

if __name__ == "__main__":
    unittest.main()
