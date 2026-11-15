using Content.Shared.CMU14.Dropship.Cas;
using Robust.Shared.Map;

namespace Content.Shared.CMU14.Guidance;

public sealed class CMUCasGuidanceIntegrationSystem : EntitySystem
{
    [Dependency] private CMUGuidanceSystem _guidance = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUCasGroundReportsEvent>(OnReports);
        SubscribeLocalEvent<CMUCasResolveGroundReportEvent>(OnResolve);
    }

    private void OnReports(ref CMUCasGroundReportsEvent args)
    {
        foreach (var report in _guidance.GetReports(args.Actor))
            args.Reports.Add(new CMUCasGroundReport(report.Id, report.Observer, report.Map, report.X, report.Y));
    }

    private void OnResolve(ref CMUCasResolveGroundReportEvent args)
    {
        if (!_guidance.TryGetGroundReport(args.Actor, args.ReportId, out var coordinates) ||
            !_maps.TryGetMap(coordinates.MapId, out var map) || map is not { } mapUid)
            return;

        args.Coordinates = _maps.TryFindGridAt(coordinates, out var grid, out _)
            ? _transform.ToCoordinates(grid, coordinates)
            : _transform.ToCoordinates(mapUid, coordinates);
    }
}
