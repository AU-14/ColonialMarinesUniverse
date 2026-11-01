"""Surface orientation, cutout preservation and embedded portable artwork."""
from io import BytesIO
from pathlib import Path
import json
import math
import struct
import sys
import unittest
from unittest.mock import patch

from PIL import Image

sys.path.insert(0, str(Path(__file__).parents[1]))
import build_models
import surfaces
import layout


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

    def test_original_pixels_keep_alpha_and_do_not_bleed_across_atlas_cells(self):
        outputs = surfaces.viewer_outputs()
        atlas = Image.open(BytesIO(outputs['surface-atlas.png']))
        self.assertEqual(atlas.size, (128, 2))
        self.assertEqual(atlas.crop((32, 0, 34, 2)).tobytes(), self.image.tobytes())
        self.assertEqual(atlas.getpixel((34, 0)), (0, 0, 0, 0))
        self.assertEqual(atlas.getpixel((31, 1)), (0, 0, 0, 0))

    def test_larger_image_expands_cells_without_resampling_existing_pixels(self):
        self.catalog['Wide'] = {'atlasIndex': 1, 'image': Image.new('RGBA', (17, 3), 'white'), 'texture': '/Textures/wide.png'}
        atlas = Image.open(BytesIO(surfaces.viewer_outputs()['surface-atlas.png']))
        self.assertEqual(atlas.size, (2048, 32))
        self.assertEqual(atlas.crop((512, 0, 514, 2)).tobytes(), self.image.tobytes())

    def test_extended_slots_cross_byte_boundaries_without_aliasing(self):
        for index in (255, 256, 257, 4095):
            self.catalog[f'Slot{index}'] = {'atlasIndex': index, 'image': self.image,
                                           'texture': f'/Textures/slot{index}.png'}
        outputs = surfaces.viewer_outputs()
        atlas = Image.open(BytesIO(outputs['surface-atlas.png']))
        document = json.loads(outputs['surfaces.json'])
        self.assertEqual(atlas.size, (128, 128))
        for index, origin in ((255, (124, 6)), (256, (126, 6)), (257, (0, 8)), (4095, (124, 126))):
            with self.subTest(index=index):
                x, y = origin
                self.assertEqual(document['surfaces'][f'Slot{index}']['rect'], [x, y, 2, 2])
                self.assertEqual(atlas.crop((x, y, x+2, y+2)).tobytes(), self.image.tobytes())

    def test_surface_projects_right_way_up_on_floor_front_and_side(self):
        for axis, upper_left, lower_right in [
            ('XZ', (-.5, -.5, .5), (.5, -.5, -.5)),
            ('XY', (-.5, .5, .5), (.5, -.5, .5)),
            ('YZ', (.5, .5, .5), (.5, -.5, -.5)),
        ]:
            with self.subTest(axis=axis):
                self.assertEqual(surfaces.uv(upper_left, axis), (0, 0))
                self.assertEqual(surfaces.uv(lower_right, axis), (1, 1))

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

    def test_unknown_surface_and_unsupported_transform_cannot_silently_drop_art(self):
        model = self.model()
        for changes in ({'surface': 'Missing'}, {'surfaceAxis': 'bad'}, {'shape': 'Ellipsoid', 'label': 'curved neighbour'}):
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                build_models.validate_model({**model, 'parts': [{**model['parts'][0], **changes}]})
        with self.assertRaisesRegex(ValueError, 'connected surface UV'):
            build_models.validate_model({**model, 'connectToNeighbours': True})

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

    def test_review_textured_slope_interpolates_vertex_shading(self):
        self.image.paste((255, 0, 0, 255), (0, 0, 2, 2))
        model = self.model(); model['parts'][0]['shape'] = 'WedgeY'
        image = build_models.render_model(model, (128,128), yaw=-math.pi/2, pitch=.4)
        colors = list(image.crop((48,48,80,80)).get_flattened_data())
        self.assertTrue(all(r > 80 and g < 8 and b < 8 for r,g,b in colors))

    def test_reverse_slope_uvs_follow_mirrored_geometry_without_mirroring_art(self):
        model = self.model(); model['parts'][0]['shape'] = 'WedgeYReverse'
        document, binary = build_models.glb_document(build_models.validate_model(model))
        attrs = document['meshes'][0]['primitives'][0]['attributes']
        self.assertEqual(document['accessors'][attrs['POSITION']]['count'],18)
        uv = document['accessors'][attrs['TEXCOORD_0']]
        self.assertEqual(uv['count'],18)
        view = document['bufferViews'][uv['bufferView']]
        expected = tuple(v for p in build_models.solid_geometry('WedgeYReverse')[0] for v in surfaces.uv(p,'XZ'))
        self.assertEqual(struct.unpack_from('<36f',binary,view['byteOffset']),expected)
        self.image.paste((255,0,0,255),(0,0,2,2))
        image = build_models.render_model(model,(128,128),yaw=math.pi/2,pitch=.4)
        self.assertTrue(all(r>80 and g<8 and b<8 for r,g,b in image.crop((48,48,80,80)).get_flattened_data()))

    def test_review_fractional_source_reveals_backing_without_writing_cutout_depth(self):
        self.image.paste((255, 0, 0, 128), (0, 0, 2, 2))
        model = self.model()
        model['parts'].append({'min': [-1, .1, 0], 'max': [1, .12, 2],
                               'color': '#0000FF', 'label': 'blue backing'})
        image = build_models.render_model(model, (128, 128), yaw=-math.pi/2, pitch=0)
        colors = list(image.crop((48, 48, 80, 80)).get_flattened_data())
        self.assertEqual(sum(r > 80 and g < 8 and b < 8 for r,g,b in colors), 512)
        self.assertEqual(sum(b > 80 and r < 8 and g < 8 for r,g,b in colors), 512)

    def test_mixed_curved_and_textured_meshes_keep_distinct_valid_accessors(self):
        model = self.model()
        model['parts'].append({'min': [2, 0, 0], 'max': [3, 1, 1], 'color': '#FFFFFF', 'shape': 'Ellipsoid', 'label': 'curved neighbour'})
        document, _ = build_models.glb_document(model)
        primitives = [mesh['primitives'][0] for mesh in document['meshes']]
        self.assertIn('TEXCOORD_0', primitives[0]['attributes'])
        self.assertNotIn('TEXCOORD_0', primitives[1]['attributes'])
        self.assertNotEqual(primitives[0]['attributes']['POSITION'], primitives[1]['attributes']['POSITION'])

    def test_room_side_mount_keeps_art_readable_and_exports_the_same_flip(self):
        model = self.model()
        model['parts'] = layout.inside_wall_parts(model['parts'])
        self.assertTrue(model['parts'][0]['surfaceFlipU'])
        document, binary = build_models.glb_document(model)
        index = document['meshes'][0]['primitives'][0]['attributes']['TEXCOORD_0']
        view = document['bufferViews'][document['accessors'][index]['bufferView']]
        expected = tuple(v for x,y,z in build_models.cube_geometry()[0]
                         for u,vv in [surfaces.uv((x,-z,y),'XZ')] for v in (1-u,vv))
        self.assertEqual(struct.unpack_from('<48f', binary, view['byteOffset']), expected)
        self.assertTrue(document['materials'][0]['doubleSided'])
        self.assertFalse(layout.inside_wall_parts(model['parts'])[0]['surfaceFlipU'])


if __name__ == '__main__':
    unittest.main()
