using System.Linq;
using Content.Shared.Body;
using Content.Shared.CCVar;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Body;

[ByRefEvent]
public readonly record struct VisualBodySpriteRefreshEvent;

public sealed partial class VisualBodySystem
{
    private static readonly IReadOnlyDictionary<HumanoidVisualLayers, List<Marking>> EmptyMarkings =
        new Dictionary<HumanoidVisualLayers, List<Marking>>();

    [Dependency] private BodySystem _body = default!;

    /// <summary>
    /// Restores the authoritative organ visuals after a local-only appearance override.
    /// </summary>
    public void ClearLocalAppearanceOverride(
        Entity<VisualBodyComponent?> body,
        Dictionary<string, (EntityUid Organ, HumanoidVisualLayers Layer)> localMarkingLayers)
    {
        RemoveLocalMarkingLayers(body.Owner, localMarkingLayers);

        if (!Resolve(body, ref body.Comp, false))
            return;

        foreach (var organ in _body.EnumerateOrgans<VisualOrganComponent>(body.Owner))
        {
            if (!_sprite.LayerMapTryGet(body.Owner, organ.Comp2.Layer, out var index, false) ||
                !_sprite.TryGetLayer(body.Owner, index, out var layer, false))
            {
                continue;
            }

            var visible = layer.Visible;
            ApplyVisual((organ.Owner, organ.Comp2), body.Owner);
            _sprite.LayerSetVisible(body.Owner, index, visible);
        }

        foreach (var organ in _body.EnumerateOrgans<VisualOrganMarkingsComponent>(body.Owner))
        {
            RemoveMarkings((organ.Owner, organ.Comp2), body.Owner);
            ApplyMarkings((organ.Owner, organ.Comp2), body.Owner);
        }
    }

    /// <summary>
    /// Applies an appearance to the local sprite without changing authoritative organ state.
    /// The body's actual organ graph remains the render template, so removed organs stay removed.
    /// </summary>
    public void ApplyLocalAppearanceOverride(
        Entity<VisualBodyComponent?> body,
        HumanoidCharacterAppearance appearance,
        Sex sex,
        Dictionary<string, (EntityUid Organ, HumanoidVisualLayers Layer)> localMarkingLayers)
    {
        if (!Resolve(body, ref body.Comp, false))
            return;

        ClearLocalAppearanceOverride(body, localMarkingLayers);

        foreach (var organ in _body.EnumerateOrgans<VisualOrganComponent>(body.Owner))
        {
            if (!_sprite.LayerMapTryGet(body.Owner, organ.Comp2.Layer, out var index, false) ||
                !_sprite.TryGetLayer(body.Owner, index, out var layer, false))
            {
                continue;
            }

            var visible = layer.Visible;
            _sprite.LayerSetColor(
                body.Owner,
                index,
                organ.Comp2.Layer.Equals(HumanoidVisualLayers.Eyes)
                    ? appearance.EyeColor
                    : appearance.SkinColor);

            if (organ.Comp2.SexStateOverrides?.TryGetValue(sex, out var state) == true)
                _sprite.LayerSetRsiState(body.Owner, index, state);

            _sprite.LayerSetVisible(body.Owner, index, visible);
        }

        foreach (var organ in _body.EnumerateOrgans<VisualOrganMarkingsComponent>(body.Owner))
        {
            RemoveMarkings((organ.Owner, organ.Comp2), body.Owner);

            if (organ.Comp1.Category is not { } category)
                continue;

            var markings = appearance.Markings.GetValueOrDefault(category) ?? EmptyMarkings;

            ApplyLocalMarkings(
                (organ.Owner, organ.Comp2),
                body.Owner,
                markings,
                localMarkingLayers);
        }
    }

    /// <summary>
    /// Applies equipment visibility changes to local-only marking layers.
    /// </summary>
    public void SetLocalMarkingVisibility(
        Entity<VisualOrganMarkingsComponent> organ,
        EntityUid body,
        HumanoidLayerVisibilityChangedEvent visibility,
        Dictionary<string, (EntityUid Organ, HumanoidVisualLayers Layer)> localMarkingLayers)
    {
        foreach (var (key, data) in localMarkingLayers)
        {
            if (data.Organ != organ.Owner ||
                data.Layer != visibility.Layer &&
                !(organ.Comp.DependentHidingLayers.TryGetValue(visibility.Layer, out var dependent) &&
                  dependent.Contains(data.Layer)))
            {
                continue;
            }

            if (_sprite.LayerMapTryGet(body, key, out var index, false))
                _sprite.LayerSetVisible(body, index, visibility.Visible);
        }
    }

