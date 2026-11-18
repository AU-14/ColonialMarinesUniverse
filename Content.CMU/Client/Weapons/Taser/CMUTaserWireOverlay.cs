using Content.Shared.CMU14.Weapons.Taser;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client.CMU14.Weapons.Taser;

/// <summary>
/// Draws each taser's wire as a thin line from the gun to its probe, the same way the rotary phone draws its cord.
/// </summary>
public sealed class CMUTaserWireOverlay : Overlay
{
    private static readonly Color WireColor = Color.FromHex("#111111");

    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public CMUTaserWireOverlay(IEntityManager entities)
    {
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var query = _entities.EntityQueryEnumerator<CMUTetherTaserComponent>();
        while (query.MoveNext(out var uid, out var taser))
        {
            if (taser.Probe is not { } probe || !_entities.EntityExists(probe))
                continue;

            var from = _transform.GetMapCoordinates(uid);
            var to = _transform.GetMapCoordinates(probe);
            if (from.MapId != args.MapId || to.MapId != args.MapId)
                continue;

            handle.DrawLine(from.Position, to.Position, WireColor);
        }
    }
}
