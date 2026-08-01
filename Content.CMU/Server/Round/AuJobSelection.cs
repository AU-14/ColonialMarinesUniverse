using System.Linq;
using Content.Server._CMU14.Threats;
using Content.Server.AU14.Scenario;
using Content.Shared._CMU14.Threats;
using Content.Shared.Preferences;
using Content.Shared.AU14.util;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Network;

namespace Content.Server.AU14.Round;

/// <summary>
/// Handles forced assignment of threat jobs at roundstart to meet ThreatPrototype slots.
/// </summary>
public sealed partial class AuJobSelectionSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private AuRoundSystem _auRoundSystem = default!;
    [Dependency] private ScenarioPlanSystem _scenarioPlan = default!;
    [Dependency] private IRobustRandom _random = default!;


    public Dictionary<NetUserId, string> ForcedJobAssignments { get; } = new();


    public List<NetUserId> AssignThreatVotePoolJobs(
        Dictionary<NetUserId, HumanoidCharacterProfile> profiles,
        IReadOnlyList<ProtoId<ThreatPrototype>> candidateThreats,
        ThreatVoteBodyCount heldBodyCount,
        string? presetId)
    {
        ForcedJobAssignments.Clear();

        if (profiles.Count == 0 || candidateThreats.Count == 0 || heldBodyCount.Total <= 0)
            return new List<NetUserId>();

        var shuffledPlayers = profiles.Keys.ToList();
        _random.Shuffle(shuffledPlayers);

        var assignments = ThreatVoteSelection.BuildHeldAssignments(
            shuffledPlayers,
            profiles,
            candidateThreats,
            heldBodyCount.Leaders,
            heldBodyCount.Members,
            presetId);

        foreach (var assignment in assignments)
        {
            ForcedJobAssignments[assignment.Player] = assignment.Job.Id;
        }

        Logger.GetSawmill("au14.jobs").Debug(
            $"[DEBUG] Held {assignments.Count} threat vote player(s): {string.Join(", ", assignments.Select(a => a.Player.ToString()))}");

        return assignments.Select(assignment => assignment.Player).ToList();
    }


    public void AssignThreatAndThirdPartyJobs(Dictionary<NetUserId, HumanoidCharacterProfile> profiles)
    {
        ForcedJobAssignments.Clear();
        var playerIds = profiles.Keys.ToList();
        var playerCount = playerIds.Count;
        Logger.GetSawmill("au14.jobs").Debug( $"[DEBUG] AssignThreatAndThirdPartyJobs: {playerCount} players");
        if (playerCount == 0)
            return;

        // Get gamemode and threat
        var selectedPresetId = _auRoundSystem.SelectedPreset?.ID;
        var presetId = selectedPresetId?.ToLowerInvariant() ?? string.Empty;
        var threat = _auRoundSystem.SelectedThreat;
        Logger.GetSawmill("au14.jobs").Debug( $"[DEBUG] Preset: {presetId}, Threat: {threat?.ID ?? "null"}");

        var threatRatio = threat?.ThreatRatio ?? 0f;

        // Third parties spawn through ThirdPartySystem's dedicated ghost-role path.
        // Do not force players into the utility ThirdParty jobs at roundstart: those
        // jobs are not station jobs and the normal spawn pipeline creates naked
        // placeholder humans when it tries to spawn them directly.
        Logger.GetSawmill("au14.jobs").Debug( $"[DEBUG] threatRatio: {threatRatio}");

        // Modes that do NOT use threat jobs (e.g., insurgency, forceonforce)
        var noThreatModes = new[] { "insurgency", "forceonforce" };
        bool useThreat = threat != null && !noThreatModes.Contains(presetId);
        Logger.GetSawmill("au14.jobs").Debug( $"[DEBUG] useThreat: {useThreat}");

        // Determine number of threat leaders/members
        int numThreatLeaders = 0;
        int numThreatMembers = 0;
        if (useThreat && threat != null)
        {
            var coveredScenarioForce = _scenarioPlan.HasMappedHostileRoundGroup(
                selectedPresetId ?? string.Empty,
                threat.ID);
            if (TryResolveThreatBodyCountFromScenarioPlan(
                    selectedPresetId ?? string.Empty,
                    threat,
                    playerCount,
                    out var bodyCount,
                    out var diagnostic))
            {
                numThreatLeaders = bodyCount.Leaders;
                numThreatMembers = bodyCount.Members;
                Logger.GetSawmill("au14.jobs").Debug(
                    $"[DEBUG] Scenario Plan resolved threat bodies: leaders={numThreatLeaders}, members={numThreatMembers}");
            }
            else if (!coveredScenarioForce &&
                     TryCalculateLegacyThreatBodyCount(threat, playerCount, out bodyCount))
            {
                numThreatLeaders = bodyCount.Leaders;
                numThreatMembers = bodyCount.Members;
                Logger.GetSawmill("au14.jobs").Warning(
                    $"[AuJobSelectionSystem] Could not resolve selected threat from Scenario Plan; falling back to legacy body-count calculation. {diagnostic}");
            }
            else
            {
                var message =
                    $"[AuJobSelectionSystem] Could not resolve selected threat body count for {threat.ID}; no threat jobs will be assigned. {diagnostic}";
                if (coveredScenarioForce)
                    Logger.GetSawmill("au14.jobs").Error(message);
                else
                    Logger.GetSawmill("au14.jobs").Warning(message);
            }

            Logger.GetSawmill("au14.jobs").Debug( $"[DEBUG] Threat leaders to assign: {numThreatLeaders}, members: {numThreatMembers}");
        }
        int numThreat = numThreatLeaders + numThreatMembers;
        numThreat = Math.Min(numThreat, playerCount);
        Logger.GetSawmill("au14.jobs").Debug( $"[DEBUG] numThreat: {numThreat} (leaders: {numThreatLeaders}, members: {numThreatMembers})");

        // Shuffle players
        var shuffledPlayers = playerIds.ToList();
        _random.Shuffle(shuffledPlayers);
        Logger.GetSawmill("au14.jobs").Debug( $"[DEBUG] Shuffled players: {string.Join(",", shuffledPlayers)}");

        // Count already assigned threat jobs
        int alreadyThreatLeaders = ForcedJobAssignments.Count(x => x.Value == "AU14JobThreatLeader");
        int alreadyThreatMembers = ForcedJobAssignments.Count(x => x.Value == "AU14JobThreatMember");
        Logger.GetSawmill("au14.jobs").Debug( $"[DEBUG] Already assigned: ThreatLeaders={alreadyThreatLeaders}, ThreatMembers={alreadyThreatMembers}");

        // Determine number of threat leaders/members to assign (subtract already assigned)
        int toAssignThreatLeaders = Math.Max(0, numThreatLeaders - alreadyThreatLeaders);
        int toAssignThreatMembers = Math.Max(0, numThreatMembers - alreadyThreatMembers);
        Logger.GetSawmill("au14.jobs").Debug( $"[DEBUG] To assign: ThreatLeaders={toAssignThreatLeaders}, ThreatMembers={toAssignThreatMembers}");
        if (toAssignThreatLeaders == 0 && toAssignThreatMembers == 0)
        {
            Logger.GetSawmill("au14.jobs").Debug("[DEBUG] No threat jobs to assign.");
            return;
        }

        // Only assign to players who do not already have a forced assignment
        var unassignedPlayers = shuffledPlayers.Where(p => !ForcedJobAssignments.ContainsKey(p)).ToList();

        // Filter players who queued for the generic threat jobs and opted into the selected threat.
        var threatLeaderJobId = new ProtoId<JobPrototype>("AU14JobThreatLeader");
        var threatMemberJobId = new ProtoId<JobPrototype>("AU14JobThreatMember");
        var selectedThreatId = new ProtoId<ThreatPrototype>(threat!.ID);

        var playersQueuedForThreatLeader = unassignedPlayers
            .Where(p => profiles.TryGetValue(p, out var profile) &&
                       CanAssignThreatJob(profile, presetId, threatLeaderJobId, selectedThreatId))
            .ToList();

        var playersQueuedForThreatMember = unassignedPlayers
            .Where(p => profiles.TryGetValue(p, out var profile) &&
                       CanAssignThreatJob(profile, presetId, threatMemberJobId, selectedThreatId))
            .ToList();

        Logger.GetSawmill("au14.jobs").Debug( $"[DEBUG] Players queued for ThreatLeader: {playersQueuedForThreatLeader.Count}, ThreatMember: {playersQueuedForThreatMember.Count}");

        // Assign threat leaders only to players who queued for it
        var assignedThreatLeaders = 0;
        for (int i = 0; assignedThreatLeaders < toAssignThreatLeaders && i < playersQueuedForThreatLeader.Count; i++)
        {
            var player = playersQueuedForThreatLeader[i];
            if (ForcedJobAssignments.ContainsKey(player))
                continue;

            ForcedJobAssignments[player] = "AU14JobThreatLeader";
            assignedThreatLeaders++;
            Logger.GetSawmill("au14.jobs").Debug( $"[DEBUG] Assigned THREAT LEADER to player {player}");
        }

        // Assign threat members only to players who queued for it
        var assignedThreatMembers = 0;
        for (int i = 0; assignedThreatMembers < toAssignThreatMembers && i < playersQueuedForThreatMember.Count; i++)
        {
            var player = playersQueuedForThreatMember[i];
            if (ForcedJobAssignments.ContainsKey(player))
                continue;

            ForcedJobAssignments[player] = "AU14JobThreatMember";
            assignedThreatMembers++;
            Logger.GetSawmill("au14.jobs").Debug( $"[DEBUG] Assigned THREAT MEMBER to player {player}");
        }

        // Log if we couldn't fill all threat slots
        if (assignedThreatLeaders < toAssignThreatLeaders)
        {
            Logger.GetSawmill("au14.jobs").Info( $"Not enough players queued for Threat Leader. Needed {toAssignThreatLeaders}, assigned {assignedThreatLeaders}");
        }
        if (assignedThreatMembers < toAssignThreatMembers)
        {
            Logger.GetSawmill("au14.jobs").Info( $"Not enough players queued for Threat Member. Needed {toAssignThreatMembers}, assigned {assignedThreatMembers}");
        }
        // The rest will be assigned normally
        Logger.GetSawmill("au14.jobs").Debug( $"[DEBUG] ForcedJobAssignments: {string.Join(", ", ForcedJobAssignments.Select(kv => $"{kv.Key}:{kv.Value}"))}");
    }

    private bool TryResolveThreatBodyCountFromScenarioPlan(
        string selectedPresetId,
        ThreatPrototype threat,
        int playerCount,
        out ThreatVoteBodyCount bodyCount,
        out string diagnostic)
    {
        var platoonSpawnRuleSystem = EntityManager.EntitySysManager.GetEntitySystem<PlatoonSpawnRuleSystem>();
        var request = new ScenarioPlanValidationRequest(
            selectedPresetId,
            playerCount,
            platoonSpawnRuleSystem.SelectedGovforPlatoon?.ID,
            platoonSpawnRuleSystem.SelectedOpforPlatoon?.ID,
            _auRoundSystem.GetSelectedPlanetId(),
            _auRoundSystem.GetSelectedPlanet()?.MapId,
            threat.ID,
            _auRoundSystem.GetSelectedGovforShip(),
            _auRoundSystem.GetSelectedOpforShip());

        if (!_scenarioPlan.TryResolveSelectedThreatForce(request, out var force, out diagnostic) ||
            force == null)
        {
            bodyCount = default;
            return false;
        }

        bodyCount = new ThreatVoteBodyCount(force.LeaderBodies, force.MemberBodies);
        return true;
    }

    private bool TryCalculateLegacyThreatBodyCount(
        ThreatPrototype threat,
        int playerCount,
        out ThreatVoteBodyCount bodyCount)
    {
        bodyCount = default;
        if (!_prototypeManager.TryIndex(threat.RoundStartSpawn, out PartySpawnPrototype? partySpawn))
            return false;

        bodyCount = ThreatVoteSelection.CalculateBodyCount(partySpawn, playerCount);
        return true;
    }

    internal static bool CanAssignThreatJob(
        HumanoidCharacterProfile profile,
        string? presetId,
        ProtoId<JobPrototype> threatJobId,
        ProtoId<ThreatPrototype> selectedThreatId)
    {
        if (!profile.GetJobPrioritiesForGamemode(presetId).TryGetValue(threatJobId, out var priority) ||
            priority == JobPriority.Never)
        {
            return false;
        }

        var threatPreferences = profile.GetThreatPreferencesForGamemode(presetId);
        if (threatPreferences.Count == 0)
            return true;

        return threatPreferences
            .Any(preference => preference.Id.Equals(selectedThreatId.Id, StringComparison.OrdinalIgnoreCase));
    }
}
