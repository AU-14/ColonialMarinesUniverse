using System.Numerics;
using Content.Shared.CMU14.Hypospray;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Client.CMU14.Hypospray;

/// <summary>
/// Draws the wire between a station and its injector while the injector is out, a plain black line drawn the
/// same way as the IV drip and patient monitor leads.
/// </summary>
public sealed partial class CMUHyposprayOverlay : Overlay
{
    [Dependency] private IEntityManager _entity = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    public CMUHyposprayOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return;

        var transformSystem = _entity.System<TransformSystem>();
        var handle = args.WorldHandle;

        var injectors = _entity.EntityQueryEnumerator<CMUHyposprayDeployedComponent, CMUHyposprayComponent>();
        while (injectors.MoveNext(out var injectorId, out _, out var injector))
        {
            if (injector.Station is not { Valid: true } station)
                continue;

            var injectorPosition = transformSystem.GetMapCoordinates(injectorId);
            var stationPosition = transformSystem.GetMapCoordinates(station);

            if (injectorPosition.MapId != args.MapId
                || stationPosition.MapId != args.MapId)
                continue;

            var range = _entity.TryGetComponent(station, out CMUHyposprayStationComponent? stationComp)
                ? stationComp.Range
                : 0f;

            // The wire goes red as the injector nears the tether limit.
            var tension = range > 0
                ? Math.Clamp(Vector2.Distance(injectorPosition.Position, stationPosition.Position) / range, 0f, 1f)
                : 1f;

            handle.DrawLine(
                injectorPosition.Position,
                stationPosition.Position,
                Color.InterpolateBetween(Color.Black, Color.Red, MathF.Max(0f, tension - 0.6f) / 0.4f));
        }
    }
}
