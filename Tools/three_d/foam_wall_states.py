"""Exact static aluminium foam layers and the original SmoothEdge visibility owner."""
from collections import defaultdict
from copy import deepcopy
import json
import math

from sprite_states import vector2

PROTOTYPE = 'RMCFoamedAluminiumMetal'
RSI = 'Effects/foam.rsi'
BASE = 'metal_foam'
EDGES = ('south', 'east', 'north', 'west')
OFFSETS = ((0, -1), (1, 0), (0, 1), (-1, 0))
MAPS = ('enum.FoamVisualLayers.Base',) + tuple('enum.EdgeLayer.'+d.title() for d in EDGES)
STATES = (BASE,) + tuple(BASE+'-'+d for d in EDGES)
TINT = '#FFFFFFCC'


def compose_parts(model, mask=15, tint=TINT):
    if type(mask) is not int or not 0 <= mask <= 15:
        raise ValueError('Foam mask must name four source-visible edges')
    definition = model['foamAppearance']
    result = list(definition['baseParts'])
    for i, edge in enumerate(EDGES):
        if mask & (1 << i):
            result += definition['edgeParts'][edge]['parts']
    if not 1 <= len(result) <= 128:
        raise ValueError('Foam source composition exceeds the whole-model part budget')
    if tint != TINT:
        from reagent_tank_states import channels, color_hex
        rgba = channels(tint)
        ratio = [rgba[0], rgba[1], rgba[2], rgba[3]/.8]
        result = [{**p, 'color': color_hex([a*b for a, b in zip(channels(p['color']), ratio)])} for p in result]
    return result


def validate(model, validate_model):
    definition = model.get('foamAppearance')
    if (not isinstance(definition, dict) or set(definition) != {'baseParts', 'edgeParts'} or
            set(definition['edgeParts']) != set(EDGES) or model.get('referencePrototype') != PROTOTYPE or
            model.get('sourcePrototypes') != [PROTOTYPE] or model.get('referenceRsi') != RSI or
            model.get('referenceState') != BASE or model.get('sourceDirections', 1) != 1 or
            model.get('referenceDirection', 0) != 0 or model.get('placement', 'floor') != 'floor' or
            model.get('useEntityRotation') is not True or vector2(model.get('groundOffset', (0, 0))) != (0, 0) or
            model.get('bakedSpriteTint', '').upper() != TINT or model.get('referenceTint', '').upper() != TINT or
            any(model.get(k) for k in ('spriteStates', 'sourceSpriteRotates', 'sourceSpriteOffset', 'chargerAppearance',
                'reagentTankAppearance', 'floorOpening', 'ceilingOpening', 'connectToNeighbours', 'directionalModels',
                'cornerSurfaces', 'wallMounted', 'randomSpritePrototypes', 'alternateAnchorModel', 'supportSurface'))):
        raise ValueError('Foam requires its independent exact five-layer SmoothEdge contract')
    template = {k: v for k, v in model.items() if k != 'foamAppearance'}
    def checked(parts):
        result = validate_model({**template, 'parts': parts})['parts']
        if any(p['color'].upper() != TINT for p in result):
            raise ValueError('Foam parts must carry source Sprite alpha exactly once')
        return result
    definition = {'baseParts': checked(definition['baseParts']), 'edgeParts': {
        edge: {'parts': checked(definition['edgeParts'][edge]['parts'])} for edge in EDGES}}
    result = {**model, 'foamAppearance': definition}
    if result['parts'] != compose_parts(result):
        raise ValueError('Default foam geometry must have all four source edge layers')
    for mask in range(16):
        compose_parts(result, mask)
    return result


def validate_source(model, resource_file):
    from PIL import Image
    folder = resource_file(RSI)
    meta = json.loads((folder/'meta.json').read_text())
    if meta['size'] != {'x': 32, 'y': 32}:
        raise ValueError('Foam source dimensions changed')
    states = {s['name']: s for s in meta['states']}
    images = {}
    for name in STATES:
        state = states.get(name, {})
        if state.get('directions', 1) != 1 or state.get('delays', [[1]]) != [[1]]:
            raise ValueError('Foam source must remain static and one-directional')
        image = Image.open(folder/(name+'.png')).convert('RGBA')
        expected = {255} if name == BASE else {0, 240, 255}
        if image.size != (32, 32) or set(image.getchannel('A').get_flattened_data()) != expected:
            raise ValueError('Foam source dimensions or original alpha classes changed')
        images[name] = image
    return images


