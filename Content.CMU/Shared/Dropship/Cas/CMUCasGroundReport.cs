using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Dropship.Cas;

/// <summary>A display-only ground report; the provider must revalidate its ID when resolving it.</summary>
[Serializable, NetSerializable]
public sealed record CMUCasGroundReport(int Id, string Observer, string Map, int X, int Y);

/// <summary>Server broadcast query for ground reports visible to Actor. No provider means no reports.</summary>
[ByRefEvent]
public readonly record struct CMUCasGroundReportsEvent(EntityUid Actor, List<CMUCasGroundReport> Reports);

/// <summary>
/// Server broadcast query to resolve a report for Actor. Providers must enforce faction, expiry,
/// planetary surface and distance authorization before setting Coordinates; null rejects the report.
/// </summary>
[ByRefEvent]
public record struct CMUCasResolveGroundReportEvent(EntityUid Actor, int ReportId)
{
    public EntityCoordinates? Coordinates;
}
