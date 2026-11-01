"""An owner pose must match its assembled geometry before it can enter a context GLB."""
from copy import deepcopy
import unittest

from export_scene import export_region
from foam_wall_states import compose_parts as foam_parts
from solution_glass_states import RMC, compose_parts as glass_parts, pose


class UtilityExportTests(unittest.TestCase):
    def fixture(self, family):
        part = dict(min=[0, 0, 0], max=[.1, .1, .1], color='#FFFFFF', label='body')
        model = dict(id='UtilityTest', label='test', status='draft', sourcePrototypes=['Source'], parts=[part])
        entity = dict(id=1, prototype='Source', position=[0, 0, 0], yaw=0, modelId=model['id'], matchKind='exact', geometryKey='pose')
        if family == 'foam':
            model['foamAppearance'] = dict(baseParts=[part], edgeParts={key:dict(parts=[{**part, 'label':key}])
                for key in ('south', 'east', 'north', 'west')})
            entity['foamPose'] = dict(edgeMask=1, spriteTint='#FFFFFFCC')
            parts = foam_parts(model, 1)
        else:
            model['referenceRsi'] = RMC
            layers = [pose('Base', 'test.rsi', 'base'), pose('Fill', 'test.rsi', 'fill', True, '#FF000080')]
            model['solutionAppearance'] = dict(defaultLayers=layers, layers=[dict(role=p['role'], rsi=p['rsi'], state=p['state'], parts=[part]) for p in layers])
            entity['solutionPose'] = dict(layers=layers, spriteTint='#FFFFFF')
            parts = glass_parts(model, layers)
        scene = dict(map=dict(path='test.yml'), instances=[entity], tiles=[], tilePalette={}, geometryVariants={'pose':deepcopy(parts)})
        return model, scene

    def test_exact_compositions_export_both_source_owners(self):
        for family in ('foam', 'solution'):
            model, scene = self.fixture(family)
            _, report = export_region(scene, [model], [0, 0, 0], floors=False)
            self.assertEqual(report['exportedEntities'], 1)
            self.assertEqual(report['solidParts'], 2)

    def test_stale_parts_and_missing_owner_are_not_exported(self):
        for family in ('foam', 'solution'):
            for change in ('color', 'owner'):
                model, scene = self.fixture(family)
                if change == 'color':
                    scene['geometryVariants']['pose'][0]['color'] = '#000000'
                else:
                    del scene['instances'][0][family+'Pose']
                with self.assertRaisesRegex(ValueError, 'No mapped geometry'):
                    export_region(scene, [model], [0, 0, 0], floors=False)
