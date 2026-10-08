import itertools
import math
from pathlib import Path
import sys
import unittest
import numpy as np
sys.path.insert(0,str(Path(__file__).parents[1]))
from build_models import validate_model, glb_document, part_bounds
from placement import surface


class PartPitchTests(unittest.TestCase):
    def test_combined_rotation_preserves_pivot_and_exports_same_corners(self):
        model=validate_model({'id':'Branch','label':'Branch','parts':[
            {'min':'1,2,3','max':'3,2.12,3.12','shape':'CylinderX','yaw':37,'pitch':63}]})
        part=model['parts'][0];low,high=part_bounds(part)
        cy,sy,cp,sp=math.cos(math.radians(37)),math.sin(math.radians(37)),math.cos(math.radians(63)),math.sin(math.radians(63))
        rotation=np.array([[cy*cp,-sy,-cy*sp],[sy*cp,cy,-sy*sp],[sp,0,cp]])
        center=(np.array(part['min'])+part['max'])/2
        corners=np.array([center+rotation@(np.array(p)-center)for p in itertools.product(*zip(part['min'],part['max']))])
        np.testing.assert_allclose(low,corners.min(axis=0));np.testing.assert_allclose(high,corners.max(axis=0))
        document,_=glb_document(model);node=document['nodes'][0];x,y,z,w=node['rotation']
        gltf=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],
                       [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],
                       [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
        basis=np.array([[1,0,0],[0,0,1],[0,-1,0]])
        np.testing.assert_allclose(gltf,basis@rotation@basis.T,atol=1e-12)
        np.testing.assert_allclose(node['translation'],basis@center)

    def test_invalid_tilt_and_unsupported_attachments_are_rejected(self):
        part={'min':'0,0,0','max':'1,1,1','pitch':30,'label':'top'}
        model={'id':'Root','label':'Root','parts':[part]}
        for pitch in (math.nan,math.inf,91,True,'45'):
            with self.assertRaisesRegex(ValueError,'part pitch'):
                validate_model({**model,'parts':[{**part,'pitch':pitch}]})
        for field in ('connectToNeighbours','wallMounted'):
            with self.assertRaisesRegex(ValueError,'part pitch'):
                validate_model({**model,field:True})
        with self.assertRaisesRegex(ValueError,'part pitch'):
            validate_model({**model,'parts':[{**part,'surface':'InvalidTexture'}]})
        self.assertIsNone(surface({**model,'supportSurface':'top'}))
