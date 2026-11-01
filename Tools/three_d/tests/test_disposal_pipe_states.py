import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import disposal_pipe_states as pipe
from scene import normalize_tint


def pair(kind='j1', prototype='DisposalJunction'):
    models = []
    for anchored in (True, False):
        state = ('pipe-' if anchored else 'conpipe-') + kind
        model = dict(id='Installed' if anchored else 'Loose', referencePrototype=prototype,
            sourcePrototypes=[prototype] if anchored else [], anchored=anchored,
            alternateAnchorModel='Loose' if anchored else 'Installed',
            referenceRsi=pipe.RSI, referenceState=state, sourceDirections=1, referenceDirection=0,
            sourceSpriteOffset='0, 0', groundOffset='0, 0', placement='floor', useEntityRotation=True,
            spriteStates={state: dict(frames=[{'parts': []}], delays=[1])})
        if anchored:
            model.update(floorOpening={'min': [-.5, -.5], 'max': [.3, .5]}, preserveSlabCladding=True)
        else:
            model['sourceSpriteRotates'] = True
        models.append(model)
    return models


def source(kind='j1'):
    return {'Transform': {'anchored': True},
        'Sprite': {'sprite': pipe.RSI, 'visible': True, 'layers': [{'map': ['pipe'], 'state': 'conpipe-'+kind}]},
        'GenericVisualizer': {'visuals': {'enum.AnchorVisuals.Anchored': {'pipe': {
            False: {'state': 'conpipe-'+kind}, True: {'state': 'pipe-'+kind}}}}}}


class DisposalAnchorTests(unittest.TestCase):
    def test_effective_anchor_selects_both_poses_without_source_mutation(self):
        for prototype, kind in pipe.SOURCES.items():
            installed, loose = pair(kind, prototype)
            library = {m['id']: m for m in (installed, loose)}
            defaults = source(kind)
            original = copy.deepcopy(defaults)
            for anchored, expected in ((True, installed), (False, loose)):
                saved = {'Transform': {'anchored': anchored, 'pos': '8.5, 12.5'}}
                frozen = copy.deepcopy(saved)
                model, reason = pipe.select_saved(installed, library, defaults, saved)
                self.assertIs(model, expected)
                self.assertIsNone(reason)
                self.assertEqual(pipe.saved_pose(model, defaults, saved, normalize_tint), (model['referenceState'], None))
                self.assertEqual(saved, frozen)
                self.assertEqual(defaults, original)

    def test_default_anchor_is_inherited_but_non_boolean_is_rejected(self):
        installed, loose = pair()
        library = {m['id']: m for m in (installed, loose)}
        self.assertIs(pipe.select_saved(loose, library, source(), {})[0], installed)
        self.assertIsNone(pipe.select_saved(installed, library, source(), {'Transform': {'anchored': 'false'}})[0])

    def test_pair_contract_rejects_missing_one_way_cross_family_and_wrong_state(self):
        for change in ('missing', 'oneWay', 'family', 'state', 'grate', 'looseOpening'):
            installed, loose = pair()
            library = {m['id']: m for m in (installed, loose)}
            if change == 'missing': del library['Loose']
            elif change == 'oneWay': loose['alternateAnchorModel'] = 'Other'
            elif change == 'family': loose['referencePrototype'] = 'DisposalXJunction'
            elif change == 'state': loose['referenceState'] = 'conpipe-j2'
            elif change == 'grate': installed['preserveSlabCladding'] = False
            else: loose['floorOpening'] = installed['floorOpening']
            with self.subTest(change=change):
                self.assertIsNone(pipe.select_saved(installed, library, source(), {})[0])

    def test_unrelated_source_never_opts_into_floor_opening(self):
        installed, loose = pair()
        installed['referencePrototype'] = 'DisposalSignalRouter'
        with self.assertRaises(ValueError): pipe.validate(installed)

    def test_changed_visual_owner_and_extra_layers_remain_fallback(self):
        model = pair()[0]
        for change in ('owner', 'target', 'state', 'effect', 'extraLayer', 'map', 'hidden', 'appearance'):
            defaults, saved = source(), {}
            if change == 'owner': defaults['GenericVisualizer']['visuals']['Other'] = {}
            elif change == 'target': defaults['GenericVisualizer']['visuals']['enum.AnchorVisuals.Anchored']['other'] = {}
            elif change == 'state': defaults['GenericVisualizer']['visuals']['enum.AnchorVisuals.Anchored']['pipe'][True]['state'] = 'pipe-x'
            elif change == 'effect': defaults['GenericVisualizer']['visuals']['enum.AnchorVisuals.Anchored']['pipe'][True]['color'] = '#FF0000'
            elif change == 'extraLayer': defaults['Sprite']['layers'].append({'state': 'smoke'})
            elif change == 'map': defaults['Sprite']['layers'][0]['map'] = ['other']
            elif change == 'hidden': defaults['Sprite']['visible'] = False
            else: saved = {'Appearance': {'data': {'enum.AnchorVisuals.Anchored': True}}}
            with self.subTest(change=change):
                self.assertIsNone(pipe.saved_pose(model, defaults, saved, normalize_tint)[0])

    def test_ordinary_sprite_transform_and_shader_guards_are_retained(self):
        model = pair()[0]
        for override in ({'noRot': True}, {'snapCardinals': True}, {'offset': '0.1, 0'}, {'scale': '2, 1'},
                         {'color': '#FFFFFFAA'}, {'layers': [{'map': ['pipe'], 'state': 'pipe-j1', 'shader': 'unshaded'}]}):
            self.assertIsNone(pipe.saved_pose(model, source(), {'Sprite': override}, normalize_tint)[0])


if __name__ == '__main__':
    unittest.main()
