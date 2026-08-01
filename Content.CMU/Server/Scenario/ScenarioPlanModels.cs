using System.Linq;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Server.AU14.Scenario;

public interface IScenarioPlanGenerator
{
    ScenarioPlanShadowSnapshot? LastShadowPlan { get; }
    IReadOnlyList<ScenarioPlan> GeneratePlans(ScenarioPlanValidationRequest request);
    ScenarioPlanShadowSnapshot GenerateShadowPlan(ScenarioPlanValidationRequest request, string reason);
    ScenarioPlanValidationReport ValidateMarkerCoverageWithBackup(
        ScenarioPlanValidationRequest request,
        out bool usedBackup,
        out string backupDiagnostic);
    bool TryResolveDeferredThreatVote(
        ScenarioPlanValidationRequest request,
        out ResolvedDeferredThreatChoice? deferredChoice,
        out string diagnostic);
    bool TryResolveSelectedThreatForce(
        ScenarioPlanValidationRequest request,
        out ResolvedThreatForcePlan? force,
        out string diagnostic);
    bool TryResolveSelectedThreatSpawnMarkers(
        ScenarioPlanValidationRequest request,
        MapId mapId,
        out ResolvedThreatSpawnMarkerSet? markerSet,
        out string diagnostic);
    bool TryResolveThreatSpawnMarkers(
        ResolvedThreatForcePlan force,
        MapId mapId,
        out ResolvedThreatSpawnMarkerSet? markerSet,
        out string diagnostic);
    bool TryResolveThirdPartySpawnMarkers(
        ScenarioPlanValidationRequest request,
        string thirdPartyId,
        MapId mapId,
        out ResolvedThirdPartySpawnMarkerSet? markerSet,
        out string diagnostic);
    bool TryResolveClfSpawnMarkers(
        ScenarioPlanValidationRequest request,
        MapId mapId,
        out ResolvedClfSpawnMarkerSet? markerSet,
        out string diagnostic);
    bool TryResolveRoundGroupPrototype(
        string roundGroupId,
        int playerCount,
        out PlannedForce? force,
        out string diagnostic);
    bool TryResolveVotingChoicesPrototype(
        string votingChoicesId,
        string presetId,
        string planetId,
        string mapId,
        int playerCount,
        out ScenarioPlan? plan,
        out string diagnostic);
    ScenarioPlanValidationReport ValidateVotingChoicesPrototypeCoverage(
        string votingChoicesId,
        string presetId,
        string planetId,
        string mapId,
        int playerCount);
    bool TryResolveVotingBackup(
        string presetId,
        string planetId,
        string mapId,
        int playerCount,
        out ScenarioPlan? plan,
        out string diagnostic);
    ScenarioPlanValidationReport ValidateMarkerCoverage(ScenarioPlanValidationRequest request);
    ScenarioPlanMarkerMigrationReport BuildMarkerMigrationReport(ScenarioPlanValidationRequest request);
}

public readonly record struct ScenarioPlanValidationRequest(
    string PresetId,
    int PlayerCount,
    string? GovforPlatoonId = null,
    string? OpforPlatoonId = null,
    string? PlanetId = null,
    string? MapId = null,
    string? SelectedThreatId = null,
    string? GovforShipId = null,
    string? OpforShipId = null);

public sealed record ScenarioPlanShadowSnapshot(
    string Reason,
    ScenarioPlanValidationRequest Request,
    ScenarioPlanValidationReport Report);

public sealed record ScenarioPlan(
    string PresetId,
    string PlanetId,
    string MapId,
    int PlayerCount,
    IReadOnlyList<PlannedForce> Forces,
    IReadOnlyList<DeferredForceChoice> DeferredForceChoices,
    IReadOnlyList<ResolvedSpawnMarker> SpawnMarkers,
    IReadOnlyList<ScenarioPlanDiagnostic> Diagnostics,
    string? SelectedThreatId = null)
{
    public IReadOnlyList<string> SourceVotingChoicesIds { get; init; } = Array.Empty<string>();
}

public sealed record PlannedForce(
    string ForceId,
    ScenarioForceKind ForceKind,
    string SourcePrototypeId,
    ResolvedSpawnPlan SpawnPlan,
    IReadOnlyList<string> WinConditionRuleIds,
    ScenarioForceTiming Timing);

