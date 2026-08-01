using System.IO;
using System.Linq;
using Content.Server._CMU14.Threats;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Presets;
using Content.Server.Maps;
using Content.Server.Spawners.Components;
using Content.Shared._CMU14.Threats;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Spawners;
using Content.Shared.AU14;
using Content.Shared.AU14.Scenario;
using Content.Shared.AU14.util;
using Robust.Shared.ContentPack;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using ParachuteMarkerComponent = Content.Shared._CMU14.Threats.ParachuteMarkerComponent;

namespace Content.Server.AU14.Scenario;

public sealed partial class ScenarioPlanSystem : EntitySystem, IScenarioPlanGenerator
{
    private const string AddClfRuleId = "AddClf";
    private const string ClfForceId = "CLFInsurgents";
    private const string ColonyCivilianJobId = "AU14JobCivilianColonist";
    private const string DistressSignalPresetId = "DistressSignal";
    private const string ColonyFallPresetId = "ColonyFall";
    private const string InsurgencyPresetId = "Insurgency";
    private const int ScenarioPlanAnnouncementMaxDiagnosticLength = 500;
    private const string SmallestCandidateReservationPolicyId = "SmallestCandidateBodyCountAllowsUnderfill";

    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IResourceManager _resources = default!;

    private readonly Dictionary<string, IReadOnlyList<ResolvedSpawnMarker>> _mapMarkerCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<ResolvedSpawnMarker>> _mapPathMarkerCache = new(StringComparer.OrdinalIgnoreCase);

    public ScenarioPlanShadowSnapshot? LastShadowPlan { get; private set; }

    public bool HasMappedHostileRoundGroup(string presetId, string threatId)
    {
        return TryGetHostileRoundGroupId(presetId, threatId, out _);
    }

    public bool HasMappedThirdPartyRoundGroup(string presetId, string thirdPartyId)
    {
        return TryGetThirdPartyRoundGroupId(presetId, thirdPartyId, out _);
    }

