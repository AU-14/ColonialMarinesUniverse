"""Saved audited cash/material appearance after StackSystem initializes its counter."""
from functools import cache
from pathlib import Path

import inventory

ROOT = Path(__file__).resolve().parents[2]
MATERIAL_STACKS = {
    'CMSteel': ('metal', 'Resources/Prototypes/_RMC14/Entities/Objects/Materials/Sheets/metal.yml'),
    'RMCPlastic': ('plastic', 'Resources/Prototypes/_RMC14/Entities/Objects/Materials/Sheets/plastic.yml'),
}


@cache
def dollar_limit():
    # The source stack prototype is unparented. Verify it instead of assuming
    # every stack uses an unlimited maximum or fabricating appearance data.
    path = ROOT / 'Resources/Prototypes/_RMC14/Entities/Objects/Misc/spacecash.yml'
    records = inventory.load_yaml(path.read_text(encoding='utf-8'))
    matches = [row for row in records if row.get('type') == 'stack' and row.get('id') == 'Dollar']
    if len(matches) != 1 or matches[0].get('parent'):
        raise ValueError('Dollar stack prototype needs a source maximum audit')
    value = matches[0].get('maxCount')
    if value is None:
        return 2**31 - 1
    if type(value) is not int or value <= 0:
        raise ValueError('Dollar stack maximum is invalid')
    return value


@cache
def material_limit(stack_type):
    """Only the two source-audited, unparented material stack prototypes."""
    if stack_type not in MATERIAL_STACKS:
        raise ValueError('Material stack owner has not been audited')
    _, filename = MATERIAL_STACKS[stack_type]
    records = inventory.load_yaml((ROOT / filename).read_text(encoding='utf-8'))
    matches = [row for row in records if row.get('type') == 'stack' and row.get('id') == stack_type]
    if len(matches) != 1 or matches[0].get('parent'):
        raise ValueError('Material stack prototype needs a source maximum audit')
    maximum = matches[0].get('maxCount')
    if type(maximum) is not int or not 0 < maximum <= 2**31 - 1:
        raise ValueError('Material stack prototype requires an explicit positive Int32 maximum')
    return maximum


def saved_threshold_state(defaults, saved, layer, states):
    """Bounded opaque Dollar/Threshold or CMSteel/RMCPlastic/None contract.

    The historical entry-point name is retained for existing callers. This never
    falls back to a guessed state for another stack type or visualizer.
    """
    stack = {**defaults.get('Stack', {}), **saved.get('Stack', {})}
    threshold = {**defaults.get('StackLayerThreshold', {}), **saved.get('StackLayerThreshold', {})}
    stack_type = stack.get('stackType')
    cash = stack_type == 'Dollar' and stack.get('layerFunction') == 'Threshold'
    material = (stack_type in MATERIAL_STACKS and stack.get('layerFunction', 'None') == 'None' and
                'StackLayerThreshold' not in defaults and 'StackLayerThreshold' not in saved)
    if (not (cash or material) or
            stack.get('composite', False) or 'Appearance' not in defaults and 'Appearance' not in saved or
            any(key in defaults or key in saved for key in ('GenericVisualizer', 'ItemCounter', 'RandomSprite'))):
        return None, 'Stack appearance requires its audited opaque cash or material owner'
    keys = layer.get('map', [])
    if not stack.get('baseLayer') or not isinstance(keys, list) or stack['baseLayer'] not in keys:
        return None, 'Stack base layer is not the authored visible layer'
    count = stack.get('count', 30)
    maximum = stack.get('maxCountOverride')
    maximum = (dollar_limit() if cash else material_limit(stack_type)) if maximum is None else maximum
    levels, cuts = stack.get('layerStates'), threshold.get('thresholds')
    if (type(count) is not int or not 0 < count <= 2**31 - 1 or type(maximum) is not int or maximum <= 0 or
            not isinstance(levels, list) or not 2 <= len(levels) <= 32 or
            any(not isinstance(state, str) or state not in states for state in levels) or
            len(set(levels)) != len(levels)):
        return None, 'Stack count, maximum or complete source compositions are unsupported'
    if material:
        prefix = MATERIAL_STACKS[stack_type][0]
        expected = [prefix, prefix + '_2', prefix + '_3', prefix + '_4']
        if maximum > 2**31 - 1 or levels != expected:
            return None, 'Material stack requires its audited four source states and Int32 maximum'
        # No ApplyLayerFunction adjustment. ItemCounterSystem uses
        # RoundToEqualLevels(actual, maximum, 4), with ToZero rounding.
        index = len(levels) - 1 if count >= maximum else count * len(levels) // maximum
        return levels[index], None
    if (not isinstance(cuts, list) or len(cuts) != len(levels) - 1 or
            any(type(cut) is not int or cut <= 0 for cut in cuts) or cuts != sorted(set(cuts))):
        return None, 'Stack count, thresholds or complete source compositions are unsupported'
    # StackSystem.ApplyThreshold then RoundToEqualLevels(..., MidpointRounding.ToZero).
    maximum = min(len(cuts) + 1, maximum)
    index = 0
    for cut in cuts:
        if count >= cut and index < maximum:
            index += 1
        else:
            break
    index = len(levels) - 1 if index >= maximum else index * len(levels) // maximum
    return levels[index], None
