// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Numerics;
using Content.Client.Botany.Components;
using Content.Shared.CMU14.Farming;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.Farming;

// A plant that grows in a planter (a tub, a plank bed) is drawn smaller, so that it does not stick out over the edge of the planter: its sprite is made one and a half times smaller
// for as long as its parent is a planter, and has its size back when it is not. It is also drawn LiftPixels higher, so that it stands in the earth of the planter and not at the
// bottom of it. The plants are few, so they are looked at every frame (the botany owns the events of its plants, and a second system cannot subscribe to them).
public sealed class PlanterPlantScaleSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private const float Shrink = 1.5f;

    // How many pixels of the sprite a plant in a planter is lifted by; up on the screen is positive.
    private const float LiftPixels = 7f;
    private static readonly Vector2 Lift = new(0f, LiftPixels / EyeManager.PixelsPerMeter);

    private readonly HashSet<EntityUid> _shrunk = new();
    private EntityQuery<FarmPlanterSoilComponent> _planters;

    public override void Initialize()
    {
        base.Initialize();

        _planters = GetEntityQuery<FarmPlanterSoilComponent>();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<PlantVisualsComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite, out var xform))
        {
            var inPlanter = _planters.HasComp(xform.ParentUid);
            if (inPlanter == _shrunk.Contains(uid))
                continue;

            var factor = inPlanter ? 1f / Shrink : Shrink;
            _sprite.SetScale((uid, sprite), sprite.Scale * new Vector2(factor, factor));
            _sprite.SetOffset((uid, sprite), inPlanter ? sprite.Offset + Lift : sprite.Offset - Lift);

            if (inPlanter)
                _shrunk.Add(uid);
            else
                _shrunk.Remove(uid);
        }
    }
}
