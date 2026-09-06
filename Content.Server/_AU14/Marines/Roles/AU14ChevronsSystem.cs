using System.Linq;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Content.Shared._RMC14.UniformAccessories;
using Content.Shared.Hands.EntitySystems;
using Content.Shared._AU14.Marines.Roles.Chevrons;
using Content.Server.Ghost.Roles.Components;
using Robust.Shared.Utility;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Content.Shared.AU14.util;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared.Preferences;
using Content.Server.AU14.Round;
using Content.Shared.NPC.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Server._AU14.Marines.Roles.Chevrons;

public sealed partial class ChevronSystem : EntitySystem
{
    private static readonly TimeSpan PlaytimeRetryDelay = TimeSpan.FromSeconds(10);

    [Dependency] private PlayTimeTrackingManager _tracking = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private SharedUniformAccessorySystem _uniformAccessory = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedRankSystem _rankSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<DidEquipEvent>(OnJumpsuitEquipped);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == null || ev.Player == null)
            return;

        if (!_prototypes.TryIndex<JobPrototype>(ev.JobId, out var jobPrototype))
            return;

        if (jobPrototype.Chevrons == null || jobPrototype.Chevrons.Count == 0)
            return;

        if (!_tracking.TryGetTrackerTimes(ev.Player, out var playTimes))
        {
            var mob = ev.Mob;
            var jobId = ev.JobId;
            var profile = ev.Profile;
            var player = ev.Player;
            Timer.Spawn(PlaytimeRetryDelay, () =>
            {
                if (Deleted(mob) || player.Status != SessionStatus.Connected)
                    return;

                if (!_tracking.TryGetTrackerTimes(player, out var retryTimes))
                {
                    Log.Error($"Playtimes weren't ready for {player} after retry!");
                    return;
                }

                TrySpawnChevronForMob(mob, jobId, profile, retryTimes);
            });
            return;
        }

        TrySpawnChevronForMob(ev.Mob, ev.JobId, ev.Profile, playTimes);
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (HasComp<ChevronSpawnedComponent>(ev.Entity))
            return;

        if (!TryComp<GhostRoleComponent>(ev.Entity, out var ghostRole) || ghostRole.JobProto == null)
            return;

        if (!_prototypes.TryIndex<JobPrototype>(ghostRole.JobProto, out var jobPrototype))
            return;

        if (jobPrototype.Chevrons == null || jobPrototype.Chevrons.Count == 0)
            return;

        if (!_tracking.TryGetTrackerTimes(ev.Player, out var playTimes))
        {
            Log.Warning($"Playtimes not ready for ghost role takeover by {ev.Player}");
            playTimes ??= new Dictionary<string, TimeSpan>();
        }

        TrySpawnChevronForMob(ev.Entity, ghostRole.JobProto, null, playTimes);
    }

    /// <summary>
    /// Fires when any item is equipped onto a mob
    /// Filters to jumpsuit slot items that have a UniformAccessoryHolderComponent,
    /// then spawns the pending chevron if one is waiting
    /// </summary>
    private void OnJumpsuitEquipped(DidEquipEvent args)
    {
        // Only care about the jumpsuit (INNERCLOTHING) slot.
        if ((args.SlotFlags & SlotFlags.INNERCLOTHING) == 0)
            return;

        // Only care if the equipped item can hold accessories.
        if (!HasComp<UniformAccessoryHolderComponent>(args.Equipment))
            return;

        var mob = args.Equipee;

        if (HasComp<ChevronSpawnedComponent>(mob))
            return;

        if (!_playerManager.TryGetSessionByEntity(mob, out var session))
            return;

        if (!TryComp<ChevronPendingComponent>(mob, out var pending) || pending.JobId == null)
            return;

        if (!_prototypes.TryIndex<JobPrototype>(pending.JobId, out var jobPrototype))
            return;

        if (jobPrototype.Chevrons == null || jobPrototype.Chevrons.Count == 0)
            return;

        if (!_tracking.TryGetTrackerTimes(session, out var playTimes))
            playTimes = new Dictionary<string, TimeSpan>();

        var profile = pending.Profile;
        var platoon = ResolvePlatoonFromMobFaction(mob);
        var chevronMap = ResolveChevronMapForPlatoon(jobPrototype, platoon);
        TrySpawnFirstValidChevron(chevronMap, profile, playTimes, mob, pending.JobId, platoon?.ID);
        RemComp<ChevronPendingComponent>(mob);
    }

    /// <summary>
    /// Central entry point for both roundstart and ghost role paths
    /// If the mob already has a valid jumpsuit equipped, spawns the chevron immediately
    /// Otherwise stores a ChevronPendingComponent for OnJumpsuitEquipped to consume
    /// </summary>
    private void TrySpawnChevronForMob(
        EntityUid mob,
        string jobId,
        HumanoidCharacterProfile? profile,
        Dictionary<string, TimeSpan> playTimes)
    {
        if (HasComp<ChevronSpawnedComponent>(mob))
            return;

        if (!_prototypes.TryIndex<JobPrototype>(jobId, out var jobPrototype))
            return;

        if (jobPrototype.Chevrons == null || jobPrototype.Chevrons.Count == 0)
            return;

        var platoon = ResolvePlatoonFromMobFaction(mob);
        var chevronMap = ResolveChevronMapForPlatoon(jobPrototype, platoon);

        if (HasEquippedJumpsuit(mob))
        {
            TrySpawnFirstValidChevron(chevronMap, profile, playTimes, mob, jobId, platoon?.ID);
        }
        else
        {
            var pending = EnsureComp<ChevronPendingComponent>(mob);
            pending.JobId = jobId;
            pending.Profile = profile;
        }
    }

    private bool HasEquippedJumpsuit(EntityUid mob)
    {
        var slots = _inventory.GetSlotEnumerator(mob, SlotFlags.INNERCLOTHING);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity != null && HasComp<UniformAccessoryHolderComponent>(slot.ContainedEntity))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the full rank name (e.g. "Major John Marine") for use in announcements
    /// Resolves from the chevron map first so the correct rank is used even before the
    /// chevron has been physically spawned onto the mob
    /// Falls back to RankComponent, then bare name
    /// </summary>
    public string GetAnnouncementFullName(EntityUid mob, string? jobId, HumanoidCharacterProfile? profile = null)
    {
        if (jobId != null)
        {
            _playerManager.TryGetSessionByEntity(mob, out var session);
            Dictionary<string, TimeSpan> playTimes;
            if (session == null || !_tracking.TryGetTrackerTimes(session, out playTimes!))
                playTimes = new Dictionary<string, TimeSpan>();

            var rank = ResolveIntendedRank(mob, jobId, profile, playTimes);
            if (rank != null)
            {
                var rankName = Loc.TryGetString($"rank-{rank.ID}", out var ln) ? ln : rank.Name;
                return $"{rankName} {Name(mob)}";
            }
        }

        return _rankSystem.GetSpeakerFullRankName(mob) ?? Name(mob);
    }

    /// <summary>
    /// Returns the short/prefix rank name (e.g. "Maj John Marine") for use in announcements
    /// Resolves from the chevron map first so the correct rank is used even before the
    /// chevron has been physically spawned onto the mob
    /// Falls back to RankComponent, then bare name
    /// </summary>
    public string GetAnnouncementShortName(EntityUid mob, string? jobId, HumanoidCharacterProfile? profile = null)
    {
        if (jobId != null)
        {
            _playerManager.TryGetSessionByEntity(mob, out var session);
            Dictionary<string, TimeSpan> playTimes;
            if (session == null || !_tracking.TryGetTrackerTimes(session, out playTimes!))
                playTimes = new Dictionary<string, TimeSpan>();

            var rank = ResolveIntendedRank(mob, jobId, profile, playTimes);
            if (rank != null)
            {
                var prefix = Loc.TryGetString($"rank-{rank.ID}.prefix", out var p) ? p : rank.Prefix;
                return $"{prefix} {Name(mob)}";
            }
        }

        return _rankSystem.GetSpeakerRankName(mob) ?? Name(mob);
    }

    /// Checks the mob's NpcFactionMemberComponent to determine if they are govfor or opfor, then returns the corresponding platoon from PlatoonSpawnRuleSystem
    private PlatoonPrototype? ResolvePlatoonFromMobFaction(EntityUid mob)
    {
        if (!TryComp<NpcFactionMemberComponent>(mob, out var factionMember))
            return null;

        var platoonRule = EntitySystem.Get<PlatoonSpawnRuleSystem>();

        foreach (var faction in factionMember.Factions)
        {
            var factionId = faction.Id;
            if (factionId.Equals("govfor", StringComparison.OrdinalIgnoreCase) ||
                factionId.Contains("govfor", StringComparison.OrdinalIgnoreCase))
            {
                return platoonRule.SelectedGovforPlatoon;
            }

            if (factionId.Equals("opfor", StringComparison.OrdinalIgnoreCase) ||
                factionId.Contains("opfor", StringComparison.OrdinalIgnoreCase))
            {
                return platoonRule.SelectedOpforPlatoon;
            }
        }

        return null;
    }

    // Returns the platoon's chevron override map for this job (walking the inheritance chain), or falls back to the job's base chevrons if no override matches
    private Dictionary<string, ChevronDefinition>? ResolveChevronMapForPlatoon(
        JobPrototype job,
        PlatoonPrototype? platoon)
    {
        if (platoon?.ChevronOverrides != null)
        {
            foreach (var (overrideJobId, overrideChevrons) in platoon.ChevronOverrides)
            {
                if (JobInheritsFrom(job.ID, overrideJobId.Id))
                {
                    return overrideChevrons.ToDictionary(
                        kvp => kvp.Key.Id,
                        kvp => kvp.Value);
                }
            }
        }

        return job.Chevrons;
    }

    private bool JobInheritsFrom(string jobId, string ancestorId)
    {
        if (jobId == ancestorId)
            return true;

        if (!_prototypes.TryIndex<JobPrototype>(jobId, out var job) || job.Parents == null)
            return false;

        foreach (var parent in job.Parents)
        {
            if (JobInheritsFrom(parent, ancestorId))
                return true;
        }

        return false;
    }

    private void TrySpawnFirstValidChevron(
        Dictionary<string, ChevronDefinition>? chevronMap,
        HumanoidCharacterProfile? profile,
        Dictionary<string, TimeSpan> playTimes,
        EntityUid mob,
        string jobId,
        string? platoonId)
    {
        if (chevronMap == null)
            return;

        // Try preferred rank first
        var preferredRankId = platoonId != null ? profile?.GetRankPreference(jobId, platoonId) : null;

        if (preferredRankId != null && chevronMap.TryGetValue(preferredRankId, out var preferredDef) && IsChevronValid(preferredDef, profile, playTimes))
        {
            SpawnChevron(preferredDef, mob);
            return;
        }

        // Fall back to first valid (auto)
        foreach (var (_, chevronDef) in chevronMap)
        {
            if (IsChevronValid(chevronDef, profile, playTimes))
            {
                SpawnChevron(chevronDef, mob);
                return;
            }
        }
    }

    private bool IsChevronValid(ChevronDefinition chevronDef, HumanoidCharacterProfile? profile, Dictionary<string, TimeSpan> playTimes)
    {
        if (chevronDef.Requirements == null) return true;
        foreach (var req in chevronDef.Requirements)
        {
            if (!req.Check(_entityManager, _prototypes, profile, playTimes, out FormattedMessage? _))
                return false;
        }
        return true;
    }

    private void SpawnChevron(ChevronDefinition chevronDef, EntityUid mob)
    {
        var coords = _entityManager.GetComponent<TransformComponent>(mob).Coordinates;
        var chevron = _entityManager.SpawnEntity(chevronDef.Entity, coords);

        if (!_uniformAccessory.TryInsertToValidSlot(chevron, mob))
            _hands.TryPickupAnyHand(mob, chevron, false);

        EnsureComp<ChevronSpawnedComponent>(mob);
    }

    /// <summary>
    /// Returns the rank prototype the mob would receive from the chevron system,
    /// without actually spawning anything. Used for announcements when the rank
    /// component hasn't been set yet (chevron is in a backpack)
    /// </summary>
    public RankPrototype? ResolveIntendedRank(EntityUid mob, string jobId, HumanoidCharacterProfile? profile, Dictionary<string, TimeSpan> playTimes)
    {
        if (!_prototypes.TryIndex<JobPrototype>(jobId, out var jobPrototype))
            return null;

        if (jobPrototype.Chevrons == null || jobPrototype.Chevrons.Count == 0)
            return null;

        var platoon = ResolvePlatoonFromMobFaction(mob);
        var chevronMap = ResolveChevronMapForPlatoon(jobPrototype, platoon);

        if (chevronMap == null)
            return null;

        // Try preferred rank first
        var preferredRankId = platoon != null ? profile?.GetRankPreference(jobId, platoon.ID) : null;
        if (preferredRankId != null &&
            chevronMap.TryGetValue(preferredRankId, out var preferredDef) &&
            IsChevronValid(preferredDef, profile, playTimes) &&
            _prototypes.TryIndex<RankPrototype>(preferredRankId, out var preferredProto))
        {
            return preferredProto;
        }

        // Fall back to first valid
        foreach (var (rankId, chevronDef) in chevronMap)
        {
            if (IsChevronValid(chevronDef, profile, playTimes) &&
                _prototypes.TryIndex<RankPrototype>(rankId, out var rankProto))
                return rankProto;
        }

        return null;
    }
}
