from pathlib import Path
import sys
import unittest
import numpy as np
sys.path.insert(0, str(Path(__file__).parents[1]))
from build_models import validate_model, glb_document

class FoliageTests(unittest.TestCase):

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