    private IEnumerable<Marking> LocalMarkings(
        Entity<VisualOrganMarkingsComponent> organ,
        IReadOnlyDictionary<HumanoidVisualLayers, List<Marking>> markings)
    {
        foreach (var markingList in markings.Values)
        {
            foreach (var marking in markingList)
            {
                yield return marking;
            }
        }

        var censorNudity = _cfg.GetCVar(CCVars.AccessibilityClientCensorNudity) ||
                           _cfg.GetCVar(CCVars.AccessibilityServerCensorNudity);
        if (!censorNudity)
            yield break;

        var group = ProtoMan.Index(organ.Comp.MarkingData.Group);
        foreach (var layer in organ.Comp.MarkingData.Layers)
        {
            if (!group.Limits.TryGetValue(layer, out var layerLimits) ||
                layerLimits.NudityDefault.Count < 1)
            {
                continue;
            }

            var layerMarkings = markings.GetValueOrDefault(layer) ?? [];
            if (layerMarkings.Any(marking =>
                    _marking.TryGetMarking(marking, out var proto) && proto.BodyPart == layer))
            {
                continue;
            }

            foreach (var marking in layerLimits.NudityDefault)
            {
                yield return new Marking(marking, 1);
            }
        }
    }

    private void ApplyLocalMarkings(
        Entity<VisualOrganMarkingsComponent> organ,
        Entity<SpriteComponent?> body,
        IReadOnlyDictionary<HumanoidVisualLayers, List<Marking>> markings,
        Dictionary<string, (EntityUid Organ, HumanoidVisualLayers Layer)> localMarkingLayers)
    {
        if (!Resolve(body, ref body.Comp))
            return;

        var markingIndex = 0;
        foreach (var marking in LocalMarkings(organ, markings))
        {
            var currentMarkingIndex = markingIndex++;
            if (!_marking.TryGetMarking(marking, out var proto) ||
                !_sprite.LayerMapTryGet(body, proto.BodyPart, out var bodyPartIndex, true) ||
                !_sprite.TryGetLayer(body, bodyPartIndex, out var bodyPartLayer, true))
            {
                continue;
            }

            organ.Comp.MarkingsDisplacement.TryGetValue(proto.BodyPart, out var displacement);

            var numDisplacements = 0;
            for (var i = 0; i < proto.Sprites.Count; i++)
            {
                if (proto.Sprites[i] is not SpriteSpecifier.Rsi rsi)
                    continue;

                var layerId = $"rmc-hidden-{organ.Owner.Id}-{currentMarkingIndex}-{i}-{proto.ID}-{rsi.RsiState}";
                var spriteLayer = _sprite.AddLayer(body, rsi, bodyPartIndex + i + numDisplacements + 1);
                _sprite.LayerMapSet(body, layerId, spriteLayer);
                _sprite.LayerSetSprite(body, spriteLayer, rsi);
                _sprite.LayerSetVisible(body, spriteLayer, bodyPartLayer.Visible);
                _sprite.LayerSetColor(
                    body,
                    spriteLayer,
                    marking.MarkingColors is not null && i < marking.MarkingColors.Count
                        ? marking.MarkingColors[i]
                        : Color.White);

                localMarkingLayers[layerId] = (organ.Owner, proto.BodyPart);

                if (displacement != null && proto.CanBeDisplaced)
                {
                    _displacement.TryAddDisplacement(
                        displacement,
                        (body, body.Comp),
                        bodyPartIndex + i + 1,
                        layerId,
                        out _);
                    numDisplacements++;
                }

                if (proto.Shaders?.TryGetValue(rsi.RsiState, out var shader) == true &&
                    _sprite.LayerMapTryGet(body, layerId, out var finalIndex, false))
                {
                    body.Comp.LayerSetShader(finalIndex, shader);
                }
            }
        }
    }

    private void RemoveLocalMarkingLayers(
        Entity<SpriteComponent?> body,
        Dictionary<string, (EntityUid Organ, HumanoidVisualLayers Layer)> localMarkingLayers)
    {
        if (!Resolve(body, ref body.Comp, false))
        {
            localMarkingLayers.Clear();
            return;
        }

        foreach (var key in localMarkingLayers.Keys)
        {
            _displacement.EnsureDisplacementIsNotOnSprite((body, body.Comp), key);

            if (!_sprite.LayerMapTryGet(body, key, out var index, false))
                continue;

            _sprite.LayerMapRemove(body, key);
            _sprite.RemoveLayer(body, index);
        }

        localMarkingLayers.Clear();
    }
}