public sealed record ScenarioForceTiming(
    int? DelayMinSeconds = null,
    int? DelayMaxSeconds = null)
{
    public static readonly ScenarioForceTiming Immediate = new();
    public bool HasDelay => DelayMinSeconds != null || DelayMaxSeconds != null;
}

public sealed record DeferredForceChoice(
    string ChoiceId,
    IReadOnlyList<PlannedForce> Candidates,
    ScenarioReservationPolicy ReservationPolicy);

public sealed record ResolvedThreatForcePlan(
    string ForceId,
    string ThreatId,
    ResolvedSpawnPlan SpawnPlan,
    int LeaderBodies,
    int MemberBodies,
    IReadOnlyList<string> WinConditionRuleIds,
    ScenarioForceTiming Timing)
{
    public int TotalBodies => LeaderBodies + MemberBodies;
}

public sealed record ResolvedDeferredThreatChoice(
    string ChoiceId,
    IReadOnlyList<ResolvedThreatForcePlan> Candidates,
    ScenarioReservationPolicy ReservationPolicy);

public sealed record ResolvedThreatSpawnMarkerSet(
    ResolvedThreatForcePlan Force,
    IReadOnlyDictionary<string, IReadOnlyList<EntityUid>> MarkersByBucket)
{
    public bool TryGetMarkers(string bucket, out IReadOnlyList<EntityUid> markers)
    {
        return MarkersByBucket.TryGetValue(bucket, out markers!);
    }
}

public sealed record ResolvedThirdPartyForcePlan(
    string ForceId,
    string ThirdPartyId,
    ResolvedSpawnPlan SpawnPlan,
    int LeaderBodies,
    int MemberBodies,
    int EntityBodies)
{
    public int TotalBodies => LeaderBodies + MemberBodies + EntityBodies;
}

public sealed record ResolvedThirdPartySpawnMarkerSet(
    ResolvedThirdPartyForcePlan Force,
    IReadOnlyDictionary<string, IReadOnlyList<EntityUid>> MarkersByBucket)
{
    public bool TryGetMarkers(string bucket, out IReadOnlyList<EntityUid> markers)
    {
        return MarkersByBucket.TryGetValue(bucket, out markers!);
    }
}

public sealed record ResolvedClfForcePlan(
    string ForceId,
    ResolvedSpawnPlan SpawnPlan,
    int CommandBodies,
    int GuerillaBodies)
{
    public int TotalBodies => CommandBodies + GuerillaBodies;
}

public sealed record ResolvedClfSpawnMarkerSet(
    ResolvedClfForcePlan Force,
    IReadOnlyDictionary<string, IReadOnlyList<EntityUid>> MarkersByBucket)
{
    public bool TryGetMarkers(string bucket, out IReadOnlyList<EntityUid> markers)
    {
        return MarkersByBucket.TryGetValue(bucket, out markers!);
    }
}

public sealed record ScenarioReservationPolicy(
    string PolicyId,
    int ReservedLeaderBodies,
    int ReservedMemberBodies)
{
    public int ReservedBodies => ReservedLeaderBodies + ReservedMemberBodies;
}

public sealed record ResolvedSpawnPlan(
    IReadOnlyList<SpawnBodyBucket> BodyBuckets,
    IReadOnlyList<SpawnMarkerRequirement> MarkerRequirements,
    bool AllowsUnderfill)
{
    public static readonly ResolvedSpawnPlan Empty = new(
        Array.Empty<SpawnBodyBucket>(),
        Array.Empty<SpawnMarkerRequirement>(),
        false);
}

public sealed record SpawnBodyBucket(
    string Bucket,
    int Count,
    IReadOnlyDictionary<string, int> Bodies)
{
    public SpawnBodyBucket(string bucket, int count)
        : this(bucket, count, new Dictionary<string, int>())
    {
    }
}

public sealed record SpawnMarkerRequirement(
    string Bucket,
    int RequiredBodyCount,
    int RequiredMarkerCount,
    IReadOnlyList<string> RequiredTags,
    bool WarningOnly = false);

public sealed record ResolvedSpawnMarker(
    string SourcePrototypeId,
    string MapId,
    ScenarioMarkerKind MarkerKind,
    IReadOnlyList<string> Tags,
    int Count,
    ResPath Source);

