import math
from pathlib import Path
import sys
import unittest
import numpy as np
sys.path.insert(0,str(Path(__file__).parents[1]))
from build_models import validate_model, render_model


class ReviewProjectionTests(unittest.TestCase):
    def test_fixed_scale_exposes_size_differences_and_retains_ground_pivot(self):
        bounds=[]
        for width in (1,2):
            model=validate_model({'id':'Scale','label':'Scale','parts':[{'min':[-width/2,-.1,0],'max':[width/2,.1,1]}]})
            pixels=np.array(render_model(model,(160,160),yaw=-math.pi/2,pitch=0,pixels_per_unit=32,screen_origin=(80,100)))
            yy,xx=np.where(np.any(pixels!=[23,33,43],axis=2));bounds.append((xx.min(),xx.max()+1,yy.min(),yy.max()+1))
        self.assertEqual(bounds,[(64,96,68,100),(48,112,68,100)])

    def test_invalid_fixed_projection_fails_explicitly(self):
        model=validate_model({'id':'Scale','label':'Scale','parts':[{'min':[0,0,0],'max':[1,1,1]}]})
        for options in ({'pixels_per_unit':0},{'pixels_per_unit':math.nan},{'screen_origin':(0,0)},
                        {'pixels_per_unit':32,'screen_origin':(0,math.inf)}):
            with self.assertRaises(ValueError):render_model(model,**options)
