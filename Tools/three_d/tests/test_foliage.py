import math
from pathlib import Path
import subprocess
import sys
import unittest
import numpy as np
sys.path.insert(0, str(Path(__file__).parents[1]))
from foliage import lobes
from primitives import solid_geometry, triangle_count
from build_models import validate_model, glb_document


class FoliageTests(unittest.TestCase):
    def test_analytic_leaves_and_mesh_stay_inside_original_ellipsoid(self):
        points, normals, indices = solid_geometry('Foliage')
        self.assertEqual(len(indices)//3, 912)
        self.assertEqual(triangle_count([{'shape': 'Foliage'}]), 912)
        for center, axes, size in lobes():
            # This contains the entire analytic surface, not just sampled vertices.
            self.assertLess(np.linalg.norm(center)+max(size)/2, .5)
            np.testing.assert_allclose(np.array(axes)@np.array(axes).T, np.eye(3), atol=1e-12)
        leaves = list(lobes())
        edges = {}
        for i, (p, n) in enumerate(zip(points, normals)):
            center, axes, size = leaves[i//26]
            relative = np.array(p)-center
            unit = np.array(axes)@relative/(np.array(size)/2)
            self.assertAlmostEqual(float(unit@unit), 1)
            gradient = np.array(axes).T@(unit/(np.array(size)/2))
            np.testing.assert_allclose(n, gradient/np.linalg.norm(gradient), atol=1e-12)
            self.assertLess(np.linalg.norm(p), .5)
        for start in range(0, len(indices), 3):
            ids = indices[start:start+3]
            a,b,c = [np.array(points[i]) for i in ids]
            center = np.array(leaves[ids[0]//26][0])
            self.assertGreater(np.dot(np.cross(b-a,c-a),(a+b+c)/3-center), 0)
            for x,y in zip(ids, ids[1:]+ids[:1]):
                key=tuple(sorted((x,y)));edges[key]=edges.get(key,0)+1
        self.assertEqual(set(edges.values()), {2})

    def test_export_bounds_match_actual_vertices_and_surfaces_are_rejected(self):
        part={'min':'-1,-.3,0', 'max':'1,.3,2', 'shape':'Foliage'}
        model=validate_model({'id':'Leaves','label':'Leaves','parts':[part]})
        document,binary=glb_document(model)
        mesh=document['meshes'][0]['primitives'][0]
        accessor=document['accessors'][mesh['attributes']['POSITION']]
        view=document['bufferViews'][accessor['bufferView']]
        actual=np.frombuffer(binary,dtype='<f4',count=accessor['count']*3,offset=view['byteOffset']).reshape((-1,3))
        self.assertEqual(accessor['min'],actual.min(0).tolist())
        self.assertEqual(accessor['max'],actual.max(0).tolist())
        with self.assertRaisesRegex(ValueError, 'surface requires'):
            validate_model({'id':'Leaves','label':'Leaves','parts':[{**part,'surface':'Unknown'}]})

    def test_native_and_browser_transform_tables_are_current(self):
        result=subprocess.run([sys.executable,str(Path(__file__).parents[1]/'generate_foliage.py'),'--check'],capture_output=True,text=True)
        self.assertEqual(result.returncode,0,result.stdout+result.stderr)
