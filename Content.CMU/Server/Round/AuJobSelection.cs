using System.Linq;
using Content.Shared.Preferences;
using Content.Shared.AU14.Threats;
using Content.Shared.AU14.util;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Network;

namespace Content.Server.AU14.Round;

/// <summary>
/// Handles forced assignment of threat and third party jobs at roundstart to meet ratios from ThreatPrototype.
/// </summary>
public sealed class AuJobSelectionSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly AuRoundSystem _auRoundSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;


    public Dictionary<NetUserId, string> ForcedJobAssignments { get; } = new();


    public void AssignThreatAndThirdPartyJobs(Dictionary<NetUserId, HumanoidCharacterProfile> profiles)
    {
        ForcedJobAssignments.Clear();
        var playerIds = profiles.Keys.ToList();
        var playerCount = playerIds.Count;
        Logger.DebugS("au14.jobs", $"[DEBUG] AssignThreatAndThirdPartyJobs: {playerCount} players");
        if (playerCount == 0)
            return;

        // Get gamemode and threat
        var preset = _auRoundSystem.GetType()
            .GetProperty("_selectedPreset",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(_auRoundSystem);
        var presetId = preset?.GetType().GetProperty("ID")?.GetValue(preset)?.ToString()?.ToLowerInvariant() ?? string.Empty;
        var threat = _auRoundSystem._selectedthreat;
        Logger.DebugS("au14.jobs", $"[DEBUG] Preset: {presetId}, Threat: {threat?.ID ?? "null"}");

        // If no threat or not a threat mode, only assign third party jobs
        float threatRatio = 0f;
        float thirdPartyRatio = 0f;
        if (threat != null)
        {
            threatRatio = threat.ThreatRatio;
            thirdPartyRatio = threat.ThirdPartyRatio;
        }
        else
        {
            // fallback: only third party
            thirdPartyRatio = 0.15f;
        }

        if (_auRoundSystem.SelectedThirdParties.Count == 0)
        {
            thirdPartyRatio = 0f;
        }
        Logger.DebugS("au14.jobs", $"[DEBUG] threatRatio: {threatRatio}, thirdPartyRatio: {thirdPartyRatio}");

        // Modes that do NOT use threat jobs (e.g., insurgency, forceonforce)
        var noThreatModes = new[] { "insurgency", "forceonforce" };
        bool useThreat = threat != null && !noThreatModes.Contains(presetId);
        Logger.DebugS("au14.jobs", $"[DEBUG] useThreat: {useThreat}");

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
                    Logger.DebugS("au14.jobs", $"[DEBUG] Scaled threat leader '{protoId}' to {scaledCount} (base={baseCount}, extra={extra}, max={entry.Maximum?.ToString() ?? "null"}, players={playerCount})");
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
                    Logger.DebugS("au14.jobs", $"[DEBUG] Scaled threat member '{protoId}' to {scaledCount} (base={baseCount}, extra={extra}, max={entry.Maximum?.ToString() ?? "null"}, players={playerCount})");
                }
                else
                {
                    numThreatMembers += staticCount;
                }
            }

            Logger.DebugS("au14.jobs", $"[DEBUG] Threat leaders to assign: {numThreatLeaders}, members: {numThreatMembers}");
        }
        int numThreat = numThreatLeaders + numThreatMembers;
        int numThirdParty = (int)Math.Round(playerCount * thirdPartyRatio);
        numThreat = Math.Min(numThreat, playerCount);
        numThirdParty = Math.Min(numThirdParty, playerCount - numThreat);
        Logger.DebugS("au14.jobs", $"[DEBUG] numThreat: {numThreat} (leaders: {numThreatLeaders}, members: {numThreatMembers}), numThirdParty: {numThirdParty}");

        // Shuffle players
        var shuffledPlayers = playerIds.ToList();
        _random.Shuffle(shuffledPlayers);
        Logger.DebugS("au14.jobs", $"[DEBUG] Shuffled players: {string.Join(",", shuffledPlayers)}");

        // Count already assigned threat/third party jobs
        int alreadyThreatLeaders = ForcedJobAssignments.Count(x => x.Value == "AU14JobThreatLeader");
        int alreadyThreatMembers = ForcedJobAssignments.Count(x => x.Value == "AU14JobThreatMember");
        int alreadyThirdPartyLeaders = ForcedJobAssignments.Count(x => x.Value == "AU14JobThirdPartyLeader");
        int alreadyThirdPartyMembers = ForcedJobAssignments.Count(x => x.Value == "AU14JobThirdPartyMember");
        Logger.DebugS("au14.jobs", $"[DEBUG] Already assigned: ThreatLeaders={alreadyThreatLeaders}, ThreatMembers={alreadyThreatMembers}, ThirdPartyLeaders={alreadyThirdPartyLeaders}, ThirdPartyMembers={alreadyThirdPartyMembers}");

        // Determine number of threat leaders/members to assign (subtract already assigned)
        int toAssignThreatLeaders = Math.Max(0, numThreatLeaders - alreadyThreatLeaders);
        int toAssignThreatMembers = Math.Max(0, numThreatMembers - alreadyThreatMembers);
        int toAssignThirdParty = Math.Max(0, numThirdParty - (alreadyThirdPartyLeaders + alreadyThirdPartyMembers));
        Logger.DebugS("au14.jobs", $"[DEBUG] To assign: ThreatLeaders={toAssignThreatLeaders}, ThreatMembers={toAssignThreatMembers}, ThirdParty={toAssignThirdParty}");

        // Only assign to players who do not already have a forced assignment
        var unassignedPlayers = shuffledPlayers.Where(p => !ForcedJobAssignments.ContainsKey(p)).ToList();

        // Filter players who have queued for threat jobs (have them in job priorities with priority != Never)
        var threatLeaderJobId = new ProtoId<JobPrototype>("AU14JobThreatLeader");
        var threatMemberJobId = new ProtoId<JobPrototype>("AU14JobThreatMember");

        var playersQueuedForThreatLeader = unassignedPlayers
            .Where(p => profiles.TryGetValue(p, out var profile) &&
                       profile.JobPriorities.TryGetValue(threatLeaderJobId, out var priority) &&
                       priority != JobPriority.Never)
            .ToList();

        var playersQueuedForThreatMember = unassignedPlayers
            .Where(p => profiles.TryGetValue(p, out var profile) &&
                       profile.JobPriorities.TryGetValue(threatMemberJobId, out var priority) &&
                       priority != JobPriority.Never)
            .ToList();

        Logger.DebugS("au14.jobs", $"[DEBUG] Players queued for ThreatLeader: {playersQueuedForThreatLeader.Count}, ThreatMember: {playersQueuedForThreatMember.Count}");

        // Assign threat leaders only to players who queued for it
        for (int i = 0; i < toAssignThreatLeaders && i < playersQueuedForThreatLeader.Count; i++)
        {
            var player = playersQueuedForThreatLeader[i];
            ForcedJobAssignments[player] = "AU14JobThreatLeader";
            Logger.DebugS("au14.jobs", $"[DEBUG] Assigned THREAT LEADER to player {player}");
        }

        // Assign threat members only to players who queued for it
        for (int i = 0; i < toAssignThreatMembers && i < playersQueuedForThreatMember.Count; i++)
        {
            var player = playersQueuedForThreatMember[i];
            ForcedJobAssignments[player] = "AU14JobThreatMember";
            Logger.DebugS("au14.jobs", $"[DEBUG] Assigned THREAT MEMBER to player {player}");
        }

        // Log if we couldn't fill all threat slots
        if (toAssignThreatLeaders > playersQueuedForThreatLeader.Count)
        {
            Logger.InfoS("au14.jobs", $"Not enough players queued for Threat Leader. Needed {toAssignThreatLeaders}, got {playersQueuedForThreatLeader.Count}");
        }
        if (toAssignThreatMembers > playersQueuedForThreatMember.Count)
        {
            Logger.InfoS("au14.jobs", $"Not enough players queued for Threat Member. Needed {toAssignThreatMembers}, got {playersQueuedForThreatMember.Count}");
        }
        // Filter players who have queued for third party jobs
        var thirdPartyLeaderJobId = new ProtoId<JobPrototype>("AU14JobThirdPartyLeader");
        var thirdPartyMemberJobId = new ProtoId<JobPrototype>("AU14JobThirdPartyMember");

        var playersQueuedForThirdParty = unassignedPlayers
            .Where(p => !ForcedJobAssignments.ContainsKey(p) &&
                       profiles.TryGetValue(p, out var profile) &&
                       (profile.JobPriorities.TryGetValue(thirdPartyLeaderJobId, out var lp) && lp != JobPriority.Never ||
                        profile.JobPriorities.TryGetValue(thirdPartyMemberJobId, out var mp) && mp != JobPriority.Never))
            .ToList();

        Logger.DebugS("au14.jobs", $"[DEBUG] Players queued for ThirdParty: {playersQueuedForThirdParty.Count}");

        // Assign third party jobs: alternate leader/member if possible, only to opted-in players
        int thirdPartyAssigned = 0;
        for (int i = 0; i < toAssignThirdParty && i < playersQueuedForThirdParty.Count; i++, thirdPartyAssigned++)
        {
            var player = playersQueuedForThirdParty[i];
            var job = (thirdPartyAssigned % 2 == 0) ? "AU14JobThirdPartyLeader" : "AU14JobThirdPartyMember";
            ForcedJobAssignments[player] = job;
            Logger.DebugS("au14.jobs", $"[DEBUG] Assigned THIRD PARTY job {job} to player {player}");
        }

        if (toAssignThirdParty > playersQueuedForThirdParty.Count)
        {
            Logger.InfoS("au14.jobs", $"Not enough players queued for Third Party. Needed {toAssignThirdParty}, got {playersQueuedForThirdParty.Count}");
        }
        // The rest will be assigned normally
        Logger.DebugS("au14.jobs", $"[DEBUG] ForcedJobAssignments: {string.Join(", ", ForcedJobAssignments.Select(kv => $"{kv.Key}:{kv.Value}"))}");
    }
}