def portable_states(model):
    return {f'edges-{mask}': {'frames': [{'parts': compose_parts(model, mask)}], 'delays': [1]} for mask in range(16)}


def viewer_references(model, resource_file, png_bytes):
    from PIL import Image
    images = validate_source(model, resource_file)
    output, references = {}, {}
    for mask in range(16):
        canvas = Image.new('RGBA', (96, 96))
        for index, state in enumerate(STATES):
            if index and not mask & (1 << (index-1)):
                continue
            image = images[state].copy()
            image.putalpha(image.getchannel('A').point(lambda a: round(a*.8)))
            dx, dy = (0, 0) if not index else OFFSETS[index-1]
            canvas.alpha_composite(image, (32+32*dx, 32-32*dy))
        name = f'references/{model["id"]}-foam-edges-{mask}.png'
        output[name] = png_bytes(canvas)
        references[f'edges-{mask}'] = ['../generated/'+name]
    return output, references


def model_document(model, build_document):
    from sprite_states import model_document as sprite_document
    virtual = {**model, 'spriteStates': portable_states(model), 'referenceState': 'edges-15', 'sourceSpriteOffset': [0, 0]}
    document, binary = sprite_document(virtual, build_document)
    document['extras']['foamAppearance'] = dict(sourceOwned=True, maskOrder=list(EDGES),
        sourceOffsets=[list(x) for x in OFFSETS], originalSpriteAlpha=.8,
        limitation='Dithered solid transparency approximates source alpha blending. Geometry height and hidden surfaces are inferred.')
    return document, binary


def owner(defaults, saved, name):
    return {**defaults.get(name, {}), **saved.get(name, {})}


def saved_pose(model, defaults, saved, normalize_tint):
    try:
        sprite = owner(defaults, saved, 'Sprite')
        smooth = owner(defaults, saved, 'IconSmooth')
        if ('SmoothEdge' not in defaults and 'SmoothEdge' not in saved or
                smooth.get('enabled', True) is not True or smooth.get('key') != 'walls' or
                smooth.get('mode') != 'NoSprite' or smooth.get('additionalKeys', []) or
                owner(defaults, saved, 'CMIconSmooth').get('smooth', False) is not False):
            raise ValueError('Foam requires its enabled walls/NoSprite SmoothEdge owner')
        if (sprite.get('noRot', False) or sprite.get('snapCardinals', False) or sprite.get('visible', True) is not True or
                vector2(sprite.get('scale', (1, 1))) != (1, 1) or vector2(sprite.get('offset', (0, 0))) != (0, 0) or
                str(sprite.get('rotation', 0)).removesuffix(' rad').removesuffix('rad').strip() not in ('0', '0.0') or
                sprite.get('granularLayersRendering', False) or sprite.get('postShader') or sprite.get('postShaders')):
            raise ValueError('Foam sprite has unsupported transform or renderer state')
        if str(sprite.get('sprite', '')).removeprefix('/Textures/') != RSI:
            raise ValueError('Foam sprite RSI changed')
        layers = sprite.get('layers', [])
        if not isinstance(layers, list) or len(layers) != 5:
            raise ValueError('Foam requires all five ordered source layers')
        for i, layer in enumerate(layers):
            offset = (0, 0) if not i else OFFSETS[i-1]
            # Serialized defaults precede SmoothEdge startup, which sets exact offsets.
            if (layer.get('state') != STATES[i] or layer.get('map') != [MAPS[i]] or
                    layer.get('visible', True) is not True or
                    vector2(layer.get('offset', offset)) != offset or
                    vector2(layer.get('scale', (1, 1))) != (1, 1) or layer.get('rotation', 0) not in (0, '0', '0 rad') or
                    layer.get('rsi', RSI) != RSI or layer.get('texture') or layer.get('shader') or layer.get('shaderPrototype') or
                    layer.get('copyToShaderParameters') is not None or layer.get('dirOffset') not in (None, 0, 'None') or
                    normalize_tint(layer.get('color', '#FFFFFF')) != '#FFFFFF'):
                raise ValueError('Foam source layer differs from its static owner contract')
        if saved.get('Appearance', {}).get('data') or 'GenericVisualizer' in defaults or 'GenericVisualizer' in saved:
            raise ValueError('Additional foam appearance owner is unsupported')
        return {'spriteTint': normalize_tint(sprite.get('color', '#FFFFFF'))}, None
    except (ValueError, KeyError, TypeError, AttributeError) as error:
        return None, str(error)


