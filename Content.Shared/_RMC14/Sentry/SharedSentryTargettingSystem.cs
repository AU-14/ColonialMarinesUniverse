using System.Collections.Generic;
using System.Linq;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.AU14.AllianceConsole;
using Content.Shared.Inventory;
using Content.Shared.NPC.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Sentry;

public abstract partial class SharedSentryTargetingSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private GunIFFSystem _iff = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private const string SentryExcludedFaction = "RMCDumb";

    public static readonly Dictionary<string, EntProtoId<IFFFactionComponent>> SentryFactionToIff = new()
    {
        { "GOVFOR", "GOVFOR" },
        { "OPFOR", "OPFOR" },
        { "Colony", "AUColonist" },
        { "Bureau", "AUBureau" },
        { "UPP", "AUUpp" }
    };

    public static readonly HashSet<string> SentryAllowedFactions = SentryFactionToIff.Keys.ToHashSet();

    private readonly HashSet<EntProtoId<IFFFactionComponent>> _friendlyIffBuffer = new();
    private readonly HashSet<EntProtoId<IFFFactionComponent>> _targetIffBuffer = new();
    private readonly HashSet<Entity<NpcFactionMemberComponent>> _factionLookupBuffer = new();
    private readonly HashSet<Entity<UserIFFComponent>> _userIffLookupBuffer = new();
    private readonly HashSet<EntityUid> _candidateLookupBuffer = new();
    private readonly HashSet<string> _friendlyNpcFactionBuffer = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<SentryTargetingComponent, MapInitEvent>(OnTargetingMapInit);
        SubscribeLocalEvent<SentryTargetingComponent, ComponentStartup>(OnTargetingStartup);
    }

    private void OnTargetingMapInit(Entity<SentryTargetingComponent> ent, ref MapInitEvent args)
    {
        if (TryComp<NpcFactionMemberComponent>(ent.Owner, out var factionMember) && factionMember.Factions.Count > 0)
            ent.Comp.OriginalFaction = factionMember.Factions.First();

        if (!HasComp<GunIFFComponent>(ent.Owner) && HasComp<GunComponent>(ent.Owner))
            _iff.EnableIntrinsicIFF(ent);
    }

    private void OnTargetingStartup(Entity<SentryTargetingComponent> ent, ref ComponentStartup args)
    {
        // Sentries begin with no faction assigned — they must be configured via multitool.
        // Only seed FriendlyFactions if the prototype explicitly pre-populated it (non-sentry use).
        if (_net.IsServer)
            ApplyTargeting(ent);
    }

    public void ApplyDeployerFactions(EntityUid sentry, EntityUid deployer)
    {
        var targeting = EnsureComp<SentryTargetingComponent>(sentry);
        targeting.FriendlyFactions.Clear();
        targeting.HumanoidAdded.Clear();

        var iffFactions = new HashSet<EntProtoId<IFFFactionComponent>>();
        var ev = new GetIFFFactionEvent(SlotFlags.IDCARD | SlotFlags.BELT | SlotFlags.POCKET, iffFactions);
        RaiseLocalEvent(deployer, ref ev);

        if (iffFactions.Count > 0)
        {
            foreach (var (sentryFaction, iffFaction) in SentryFactionToIff)
            {
                if (iffFactions.Contains(iffFaction))
                    targeting.FriendlyFactions.Add(sentryFaction);
            }
        }

        // Fallback to NPC faction when IFF yielded nothing (no IFF at all, or IFF not in the standard map).
        if (targeting.FriendlyFactions.Count == 0 &&
            TryComp<NpcFactionMemberComponent>(deployer, out var npcFaction))
        {
            foreach (var faction in npcFaction.Factions)
            {
                if (faction != SentryExcludedFaction)
                    targeting.FriendlyFactions.Add(faction);
            }

            if (npcFaction.Factions.Count > 0)
                targeting.OriginalFaction = npcFaction.Factions.First();
        }

        targeting.DeployedFriendlyFactions.Clear();
        targeting.DeployedFriendlyFactions.UnionWith(targeting.FriendlyFactions);

        if (_net.IsServer)
            ApplyTargeting((sentry, targeting));

        Dirty(sentry, targeting);
    }

    public void SetFriendlyFactions(Entity<SentryTargetingComponent> ent, HashSet<string> factions)
    {
        ent.Comp.FriendlyFactions.Clear();
        ent.Comp.HumanoidAdded.Clear();

        var friendly = factions
            .Where(f => f != SentryExcludedFaction && f != "Humanoid" && SentryAllowedFactions.Contains(f))
            .ToHashSet();

        if (factions.Contains("Humanoid"))
        {
            foreach (var faction in GetHumanoidFactions())
            {
                if (friendly.Add(faction))
                    ent.Comp.HumanoidAdded.Add(faction);
            }
        }

        ent.Comp.FriendlyFactions.UnionWith(friendly);

        if (_net.IsServer)
            ApplyTargeting(ent);

        Dirty(ent.Owner, ent.Comp);
    }

    public void ResetToDefault(Entity<SentryTargetingComponent> ent)
    {
        ent.Comp.FriendlyFactions.Clear();
        ent.Comp.HumanoidAdded.Clear();

        if (ent.Comp.DeployedFriendlyFactions.Count > 0)
            ent.Comp.FriendlyFactions.UnionWith(ent.Comp.DeployedFriendlyFactions);

        if (_net.IsServer)
            ApplyTargeting(ent);

        Dirty(ent.Owner, ent.Comp);
    }

    public void ToggleFaction(Entity<SentryTargetingComponent> ent, string faction, bool friendly)
    {
        if (faction == SentryExcludedFaction)
            return;

        if (faction == "Humanoid")
        {
            ToggleHumanoid(ent, friendly);
            if (_net.IsServer)
                ApplyTargeting(ent);
            Dirty(ent.Owner, ent.Comp);
            return;
        }

        if (friendly)
            ent.Comp.FriendlyFactions.Add(faction);
        else
            ent.Comp.FriendlyFactions.Remove(faction);

        if (_net.IsServer)
            ApplyTargeting(ent);

        Dirty(ent.Owner, ent.Comp);
    }

    public void ToggleHumanoid(Entity<SentryTargetingComponent> ent, bool friendly)
    {
        if (friendly)
        {
            foreach (var faction in GetHumanoidFactions())
            {
                if (ent.Comp.FriendlyFactions.Add(faction))
                    ent.Comp.HumanoidAdded.Add(faction);
            }
        }
        else
        {
            foreach (var faction in ent.Comp.HumanoidAdded)
                ent.Comp.FriendlyFactions.Remove(faction);

            ent.Comp.HumanoidAdded.Clear();
        }
    }

    private void BuildFriendlyIff(SentryTargetingComponent comp)
    {
        _friendlyIffBuffer.Clear();

        foreach (var faction in comp.FriendlyFactions)
        {
            if (SentryFactionToIff.TryGetValue(faction, out var iff))
                _friendlyIffBuffer.Add(iff);
        }
    }

    private bool IsFriendlyByIff(EntityUid target)
    {
        _targetIffBuffer.Clear();
        var ev = new GetIFFFactionEvent(SlotFlags.IDCARD, _targetIffBuffer);
        RaiseLocalEvent(target, ref ev);

        foreach (var faction in _targetIffBuffer)
        {
            if (_friendlyIffBuffer.Contains(faction))
                return true;
        }

        return false;
    }

    public bool IsValidTarget(Entity<SentryTargetingComponent> sentry, EntityUid target)
    {
        if (!HasComp<UserIFFComponent>(target) && !HasComp<NpcFactionMemberComponent>(target))
            return false;

        // Unconfigured sentry targets no one.
        if (sentry.Comp.FriendlyFactions.Count == 0)
            return false;

        // NPC factions that should never be targeted: alliance-friendly + non-IFF-mapped FriendlyFactions
        if (TryComp<NpcFactionMemberComponent>(target, out var targetFaction))
        {
            foreach (var allianceFriendly in sentry.Comp.AllianceFriendlyNpcFactions)
            {
                if (targetFaction.Factions.Contains(allianceFriendly))
                    return false;
            }
            foreach (var faction in sentry.Comp.FriendlyFactions)
            {
                if (!SentryFactionToIff.ContainsKey(faction) && targetFaction.Factions.Contains(faction))
                    return false;
            }
        }

        BuildFriendlyIff(sentry.Comp);
        var friendly = IsFriendlyByIff(target);
        _friendlyIffBuffer.Clear();
        _targetIffBuffer.Clear();
        return !friendly;
    }

    public IEnumerable<EntityUid> GetNearbyIffHostiles(Entity<SentryTargetingComponent> ent, float range)
    {
        BuildFriendlyIff(ent.Comp);

        // Build a combined set of NPC factions that should never be targeted:
        // 1) FriendlyFactions entries that have no IFF mapping (e.g. AUWeYu)
        // 2) Alliance-friendly factions set by the alliance console
        _friendlyNpcFactionBuffer.Clear();
        foreach (var faction in ent.Comp.FriendlyFactions)
        {
            if (!SentryFactionToIff.ContainsKey(faction))
                _friendlyNpcFactionBuffer.Add(faction);
        }
        foreach (var faction in ent.Comp.AllianceFriendlyNpcFactions)
            _friendlyNpcFactionBuffer.Add(faction.Id);

        var coords = _xform.GetMapCoordinates(ent);

        _candidateLookupBuffer.Clear();
        _lookup.GetEntitiesInRange(coords, range, _userIffLookupBuffer);
        foreach (var target in _userIffLookupBuffer)
            _candidateLookupBuffer.Add(target.Owner);

        _lookup.GetEntitiesInRange(coords, range, _factionLookupBuffer);
        foreach (var target in _factionLookupBuffer)
            _candidateLookupBuffer.Add(target.Owner);

        foreach (var target in _candidateLookupBuffer)
        {
            if (target == ent.Owner)
                continue;

            if (IsFriendlyByIff(target))
                continue;

            if (_friendlyNpcFactionBuffer.Count > 0 &&
                TryComp<NpcFactionMemberComponent>(target, out var targetNpc))
            {
                var isFriendly = false;
                foreach (var f in targetNpc.Factions)
                {
                    if (_friendlyNpcFactionBuffer.Contains(f.Id))
                    {
                        isFriendly = true;
                        break;
                    }
                }
                if (isFriendly)
                    continue;
            }

            yield return target;
        }

        _candidateLookupBuffer.Clear();
        _userIffLookupBuffer.Clear();
        _factionLookupBuffer.Clear();
        _friendlyIffBuffer.Clear();
        _targetIffBuffer.Clear();
        _friendlyNpcFactionBuffer.Clear();
    }

    private void ApplyTargeting(Entity<SentryTargetingComponent> ent)
    {
        UpdateSentryIFF(ent);
    }

    private void UpdateSentryIFF(Entity<SentryTargetingComponent> ent)
    {
        if (!TryComp<UserIFFComponent>(ent.Owner, out var userIff))
            return;

        _iff.ClearUserFactions((ent.Owner, userIff));

        foreach (var faction in ent.Comp.FriendlyFactions)
        {
            if (SentryFactionToIff.TryGetValue(faction, out var iff))
                _iff.AddUserFaction((ent.Owner, userIff), iff);
        }
    }

    public IEnumerable<string> GetHumanoidFactions()
    {
        return SentryAllowedFactions;
    }

    public bool ContainsAllNonXeno(HashSet<string> friendlyFactions)
    {
        return GetHumanoidFactions().All(friendlyFactions.Contains);
    }

    /// <summary>
    /// Applies the provided set of alliance-friendly NPC factions to this sentry,
    /// replacing any previously applied alliance state.
    /// </summary>
    public void ApplyAllianceFactions(EntityUid sentryUid, SentryTargetingComponent targeting, IEnumerable<string> friendlyNpcFactions)
    {
        targeting.AllianceFriendlyNpcFactions.Clear();
        foreach (var f in friendlyNpcFactions)
            targeting.AllianceFriendlyNpcFactions.Add(f);
        Dirty(sentryUid, targeting);
    }

    /// <summary>
    /// Returns whether this sentry has the given side faction (e.g. "GOVFOR" or "OPFOR") in its friendly list.
    /// Used by the alliance console to determine which sentries belong to a given side.
    /// </summary>
    public bool HasSideFaction(SentryTargetingComponent targeting, string sideFaction)
    {
        return targeting.FriendlyFactions.Contains(sideFaction);
    }

    /// <summary>
    /// Applies alliance-friendly NPC factions to a sentry based on the provided global state dictionary.
    /// Only applies factions relevant to this sentry's own side.
    /// </summary>
    public void ApplyAllianceStateToSentry(EntityUid sentryUid, SentryTargetingComponent targeting,
        Dictionary<string, Dictionary<string, AllianceStatus>> globalState)
    {
        var friendly = new HashSet<string>();

        foreach (var (sideFaction, sideState) in globalState)
        {
            if (!targeting.FriendlyFactions.Contains(sideFaction))
                continue;

            foreach (var (npcFaction, status) in sideState)
            {
                if (status == AllianceStatus.Friendly)
                    friendly.Add(npcFaction);
            }
        }

        ApplyAllianceFactions(sentryUid, targeting, friendly);
    }

    /// <summary>
    /// Returns true if the sentry has been assigned a team faction.
    /// </summary>
    public bool IsConfigured(Entity<SentryTargetingComponent> ent)
    {
        return ent.Comp.FriendlyFactions.Count > 0;
    }

    /// <summary>
    /// Clears the sentry's faction assignment, returning it to idle (no-fire) state.
    /// </summary>
    public void ClearFactionAssignment(Entity<SentryTargetingComponent> ent)
    {
        ent.Comp.FriendlyFactions.Clear();
        ent.Comp.DeployedFriendlyFactions.Clear();
        ent.Comp.HumanoidAdded.Clear();

        if (_net.IsServer)
            ApplyTargeting(ent);

        Dirty(ent.Owner, ent.Comp);
    }

    /// <summary>
    /// Adds an alliance-friendly NPC faction to all deployed sentries matching the given side faction
    /// (e.g. "GOVFOR" or "OPFOR").  New sentries spawned after this will pick up the state via the
    /// <see cref="AllianceConsoleSystem"/> on their targeting component init.
    /// </summary>
    public void AddAllianceFriendlyFaction(string sideFaction, string npcFaction)
    {
        var query = AllEntityQuery<SentryTargetingComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.FriendlyFactions.Contains(sideFaction))
                continue;

            comp.AllianceFriendlyNpcFactions.Add(npcFaction);
            Dirty(uid, comp);
        }
    }

    /// <summary>
    /// Removes an alliance-friendly NPC faction from all deployed sentries matching the given side.
    /// </summary>
    public void RemoveAllianceFriendlyFaction(string sideFaction, string npcFaction)
    {
        var query = AllEntityQuery<SentryTargetingComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.FriendlyFactions.Contains(sideFaction))
                continue;

            comp.AllianceFriendlyNpcFactions.Remove(npcFaction);
            Dirty(uid, comp);
        }
    }
}
