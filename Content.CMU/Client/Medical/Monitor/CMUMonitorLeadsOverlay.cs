using Content.Shared.CMU14.Medical.Monitor;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Client.CMU14.Medical.Monitor;

/// <summary>
/// Draws the lead wires from a patient monitor to its patient, like an IV line.
/// </summary>
public sealed class CMUMonitorLeadsOverlay : Overlay
{
    private static readonly Color LeadColor = Color.FromHex("#C8C8C8");

    [Dependency] private IEntityManager _entity = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    public CMUMonitorLeadsOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return;

        var transform = _entity.System<TransformSystem>();
        var query = _entity.EntityQueryEnumerator<CMUPatientMonitorComponent>();
        while (query.MoveNext(out var uid, out var monitor))
        {
            if (monitor.AttachedTo is not { Valid: true } patient || !_entity.EntityExists(patient))
                continue;

            var from = transform.GetMapCoordinates(uid);
            var to = transform.GetMapCoordinates(patient);
            if (from.MapId != args.MapId || to.MapId != args.MapId)
                continue;

            args.WorldHandle.DrawLine(from.Position, to.Position, LeadColor);
        }
    }
}
