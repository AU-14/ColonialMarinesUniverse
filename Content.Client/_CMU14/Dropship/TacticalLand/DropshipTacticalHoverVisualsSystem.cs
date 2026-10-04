using System;
using System.Numerics;
using Content.Shared._CMU14.Dropship.TacticalLand;
using Robust.Client.GameObjects;

namespace Content.Client._CMU14.Dropship.TacticalLand;

public sealed partial class DropshipTacticalHoverVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var shadows = EntityQueryEnumerator<DropshipTacticalHoverShadowComponent, SpriteComponent>();
        while (shadows.MoveNext(out var uid, out var shadow, out var sprite))
        {
            var scale = new Vector2(
                Math.Max(1, shadow.Footprint.X),
                Math.Max(1, shadow.Footprint.Y));
            _sprite.SetScale((uid, sprite), scale);
        }
    }
}
