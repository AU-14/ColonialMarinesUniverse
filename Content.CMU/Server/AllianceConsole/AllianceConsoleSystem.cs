using Content.Server.Administration.Logs;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.AU14.AllianceConsole;
using Content.Shared.Database;
using Content.Shared.Inventory;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared._RMC14.Sentry;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.AllianceConsole;

public sealed class AllianceConsoleSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private SharedSentryTargetingSystem _sentryTargeting = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    // Maps side ("GOVFOR"/"OPFOR") → npcFactionId → status.
    // Persists across sentry spawns so new sentries pick up current state.
    private readonly Dictionary<string, Dictionary<string, AllianceStatus>> _globalState = new()
    {
        { "GOVFOR", new Dictionary<string, AllianceStatus>() },
        { "OPFOR",  new Dictionary<string, AllianceStatus>() },
    };

    // Tracks which ID card entities received access grants: (side, targetFaction) → EntityUid set.
    private readonly Dictionary<(string Side, string TargetFaction), HashSet<EntityUid>> _grantedIdCards = new();

    private static readonly ProtoId<AccessLevelPrototype>[] GovforRiflemanTags =
    {
        "AU14AccessGovfor",
        "AU14AccessGovforSquad",
    };

    private static readonly ProtoId<AccessLevelPrototype>[] OpforRiflemanTags =
    {
        "AU14AccessOpfor",
        "AU14AccessOpforSquad",
    };

    public override void Initialize()
    {
        SubscribeLocalEvent<AllianceConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<AllianceConsoleComponent, AllianceConsoleSetFactionStatusMsg>(OnSetFactionStatus);
        // Covers both deployable sentries (SentryComponent) and static turrets (SentryTargetingComponent only).
        SubscribeLocalEvent<SentryTargetingComponent, SentryFactionAssignedEvent>(OnSentryFactionAssigned);
    }

    private void OnUiOpened(Entity<AllianceConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<AllianceConsoleComponent> ent)
    {
        var state = new AllianceConsoleBuiState(
            ent.Comp.Faction,
            new Dictionary<string, AllianceStatus>(ent.Comp.FactionStatuses),
            new List<string>(ent.Comp.ControllableFactions));
        _ui.SetUiState(ent.Owner, AllianceConsoleUiKey.Key, state);
    }

    private void OnSetFactionStatus(Entity<AllianceConsoleComponent> ent, ref AllianceConsoleSetFactionStatusMsg args)
    {
        if (!ent.Comp.ControllableFactions.Contains(args.TargetFaction))
            return;

        var oldStatus = ent.Comp.FactionStatuses.TryGetValue(args.TargetFaction, out var prev)
            ? prev
            : AllianceStatus.Neutral;

        if (oldStatus == args.Status)
            return;

        ent.Comp.FactionStatuses[args.TargetFaction] = args.Status;
        Dirty(ent);

        var sideFactionUpper = ent.Comp.Faction.ToUpperInvariant();
        ApplyFactionStatus(sideFactionUpper, args.TargetFaction, oldStatus, args.Status);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.Actor)} set {args.TargetFaction} to {args.Status} on {ToPrettyString(ent)} ({ent.Comp.Faction})");

        UpdateUi(ent);
    }

    private void OnSentryFactionAssigned(Entity<SentryTargetingComponent> ent, ref SentryFactionAssignedEvent args)
    {
        ApplyAllianceToSentry(ent.Owner, ent.Comp);
    }

    /// <summary>
    /// Called after a sentry/turret has been assigned to a faction.
    /// Applies the current global alliance state to that entity.
    /// </summary>
    public void ApplyAllianceToSentry(EntityUid sentryUid, SentryTargetingComponent targeting)
    {
        _sentryTargeting.ApplyAllianceStateToSentry(sentryUid, targeting, _globalState);
    }

    private void ApplyFactionStatus(string sideFactionUpper, string targetFaction, AllianceStatus oldStatus, AllianceStatus newStatus)
    {
        if (!_globalState.TryGetValue(sideFactionUpper, out var sideState))
            return;

        sideState[targetFaction] = newStatus;

        // NPC faction relationship changes (bi-directional).
        if (oldStatus == AllianceStatus.Friendly && newStatus != AllianceStatus.Friendly)
        {
            _npcFaction.RealMakeNeutral(sideFactionUpper, targetFaction);
            _npcFaction.RealMakeNeutral(targetFaction, sideFactionUpper);
        }
        else if (newStatus == AllianceStatus.Hostile && oldStatus != AllianceStatus.Hostile)
        {
            _npcFaction.RealMakeHostile(sideFactionUpper, targetFaction);
            _npcFaction.RealMakeHostile(targetFaction, sideFactionUpper);
        }
        else if (newStatus == AllianceStatus.Friendly)
        {
            _npcFaction.RealMakeFriendly(sideFactionUpper, targetFaction);
            _npcFaction.RealMakeFriendly(targetFaction, sideFactionUpper);
        }

        // Sentry targeting — update all existing deployed sentries on this side.
        if (newStatus == AllianceStatus.Friendly)
        {
            _sentryTargeting.AddAllianceFriendlyFaction(sideFactionUpper, targetFaction);
            GrantAllianceAccess(sideFactionUpper, targetFaction);
        }
        else
        {
            _sentryTargeting.RemoveAllianceFriendlyFaction(sideFactionUpper, targetFaction);
            if (oldStatus == AllianceStatus.Friendly)
                RevokeAllianceAccess(sideFactionUpper, targetFaction);
        }
    }

    private void GrantAllianceAccess(string sideFaction, string targetFaction)
    {
        var tags = GetSideAccessTags(sideFaction);
        if (tags.Length == 0)
            return;

        var key = (sideFaction, targetFaction);
        if (!_grantedIdCards.TryGetValue(key, out var granted))
        {
            granted = new HashSet<EntityUid>();
            _grantedIdCards[key] = granted;
        }

        var query = AllEntityQuery<NpcFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var npcFaction))
        {
            if (!npcFaction.Factions.Contains(targetFaction))
                continue;

            if (!_inventory.TryGetSlotEntity(uid, "id", out var idCard))
                continue;

            if (!TryComp<AccessComponent>(idCard, out var access))
                continue;

            foreach (var tag in tags)
                access.Tags.Add(tag);
            Dirty(idCard.Value, access);
            granted.Add(idCard.Value);
        }
    }

    private void RevokeAllianceAccess(string sideFaction, string targetFaction)
    {
        var key = (sideFaction, targetFaction);
        if (!_grantedIdCards.TryGetValue(key, out var granted))
            return;

        var tags = GetSideAccessTags(sideFaction);

        foreach (var uid in granted)
        {
            if (!TryComp<AccessComponent>(uid, out var access))
                continue;

            foreach (var tag in tags)
                access.Tags.Remove(tag);
            Dirty(uid, access);
        }

        granted.RemoveWhere(uid => !EntityManager.EntityExists(uid));
        _grantedIdCards.Remove(key);
    }

    private static ProtoId<AccessLevelPrototype>[] GetSideAccessTags(string sideFaction)
    {
        return sideFaction switch
        {
            "GOVFOR" => GovforRiflemanTags,
            "OPFOR"  => OpforRiflemanTags,
            _        => Array.Empty<ProtoId<AccessLevelPrototype>>(),
        };
    }

    /// <summary>
    /// Returns the current alliance status for a faction as seen from a given side.
    /// </summary>
    public AllianceStatus GetStatus(string sideFactionUpper, string targetFaction)
    {
        if (_globalState.TryGetValue(sideFactionUpper, out var sideState) &&
            sideState.TryGetValue(targetFaction, out var status))
            return status;
        return AllianceStatus.Neutral;
    }
}