class EdgeGraph:
    """The owner's grid-cardinal key matching, independent of source sprite yaw."""
    def __init__(self, records, defaults, transforms):
        self.records, self.defaults, self.transforms = records, defaults, transforms
        self.cells = defaultdict(list)
        self.unknown = defaultdict(list)
        for uid, record in records.items():
            if transforms.local(uid).get('anchored', False) is not True:
                continue
            proto = record['prototype']
            smooth = owner(defaults.get(proto, {}), record['components'], 'IconSmooth')
            if smooth.get('enabled', True) is not True:
                continue
            if smooth.get('key') != 'walls' and proto in defaults:
                continue
            try:
                context = self.grid_context(uid)
            except (ValueError, TypeError, KeyError):
                continue
            if context is None:
                continue
            if proto not in defaults and proto:
                self.unknown[context].append(uid)
            elif smooth.get('key') == 'walls':
                self.cells[context].append(uid)

    def grid_context(self, uid):
        parent, visited = uid, set()
        while parent in self.records and 'MapGrid' not in self.records[parent]['components']:
            if parent in visited:
                raise ValueError('Cyclic foam grid parent')
            visited.add(parent)
            parent = int(self.transforms.local(parent).get('parent', 0))
        if parent not in self.records:
            return None
        gx, gy, yaw, _ = self.transforms.resolve(parent)
        x, y, _, _ = self.transforms.resolve(uid)
        c, s = math.cos(yaw), math.sin(yaw)
        return parent, math.floor(c*(x-gx)+s*(y-gy)), math.floor(-s*(x-gx)+c*(y-gy))

    def mask(self, uid):
        context = self.grid_context(uid)
        if context is None:
            return 15, {edge: [] for edge in EDGES}
        grid, x, y = context
        matches, mask = {}, 0
        for i, (edge, (dx, dy)) in enumerate(zip(EDGES, OFFSETS)):
            cell = grid, x+dx, y+dy
            if self.unknown[cell]:
                raise ValueError('Unresolved anchored neighbor may own a smoothing key')
            matches[edge] = sorted(self.cells[cell])
            if not matches[edge]:
                mask |= 1 << i
        return mask, matches


def apply_scene(instances, library, records, defaults, transforms, variants, normalize_tint):
    targets = [e for e in instances if library.get(e.get('modelId'), {}).get('foamAppearance')]
    if not targets:
        return {'applied': [], 'rejected': []}
    graph = EdgeGraph(records, defaults, transforms)
    result = {'applied': [], 'rejected': []}
    for entity in targets:
        model = library[entity['modelId']]
        try:
            if entity['prototype'] != PROTOTYPE or entity.get('matchKind') != 'exact':
                raise ValueError('Only the exact aluminium foam source can use this geometry')
            pose, reason = saved_pose(model, defaults.get(PROTOTYPE, {}), records[entity['id']]['components'], normalize_tint)
            if pose is None:
                raise ValueError(reason)
            mask, matches = graph.mask(entity['id'])
            key = f"{model['id']}:foam:edges-{mask}:{pose['spriteTint']}"
            variants[key] = compose_parts(model, mask, pose['spriteTint'])
            entity.update(foamPose={**pose, 'edgeMask': mask, 'matchingNeighbors': matches}, geometryKey=key)
            result['applied'].append({'id': entity['id'], 'edgeMask': mask})
        except (ValueError, TypeError, KeyError, AttributeError) as error:
            entity.update(baseModelId=model['id'], modelId=None, modelStatus=None, matchKind='unmapped', unsupportedState=str(error))
            result['rejected'].append({'id': entity['id'], 'reason': str(error)})
    return result
