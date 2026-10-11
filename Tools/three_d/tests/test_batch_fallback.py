from copy import deepcopy
import unittest
from export_model_batch import rejected_inherited_candidate


class BatchFallbackTests(unittest.TestCase):
    def test_new_ancestor_diagnostic_keeps_unmapped_source_unchanged(self):
        before = dict(id=1, prototype='Cleaver', position=[1, 2, 0], yaw=0, modelId=None, matchKind='unmapped')
        after = dict(before, baseModelId='Knife', matchedPrototype='KitchenKnife', candidateModelIds=['Knife'], unsupportedState='Different RSI')
        library = {'Knife':dict(sourcePrototypes=['KitchenKnife'])}
        self.assertTrue(rejected_inherited_candidate(before, after, library, {'Knife'}))
        for key, value in [('position', [0, 0, 0]), ('renderOffset', [0, 0, 1]), ('geometryKey', 'new'),
                           ('modelId', 'Knife'), ('unsupportedState', ''), ('matchedPrototype', 'Other'), ('prototype', 'KitchenKnife')]:
            changed = deepcopy(after); changed[key] = value
            self.assertFalse(rejected_inherited_candidate(before, changed, library, {'Knife'}), key)
        self.assertFalse(rejected_inherited_candidate(before, after, library, set()))