    public IReadOnlyList<ScenarioPlan> GeneratePlans(ScenarioPlanValidationRequest request)
    {
        if (!_prototypes.TryIndex<GamePresetPrototype>(request.PresetId, out var preset) ||
            preset.SupportedPlanets is not { Count: > 0 })
        {
            return Array.Empty<ScenarioPlan>();
        }

        var plans = new List<ScenarioPlan>(preset.SupportedPlanets.Count);
        foreach (var planetId in preset.SupportedPlanets)
        {
            if (request.PlanetId != null &&
                !planetId.Equals(request.PlanetId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryGetPlanet(planetId, out var planet))
                continue;

            if (request.MapId != null &&
                !planet.MapId.Equals(request.MapId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var plan = BuildPlan(preset, planetId, planet, request);
            plans.Add(ApplyVotingChoicesPrototypeSlices(plan, request));
        }

        return plans;
    }

    public ScenarioPlanShadowSnapshot GenerateShadowPlan(ScenarioPlanValidationRequest request, string reason)
    {
        var report = ValidateMarkerCoverageWithBackup(request, out var usedBackup, out var backupDiagnostic);

        var snapshot = new ScenarioPlanShadowSnapshot(reason, request, report);
        LastShadowPlan = snapshot;

        var sawmill = Logger.GetSawmill("au14.scenario");
        if (usedBackup)
        {
            sawmill.Warning(
                $"[ScenarioPlanSystem] Shadow Scenario Plan validation failed for {request.PresetId} ({reason}); using validated Voting Backup for planet {request.PlanetId} map {request.MapId}. Diagnostic: {backupDiagnostic}");
            AnnounceScenarioPlanFailure(
                "au14-scenario-plan-failed-backup-announcement",
                request,
                reason,
                backupDiagnostic);
        }
        else if (report.IsValid)
        {
            sawmill.Info(
                $"[ScenarioPlanSystem] Shadow Scenario Plan generated for {request.PresetId} ({reason}) with {report.Plans.Count} plan(s).");
        }
        else
        {
            sawmill.Warning(
                $"[ScenarioPlanSystem] Shadow Scenario Plan generated diagnostics for {request.PresetId} ({reason}): {report}. Backup diagnostic: {backupDiagnostic}");
            AnnounceScenarioPlanFailure(
                "au14-scenario-plan-failed-no-backup-announcement",
                request,
                reason,
                report.ToString());
        }

        return snapshot;
    }

    public ScenarioPlanValidationReport ValidateMarkerCoverageWithBackup(
        ScenarioPlanValidationRequest request,
        out bool usedBackup,
        out string backupDiagnostic)
    {
        var report = ValidateMarkerCoverage(request);
        usedBackup = false;
        backupDiagnostic = string.Empty;

        if (report.IsValid ||
            request.PlanetId == null ||
            request.MapId == null)
        {
            return report;
        }

        var markerDiagnostic = report.ToString();
        var backupResolveDiagnostic = string.Empty;
        if (TryResolveVotingBackup(
                request.PresetId,
                request.PlanetId,
                request.MapId,
                request.PlayerCount,
                out var backupPlan,
                out backupResolveDiagnostic) &&
            backupPlan != null)
        {
            usedBackup = true;
            backupDiagnostic = markerDiagnostic;
            return new ScenarioPlanValidationReport(new[] { backupPlan }, backupPlan.Diagnostics);
        }

        backupDiagnostic = backupResolveDiagnostic;
        return report;
    }

    private void AnnounceScenarioPlanFailure(
        string locId,
        ScenarioPlanValidationRequest request,
        string reason,
        string diagnostic)
    {
        _chat.DispatchServerAnnouncement(
            Loc.GetString(locId,
                ("preset", request.PresetId),
                ("reason", reason),
                ("planet", request.PlanetId ?? "<any>"),
                ("map", request.MapId ?? "<any>"),
                ("threat", request.SelectedThreatId ?? "<none>"),
                ("diagnostic", PrepareAnnouncementDiagnostic(diagnostic))),
            Color.Red);
    }

    private static string PrepareAnnouncementDiagnostic(string diagnostic)
    {
        if (string.IsNullOrWhiteSpace(diagnostic))
            return "No diagnostic details were reported.";

        diagnostic = diagnostic
            .Replace('\r', ' ')
            .Replace('\n', ' ');

        if (diagnostic.Length <= ScenarioPlanAnnouncementMaxDiagnosticLength)
            return diagnostic;

        return $"{diagnostic[..ScenarioPlanAnnouncementMaxDiagnosticLength]}...";
    }

    private IReadOnlyList<ScenarioPlan> GeneratePlansForRuntimeResolution(
        ScenarioPlanValidationRequest request,
        string reason)
    {
        if (request.PlanetId == null ||
            request.MapId == null)
        {
            return GeneratePlans(request);
        }

        var report = ValidateMarkerCoverageWithBackup(request, out var usedBackup, out _);
        if (usedBackup)
        {
            Logger.GetSawmill("au14.scenario").Warning(
                $"[ScenarioPlanSystem] Runtime Scenario Plan validation failed for {request.PresetId} ({reason}); using validated Voting Backup for planet {request.PlanetId} map {request.MapId}.");
        }

        return report.IsValid
            ? report.Plans
            : GeneratePlans(request);
    }

    public bool TryResolveDeferredThreatVote(
        ScenarioPlanValidationRequest request,
        out ResolvedDeferredThreatChoice? deferredChoice,
        out string diagnostic)
    {
        deferredChoice = null;

        var plans = GeneratePlansForRuntimeResolution(request, "DeferredThreatVote");
        if (plans.Count != 1)
        {
            diagnostic =
                $"Expected exactly one Scenario Plan for deferred threat vote preset '{request.PresetId}' " +
                $"planet '{request.PlanetId ?? "<any>"}' map '{request.MapId ?? "<any>"}', but generated {plans.Count}.";
            return false;
        }

        var choice = plans[0].DeferredForceChoices
            .FirstOrDefault(choice => choice.ChoiceId.StartsWith("DeferredThreat:", StringComparison.Ordinal));
        if (choice == null)
        {
            diagnostic = $"Scenario Plan for preset '{request.PresetId}' has no deferred threat choice.";
            return false;
        }

        var candidates = new List<ResolvedThreatForcePlan>(choice.Candidates.Count);
        foreach (var force in choice.Candidates)
        {
            if (!TryResolveThreatForcePlan(force, out var resolved, out diagnostic))
            {
                deferredChoice = null;
                return false;
            }

            candidates.Add(resolved);
        }

        if (candidates.Count == 0)
        {
            diagnostic = $"Deferred threat choice '{choice.ChoiceId}' has no resolved threat force plans.";
            return false;
        }

        deferredChoice = new ResolvedDeferredThreatChoice(
            choice.ChoiceId,
            candidates,
            choice.ReservationPolicy);
        diagnostic = string.Empty;
        return true;
    }

    public bool TryResolveSelectedThreatForce(
        ScenarioPlanValidationRequest request,
        out ResolvedThreatForcePlan? force,
        out string diagnostic)
    {
        force = null;

        if (string.IsNullOrWhiteSpace(request.SelectedThreatId))
        {
            diagnostic = "No selected threat id was provided.";
            return false;
        }

        foreach (var plan in GeneratePlansForRuntimeResolution(request, "SelectedThreatForce"))
        {
            foreach (var plannedForce in plan.Forces)
            {
                if (plannedForce.ForceKind != ScenarioForceKind.Hostile ||
                    !plannedForce.SourcePrototypeId.Equals(request.SelectedThreatId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (TryResolveThreatForcePlan(plannedForce, out var resolved, out diagnostic))
                {
                    force = resolved;
                    return true;
                }
            }
        }

        if (!_prototypes.TryIndex<ThreatPrototype>(request.SelectedThreatId, out var threat))
        {
            diagnostic = $"Selected threat prototype '{request.SelectedThreatId}' could not be resolved.";
            return false;
        }

        if (!TryBuildThreatForce(threat, request.PresetId, request.PlayerCount, out var directForce))
        {
            diagnostic = $"Selected threat '{threat.ID}' has no resolvable round-start Spawn Plan.";
            return false;
        }

        if (!TryResolveThreatForcePlan(directForce, out var directResolved, out diagnostic))
            return false;

        force = directResolved;
        return true;
    }

    public bool TryResolveSelectedThreatSpawnMarkers(
        ScenarioPlanValidationRequest request,
        MapId mapId,
        out ResolvedThreatSpawnMarkerSet? markerSet,
        out string diagnostic)
    {
        markerSet = null;

        if (!TryResolveSelectedThreatForce(request, out var force, out diagnostic) ||
            force == null)
        {
            return false;
        }

        return TryResolveThreatSpawnMarkers(force, mapId, out markerSet, out diagnostic);
    }

    public bool TryResolveThreatSpawnMarkers(
        ResolvedThreatForcePlan force,
        MapId mapId,
        out ResolvedThreatSpawnMarkerSet? markerSet,
        out string diagnostic)
    {
        markerSet = null;

        if (!TryResolveRuntimeSpawnMarkerBuckets(
                force.ForceId,
                force.SpawnPlan,
                mapId,
                out var markersByBucket,
                out diagnostic))
        {
            return false;
        }

        markerSet = new ResolvedThreatSpawnMarkerSet(force, markersByBucket);
        diagnostic = string.Empty;
        return true;
    }

    public bool TryResolveThirdPartySpawnMarkers(
        ScenarioPlanValidationRequest request,
        string thirdPartyId,
        MapId mapId,
        out ResolvedThirdPartySpawnMarkerSet? markerSet,
        out string diagnostic)
    {
        markerSet = null;

        if (!TryResolveThirdPartyForce(request, thirdPartyId, out var force, out diagnostic) ||
            force == null)
        {
            return false;
        }

        if (!TryResolveRuntimeSpawnMarkerBuckets(
                force.ForceId,
                force.SpawnPlan,
                mapId,
                out var markersByBucket,
                out diagnostic))
        {
            return false;
        }

        markerSet = new ResolvedThirdPartySpawnMarkerSet(force, markersByBucket);
        diagnostic = string.Empty;
        return true;
    }

    public bool TryResolveClfSpawnMarkers(
        ScenarioPlanValidationRequest request,
        MapId mapId,
        out ResolvedClfSpawnMarkerSet? markerSet,
        out string diagnostic)
    {
        markerSet = null;

        if (!TryResolveClfForce(request, out var force, out diagnostic) ||
            force == null)
        {
            return false;
        }

        if (!TryResolveRuntimeSpawnMarkerBuckets(
                force.ForceId,
                force.SpawnPlan,
                mapId,
                out var markersByBucket,
                out diagnostic))
        {
            return false;
        }

        markerSet = new ResolvedClfSpawnMarkerSet(force, markersByBucket);
        diagnostic = string.Empty;
        return true;
    }

    public bool TryResolveRoundGroupPrototype(
        string roundGroupId,
        int playerCount,
        out PlannedForce? force,
        out string diagnostic)
    {
        force = null;

        if (!_prototypes.TryIndex<RoundGroupPrototype>(roundGroupId, out var roundGroup))
        {
            diagnostic = $"Round Group prototype '{roundGroupId}' could not be resolved.";
            return false;
        }

        if (!TryGetRoundGroupSpawn(roundGroup, out var spawn, out diagnostic))
            return false;

        var bodyBuckets = spawn.BodyBuckets
            .Select(bucket => new SpawnBodyBucket(
                bucket.Bucket,
                bucket.CalculateBodyCount(playerCount),
                ResolveSpawnBodyEntries(bucket, playerCount)))
            .ToList();
        var bucketCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var bucket in bodyBuckets)
        {
            bucketCounts[bucket.Bucket] = bucketCounts.TryGetValue(bucket.Bucket, out var count)
                ? count + bucket.Count
                : bucket.Count;
        }

        var requirements = spawn.MarkerRequirements
            .Select(requirement => new SpawnMarkerRequirement(
                requirement.Bucket,
                bucketCounts.TryGetValue(requirement.Bucket, out var requiredBodies)
                    ? requiredBodies
                    : requirement.RequiredBodyCount,
                requirement.RequiredMarkerCount,
                requirement.RequiredTags.ToArray(),
                requirement.WarningOnly))
            .ToList();
        var resolvedSpawnPlan = new ResolvedSpawnPlan(
            bodyBuckets,
            requirements,
            spawn.AllowsUnderfill);
        var forceKind = ToScenarioForceKind(roundGroup.Kind);

        force = new PlannedForce(
            BuildPrototypeForceId(roundGroup, forceKind),
            forceKind,
            roundGroup.SourcePrototypeId,
            resolvedSpawnPlan,
            roundGroup.WinConditionRuleIds.ToArray(),
            ToScenarioForceTiming(roundGroup.Timing));
        diagnostic = string.Empty;
        return true;
    }

    private bool TryGetRoundGroupSpawn(
        RoundGroupPrototype roundGroup,
        out ScenarioSpawnDefinition spawn,
        out string diagnostic)
    {
        spawn = roundGroup.Spawn;
        diagnostic = string.Empty;
        return true;
    }

    private static IReadOnlyDictionary<string, int> ResolveSpawnBodyEntries(
        ScenarioSpawnBodyBucketDefinition bucket,
        int playerCount)
    {
        if (bucket.Bodies.Count == 0)
            return new Dictionary<string, int>();

        var bodies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (bodyId, staticCount) in bucket.Bodies)
        {
            bodies[bodyId] = bucket.Scaling.TryGetValue(bodyId, out var scaling)
                ? JobScaling.CalculateScaledSlots(playerCount, staticCount, scaling)
                : Math.Max(0, staticCount);
        }

        return bodies;
    }

    public bool TryResolveVotingChoicesPrototype(
        string votingChoicesId,
        string presetId,
        string planetId,
        string mapId,
        int playerCount,
        out ScenarioPlan? plan,
        out string diagnostic)
    {
        plan = null;

        if (!_prototypes.TryIndex<VotingChoicesPrototype>(votingChoicesId, out var votingChoices))
        {
            diagnostic = $"Voting Choices prototype '{votingChoicesId}' could not be resolved.";
            return false;
        }

        if (votingChoices.Presets.Count > 0 &&
            !ContainsIgnoreCase(votingChoices.Presets, presetId))
        {
            diagnostic = $"Voting Choices prototype '{votingChoices.ID}' does not support preset '{presetId}'.";
            return false;
        }

        if (votingChoices.SupportedPlanets.Count > 0 &&
            !ContainsIgnoreCase(votingChoices.SupportedPlanets, planetId))
        {
            diagnostic = $"Voting Choices prototype '{votingChoices.ID}' does not support planet '{planetId}'.";
            return false;
        }

        if (!TryGetVotingChoicesSlice(
                votingChoices,
                planetId,
                false,
                out var groups,
                out var deferredForceChoices,
                out diagnostic))
        {
            return false;
        }

        return TryBuildVotingChoicesPrototypePlan(
            votingChoices.ID,
            presetId,
            planetId,
            mapId,
            playerCount,
            groups,
            deferredForceChoices,
            out plan,
            out diagnostic);
    }

    public ScenarioPlanValidationReport ValidateVotingChoicesPrototypeCoverage(
        string votingChoicesId,
        string presetId,
        string planetId,
        string mapId,
        int playerCount)
    {
        var diagnostics = new List<ScenarioPlanDiagnostic>();
        if (!TryResolveVotingChoicesPrototype(
                votingChoicesId,
                presetId,
                planetId,
                mapId,
                playerCount,
                out var plan,
                out var diagnostic) ||
            plan == null)
        {
            diagnostics.Add(BuildDiagnostic(
                ScenarioDiagnosticSeverity.Error,
                presetId,
                planetId,
                mapId,
                "VotingChoices",
                votingChoicesId,
                "VotingChoices",
                0,
                1,
                0,
                Array.Empty<string>(),
                diagnostic));

            return new ScenarioPlanValidationReport(Array.Empty<ScenarioPlan>(), diagnostics);
        }

        return ValidateResolvedVotingChoicesPlan(plan, presetId, planetId, mapId);
    }

    public bool TryResolveVotingBackup(
        string presetId,
        string planetId,
        string mapId,
        int playerCount,
        out ScenarioPlan? plan,
        out string diagnostic)
    {
        plan = null;
        var diagnostics = new List<string>();
        if (TryResolveInlineVotingBackup(
                presetId,
                planetId,
                mapId,
                playerCount,
                out plan,
                out var inlineDiagnostic))
        {
            diagnostic = string.Empty;
            return true;
        }

        diagnostics.Add(inlineDiagnostic);

        foreach (var backup in _prototypes.EnumeratePrototypes<VotingBackupPrototype>()
                     .Where(backup => backup.Preset.Equals(presetId, StringComparison.OrdinalIgnoreCase))
                     .Where(backup => backup.SupportedPlanets.Count == 0 ||
                                        ContainsIgnoreCase(backup.SupportedPlanets, planetId))
                     .OrderBy(backup => backup.ID, StringComparer.OrdinalIgnoreCase))
        {
            var report = ValidateVotingChoicesPrototypeCoverage(
                backup.VotingChoices.Id,
                presetId,
                planetId,
                mapId,
                playerCount);
            if (report.IsValid && report.Plans.Count == 1)
            {
                plan = report.Plans[0];
                diagnostic = string.Empty;
                return true;
            }

            diagnostics.Add($"{backup.ID}: {report}");
        }

        diagnostic = diagnostics.Count == 0
            ? $"No Voting Backup prototype matched preset '{presetId}' and planet '{planetId}'."
            : string.Join("; ", diagnostics);
        return false;
    }

    private ScenarioPlanValidationReport ValidateResolvedVotingChoicesPlan(
        ScenarioPlan plan,
        string presetId,
        string planetId,
        string mapId)
    {
        var diagnostics = new List<ScenarioPlanDiagnostic>();
        diagnostics.AddRange(plan.Diagnostics);
        var markers = GetMapMarkers(mapId, diagnostics, presetId, planetId);
        var planWithMarkers = plan with
        {
            SpawnMarkers = markers,
            Diagnostics = diagnostics,
        };
        ValidatePlanMarkers(planWithMarkers, diagnostics);
        planWithMarkers = planWithMarkers with { Diagnostics = diagnostics };

        return new ScenarioPlanValidationReport(new[] { planWithMarkers }, diagnostics);
    }

    private bool TryResolveInlineVotingBackup(
        string presetId,
        string planetId,
        string mapId,
        int playerCount,
        out ScenarioPlan? plan,
        out string diagnostic)
    {
        plan = null;
        var diagnostics = new List<string>();
        foreach (var votingChoices in _prototypes.EnumeratePrototypes<VotingChoicesPrototype>()
                     .Where(votingChoices => votingChoices.Presets.Count == 0 ||
                                             ContainsIgnoreCase(votingChoices.Presets, presetId))
                     .Where(votingChoices => votingChoices.SupportedPlanets.Count == 0 ||
                                             ContainsIgnoreCase(votingChoices.SupportedPlanets, planetId))
                     .OrderBy(votingChoices => votingChoices.ID, StringComparer.OrdinalIgnoreCase))
        {
            if (!TryGetVotingChoicesSlice(
                    votingChoices,
                    planetId,
                    true,
                    out var groups,
                    out var deferredForceChoices,
                    out var sliceDiagnostic))
            {
                if (!string.IsNullOrWhiteSpace(sliceDiagnostic))
                    diagnostics.Add($"{votingChoices.ID}: {sliceDiagnostic}");

                continue;
            }

            if (!TryBuildVotingChoicesPrototypePlan(
                    votingChoices.ID,
                    presetId,
                    planetId,
                    mapId,
                    playerCount,
                    groups,
                    deferredForceChoices,
                    out var candidatePlan,
                    out var buildDiagnostic) ||
                candidatePlan == null)
            {
                diagnostics.Add($"{votingChoices.ID}: {buildDiagnostic}");
                continue;
            }

            var report = ValidateResolvedVotingChoicesPlan(candidatePlan, presetId, planetId, mapId);
            if (report.IsValid && report.Plans.Count == 1)
            {
                plan = report.Plans[0];
                diagnostic = string.Empty;
                return true;
            }

            diagnostics.Add($"{votingChoices.ID}: {report}");
        }

        diagnostic = diagnostics.Count == 0
            ? $"No inline Voting Choices backup matched preset '{presetId}' and planet '{planetId}'."
            : string.Join("; ", diagnostics);
        return false;
    }

    private static bool TryGetVotingChoicesSlice(
        VotingChoicesPrototype votingChoices,
        string planetId,
        bool backup,
        out IReadOnlyList<ProtoId<RoundGroupPrototype>> groups,
        out IReadOnlyList<ScenarioDeferredForceChoiceDefinition> deferredForceChoices,
        out string diagnostic)
    {
        groups = votingChoices.Groups;
        deferredForceChoices = votingChoices.DeferredForceChoices;
        diagnostic = string.Empty;

        if (votingChoices.PlanetChoices.Count == 0)
            return !backup;

        var matchingChoices = votingChoices.PlanetChoices
            .Where(choice => choice.SupportsPlanet(planetId))
            .ToList();
        if (matchingChoices.Count == 0)
        {
            diagnostic = $"Voting Choices prototype '{votingChoices.ID}' has no planetChoices entry for planet '{planetId}'.";
            groups = Array.Empty<ProtoId<RoundGroupPrototype>>();
            deferredForceChoices = Array.Empty<ScenarioDeferredForceChoiceDefinition>();
            return false;
        }

        if (matchingChoices.Count > 1)
        {
            diagnostic = $"Voting Choices prototype '{votingChoices.ID}' has {matchingChoices.Count} planetChoices entries for planet '{planetId}'.";
            groups = Array.Empty<ProtoId<RoundGroupPrototype>>();
            deferredForceChoices = Array.Empty<ScenarioDeferredForceChoiceDefinition>();
            return false;
        }

        var match = matchingChoices[0];
        groups = backup
            ? match.BackupGroups
            : match.Groups;
        deferredForceChoices = backup
            ? match.BackupDeferredForceChoices
            : match.DeferredForceChoices;
        if (backup && !match.HasBackupData)
        {
            diagnostic = $"Voting Choices prototype '{votingChoices.ID}' has no inline backup for planet '{planetId}'.";
            return false;
        }

        return true;
    }

    private bool TryBuildVotingChoicesPrototypePlan(
        string votingChoicesId,
        string presetId,
        string planetId,
        string mapId,
        int playerCount,
        IReadOnlyList<ProtoId<RoundGroupPrototype>> groups,
        IReadOnlyList<ScenarioDeferredForceChoiceDefinition> deferredForceChoiceDefinitions,
        out ScenarioPlan? plan,
        out string diagnostic)
    {
        plan = null;

        var forces = new List<PlannedForce>();
        foreach (var roundGroupId in groups)
        {
            if (!TryResolveRoundGroupPrototype(roundGroupId.Id, playerCount, out var force, out diagnostic) ||
                force == null)
            {
                return false;
            }

            AddPrototypeForceIfMissing(forces, force);
        }

        var deferredChoices = new List<DeferredForceChoice>();
        foreach (var choice in deferredForceChoiceDefinitions)
        {
            var candidates = new List<PlannedForce>(choice.Candidates.Count);
            foreach (var candidateId in choice.Candidates)
            {
                if (!TryResolveRoundGroupPrototype(candidateId.Id, playerCount, out var candidate, out diagnostic) ||
                    candidate == null)
                {
                    return false;
                }

                candidates.Add(candidate);
                AddPrototypeForceIfMissing(forces, candidate);
            }

            deferredChoices.Add(new DeferredForceChoice(
                BuildPrototypeChoiceId(choice.ChoiceId, presetId, planetId, mapId),
                candidates,
                BuildPrototypeReservationPolicy(choice.ReservationPolicy, candidates)));
        }

        plan = new ScenarioPlan(
            presetId,
            planetId,
            mapId,
            playerCount,
            forces,
            deferredChoices,
            Array.Empty<ResolvedSpawnMarker>(),
            Array.Empty<ScenarioPlanDiagnostic>())
        {
            SourceVotingChoicesIds = new[] { votingChoicesId },
        };
        diagnostic = string.Empty;
        return true;
    }

    private ScenarioPlan ApplyVotingChoicesPrototypeSlices(
        ScenarioPlan adapterPlan,
        ScenarioPlanValidationRequest request)
    {
        var matchingChoices = GetRuntimeVotingChoicesPrototypeSlices(
                adapterPlan.PresetId,
                adapterPlan.PlanetId)
            .ToList();
        if (matchingChoices.Count == 0)
            return adapterPlan;

        var forces = adapterPlan.Forces.ToList();
        var deferredChoices = adapterPlan.DeferredForceChoices.ToList();
        var sourceVotingChoicesIds = adapterPlan.SourceVotingChoicesIds.ToList();

        foreach (var votingChoices in matchingChoices)
        {
            var report = ValidateVotingChoicesPrototypeCoverage(
                votingChoices.ID,
                adapterPlan.PresetId,
                adapterPlan.PlanetId,
                adapterPlan.MapId,
                request.PlayerCount);
            if (!report.IsValid || report.Plans.Count != 1)
            {
                Logger.GetSawmill("au14.scenario").Warning(
                    $"[ScenarioPlanSystem] Voting Choices prototype '{votingChoices.ID}' did not validate as a runtime slice for {adapterPlan.PresetId}/{adapterPlan.PlanetId}; preserving adapter-generated plan. {report}");
                continue;
            }

            var prototypePlan = report.Plans[0];
            if (!CanApplyVotingChoicesPrototypeSlice(
                    prototypePlan,
                    deferredChoices,
                    out var compatibilityDiagnostic))
            {
                Logger.GetSawmill("au14.scenario").Debug(
                    $"[ScenarioPlanSystem] Voting Choices prototype '{votingChoices.ID}' is not a complete runtime slice for {adapterPlan.PresetId}/{adapterPlan.PlanetId}; preserving adapter-generated choices. {compatibilityDiagnostic}");
                continue;
            }

            foreach (var force in prototypePlan.Forces)
            {
                AddPrototypeForceIfMissing(forces, force);
            }

            foreach (var choice in prototypePlan.DeferredForceChoices)
            {
                ReplaceDeferredChoice(deferredChoices, choice);
                foreach (var candidate in choice.Candidates)
                {
                    AddPrototypeForceIfMissing(forces, candidate);
                }
            }

            if (!sourceVotingChoicesIds.Contains(votingChoices.ID, StringComparer.OrdinalIgnoreCase))
                sourceVotingChoicesIds.Add(votingChoices.ID);
        }

        return sourceVotingChoicesIds.Count == adapterPlan.SourceVotingChoicesIds.Count
            ? adapterPlan
            : adapterPlan with
            {
                Forces = forces,
                DeferredForceChoices = deferredChoices,
                SourceVotingChoicesIds = sourceVotingChoicesIds,
            };
    }

    private static bool CanApplyVotingChoicesPrototypeSlice(
        ScenarioPlan prototypePlan,
        List<DeferredForceChoice> adapterChoices,
        out string diagnostic)
    {
        foreach (var prototypeChoice in prototypePlan.DeferredForceChoices)
        {
            var adapterChoice = adapterChoices.FirstOrDefault(choice =>
                choice.ChoiceId.Equals(prototypeChoice.ChoiceId, StringComparison.OrdinalIgnoreCase));
            if (adapterChoice == null)
            {
                diagnostic = $"Choice '{prototypeChoice.ChoiceId}' has no adapter-generated counterpart.";
                return false;
            }

            var prototypeCandidates = prototypeChoice.Candidates
                .Select(candidate => candidate.SourcePrototypeId)
                .ToList();
            var adapterCandidates = adapterChoice.Candidates
                .Select(candidate => candidate.SourcePrototypeId)
                .ToList();
            if (!prototypeCandidates.SequenceEqual(adapterCandidates, StringComparer.OrdinalIgnoreCase))
            {
                diagnostic =
                    $"Choice '{prototypeChoice.ChoiceId}' candidates [{string.Join(", ", prototypeCandidates)}] " +
                    $"do not match adapter candidates [{string.Join(", ", adapterCandidates)}].";
                return false;
            }
        }

        diagnostic = string.Empty;
        return true;
    }

    private IReadOnlyList<VotingChoicesPrototype> GetRuntimeVotingChoicesPrototypeSlices(
        string presetId,
        string planetId)
    {
        var votingBackupChoicesIds = _prototypes.EnumeratePrototypes<VotingBackupPrototype>()
            .Select(backup => backup.VotingChoices.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return _prototypes.EnumeratePrototypes<VotingChoicesPrototype>()
            .Where(votingChoices =>
                !votingBackupChoicesIds.Contains(votingChoices.ID) ||
                votingChoices.DeferredForceChoices.Count == 0)
            .Where(votingChoices => votingChoices.Presets.Count == 0 ||
                                      ContainsIgnoreCase(votingChoices.Presets, presetId))
            .Where(votingChoices => votingChoices.SupportedPlanets.Count == 0 ||
                                      ContainsIgnoreCase(votingChoices.SupportedPlanets, planetId))
            .Where(votingChoices => votingChoices.Groups.Count > 0 ||
                                      votingChoices.DeferredForceChoices.Count > 0 ||
                                      HasPlanetRuntimeChoice(votingChoices, planetId) ||
                                      votingBackupChoicesIds.Contains(votingChoices.ID))
            .OrderBy(votingChoices => votingChoices.ID, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool HasPlanetRuntimeChoice(VotingChoicesPrototype votingChoices, string planetId)
    {
        return votingChoices.PlanetChoices.Any(choice =>
            choice.SupportsPlanet(planetId) &&
            choice.HasData);
    }

    private static void AddPrototypeForceIfMissing(List<PlannedForce> forces, PlannedForce force)
    {
        if (forces.Any(existing =>
                existing.ForceKind == force.ForceKind &&
                existing.ForceId.Equals(force.ForceId, StringComparison.OrdinalIgnoreCase) &&
                existing.SourcePrototypeId.Equals(force.SourcePrototypeId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        forces.Add(force);
    }

    private static void ReplaceDeferredChoice(
        List<DeferredForceChoice> deferredChoices,
        DeferredForceChoice replacement)
    {
        var index = deferredChoices.FindIndex(existing =>
            existing.ChoiceId.Equals(replacement.ChoiceId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            deferredChoices[index] = replacement;
            return;
        }

        deferredChoices.Add(replacement);
    }

    private static ScenarioReservationPolicy BuildPrototypeReservationPolicy(
        string policyId,
        IReadOnlyList<PlannedForce> candidates)
    {
        if (policyId.Equals(SmallestCandidateReservationPolicyId, StringComparison.OrdinalIgnoreCase))
            return BuildSmallestCandidateReservationPolicy(candidates);

        return new ScenarioReservationPolicy(
            string.IsNullOrWhiteSpace(policyId) ? "VotingChoicesPrototype" : policyId,
            0,
            0);
    }

    private static string BuildPrototypeChoiceId(
        string choiceId,
        string presetId,
        string planetId,
        string mapId)
    {
        return choiceId
            .Replace("{presetId}", presetId, StringComparison.OrdinalIgnoreCase)
            .Replace("{planetId}", planetId, StringComparison.OrdinalIgnoreCase)
            .Replace("{mapId}", mapId, StringComparison.OrdinalIgnoreCase);
    }

    private static ScenarioForceKind ToScenarioForceKind(RoundGroupKind kind)
    {
        return kind switch
        {
            RoundGroupKind.Hostile => ScenarioForceKind.Hostile,
            RoundGroupKind.ThirdParty => ScenarioForceKind.ThirdParty,
            RoundGroupKind.Clf => ScenarioForceKind.Clf,
            RoundGroupKind.Platoon => ScenarioForceKind.Platoon,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    private static string BuildPrototypeForceId(
        RoundGroupPrototype roundGroup,
        ScenarioForceKind forceKind)
    {
        if (!string.IsNullOrWhiteSpace(roundGroup.GroupId))
            return roundGroup.GroupId;

        var sourceId = string.IsNullOrWhiteSpace(roundGroup.SourcePrototypeId)
            ? roundGroup.ID
            : roundGroup.SourcePrototypeId;

        return forceKind switch
        {
            ScenarioForceKind.Hostile => $"Hostile:{sourceId}",
            ScenarioForceKind.ThirdParty => $"ThirdParty:Prototype:{sourceId}",
            ScenarioForceKind.Clf => ClfForceId,
            ScenarioForceKind.Platoon => $"Platoon:{sourceId}",
            _ => roundGroup.ID,
        };
    }

    private static ScenarioForceTiming ToScenarioForceTiming(ScenarioSpawnTimingDefinition timing)
    {
        return new ScenarioForceTiming(timing.DelayMinSeconds, timing.DelayMaxSeconds);
    }

    private static ScenarioForceTiming BuildLegacyThreatTiming(string presetId, ThreatPrototype threat)
    {
        if (!presetId.Equals(ColonyFallPresetId, StringComparison.OrdinalIgnoreCase))
            return ScenarioForceTiming.Immediate;

        return new ScenarioForceTiming(threat.SpawnDelayMin, threat.SpawnDelayMax);
    }

    private static ScenarioMarkerKind ToScenarioMarkerKind(SpawnMarkerKind kind)
    {
        return kind switch
        {
            SpawnMarkerKind.ThreatMarker => ScenarioMarkerKind.ThreatMarker,
            SpawnMarkerKind.ThirdPartyMarker => ScenarioMarkerKind.ThirdPartyMarker,
            SpawnMarkerKind.ClfSafehouse => ScenarioMarkerKind.ClfSafehouse,
            SpawnMarkerKind.ClfCivilianSpawn => ScenarioMarkerKind.ClfCivilianSpawn,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    public ScenarioPlanValidationReport ValidateMarkerCoverage(ScenarioPlanValidationRequest request)
    {
        var diagnostics = new List<ScenarioPlanDiagnostic>();

        if (!_prototypes.TryIndex<GamePresetPrototype>(request.PresetId, out var preset))
        {
            diagnostics.Add(BuildDiagnostic(
                ScenarioDiagnosticSeverity.Error,
                request.PresetId,
                string.Empty,
                string.Empty,
                "ScenarioPlan",
                request.PresetId,
                "Preset",
                0,
                1,
                0,
                Array.Empty<string>(),
                $"Unknown Voting Choices preset '{request.PresetId}'."));

            return new ScenarioPlanValidationReport(Array.Empty<ScenarioPlan>(), diagnostics);
        }

        var plans = GeneratePlans(request);
        if (plans.Count == 0)
        {
            var selectedContext = request.PlanetId != null || request.MapId != null
                ? $" matching planet '{request.PlanetId ?? "<any>"}' and map '{request.MapId ?? "<any>"}'"
                : string.Empty;

            diagnostics.Add(BuildDiagnostic(
                ScenarioDiagnosticSeverity.Error,
                preset.ID,
                string.Empty,
                string.Empty,
                "ScenarioPlan",
                preset.ID,
                "Planet",
                0,
                1,
                0,
                Array.Empty<string>(),
                $"Voting Choices preset '{preset.ID}' has no supported planets{selectedContext} to validate."));
        }

        foreach (var plan in plans)
        {
            diagnostics.AddRange(plan.Diagnostics);
            ValidatePlanMarkers(plan, diagnostics);
        }

        return new ScenarioPlanValidationReport(plans, diagnostics);
    }

    public ScenarioPlanMarkerMigrationReport BuildMarkerMigrationReport(ScenarioPlanValidationRequest request)
    {
        var hints = new List<ScenarioPlanMarkerMigrationHint>();
        foreach (var plan in GeneratePlans(request))
        {
            foreach (var force in plan.Forces)
            {
                foreach (var requirement in force.SpawnPlan.MarkerRequirements)
                {
                    var matchingSources = FindMatchingMarkerSources(plan.SpawnMarkers, requirement.RequiredTags);
                    hints.Add(new ScenarioPlanMarkerMigrationHint(
                        plan.PresetId,
                        plan.PlanetId,
                        plan.MapId,
                        force.ForceId,
                        force.SourcePrototypeId,
                        requirement.Bucket,
                        requirement.RequiredBodyCount,
                        requirement.RequiredMarkerCount,
                        matchingSources.Sum(source => source.Count),
                        requirement.RequiredTags,
                        matchingSources));
                }
            }
        }

        return new ScenarioPlanMarkerMigrationReport(hints);
    }

    private ScenarioPlan BuildPlan(
        GamePresetPrototype preset,
        string planetId,
        RMCPlanetMapPrototypeComponent planet,
        ScenarioPlanValidationRequest request)
    {
        var diagnostics = new List<ScenarioPlanDiagnostic>();
        var markers = GetMapMarkers(planet.MapId, diagnostics, preset.ID, planetId).ToList();
        var includedMarkerSources = markers
            .Select(marker => marker.Source.ToString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var forces = new List<PlannedForce>();
        var deferredChoices = new List<DeferredForceChoice>();

        AddPlatoonForces(preset, planet, request.PlayerCount, forces, deferredChoices);

        if (IsPostRoundstartThreatVotePreset(preset.ID))
        {
            AddDeferredThreatChoice(
                preset.ID,
                planetId,
                planet,
                request,
                forces,
                deferredChoices,
                markers,
                includedMarkerSources);
        }

        if (preset.ID.Equals(InsurgencyPresetId, StringComparison.OrdinalIgnoreCase))
            AddClfForce(preset.ID, planetId, planet, request.PlayerCount, forces, diagnostics);

        return new ScenarioPlan(
            preset.ID,
            planetId,
            planet.MapId,
            request.PlayerCount,
            forces,
            deferredChoices,
            markers,
            diagnostics,
            request.SelectedThreatId);
    }

    private void AddDeferredThreatChoice(
        string presetId,
        string planetId,
        RMCPlanetMapPrototypeComponent planet,
        ScenarioPlanValidationRequest request,
        List<PlannedForce> forces,
        List<DeferredForceChoice> deferredChoices,
        List<ResolvedSpawnMarker> markers,
        HashSet<string> includedMarkerSources)
    {
        var candidates = new List<PlannedForce>();
        foreach (var threatId in planet.AllowedThreats)
        {
            if (!_prototypes.TryIndex(threatId, out ThreatPrototype? threat) ||
                !ThreatVoteSelection.IsThreatAllowed(
                    threat,
                    presetId,
                    request.GovforPlatoonId,
                    request.OpforPlatoonId,
                    request.PlayerCount))
            {
                continue;
            }

            if (!TryBuildThreatForce(threat, presetId, request.PlayerCount, out var force))
                continue;

            candidates.Add(force);
            forces.Add(force);

            AddThirdPartyCandidatesForThreat(
                presetId,
                planet,
                threat,
                request,
                forces,
                markers,
                includedMarkerSources);
        }

        if (candidates.Count == 0)
            return;

        deferredChoices.Add(new DeferredForceChoice(
            $"DeferredThreat:{presetId}:{planetId}",
            candidates,
            BuildSmallestCandidateReservationPolicy(candidates)));
    }

    private bool TryBuildThreatForce(
        ThreatPrototype threat,
        string presetId,
        int playerCount,
        out PlannedForce force)
    {
        if (TryGetHostileRoundGroupId(presetId, threat.ID, out var roundGroupId))
        {
            if (TryBuildHostileForceFromPrototype(
                    roundGroupId,
                    threat,
                    playerCount,
                    out force,
                    out var prototypeDiagnostic))
            {
                return true;
            }

            Logger.GetSawmill("au14.scenario").Error(
                $"[ScenarioPlanSystem] Round Group '{roundGroupId}' could not resolve for hostile '{threat.ID}' ({prototypeDiagnostic}); covered hostiles no longer fall back to the legacy Threat prototype adapter.");
            force = default!;
            return false;
        }

        return TryBuildLegacyThreatForce(threat, presetId, playerCount, out force);
    }

    private bool TryBuildHostileForceFromPrototype(
        string roundGroupId,
        ThreatPrototype threat,
        int playerCount,
        out PlannedForce force,
        out string diagnostic)
    {
        force = default!;

        if (!TryResolveRoundGroupPrototype(roundGroupId, playerCount, out var prototypeForce, out diagnostic) ||
            prototypeForce == null)
        {
            return false;
        }

        if (prototypeForce.ForceKind != ScenarioForceKind.Hostile)
        {
            diagnostic = $"Round Group '{roundGroupId}' resolved force kind '{prototypeForce.ForceKind}' instead of Hostile.";
            return false;
        }

        if (!prototypeForce.SourcePrototypeId.Equals(threat.ID, StringComparison.OrdinalIgnoreCase))
        {
            diagnostic =
                $"Round Group '{roundGroupId}' source '{prototypeForce.SourcePrototypeId}' did not match hostile '{threat.ID}'.";
            return false;
        }

        force = prototypeForce;
        diagnostic = string.Empty;
        return true;
    }

    private bool TryBuildLegacyThreatForce(
        ThreatPrototype threat,
        string presetId,
        int playerCount,
        out PlannedForce force)
    {
        force = default!;
        if (!_prototypes.TryIndex(threat.RoundStartSpawn, out PartySpawnPrototype? spawn))
            return false;

        var spawnPlan = BuildPartySpawnPlan(
            spawn,
            playerCount,
            thirdParty: false,
            parachute: false,
            reusableMarkers: true,
            warningOnly: false);

        if (spawnPlan.BodyBuckets.Sum(bucket => bucket.Count) <= 0)
            return false;

        force = new PlannedForce(
            $"Hostile:{threat.ID}",
            ScenarioForceKind.Hostile,
            threat.ID,
            spawnPlan,
            threat.WinConditions.ToArray(),
            BuildLegacyThreatTiming(presetId, threat));
        return true;
    }

    private bool TryGetHostileRoundGroupId(
        string presetId,
        string threatId,
        out string roundGroupId)
    {
        return TryGetRoundGroupId(
            presetId,
            RoundForceSide.Hostile,
            RoundForceSource.Threat,
            threatId,
            forceId: null,
            out roundGroupId);
    }

    private bool TryGetThirdPartyRoundGroupId(
        string presetId,
        string thirdPartyId,
        out string roundGroupId)
    {
        return TryGetRoundGroupId(
            presetId,
            RoundForceSide.ThirdParty,
            RoundForceSource.ThirdParty,
            thirdPartyId,
            forceId: null,
            out roundGroupId);
    }

    private bool TryBuildThirdPartyForce(
        string presetId,
        ThirdPartyPrototype thirdParty,
        string? threatId,
        int playerCount,
        out PlannedForce force)
    {
        if (TryGetThirdPartyRoundGroupId(presetId, thirdParty.ID, out var roundGroupId))
        {
            if (TryBuildThirdPartyForceFromPrototype(
                    roundGroupId,
                    thirdParty,
                    threatId,
                    playerCount,
                    out force,
                    out var prototypeDiagnostic))
            {
                return true;
            }

            Logger.GetSawmill("au14.scenario").Error(
                $"[ScenarioPlanSystem] Round Group '{roundGroupId}' could not resolve for third-party '{thirdParty.ID}' ({prototypeDiagnostic}); covered third parties no longer fall back to the legacy PartySpawn adapter.");
            force = default!;
            return false;
        }

        return TryBuildLegacyThirdPartyForce(thirdParty, threatId, playerCount, out force);
    }

    private bool TryBuildThirdPartyForceFromPrototype(
        string roundGroupId,
        ThirdPartyPrototype thirdParty,
        string? threatId,
        int playerCount,
        out PlannedForce force,
        out string diagnostic)
    {
        force = default!;

        if (!TryResolveRoundGroupPrototype(roundGroupId, playerCount, out var prototypeForce, out diagnostic) ||
            prototypeForce == null)
        {
            return false;
        }

        if (prototypeForce.ForceKind != ScenarioForceKind.ThirdParty)
        {
            diagnostic = $"Round Group '{roundGroupId}' resolved force kind '{prototypeForce.ForceKind}' instead of ThirdParty.";
            return false;
        }

        if (!prototypeForce.SourcePrototypeId.Equals(thirdParty.ID, StringComparison.OrdinalIgnoreCase))
        {
            diagnostic =
                $"Round Group '{roundGroupId}' source '{prototypeForce.SourcePrototypeId}' did not match third-party '{thirdParty.ID}'.";
            return false;
        }

        force = prototypeForce with
        {
            ForceId = BuildThirdPartyForceId(thirdParty.ID, threatId),
        };
        diagnostic = string.Empty;
        return true;
    }

    private bool TryBuildLegacyThirdPartyForce(
        ThirdPartyPrototype thirdParty,
        string? threatId,
        int playerCount,
        out PlannedForce force)
    {
        force = default!;
        if (!_prototypes.TryIndex(thirdParty.PartySpawn, out PartySpawnPrototype? spawn))
            return false;

        var spawnPlan = BuildPartySpawnPlan(
            spawn,
            playerCount,
            thirdParty: true,
            parachute: IsParachuteEntry(thirdParty),
            reusableMarkers: true,
            warningOnly: false);

        if (spawnPlan.BodyBuckets.Sum(bucket => bucket.Count) <= 0)
            return false;

        force = new PlannedForce(
            BuildThirdPartyForceId(thirdParty.ID, threatId),
            ScenarioForceKind.ThirdParty,
            thirdParty.ID,
            spawnPlan,
            Array.Empty<string>(),
            ScenarioForceTiming.Immediate);
        return true;
    }

    private static string BuildThirdPartyForceId(string thirdPartyId, string? threatId)
    {
        var threatContext = string.IsNullOrWhiteSpace(threatId)
            ? "Direct"
            : threatId;

        return $"ThirdParty:{threatContext}:{thirdPartyId}";
    }

    private void AddThirdPartyCandidatesForThreat(
        string presetId,
        RMCPlanetMapPrototypeComponent planet,
        ThreatPrototype threat,
        ScenarioPlanValidationRequest request,
        List<PlannedForce> forces,
        List<ResolvedSpawnMarker> markers,
        HashSet<string> includedMarkerSources)
    {
        foreach (var thirdPartyId in planet.ThirdParties)
        {
            if (!_prototypes.TryIndex(thirdPartyId, out ThirdPartyPrototype? thirdParty) ||
                !IsThirdPartyAllowed(
                    thirdParty,
                    presetId,
                    threat.ID,
                    request.GovforPlatoonId,
                    request.OpforPlatoonId,
                    request.PlayerCount) ||
                !ThirdPartyUsesMarkerValidation(thirdParty))
            {
                continue;
            }

            if (!TryBuildThirdPartyForce(presetId, thirdParty, threat.ID, request.PlayerCount, out var force))
                continue;

            if (IsShuttleEntry(thirdParty))
                IncludeMapPathMarkers(thirdParty.dropshippath, markers, includedMarkerSources);

            forces.Add(force);
        }
    }

    private void AddClfForce(
        string presetId,
        string planetId,
        RMCPlanetMapPrototypeComponent planet,
        int playerCount,
        List<PlannedForce> forces,
        List<ScenarioPlanDiagnostic> diagnostics)
    {
        if (!TryBuildClfForce(playerCount, out var force, out _))
        {
            diagnostics.Add(BuildDiagnostic(
                ScenarioDiagnosticSeverity.Error,
                presetId,
                planetId,
                planet.MapId,
                ClfForceId,
                AddClfRuleId,
                "CLFJobs",
                0,
                1,
                0,
                Array.Empty<string>(),
                "Insurgency CLF Round Force could not resolve AddClf job data."));
            return;
        }

        forces.Add(force);
    }

    private void AddPlatoonForces(
        GamePresetPrototype preset,
        RMCPlanetMapPrototypeComponent planet,
        int playerCount,
        List<PlannedForce> forces,
        List<DeferredForceChoice> deferredChoices)
    {
        if (preset.RequiresGovforVote && planet.PlatoonsGovfor.Count > 0)
        {
            deferredChoices.Add(BuildPlatoonChoice("GovforPlatoon", planet.PlatoonsGovfor, playerCount));
        }
        else if (!string.IsNullOrWhiteSpace(planet.DefaultGovforPlatoon))
        {
            forces.Add(BuildPlatoonForce("GovforPlatoon", planet.DefaultGovforPlatoon, playerCount));
        }

        if (preset.RequiresOpforVote && planet.PlatoonsOpfor.Count > 0)
        {
            deferredChoices.Add(BuildPlatoonChoice("OpforPlatoon", planet.PlatoonsOpfor, playerCount));
        }
        else if (!string.IsNullOrWhiteSpace(planet.DefaultOpforPlatoon))
        {
            forces.Add(BuildPlatoonForce("OpforPlatoon", planet.DefaultOpforPlatoon, playerCount));
        }
    }

    private DeferredForceChoice BuildPlatoonChoice(
        string choiceId,
        IReadOnlyList<ProtoId<PlatoonPrototype>> platoons,
        int playerCount)
    {
        var candidates = platoons
            .Select(id => BuildPlatoonForce(choiceId, id.Id, playerCount))
            .ToList();

        return new DeferredForceChoice(
            choiceId,
            candidates,
            new ScenarioReservationPolicy("ExistingPlatoonVote", 0, 0));
    }

    private PlannedForce BuildPlatoonForce(string prefix, string platoonId, int playerCount)
    {
        if (TryGetPlatoonRoundGroupId(prefix, platoonId, out var roundGroupId))
        {
            if (TryBuildPlatoonForceFromPrototype(
                    roundGroupId,
                    prefix,
                    platoonId,
                    playerCount,
                    out var prototypeForce,
                    out var prototypeDiagnostic))
            {
                return prototypeForce;
            }

            Logger.GetSawmill("au14.scenario").Warning(
                $"[ScenarioPlanSystem] Round Group '{roundGroupId}' could not resolve for platoon '{prefix}:{platoonId}' ({prototypeDiagnostic}); using legacy platoon vote adapter.");
        }

        return BuildLegacyPlatoonForce(prefix, platoonId);
    }

    private bool TryBuildPlatoonForceFromPrototype(
        string roundGroupId,
        string prefix,
        string platoonId,
        int playerCount,
        out PlannedForce force,
        out string diagnostic)
    {
        force = default!;

        if (!TryResolveRoundGroupPrototype(roundGroupId, playerCount, out var prototypeForce, out diagnostic) ||
            prototypeForce == null)
        {
            return false;
        }

        var forceId = BuildPlatoonForceId(prefix, platoonId);
        if (prototypeForce.ForceKind != ScenarioForceKind.Platoon)
        {
            diagnostic = $"Round Group '{roundGroupId}' resolved force kind '{prototypeForce.ForceKind}' instead of Platoon.";
            return false;
        }

        if (!prototypeForce.SourcePrototypeId.Equals(platoonId, StringComparison.OrdinalIgnoreCase) ||
            !prototypeForce.ForceId.Equals(forceId, StringComparison.OrdinalIgnoreCase))
        {
            diagnostic =
                $"Round Group '{roundGroupId}' resolved '{prototypeForce.ForceId}/{prototypeForce.SourcePrototypeId}' instead of '{forceId}/{platoonId}'.";
            return false;
        }

        force = prototypeForce;
        diagnostic = string.Empty;
        return true;
    }

    private static PlannedForce BuildLegacyPlatoonForce(string prefix, string platoonId)
    {
        return new PlannedForce(
            BuildPlatoonForceId(prefix, platoonId),
            ScenarioForceKind.Platoon,
            platoonId,
            ResolvedSpawnPlan.Empty,
            Array.Empty<string>(),
            ScenarioForceTiming.Immediate);
    }

    private static string BuildPlatoonForceId(string prefix, string platoonId)
    {
        return $"{prefix}:{platoonId}";
    }

    private bool TryGetPlatoonRoundGroupId(
        string prefix,
        string platoonId,
        out string roundGroupId)
    {
        roundGroupId = string.Empty;
        var forceId = BuildPlatoonForceId(prefix, platoonId);
        var side = GetPlatoonRoundForceSide(prefix);
        return side != RoundForceSide.None &&
               TryGetRoundGroupId(
                   presetId: null,
                   side,
                   RoundForceSource.Platoon,
                   platoonId,
                   forceId,
                   out roundGroupId);
    }

    private bool TryGetRoundGroupId(
        string? presetId,
        RoundForceSide side,
        RoundForceSource source,
        string sourcePrototypeId,
        string? forceId,
        out string roundGroupId)
    {
        roundGroupId = string.Empty;
        if (!TryGetScenarioForceKind(side, out var forceKind))
            return false;

        foreach (var roundGroup in _prototypes.EnumeratePrototypes<RoundGroupPrototype>()
                     .OrderBy(force => force.ID, StringComparer.OrdinalIgnoreCase))
        {
            if (ToScenarioForceKind(roundGroup.Kind) != forceKind)
                continue;

            if (presetId != null &&
                !IsRoundGroupAvailableForPreset(roundGroup.ID, presetId))
            {
                continue;
            }

            if (GetRoundForceSide(roundGroup, forceKind) != side ||
                GetRoundForceSource(roundGroup) != source ||
                !roundGroup.SourcePrototypeId.Equals(sourcePrototypeId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (forceId != null &&
                !BuildPrototypeForceId(roundGroup, forceKind).Equals(forceId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            roundGroupId = roundGroup.ID;
            return true;
        }

        return false;
    }

    private bool IsRoundGroupAvailableForPreset(string roundGroupId, string presetId)
    {
        foreach (var votingChoices in _prototypes.EnumeratePrototypes<VotingChoicesPrototype>())
        {
            if (votingChoices.Presets.Count > 0 &&
                !ContainsIgnoreCase(votingChoices.Presets, presetId))
            {
                continue;
            }

            if (votingChoices.Groups.Any(force => force.Id.Equals(roundGroupId, StringComparison.OrdinalIgnoreCase)))
                return true;

            if (votingChoices.DeferredForceChoices.Any(choice =>
                    choice.Candidates.Any(force => force.Id.Equals(roundGroupId, StringComparison.OrdinalIgnoreCase))))
            {
                return true;
            }

            foreach (var planetChoice in votingChoices.PlanetChoices)
            {
                if (planetChoice.Groups.Any(force => force.Id.Equals(roundGroupId, StringComparison.OrdinalIgnoreCase)))
                    return true;

                if (planetChoice.DeferredForceChoices.Any(choice =>
                        choice.Candidates.Any(force => force.Id.Equals(roundGroupId, StringComparison.OrdinalIgnoreCase))))
                {
                    return true;
                }

                if (planetChoice.BackupDeferredForceChoices.Any(choice =>
                        choice.Candidates.Any(force => force.Id.Equals(roundGroupId, StringComparison.OrdinalIgnoreCase))))
                {
                    return true;
                }

                if (planetChoice.BackupGroups.Any(force => force.Id.Equals(roundGroupId, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
        }

        return false;
    }

    private static RoundForceSide GetRoundForceSide(
        RoundGroupPrototype roundGroup,
        ScenarioForceKind forceKind)
    {
        if (roundGroup.Side != RoundForceSide.None)
            return roundGroup.Side;

        return forceKind switch
        {
            ScenarioForceKind.Clf => RoundForceSide.Clf,
            ScenarioForceKind.Hostile => RoundForceSide.Hostile,
            ScenarioForceKind.ThirdParty => RoundForceSide.ThirdParty,
            ScenarioForceKind.Platoon => GetPlatoonRoundForceSide(BuildPrototypeForceId(roundGroup, forceKind)),
            _ => RoundForceSide.None,
        };
    }

    private static RoundForceSource GetRoundForceSource(RoundGroupPrototype roundGroup)
    {
        if (roundGroup.Source != RoundForceSource.None)
            return roundGroup.Source;

        return roundGroup.Kind switch
        {
            RoundGroupKind.Clf => RoundForceSource.GameRule,
            RoundGroupKind.Hostile => RoundForceSource.Threat,
            RoundGroupKind.Platoon => RoundForceSource.Platoon,
            RoundGroupKind.ThirdParty => RoundForceSource.ThirdParty,
            _ => RoundForceSource.None,
        };
    }

    private static RoundForceSide GetPlatoonRoundForceSide(string prefix)
    {
        var normalizedPrefix = prefix.Split(':', 2)[0];
        if (normalizedPrefix.Equals("GovforPlatoon", StringComparison.OrdinalIgnoreCase))
            return RoundForceSide.Govfor;

        if (normalizedPrefix.Equals("OpforPlatoon", StringComparison.OrdinalIgnoreCase))
            return RoundForceSide.Opfor;

        return RoundForceSide.None;
    }

    private static bool TryGetScenarioForceKind(RoundForceSide side, out ScenarioForceKind forceKind)
    {
        forceKind = side switch
        {
            RoundForceSide.Govfor => ScenarioForceKind.Platoon,
            RoundForceSide.Opfor => ScenarioForceKind.Platoon,
            RoundForceSide.Clf => ScenarioForceKind.Clf,
            RoundForceSide.Hostile => ScenarioForceKind.Hostile,
            RoundForceSide.ThirdParty => ScenarioForceKind.ThirdParty,
            _ => default,
        };

        return side is RoundForceSide.Govfor or
            RoundForceSide.Opfor or
            RoundForceSide.Clf or
            RoundForceSide.Hostile or
            RoundForceSide.ThirdParty;
    }

    private ResolvedSpawnPlan BuildPartySpawnPlan(
        PartySpawnPrototype spawn,
        int playerCount,
        bool thirdParty,
        bool parachute,
        bool reusableMarkers,
        bool warningOnly)
    {
        var bodyCount = ThreatVoteSelection.CalculateBodyCount(spawn, playerCount);
        var entityCount = spawn.EntitiesToSpawn.Values.Sum();
        var bodyBuckets = new List<SpawnBodyBucket>
        {
            BuildPartySpawnBodyBucket(
                ThreatMarkerType.Leader.ToString(),
                spawn.LeadersToSpawn,
                spawn.Scaling,
                playerCount),
            BuildPartySpawnBodyBucket(
                ThreatMarkerType.Member.ToString(),
                spawn.GruntsToSpawn,
                spawn.Scaling,
                playerCount),
            BuildPartySpawnBodyBucket(
                ThreatMarkerType.Entity.ToString(),
                spawn.EntitiesToSpawn,
                new Dictionary<string, JobScaleEntry>(),
                playerCount),
        };

        var requirements = new List<SpawnMarkerRequirement>();
        AddMarkerRequirement(ThreatMarkerType.Leader, bodyCount.Leaders);
        AddMarkerRequirement(ThreatMarkerType.Member, bodyCount.Members);
        AddMarkerRequirement(ThreatMarkerType.Entity, entityCount);

        return new ResolvedSpawnPlan(bodyBuckets, requirements, AllowsUnderfill: !thirdParty);

        void AddMarkerRequirement(ThreatMarkerType markerType, int requiredBodies)
        {
            if (requiredBodies <= 0)
                return;

            var markerId = spawn.Markers.TryGetValue(markerType, out var id)
                ? id
                : string.Empty;
            var requiredMarkers = reusableMarkers ? 1 : requiredBodies;
            requirements.Add(new SpawnMarkerRequirement(
                markerType.ToString(),
                requiredBodies,
                requiredMarkers,
                ThreatMarkerTags(markerType, markerId, thirdParty, parachute),
                warningOnly));
        }
    }

    private static SpawnBodyBucket BuildPartySpawnBodyBucket(
        string bucket,
        IReadOnlyDictionary<string, int> bodies,
        IReadOnlyDictionary<string, JobScaleEntry> scaling,
        int playerCount)
    {
        var resolvedBodies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (bodyId, staticCount) in bodies)
        {
            resolvedBodies[bodyId] = scaling.TryGetValue(bodyId, out var entry)
                ? JobScaling.CalculateScaledSlots(playerCount, staticCount, entry)
                : Math.Max(0, staticCount);
        }

        return new SpawnBodyBucket(
            bucket,
            resolvedBodies.Values.Sum(),
            resolvedBodies);
    }

    private static ScenarioReservationPolicy BuildSmallestCandidateReservationPolicy(IReadOnlyList<PlannedForce> candidates)
    {
        var smallest = candidates
            .Select(force => new
            {
                Leaders = GetBucketCount(force.SpawnPlan, ThreatMarkerType.Leader.ToString()),
                Members = GetBucketCount(force.SpawnPlan, ThreatMarkerType.Member.ToString()),
            })
            .OrderBy(candidate => candidate.Leaders + candidate.Members)
            .FirstOrDefault();

        return new ScenarioReservationPolicy(
            SmallestCandidateReservationPolicyId,
            smallest?.Leaders ?? 0,
            smallest?.Members ?? 0);
    }

    private static int GetBucketCount(ResolvedSpawnPlan spawnPlan, string bucket)
    {
        return spawnPlan.BodyBuckets
            .Where(bodyBucket => bodyBucket.Bucket.Equals(bucket, StringComparison.OrdinalIgnoreCase))
            .Sum(bodyBucket => bodyBucket.Count);
    }

    private static bool TryResolveThreatForcePlan(
        PlannedForce force,
        out ResolvedThreatForcePlan resolved,
        out string diagnostic)
    {
        resolved = default!;

        if (force.ForceKind != ScenarioForceKind.Hostile)
        {
            diagnostic = $"Force '{force.ForceId}' is not a hostile threat force.";
            return false;
        }

        var leaderBodies = GetBucketCount(force.SpawnPlan, ThreatMarkerType.Leader.ToString());
        var memberBodies = GetBucketCount(force.SpawnPlan, ThreatMarkerType.Member.ToString());
        if (leaderBodies + memberBodies <= 0)
        {
            diagnostic = $"Hostile force '{force.ForceId}' has no leader or member bodies in its Spawn Plan.";
            return false;
        }

        resolved = new ResolvedThreatForcePlan(
            force.ForceId,
            force.SourcePrototypeId,
            force.SpawnPlan,
            leaderBodies,
            memberBodies,
            force.WinConditionRuleIds,
            force.Timing);
        diagnostic = string.Empty;
        return true;
    }

    private bool TryResolveThirdPartyForce(
        ScenarioPlanValidationRequest request,
        string thirdPartyId,
        out ResolvedThirdPartyForcePlan? force,
        out string diagnostic)
    {
        force = null;

        if (string.IsNullOrWhiteSpace(thirdPartyId))
        {
            diagnostic = "No third-party id was provided.";
            return false;
        }

        foreach (var plan in GeneratePlansForRuntimeResolution(request, "ThirdPartyForce"))
        {
            foreach (var plannedForce in plan.Forces)
            {
                if (plannedForce.ForceKind != ScenarioForceKind.ThirdParty ||
                    !plannedForce.SourcePrototypeId.Equals(thirdPartyId, StringComparison.OrdinalIgnoreCase) ||
                    !ThirdPartyForceMatchesThreatContext(plannedForce, thirdPartyId, request.SelectedThreatId))
                {
                    continue;
                }

                if (TryResolveThirdPartyForcePlan(plannedForce, out var resolved, out diagnostic))
                {
                    force = resolved;
                    return true;
                }
            }
        }

        if (!_prototypes.TryIndex<ThirdPartyPrototype>(thirdPartyId, out var thirdParty))
        {
            diagnostic = $"Third-party prototype '{thirdPartyId}' could not be resolved.";
            return false;
        }

        if (!TryBuildThirdPartyForce(request.PresetId, thirdParty, request.SelectedThreatId, request.PlayerCount, out var directForce))
        {
            diagnostic = $"Third-party '{thirdParty.ID}' has no resolvable Spawn Plan.";
            return false;
        }

        if (!TryResolveThirdPartyForcePlan(directForce, out var directResolved, out diagnostic))
            return false;

        force = directResolved;
        return true;
    }

    private static bool ThirdPartyForceMatchesThreatContext(
        PlannedForce force,
        string thirdPartyId,
        string? selectedThreatId)
    {
        if (string.IsNullOrWhiteSpace(selectedThreatId))
            return true;

        return force.ForceId.Equals(
            BuildThirdPartyForceId(thirdPartyId, selectedThreatId),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveThirdPartyForcePlan(
        PlannedForce force,
        out ResolvedThirdPartyForcePlan resolved,
        out string diagnostic)
    {
        resolved = default!;

        if (force.ForceKind != ScenarioForceKind.ThirdParty)
        {
            diagnostic = $"Force '{force.ForceId}' is not a third-party force.";
            return false;
        }

        var leaderBodies = GetBucketCount(force.SpawnPlan, ThreatMarkerType.Leader.ToString());
        var memberBodies = GetBucketCount(force.SpawnPlan, ThreatMarkerType.Member.ToString());
        var entityBodies = GetBucketCount(force.SpawnPlan, ThreatMarkerType.Entity.ToString());
        if (leaderBodies + memberBodies + entityBodies <= 0)
        {
            diagnostic = $"Third-party force '{force.ForceId}' has no bodies or entities in its Spawn Plan.";
            return false;
        }

        resolved = new ResolvedThirdPartyForcePlan(
            force.ForceId,
            force.SourcePrototypeId,
            force.SpawnPlan,
            leaderBodies,
            memberBodies,
            entityBodies);
        diagnostic = string.Empty;
        return true;
    }

    private bool TryResolveClfForce(
        ScenarioPlanValidationRequest request,
        out ResolvedClfForcePlan? force,
        out string diagnostic)
    {
        force = null;

        foreach (var plan in GeneratePlansForRuntimeResolution(request, "ClfForce"))
        {
            foreach (var plannedForce in plan.Forces)
            {
                if (plannedForce.ForceKind != ScenarioForceKind.Clf)
                    continue;

                if (TryResolveClfForcePlan(plannedForce, out var resolved, out diagnostic))
                {
                    force = resolved;
                    return true;
                }
            }
        }

        if (!TryBuildClfForce(request.PlayerCount, out var directForce, out diagnostic))
        {
            return false;
        }

        if (!TryResolveClfForcePlan(directForce, out var directResolved, out diagnostic))
            return false;

        force = directResolved;
        return true;
    }

    private bool TryBuildClfForce(int playerCount, out PlannedForce force, out string diagnostic)
    {
        if (!TryGetRoundGroupId(
                InsurgencyPresetId,
                RoundForceSide.Clf,
                RoundForceSource.GameRule,
                AddClfRuleId,
                ClfForceId,
                out var roundGroupId))
        {
            force = default!;
            diagnostic =
                $"No CLF Round Group maps '{InsurgencyPresetId}/{AddClfRuleId}/{ClfForceId}'.";
            return false;
        }

        if (TryResolveRoundGroupPrototype(
                roundGroupId,
                playerCount,
                out var prototypeForce,
                out diagnostic) &&
            prototypeForce != null &&
            prototypeForce.ForceKind == ScenarioForceKind.Clf)
        {
            force = prototypeForce;
            diagnostic = string.Empty;
            return true;
        }

        var prototypeDiagnostic = diagnostic;
        force = default!;
        diagnostic =
            $"Round Group '{roundGroupId}' could not resolve for CLF planning ({prototypeDiagnostic}); covered CLF planning no longer falls back to the legacy AddClf job adapter.";
        return false;
    }

    private static bool TryResolveClfForcePlan(
        PlannedForce force,
        out ResolvedClfForcePlan resolved,
        out string diagnostic)
    {
        resolved = default!;

        if (force.ForceKind != ScenarioForceKind.Clf)
        {
            diagnostic = $"Force '{force.ForceId}' is not a CLF force.";
            return false;
        }

        var commandBodies = GetBucketCount(force.SpawnPlan, "CLFCommand");
        var guerillaBodies = GetBucketCount(force.SpawnPlan, "CLFGuerilla");
        if (commandBodies + guerillaBodies <= 0)
        {
            diagnostic = $"CLF force '{force.ForceId}' has no command or guerilla bodies in its Spawn Plan.";
            return false;
        }

        resolved = new ResolvedClfForcePlan(
            force.ForceId,
            force.SpawnPlan,
            commandBodies,
            guerillaBodies);
        diagnostic = string.Empty;
        return true;
    }

    private void ValidatePlanMarkers(
        ScenarioPlan plan,
        List<ScenarioPlanDiagnostic> diagnostics)
    {
        foreach (var force in plan.Forces)
        {
            foreach (var requirement in force.SpawnPlan.MarkerRequirements)
            {
                var available = CountMatchingMarkers(plan.SpawnMarkers, requirement.RequiredTags);
                if (available >= requirement.RequiredMarkerCount)
                    continue;

                diagnostics.Add(BuildDiagnostic(
                    requirement.WarningOnly
                        ? ScenarioDiagnosticSeverity.Warning
                        : ScenarioDiagnosticSeverity.Error,
                    plan.PresetId,
                    plan.PlanetId,
                    plan.MapId,
                    force.ForceId,
                    force.SourcePrototypeId,
                    requirement.Bucket,
                    requirement.RequiredBodyCount,
                    requirement.RequiredMarkerCount,
                    available,
                    requirement.RequiredTags,
                    "Spawn Marker coverage is insufficient for this adapted Spawn Plan."));
            }
        }
    }

    private IReadOnlyList<ResolvedSpawnMarker> GetMapMarkers(
        string mapId,
        List<ScenarioPlanDiagnostic> diagnostics,
        string presetId,
        string planetId)
    {
        if (_mapMarkerCache.TryGetValue(mapId, out var cached))
            return cached;

        if (!_prototypes.TryIndex<GameMapPrototype>(mapId, out var gameMap))
        {
            diagnostics.Add(BuildDiagnostic(
                ScenarioDiagnosticSeverity.Error,
                presetId,
                planetId,
                mapId,
                "ScenarioMap",
                mapId,
                "Map",
                0,
                1,
                0,
                Array.Empty<string>(),
                $"GameMapPrototype '{mapId}' could not be resolved."));

            return Array.Empty<ResolvedSpawnMarker>();
        }

        var placedPrototypes = CountMapEntityPrototypes(EnumerateMapPaths(gameMap));
        var markers = new List<ResolvedSpawnMarker>();
        AddMarkersFromPlacedPrototypes(mapId, placedPrototypes, markers);

        _mapMarkerCache[mapId] = markers;
        return markers;
    }

    private IReadOnlyList<ResolvedSpawnMarker> GetMapPathMarkers(ResPath mapPath)
    {
        var cacheKey = mapPath.ToString();
        if (_mapPathMarkerCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var placedPrototypes = CountMapEntityPrototypes(new[] { mapPath });
        var markers = new List<ResolvedSpawnMarker>();
        AddMarkersFromPlacedPrototypes(cacheKey, placedPrototypes, markers);

        _mapPathMarkerCache[cacheKey] = markers;
        return markers;
    }

    private void AddMarkersFromPlacedPrototypes(
        string mapId,
        Dictionary<string, Dictionary<ResPath, int>> placedPrototypes,
        List<ResolvedSpawnMarker> markers)
    {
        foreach (var (prototypeId, countByPath) in placedPrototypes)
        {
            if (!_prototypes.TryIndex<EntityPrototype>(prototypeId, out var entityPrototype))
                continue;

            foreach (var (sourcePath, count) in countByPath)
            {
                AddMarkersFromPrototype(mapId, sourcePath, prototypeId, entityPrototype, count, markers);
            }
        }
    }

    private Dictionary<string, Dictionary<ResPath, int>> CountMapEntityPrototypes(IEnumerable<ResPath> mapPaths)
    {
        var counts = new Dictionary<string, Dictionary<ResPath, int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in mapPaths)
        {
            if (!_resources.TryContentFileRead(path, out var file))
                continue;

            using (file)
            {
                using var reader = new StreamReader(file);
                while (reader.ReadLine() is { } line)
                {
                    line = line.Trim();
                    const string prefix = "- proto: ";
                    if (!line.StartsWith(prefix, StringComparison.Ordinal))
                        continue;

                    var prototypeId = line[prefix.Length..].Trim();
                    if (prototypeId.Length == 0)
                        continue;

                    if (!counts.TryGetValue(prototypeId, out var byPath))
                    {
                        byPath = new Dictionary<ResPath, int>();
                        counts[prototypeId] = byPath;
                    }

                    byPath.TryGetValue(path, out var existing);
                    byPath[path] = existing + 1;
                }
            }
        }

        return counts;
    }

    private static IEnumerable<ResPath> EnumerateMapPaths(GameMapPrototype gameMap)
    {
        yield return gameMap.MapPath;

        foreach (var path in gameMap.MapsBelow)
        {
            yield return path;
        }

        foreach (var path in gameMap.MapsAbove)
        {
            yield return path;
        }
    }

    private void AddMarkersFromPrototype(
        string mapId,
        ResPath sourcePath,
        string prototypeId,
        EntityPrototype entityPrototype,
        int count,
        List<ResolvedSpawnMarker> markers)
    {
        if (entityPrototype.TryComp<ScenarioSpawnMarkerComponent>(out var scenarioMarker, _componentFactory))
        {
            markers.Add(new ResolvedSpawnMarker(
                prototypeId,
                mapId,
                ToScenarioMarkerKind(scenarioMarker.Kind),
                ScenarioMarkerTagsFor(
                    scenarioMarker.Tags,
                    entityPrototype.TryComp<ParachuteMarkerComponent>(out _, _componentFactory)),
                count * Math.Max(1, scenarioMarker.Count),
                sourcePath));
            return;
        }

        if (entityPrototype.TryComp<ThreatSpawnMarkerComponent>(out var threatMarker, _componentFactory))
        {
            var thirdParty = threatMarker.ThirdParty;
            var parachute = entityPrototype.TryComp<ParachuteMarkerComponent>(out _, _componentFactory);
            markers.Add(new ResolvedSpawnMarker(
                prototypeId,
                mapId,
                thirdParty ? ScenarioMarkerKind.ThirdPartyMarker : ScenarioMarkerKind.ThreatMarker,
                ThreatMarkerTags(threatMarker.ThreatMarkerType, threatMarker.ID, thirdParty, parachute),
                count,
                sourcePath));
        }

        if (entityPrototype.TryComp<XenoLeaderSpawnPointComponent>(out _, _componentFactory))
        {
            markers.Add(new ResolvedSpawnMarker(
                prototypeId,
                mapId,
                ScenarioMarkerKind.ThreatMarker,
                ThreatMarkerTags(ThreatMarkerType.Leader, string.Empty, thirdParty: false),
                count,
                sourcePath));
            return;
        }

        if (entityPrototype.TryComp<XenoSpawnPointComponent>(out _, _componentFactory))
        {
            markers.Add(new ResolvedSpawnMarker(
                prototypeId,
                mapId,
                ScenarioMarkerKind.ThreatMarker,
                ThreatMarkerTags(ThreatMarkerType.Member, string.Empty, thirdParty: false),
                count,
                sourcePath));
            return;
        }

        if (entityPrototype.TryComp<SafehouseMarkerComponent>(out _, _componentFactory))
        {
            markers.Add(new ResolvedSpawnMarker(
                prototypeId,
                mapId,
                ScenarioMarkerKind.ClfSafehouse,
                new[] { ClfSafehouseTag() },
                count,
                sourcePath));
        }

        if (entityPrototype.TryComp<SpawnPointComponent>(out var spawnPoint, _componentFactory) &&
            spawnPoint.Job != null &&
            spawnPoint.Job.Value.Id.Equals(ColonyCivilianJobId, StringComparison.OrdinalIgnoreCase))
        {
            markers.Add(new ResolvedSpawnMarker(
                prototypeId,
                mapId,
                ScenarioMarkerKind.ClfCivilianSpawn,
                new[] { ClfCivilianSpawnTag() },
                count,
                sourcePath));
        }
    }

    private bool TryGetPlanet(string planetId, out RMCPlanetMapPrototypeComponent planet)
    {
        planet = default!;
        if (!_prototypes.TryIndex<EntityPrototype>(planetId, out var planetPrototype) ||
            !planetPrototype.TryComp<RMCPlanetMapPrototypeComponent>(out var planetComp, _componentFactory))
        {
            return false;
        }

        planet = planetComp;
        return true;
    }

    private static int CountMatchingMarkers(
        IReadOnlyList<ResolvedSpawnMarker> markers,
        IReadOnlyList<string> requiredTags)
    {
        var count = 0;
        foreach (var marker in markers)
        {
            if (requiredTags.All(tag => marker.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
                count += marker.Count;
        }

        return count;
    }

    private static IReadOnlyList<ScenarioPlanMarkerSource> FindMatchingMarkerSources(
        IReadOnlyList<ResolvedSpawnMarker> markers,
        IReadOnlyList<string> requiredTags)
    {
        return markers
            .Where(marker => requiredTags.All(tag => marker.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
            .Select(marker => new ScenarioPlanMarkerSource(
                marker.SourcePrototypeId,
                marker.MarkerKind,
                marker.Tags,
                marker.Count,
                marker.Source))
            .OrderBy(source => source.Source.ToString(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(source => source.SourcePrototypeId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool TryResolveRuntimeSpawnMarkerBuckets(
        string forceId,
        ResolvedSpawnPlan spawnPlan,
        MapId mapId,
        out Dictionary<string, IReadOnlyList<EntityUid>> markersByBucket,
        out string diagnostic)
    {
        markersByBucket = new Dictionary<string, IReadOnlyList<EntityUid>>(StringComparer.OrdinalIgnoreCase);
        var missingBuckets = new List<string>();

        foreach (var requirement in spawnPlan.MarkerRequirements)
        {
            var markers = ResolveRuntimeSpawnMarkers(mapId, requirement.RequiredTags);
            markersByBucket[requirement.Bucket] = markers;

            if (markers.Count >= requirement.RequiredMarkerCount ||
                requirement.WarningOnly)
            {
                continue;
            }

            missingBuckets.Add(
                $"{requirement.Bucket} required {requirement.RequiredMarkerCount} marker(s), found {markers.Count} " +
                $"for tags [{string.Join(", ", requirement.RequiredTags)}]");
        }

        if (markersByBucket.Count == 0)
        {
            diagnostic = $"Force '{forceId}' has no Spawn Marker requirements.";
            return false;
        }

        if (missingBuckets.Count > 0)
        {
            diagnostic =
                $"Force '{forceId}' could not resolve live Spawn Markers on map {mapId}: " +
                string.Join("; ", missingBuckets);
            return false;
        }

        diagnostic = string.Empty;
        return true;
    }

    private List<EntityUid> ResolveRuntimeSpawnMarkers(
        MapId mapId,
        IReadOnlyList<string> requiredTags)
    {
        var markers = new List<EntityUid>();
        var explicitMarkers = new HashSet<EntityUid>();

        var scenarioMarkerQuery = EntityQueryEnumerator<ScenarioSpawnMarkerComponent, TransformComponent>();
        while (scenarioMarkerQuery.MoveNext(out var uid, out var scenarioMarker, out var transform))
        {
            if (transform.MapID != mapId)
                continue;

            explicitMarkers.Add(uid);
            var scenarioTags = ScenarioMarkerTagsFor(scenarioMarker.Tags, HasComp<ParachuteMarkerComponent>(uid));
            if (requiredTags.All(tag => scenarioTags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
                markers.Add(uid);
        }

        var query = EntityQueryEnumerator<ThreatSpawnMarkerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var marker, out var transform))
        {
            if (transform.MapID != mapId ||
                explicitMarkers.Contains(uid))
                continue;

            var markerTags = ThreatMarkerTags(
                marker.ThreatMarkerType,
                marker.ID,
                marker.ThirdParty,
                HasComp<ParachuteMarkerComponent>(uid));
            if (requiredTags.All(tag => markerTags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
                markers.Add(uid);
        }

        var safehouseQuery = EntityQueryEnumerator<SafehouseMarkerComponent, TransformComponent>();
        while (safehouseQuery.MoveNext(out var uid, out _, out var transform))
        {
            if (transform.MapID != mapId ||
                explicitMarkers.Contains(uid))
                continue;

            var markerTags = new[] { ClfSafehouseTag() };
            if (requiredTags.All(tag => markerTags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
                markers.Add(uid);
        }

        var spawnPointQuery = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (spawnPointQuery.MoveNext(out var uid, out var spawnPoint, out var transform))
        {
            if (transform.MapID != mapId ||
                explicitMarkers.Contains(uid) ||
                spawnPoint.Job == null ||
                !spawnPoint.Job.Value.Id.Equals(ColonyCivilianJobId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var markerTags = new[] { ClfCivilianSpawnTag() };
            if (requiredTags.All(tag => markerTags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
                markers.Add(uid);
        }

        var leaderTags = ThreatMarkerTags(ThreatMarkerType.Leader, string.Empty, thirdParty: false);
        var leaderQuery = EntityQueryEnumerator<XenoLeaderSpawnPointComponent, TransformComponent>();
        while (leaderQuery.MoveNext(out var uid, out _, out var transform))
        {
            if (transform.MapID != mapId ||
                explicitMarkers.Contains(uid))
            {
                continue;
            }

            if (requiredTags.All(tag => leaderTags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
                markers.Add(uid);
        }

        var memberTags = ThreatMarkerTags(ThreatMarkerType.Member, string.Empty, thirdParty: false);
        var memberQuery = EntityQueryEnumerator<XenoSpawnPointComponent, TransformComponent>();
        while (memberQuery.MoveNext(out var uid, out _, out var transform))
        {
            if (transform.MapID != mapId ||
                explicitMarkers.Contains(uid))
            {
                continue;
            }

            if (requiredTags.All(tag => memberTags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
                markers.Add(uid);
        }

        return markers;
    }

    private static bool IsPostRoundstartThreatVotePreset(string presetId)
    {
        return presetId.Equals(DistressSignalPresetId, StringComparison.OrdinalIgnoreCase) ||
               presetId.Equals(ColonyFallPresetId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ThirdPartyUsesMarkerValidation(ThirdPartyPrototype thirdParty)
    {
        var entryMethod = thirdParty.EntryMethod?.ToLowerInvariant() ?? "ground";
        return entryMethod is "ground" or "parachute" or "shuttle";
    }

    private static bool IsShuttleEntry(ThirdPartyPrototype thirdParty)
    {
        return (thirdParty.EntryMethod?.ToLowerInvariant() ?? "ground") == "shuttle";
    }

    private static bool IsParachuteEntry(ThirdPartyPrototype thirdParty)
    {
        return (thirdParty.EntryMethod?.ToLowerInvariant() ?? "ground") == "parachute";
    }

    private void IncludeMapPathMarkers(
        ResPath mapPath,
        List<ResolvedSpawnMarker> markers,
        HashSet<string> includedMarkerSources)
    {
        if (!includedMarkerSources.Add(mapPath.ToString()))
            return;

        markers.AddRange(GetMapPathMarkers(mapPath));
    }

    private static bool IsThirdPartyAllowed(
        ThirdPartyPrototype proto,
        string currentGamemode,
        string? currentThreat,
        string? govforPlatoon,
        string? opforPlatoon,
        int playerCount)
    {
        if (ContainsIgnoreCase(proto.BlacklistedGamemodes, currentGamemode))
            return false;

        if (proto.whitelistedgamemodes.Count > 0 &&
            !ContainsIgnoreCase(proto.whitelistedgamemodes, currentGamemode))
        {
            return false;
        }

        if (proto.MaxPlayers < playerCount || proto.MinPlayers > playerCount)
            return false;

        if (currentThreat != null && ContainsIgnoreCase(proto.BlacklistedThreats, currentThreat))
            return false;

        if (proto.WhitelistedThreats.Count > 0 &&
            (currentThreat == null || !ContainsIgnoreCase(proto.WhitelistedThreats, currentThreat)))
        {
            return false;
        }

        if (govforPlatoon != null && ContainsIgnoreCase(proto.BlacklistedPlatoons, govforPlatoon))
            return false;

        if (opforPlatoon != null && ContainsIgnoreCase(proto.BlacklistedPlatoons, opforPlatoon))
            return false;

        if (proto.WhitelistedPlatoons.Any() &&
            ((govforPlatoon != null && !ContainsIgnoreCase(proto.WhitelistedPlatoons, govforPlatoon)) ||
             (opforPlatoon != null && !ContainsIgnoreCase(proto.WhitelistedPlatoons, opforPlatoon))))
        {
            return false;
        }

        return true;
    }

    private static bool ContainsIgnoreCase(IEnumerable<string> values, string value)
    {
        return values.Any(candidate => candidate.Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> ScenarioMarkerTagsFor(
        IReadOnlyList<string> tags,
        bool parachute)
    {
        if (!parachute ||
            tags.Contains(ScenarioMarkerTags.EntryParachute, StringComparer.OrdinalIgnoreCase))
        {
            return tags;
        }

        var merged = tags.ToList();
        merged.Add(ScenarioMarkerTags.EntryParachute);
        return merged;
    }

    private static IReadOnlyList<string> ThreatMarkerTags(
        ThreatMarkerType markerType,
        string markerId,
        bool thirdParty,
        bool parachute = false)
    {
        var tags = new List<string>
        {
            thirdParty ? ScenarioMarkerTags.ForceThirdParty : ScenarioMarkerTags.ForceHostile,
            ScenarioMarkerTags.Bucket(markerType.ToString()),
            ScenarioMarkerTags.MarkerId(markerId),
        };

        if (parachute)
            tags.Add(ScenarioMarkerTags.EntryParachute);

        return tags;
    }

    private static string ClfSafehouseTag()
    {
        return ScenarioMarkerTags.ForceClfSafehouse;
    }

    private static string ClfCivilianSpawnTag()
    {
        return ScenarioMarkerTags.ClfCivilianSpawn(ColonyCivilianJobId);
    }

    private static ScenarioPlanDiagnostic BuildDiagnostic(
        ScenarioDiagnosticSeverity severity,
        string presetId,
        string planetId,
        string mapId,
        string forceId,
        string sourcePrototypeId,
        string bucket,
        int requiredBodyCount,
        int requiredMarkerCount,
        int availableMarkers,
        IReadOnlyList<string> missingTags,
        string message)
    {
        return new ScenarioPlanDiagnostic(
            severity,
            presetId,
            planetId,
            mapId,
            forceId,
            sourcePrototypeId,
            bucket,
            requiredBodyCount,
            requiredMarkerCount,
            availableMarkers,
            missingTags,
            message);
    }
}
