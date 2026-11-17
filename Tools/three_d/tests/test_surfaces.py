"""Surface orientation, cutout preservation and embedded portable artwork."""
from io import BytesIO
from pathlib import Path
import struct
import sys
import unittest
from unittest.mock import patch

from PIL import Image

sys.path.insert(0, str(Path(__file__).parents[1]))
import build_models
import surfaces

class SurfaceTests(unittest.TestCase):
    def setUp(self):
        self.image = Image.new('RGBA', (2, 2))
        self.image.putdata([(255, 0, 0, 255), (0, 255, 0, 255),
                            (0, 0, 255, 255), (200, 90, 40, 0)])
        self.catalog = {'FourCorners': {'atlasIndex': 17, 'texture': '/Textures/test.png', 'image': self.image}}
        self.mock = patch.object(surfaces, 'load_surfaces', return_value=self.catalog)
        self.mock.start()
        self.addCleanup(self.mock.stop)

    def model(self, axis='XZ'):
        return build_models.validate_model({'id': 'Panel', 'label': 'Asymmetric printed plate',
            'sourcePrototypes': [], 'parts': [{'min': [-1, -.02, 0], 'max': [1, .02, 2],
            'color': '#FFFFFF', 'surface': 'FourCorners', 'surfaceAxis': axis}]})

    def test_glb_embeds_exact_png_and_maps_uvs_after_coordinate_conversion(self):
        for axis in surfaces.AXES:
            with self.subTest(axis=axis):
                document, binary = build_models.glb_document(self.model(axis))
                image_view = document['bufferViews'][document['images'][0]['bufferView']]
                start = image_view['byteOffset']
                embedded = Image.open(BytesIO(binary[start:start + image_view['byteLength']]))
                self.assertEqual(embedded.tobytes(), self.image.tobytes())
                attributes = document['meshes'][0]['primitives'][0]['attributes']
                accessor = document['accessors'][attributes['TEXCOORD_0']]
                uv_view = document['bufferViews'][accessor['bufferView']]
                uvs = struct.unpack_from('<48f', binary, uv_view['byteOffset'])
                vertices = build_models.cube_geometry()[0]
                expected = tuple(v for x, y, z in vertices for v in surfaces.uv((x, -z, y), axis))
                self.assertEqual(uvs, expected)
                self.assertEqual(document['materials'][0]['alphaMode'], 'MASK')
                self.assertEqual(document['samplers'][0]['magFilter'], 9728)

    def test_fractional_source_or_paint_alpha_exports_blending_with_original_pixels(self):
        for source_alpha, paint in ((128, '#FFFFFF'), (255, '#FFFFFF80')):
            with self.subTest(source_alpha=source_alpha, paint=paint):
                self.image.putpixel((0, 0), (0, 0, 0, source_alpha))
                model = self.model()
                model['parts'][0]['color'] = paint
                document, binary = build_models.glb_document(model)
                material = document['materials'][0]
                self.assertEqual(material['alphaMode'], 'BLEND')
                self.assertNotIn('alphaCutoff', material)
                view = document['bufferViews'][document['images'][0]['bufferView']]
                embedded = Image.open(BytesIO(binary[view['byteOffset']:view['byteOffset']+view['byteLength']]))
                self.assertEqual(embedded.tobytes(), self.image.tobytes())

    def test_sloped_surface_exports_matching_vertex_and_uv_counts(self):
        model = self.model(); model['parts'][0]['shape'] = 'WedgeY'
        document, binary = build_models.glb_document(build_models.validate_model(model))
        attrs = document['meshes'][0]['primitives'][0]['attributes']
        self.assertEqual(document['accessors'][attrs['POSITION']]['count'],18)
        self.assertEqual(document['accessors'][attrs['TEXCOORD_0']]['count'],18)
        view = document['bufferViews'][document['accessors'][attrs['TEXCOORD_0']]['bufferView']]
        expected = tuple(v for p in build_models.solid_geometry('WedgeY')[0] for v in surfaces.uv(p,'XZ'))
        self.assertEqual(struct.unpack_from('<36f',binary,view['byteOffset']),expected)

    def test_mixed_curved_and_textured_meshes_keep_distinct_valid_accessors(self):
        model = self.model()
        model['parts'].append({'min': [2, 0, 0], 'max': [3, 1, 1], 'color': '#FFFFFF', 'shape': 'Ellipsoid', 'label': 'curved neighbour'})
        document, _ = build_models.glb_document(model)
        primitives = [mesh['primitives'][0] for mesh in document['meshes']]
        self.assertIn('TEXCOORD_0', primitives[0]['attributes'])
        self.assertNotIn('TEXCOORD_0', primitives[1]['attributes'])
        self.assertNotEqual(primitives[0]['attributes']['POSITION'], primitives[1]['attributes']['POSITION'])

if __name__ == '__main__':
    unittest.main()
