import unittest
import numpy as np
from primitives import wedge_geometry,triangle_count

class WedgeTests(unittest.TestCase):
    def test_closed_outward_mesh_fills_half_its_box(self):
        for reverse in (False,True):
            with self.subTest(reverse=reverse):
                positions,normals,indices = wedge_geometry(reverse)
                volume = 0
                for i in range(0,len(indices),3):
                    a,b,c = [np.array(positions[j]) for j in indices[i:i+3]]
                    cross = np.cross(b-a,c-a)
                    self.assertGreater(np.dot(cross,normals[indices[i]]),0)
                    self.assertTrue(all(p[2]<=p[1]*(-1 if reverse else 1) for p in (a,b,c)))
                    volume += np.dot(a,np.cross(b,c))/6
                self.assertAlmostEqual(volume,.5)
                self.assertEqual(triangle_count([{'shape':'WedgeYReverse'if reverse else'WedgeY'}]),8)
