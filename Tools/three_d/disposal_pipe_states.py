"""Bounded disposal junction anchor poses; source components are never mutated."""
from copy import deepcopy

FIELDS = ('anchored', 'alternateAnchorModel', 'preserveSlabCladding')
SOURCES = {'DisposalJunction': 'j1', 'DisposalJunctionFlipped': 'j2', 'DisposalXJunction': 'x'}
RSI = 'Structures/Piping/disposal.rsi'


def opted_in(model):
    return any(field in model for field in FIELDS)


def validate(model):
    if not opted_in(model):
        return model
    prototype = model.get('referencePrototype')
    anchored = model.get('anchored')
    if prototype not in SOURCES or type(anchored) is not bool:
        raise ValueError('Anchor poses require an explicit audited disposal source and boolean anchored state')
    state = ('pipe-' if anchored else 'conpipe-') + SOURCES[prototype]
    poses = model.get('spriteStates', {})
    if (not isinstance(model.get('alternateAnchorModel'), str) or not model['alternateAnchorModel'] or
            model['alternateAnchorModel'] == model['id'] or model.get('referenceRsi') != RSI or
            model.get('referenceState') != state or model.get('sourceDirections', 1) != 1 or
            model.get('referenceDirection', 0) != 0 or model.get('sourcePrototypes', []) != ([prototype] if anchored else []) or
            model.get('placement', 'floor') != 'floor' or model.get('useEntityRotation') is not True or
            set(poses) != {state} or len(poses[state].get('frames', [])) != 1 or poses[state].get('delays') != [1] or
            model.get('sourceSpriteRotates', False) is not (not anchored) or
            bool(model.get('floorOpening')) is not anchored or model.get('ceilingOpening') or
            model.get('preserveSlabCladding', False) is not anchored or
            any(model.get(field) for field in ('alternateFoldModel', 'alternateDoorModel', 'reagentTankAppearance',
                'chargerAppearance', 'randomSpritePrototypes', 'directionalModels', 'floorOpeningCompanions'))):
        raise ValueError('Disposal anchor pose requires its exact static state, reciprocal link and correct aperture contract')
    from sprite_states import vector2
    if vector2(model.get('sourceSpriteOffset', (0, 0))) != (0, 0) or vector2(model.get('groundOffset', (0, 0))) != (0, 0):
        raise ValueError('Disposal anchor pose requires the original zero pivot offsets')
    return model


def validate_links(models):
    library = {m['id']: m for m in models}
    for model in models:
        validate(model)
        if not opted_in(model):
            continue
        other = library.get(model['alternateAnchorModel'])
        if (other is None or other.get('alternateAnchorModel') != model['id'] or
                other.get('anchored') is not (not model['anchored']) or
                other.get('referencePrototype') != model['referencePrototype']):
            raise ValueError('Disposal anchor models must form a reciprocal installed/loose pair for the same source')
        validate(other)


def select_saved(model, library, default_components, saved_components):
    """Choose a reciprocal pair from effective Transform.Anchored, never appearance guesses."""
    try:
        validate(model)
        if not opted_in(model):
            return None, 'Model has no disposal anchor contract'
        anchored = {**default_components.get('Transform', {}), **saved_components.get('Transform', {})}.get('anchored', False)
        if type(anchored) is not bool:
            return None, 'Saved anchored state is not a boolean'
        alternate = library.get(model['alternateAnchorModel'])
        validate_links([model, alternate] if alternate else [model])
        return (model if model['anchored'] == anchored else alternate), None
    except (ValueError, TypeError, KeyError, AttributeError):
        return None, 'Disposal anchor model pair is unsupported'


def saved_pose(model, default_components, saved_components, normalize_tint):
    """Prove exactly the original AnchorVisuals owner before evaluating ordinary sprite guards."""
    try:
        validate(model)
        prototype = model['referencePrototype']
        kind = SOURCES[prototype]
        anchored = {**default_components.get('Transform', {}), **saved_components.get('Transform', {})}.get('anchored', False)
        if type(anchored) is not bool or anchored != model['anchored']:
            return None, 'Model anchor pose differs from the effective source transform'
        visualizer = {**default_components.get('GenericVisualizer', {}), **saved_components.get('GenericVisualizer', {})}
        visuals = visualizer.get('visuals')
        if not isinstance(visuals, dict) or set(visuals) != {'enum.AnchorVisuals.Anchored'}:
            return None, 'Disposal source requires exactly its AnchorVisuals owner'
        layer_map = visuals['enum.AnchorVisuals.Anchored']
        if not isinstance(layer_map, dict) or set(layer_map) != {'pipe'}:
            return None, 'Disposal anchor owner targets an unsupported layer'
        choices = {str(k).lower(): v for k, v in layer_map['pipe'].items()}
        if choices != {'false': {'state': 'conpipe-'+kind}, 'true': {'state': 'pipe-'+kind}}:
            return None, 'Disposal anchor owner has unsupported state changes'
        sprite = {**default_components.get('Sprite', {}), **saved_components.get('Sprite', {})}
        layers = sprite.get('layers')
        if (not isinstance(layers, list) or len(layers) != 1 or not isinstance(layers[0], dict) or
                layers[0].get('map') != ['pipe'] or layers[0].get('state') not in ('pipe-'+kind, 'conpipe-'+kind)):
            return None, 'Disposal source requires its sole mapped pipe layer'
        if saved_components.get('Appearance', {}).get('data'):
            return None, 'Saved disposal appearance data requires the live owner'
        # Only a fresh local copy receives the proven owner's state selection.
        # No prototype, saved component, or source layer dictionary is rewritten.
        local_defaults = deepcopy(default_components)
        local_saved = deepcopy(saved_components)
        sprite = deepcopy(sprite)
        sprite['layers'][0]['state'] = model['referenceState']
        local_defaults['Sprite'] = sprite
        local_saved.pop('Sprite', None)
        from sprite_states import saved_pose as ordinary_saved_pose
        return ordinary_saved_pose(model, local_defaults, local_saved, normalize_tint)
    except (ValueError, TypeError, KeyError, AttributeError):
        return None, 'Disposal source appearance cannot be proven'


def resolve_scene_pose(instance, model, default_components, saved_components, normalize_tint):
    state, reason = saved_pose(model, default_components, saved_components, normalize_tint)
    if state is None:
        instance.update(baseModelId=model['id'], modelId=None, modelStatus=None, matchKind='unmapped', unsupportedState=reason)
        return False
    sprite = {**default_components.get('Sprite', {}), **saved_components.get('Sprite', {})}
    instance.update(spriteState=state, spriteFrame=0, referenceState=state,
                    spriteStateTint=normalize_tint(sprite.get('color', '#FFFFFF')))
    return True
