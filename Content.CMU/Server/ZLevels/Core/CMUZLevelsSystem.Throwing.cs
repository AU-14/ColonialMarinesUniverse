using System.Numerics;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Item;
using Robust.Server.GameStates;
using Robust.Shared;
using Robust.Shared.GameObjects;

namespace Content.Server.CMU14.ZLevels.Core;

public sealed partial class CMUZLevelsSystem
{
    private void OnExpandOverheadItemPvs(ref ExpandPvsEvent args)
    {
        if (!_zLevelsEnabled)
            return;

        var range = _config.GetCVar(CVars.NetMaxUpdateRange);
        var query = EntityQueryEnumerator<CMUZFallingComponent, ItemComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var xform))
        {
            var position = _transform.GetWorldPosition(xform);
            var visible = args.Session.AttachedEntity is { } attached &&
                CanViewOverheadItem(uid, position, attached, range);

            if (!visible)
            {
                foreach (var view in args.Session.ViewSubscriptions)
                {
                    if (!CanViewOverheadItem(uid, position, view, range))
                        continue;

                    visible = true;
                    break;
                }
            }

            if (visible)
                (args.Entities ??= new()).Add(uid);
        }
    }

    private bool CanViewOverheadItem(EntityUid item, Vector2 position, EntityUid view, float range)
    {
        // Warm upper-level PVS probes are not actual viewpoints. Using them here would
        // let a probe on a roof extend visibility through that roof for a viewer below.
        if (IsZLevelProbe(view) ||
            !TryComp(view, out TransformComponent? xform) || xform.MapUid is not { } map)
            return false;

        if (TryComp<EyeComponent>(view, out var eye))
            range *= eye.PvsScale;

        var delta = Vector2.Abs(position - _transform.GetWorldPosition(xform));
        return delta.X <= range && delta.Y <= range &&
            TryGetOverheadItemProjection(item, map, out _, out _);
    }
}
