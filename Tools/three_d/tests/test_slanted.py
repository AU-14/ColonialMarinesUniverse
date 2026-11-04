import math
import unittest
import sys
from pathlib import Path
import numpy as np
sys.path.insert(0, str(Path(__file__).parents[1]))
from primitives import solid_geometry, triangle_count
from build_models import validate_model, glb_document, render_model


class SlantedTests(unittest.TestCase):
    def test_closed_tapered_mesh_normals_and_export_bounds(self):
        for axis in 'XY':
            for reverse in (False, True):
                shape = 'Slanted' + axis + ('Reverse' if reverse else '')
                with self.subTest(shape=shape):
                    points, normals, indices = solid_geometry(shape)
                    ai = 'XY'.index(axis)
                    k = -.85 if reverse else .85
                    s = math.sqrt(1-k*k)
                    edges = {}
                    for p, n in zip(points, normals):
                        unit = list(p)
                        unit[ai] = (p[ai]-k*p[2])/s
                        self.assertAlmostEqual(sum(v*v for v in unit), .25)
                        self.assertLessEqual(max(abs(v) for v in p), .5000001)
                        self.assertAlmostEqual(sum(v*v for v in n), 1)
                        gradient = np.array(p)
                        gradient[ai] = (p[ai]-k*p[2])/(s*s)
                        gradient[2] = p[2]-k*gradient[ai]
                        np.testing.assert_allclose(n, gradient/np.linalg.norm(gradient), atol=1e-12)
                    for i in range(0, len(indices), 3):
                        ids = indices[i:i+3]
                        a, b, c = [np.array(points[j]) for j in ids]
                        self.assertGreater(np.dot(np.cross(b-a, c-a), (a+b+c)/3), 0)
                        for j, l in zip(ids, ids[1:]+ids[:1]):
                            key = tuple(sorted((j, l)))
                            edges[key] = edges.get(key, 0)+1
                    self.assertEqual(set(edges.values()), {2})
                    model = validate_model({'id': shape, 'label': shape, 'parts': [
                        {'min': '-.5,-.5,-.5', 'max': '.5,.5,.5', 'shape': shape}]})
                    document, binary = glb_document(model)
                    mesh = document['meshes'][0]['primitives'][0]
                    accessor = document['accessors'][mesh['attributes']['POSITION']]
                    view = document['bufferViews'][accessor['bufferView']]
                    actual = np.frombuffer(binary, dtype='<f4', count=accessor['count']*3,
                                           offset=view['byteOffset']).reshape((-1,3))
                    self.assertEqual(accessor['min'], actual.min(axis=0).tolist())
                    self.assertEqual(accessor['max'], actual.max(axis=0).tolist())
                    self.assertEqual(document['accessors'][mesh['indices']]['count'], 224*3)
                    self.assertEqual(triangle_count(model['parts']), 224)

    def test_rasterized_leaf_has_a_diagonal_body_and_empty_opposite_corners(self):
        for reverse in (False, True):
            model = validate_model({'id': 'Leaf', 'label': 'Leaf', 'parts': [
                {'min': '-.5,-.1,-.5', 'max': '.5,.1,.5', 'color': '#FFFFFF',
                 'shape': 'SlantedXReverse' if reverse else 'SlantedX'}]})
            pixels = np.array(render_model(model, (128,128), yaw=-math.pi/2, pitch=0))
            mask = np.any(pixels != [23,33,43], axis=2)
            self.assertTrue(mask[64,64])
            self.assertEqual(bool(mask[88,40]), not reverse)
            self.assertEqual(bool(mask[88,88]), reverse)
            self.assertEqual(bool(mask[40,88]), not reverse)
            self.assertEqual(bool(mask[40,40]), reverse)

    def test_slanted_leaves_reject_surface_textures(self):
        for axis in 'XY':
            with self.assertRaisesRegex(ValueError, 'surface requires'):
                validate_model({'id': 'Leaf', 'label': 'Leaf', 'parts': [
                    {'min': '0,0,0', 'max': '1,1,1', 'shape': 'Slanted'+axis, 'surface': 'Unknown'}]})