public sealed record ScenarioPlanDiagnostic(
    ScenarioDiagnosticSeverity Severity,
    string PresetId,
    string PlanetId,
    string MapId,
    string ForceId,
    string SourcePrototypeId,
    string Bucket,
    int RequiredBodyCount,
    int RequiredMarkerCount,
    int AvailableMarkers,
    IReadOnlyList<string> MissingTags,
    string Message)
{
    public override string ToString()
    {
        var missing = MissingTags.Count == 0
            ? "none"
            : string.Join(", ", MissingTags);

        return
            $"{Severity}: Gamemode {PresetId}, planet {PlanetId}, map {MapId}, force {ForceId} ({SourcePrototypeId}), " +
            $"bucket {Bucket}, required bodies {RequiredBodyCount}, required markers {RequiredMarkerCount}, " +
            $"available markers {AvailableMarkers}, missing capabilities/tags {missing}. {Message}";
    }
}

public sealed class ScenarioPlanValidationReport
{
    public ScenarioPlanValidationReport(
        IReadOnlyList<ScenarioPlan> plans,
        IReadOnlyList<ScenarioPlanDiagnostic> diagnostics)
    {
        Plans = plans;
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<ScenarioPlan> Plans { get; }
    public IReadOnlyList<ScenarioPlanDiagnostic> Diagnostics { get; }

    public bool IsValid => Diagnostics.All(diagnostic => diagnostic.Severity != ScenarioDiagnosticSeverity.Error);

    public override string ToString()
    {
        if (Diagnostics.Count == 0)
        {
            var planNames = Plans
                .Select(plan => $"{plan.PresetId}/{plan.PlanetId} ({plan.MapId})")
                .Order(StringComparer.OrdinalIgnoreCase);

            return $"Scenario Plan marker coverage passed for: {string.Join(", ", planNames)}";
        }

        return string.Join(Environment.NewLine, Diagnostics.Select(diagnostic => diagnostic.ToString()));
    }
}

public sealed class ScenarioPlanMarkerMigrationReport
{
    public ScenarioPlanMarkerMigrationReport(IReadOnlyList<ScenarioPlanMarkerMigrationHint> hints)
    {
        Hints = hints;
    }

    public IReadOnlyList<ScenarioPlanMarkerMigrationHint> Hints { get; }

    public IReadOnlyList<ScenarioPlanMarkerMigrationHint> UnsatisfiedHints =>
        Hints
            .Where(hint => !hint.IsSatisfied)
            .ToList();

    public override string ToString()
    {
        if (Hints.Count == 0)
            return "No Scenario Plan Spawn Marker requirements were generated.";

        return string.Join(Environment.NewLine, Hints.Select(hint => hint.ToString()));
    }
}

public sealed record ScenarioPlanMarkerMigrationHint(
    string PresetId,
    string PlanetId,
    string MapId,
    string ForceId,
    string SourcePrototypeId,
    string Bucket,
    int RequiredBodyCount,
    int RequiredMarkerCount,
    int AvailableMarkers,
    IReadOnlyList<string> RequiredTags,
    IReadOnlyList<ScenarioPlanMarkerSource> MatchingMarkerSources)
{
    public bool IsSatisfied => AvailableMarkers >= RequiredMarkerCount;

    public override string ToString()
    {
        var status = IsSatisfied ? "OK" : "NEEDS MARKER";
        var tags = RequiredTags.Count == 0
            ? "none"
            : string.Join(", ", RequiredTags);
        var sources = MatchingMarkerSources.Count == 0
            ? "none"
            : string.Join("; ", MatchingMarkerSources.Select(source => source.ToString()));

        return
            $"{status}: {PresetId}/{PlanetId} ({MapId}) force {ForceId} ({SourcePrototypeId}) bucket {Bucket} " +
            $"needs {RequiredMarkerCount} marker(s) for {RequiredBodyCount} bodies with tags [{tags}], " +
            $"found {AvailableMarkers}. Matching marker sources: {sources}";
    }
}

public sealed record ScenarioPlanMarkerSource(
    string SourcePrototypeId,
    ScenarioMarkerKind MarkerKind,
    IReadOnlyList<string> Tags,
    int Count,
    ResPath Source)
{
    public override string ToString()
    {
        return $"{SourcePrototypeId} x{Count} at {Source} ({MarkerKind}, tags: {string.Join(", ", Tags)})";
    }
}

public enum ScenarioForceKind
{
    Hostile,
    ThirdParty,
    Clf,
    Platoon,
}

public enum ScenarioMarkerKind
{
    ThreatMarker,
    ThirdPartyMarker,
    ClfSafehouse,
    ClfCivilianSpawn,
}

public enum ScenarioDiagnosticSeverity
{
    Warning,
    Error,
}
