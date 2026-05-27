using System.Linq;
using Content.Shared.Preferences;
using Content.Shared.AU14.Threats;
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
    [Dependency] private IRobustRandom _random = default!;


    public Dictionary<NetUserId, string> ForcedJobAssignments { get; } = new();


    public void AssignThreatAndThirdPartyJobs(Dictionary<NetUserId, HumanoidCharacterProfile> profiles)
    {
        ForcedJobAssignments.Clear();
        var playerIds = profiles.Keys.ToList();
        var playerCount = playerIds.Count;
        Logger.GetSawmill("au14.jobs").Debug( $"[DEBUG] AssignThreatAndThirdPartyJobs: {playerCount} players");
        if (playerCount == 0)
            return;

        // Get gamemode and threat
        var presetId = _auRoundSystem.SelectedPreset?.ID.ToLowerInvariant() ?? string.Empty;
        var threat = _auRoundSystem._selectedthreat;
        Logger.GetSawmill("au14.jobs").Debug( $"[DEBUG] Preset: {presetId}, Threat: {threat?.ID ?? "null"}");

        var threatRatio = threat?.ThreatRatio ?? 0f;

        // Third parties spawn through AuThirdPartySystem's dedicated ghost-role path.
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
        if (useThreat && threat != null && _prototypeManager.TryIndex(threat.RoundStartSpawn, out PartySpawnPrototype? partySpawn))
        {
            // Sum scaled counts for each individual leader entity prototype
            foreach (var (protoId, staticCount) in partySpawn.LeadersToSpawn)
            {
                if (partySpawn.Scaling.TryGetValue(protoId, out var entry))
                {
                    var baseCount = entry.Benchmark ?? staticCount;
                    var extra = JobScaling.CalculateExtraSlots(playerCount, entry);
                    var scaledCount = JobScaling.CalculateScaledSlots(playerCount, staticCount, entry);
                    numThreatLeaders += scaledCount;
                    Logger.GetSawmill("au14.jobs").Debug( $"[DEBUG] Scaled threat leader '{protoId}' to {scaledCount} (base={baseCount}, extra={extra}, max={entry.Maximum?.ToString() ?? "null"}, players={playerCount})");
                }
                else
                {
                    numThreatLeaders += staticCount;
                }
            }

            // Sum scaled counts for each individual grunt/member entity prototype
            foreach (var (protoId, staticCount) in partySpawn.GruntsToSpawn)
            {
                if (partySpawn.Scaling.TryGetValue(protoId, out var entry))
                {
                    var baseCount = entry.Benchmark ?? staticCount;
                    var extra = JobScaling.CalculateExtraSlots(playerCount, entry);
                    var scaledCount = JobScaling.CalculateScaledSlots(playerCount, staticCount, entry);
                    numThreatMembers += scaledCount;
                    Logger.GetSawmill("au14.jobs").Debug( $"[DEBUG] Scaled threat member '{protoId}' to {scaledCount} (base={baseCount}, extra={extra}, max={entry.Maximum?.ToString() ?? "null"}, players={playerCount})");
                }
                else
                {
                    numThreatMembers += staticCount;
                }
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

        return profile.GetThreatPreferencesForGamemode(presetId)
            .Any(preference => preference.Id.Equals(selectedThreatId.Id, StringComparison.OrdinalIgnoreCase));
    }
}
