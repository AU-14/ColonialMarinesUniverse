import math
from pathlib import Path
import sys
import unittest
import numpy as np
sys.path.insert(0,str(Path(__file__).parents[1]))
from build_models import validate_model, glb_document, part_bounds, render_model
from placement import surface


class PartYawTests(unittest.TestCase):
    def test_local_rotation_keeps_off_center_pivot_and_exports_correct_gltf_axis(self):
        model=validate_model({'id':'BentBranch','label':'Bent branch','parts':[
            {'min':'1,2,0','max':'3,2.2,1','shape':'Ellipsoid','yaw':90}]})
        part=model['parts'][0];low,high=part_bounds(part)
        np.testing.assert_allclose(low,[1.9,1.1,0],atol=1e-12)
        np.testing.assert_allclose(high,[2.1,3.1,1],atol=1e-12)
        document,_=glb_document(model);node=document['nodes'][0]
        np.testing.assert_allclose(node['translation'],[2,.5,-2.1])
        np.testing.assert_allclose(node['rotation'],[0,math.sqrt(.5),0,math.sqrt(.5)])
        self.assertEqual(node['scale'],[2,1,.20000000000000018])

    def test_rendered_horizontal_root_rotates_about_its_center(self):
        part={'min':'-.8,-.08,0','max':'.8,.08,.16','shape':'Ellipsoid','color':'#FFFFFF'}
        def coverage(yaw):
            model=validate_model({'id':'Root','label':'Root','parts':[{**part,'yaw':yaw}]})
            pixels=np.array(render_model(model,(160,160),yaw=-math.pi/2,pitch=.55));mask=np.any(pixels!=[23,33,43],axis=2)
            yy,xx=np.where(mask);return xx.max()-xx.min(),yy.max()-yy.min()
        wide,tall=coverage(0),coverage(90)
        self.assertGreater(wide[0],wide[1]*3)
        self.assertGreater(tall[1],tall[0]*3)

    def test_invalid_angles_and_unsupported_surface_joins_fail_closed(self):
        part={'min':'0,0,0','max':'1,1,1','yaw':30}
        model={'id':'Root','label':'Root','parts':[part]}
        for yaw in (math.nan,math.inf,361,True,'45'):
            with self.assertRaisesRegex(ValueError,'part yaw'):
                validate_model({**model,'parts':[{**part,'yaw':yaw}]})
        for field in ('connectToNeighbours','wallMounted'):
            with self.assertRaisesRegex(ValueError,'part yaw'):
                validate_model({**model,field:True})
        self.assertIsNone(surface({**model,'supportSurface':'top','parts':[{**part,'label':'top'}]}))
