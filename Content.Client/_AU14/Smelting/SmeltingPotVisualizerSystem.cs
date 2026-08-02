// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Numerics;
using Content.Shared._AU14.Smelting;
using Content.Shared.Stacks;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._AU14.Smelting;

/// <summary>
/// Draws the pot's contents as small "pips" sitting in the crucible - one per unit of material - and hisses
/// the pips away as batches consume them.
///
/// The pip positions come from a fixed scatter table rather than being randomised per update: a pip must stay
/// where it is while its neighbours are consumed, otherwise the whole pile reshuffles every few seconds.
/// </summary>
public sealed class SmeltingPotVisualizerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    /// <summary>🔧 TUNABLE: most pips drawn at once. Past this each pip stands for several units - 100 real
    /// layers per pot, rebuilt on every change, is not worth the fidelity when several pots share a fire.</summary>
    private const int MaxPips = 20;

    /// <summary>🔧 TUNABLE: pip size relative to a full tile sprite.</summary>
    private const float PipScale = 0.28f;

    /// <summary>🔧 TUNABLE: pip tint. Darkened and part-transparent so the contents read as sitting DOWN IN
    /// the melt rather than as bright icons pasted on top of the pot.</summary>
    private static readonly Color PipTint = new(0.45f, 0.42f, 0.40f, 0.62f);

    /// <summary>🔧 TUNABLE: how far the scatter spreads. Below 1 the pips pull toward the centre of the pot,
    /// keeping them inside the bowl instead of creeping over its rim.</summary>
    private const float PipSpread = 0.55f;

    /// <summary>🔧 TUNABLE: lifts the whole pile to sit in the mouth of the pot rather than at its base.</summary>
    private const float PipCentreY = -0.04f;

    private const string PipLayerPrefix = "au14-pip-";

    /// <summary>Scatter offsets in tile fractions, ordered so early pips sit low and centre (the bottom of the
    /// pot fills first) and later ones stack up and outward. Scaled by <see cref="PipSpread"/> at use.</summary>
    private static readonly Vector2[] PipOffsets =
    {
        new(0.00f, -0.06f), new(-0.10f, -0.04f), new(0.10f, -0.03f), new(-0.05f, 0.02f),
        new(0.06f, 0.03f), new(-0.15f, 0.01f), new(0.15f, 0.00f), new(0.00f, 0.06f),
        new(-0.09f, 0.07f), new(0.09f, 0.07f), new(-0.18f, 0.06f), new(0.18f, 0.05f),
        new(-0.04f, 0.11f), new(0.05f, 0.11f), new(-0.13f, 0.12f), new(0.13f, 0.11f),
        new(0.00f, 0.15f), new(-0.08f, 0.16f), new(0.08f, 0.16f), new(0.00f, -0.11f),
    };

    public override void Initialize()
    {
        SubscribeLocalEvent<SmeltingPotComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<SmeltingPotComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnState(Entity<SmeltingPotComponent> pot, ref AfterAutoHandleStateEvent args)
    {
        UpdatePips(pot);
    }

    private void OnShutdown(Entity<SmeltingPotComponent> pot, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(pot, out var sprite))
            ClearPips(new Entity<SpriteComponent?>(pot, sprite));
    }

    private void UpdatePips(Entity<SmeltingPotComponent> pot)
    {
        if (!TryComp<SpriteComponent>(pot, out var sprite))
            return;

        ClearPips(new Entity<SpriteComponent?>(pot, sprite));

        if (pot.Comp.Amount <= 0)
            return;

        if (GetPipTexture(pot.Comp) is not { } texture)
            return;

        // Past MaxPips each pip represents several units, so the pile still visibly grows and shrinks without
        // the layer count running away.
        var pips = Math.Min(pot.Comp.Amount, MaxPips);

        for (var i = 0; i < pips; i++)
        {
            var offset = PipOffsets[i % PipOffsets.Length] * PipSpread + new Vector2(0f, PipCentreY);

            var index = _sprite.AddLayer((pot, sprite), texture);
            _sprite.LayerSetScale((pot, sprite), index, new Vector2(PipScale, PipScale));
            _sprite.LayerSetOffset((pot, sprite), index, offset);
            _sprite.LayerSetColor((pot, sprite), index, PipTint);
            _sprite.LayerMapSet((pot, sprite), PipLayerPrefix + i, index);
        }
    }

    private void ClearPips(Entity<SpriteComponent?> sprite)
    {
        for (var i = 0; i < MaxPips; i++)
        {
            var key = PipLayerPrefix + i;
            if (_sprite.LayerMapTryGet(sprite, key, out var index, false))
                _sprite.RemoveLayer(sprite, index);
        }
    }

    /// <summary>Pips reuse the material's own stack icon, so ore looks like ore and sheets look like sheets
    /// without any new art. Electronics have no stack, so they carry an icon on the component instead.</summary>
    private SpriteSpecifier? GetPipTexture(SmeltingPotComponent pot)
    {
        if (pot.Electronics)
            return pot.ElectronicsIcon;

        return pot.Material is { } material && _prototype.TryIndex(material, out var proto) ? proto.Icon : null;
    }
}
