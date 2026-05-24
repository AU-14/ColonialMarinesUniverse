using Content.Shared._RMC14.NPC;
using Content.Shared._RMC14.Sentry;
using Content.Shared._RMC14.Tools;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Robust.Shared.Network;

namespace Content.Shared.AU14.TurretFaction;

public sealed class TurretFactionSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCNPCSystem _rmcNpc = default!;
    [Dependency] private SharedSentryTargetingSystem _targeting = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SentryTargetingComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SentryTargetingComponent, TurretAssignFactionDoAfterEvent>(OnAssignFactionDoAfter);
        SubscribeLocalEvent<SentryTargetingComponent, TurretClearFactionDoAfterEvent>(OnClearFactionDoAfter);
    }

    private void OnInteractUsing(Entity<SentryTargetingComponent> ent, ref InteractUsingEvent args)
    {
        // SentrySystem handles entities with SentryComponent.
        if (HasComp<SentryComponent>(ent))
            return;

        if (!HasComp<MultitoolComponent>(args.Used))
            return;

        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(1),
            new TurretAssignFactionDoAfterEvent(), ent)
        {
            BreakOnMove = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnAssignFactionDoAfter(Entity<SentryTargetingComponent> ent, ref TurretAssignFactionDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        _targeting.ApplyDeployerFactions(ent.Owner, args.User);

        if (_net.IsServer)
            SyncNpcFaction(ent);

        var ev = new SentryFactionAssignedEvent(args.User);
        RaiseLocalEvent(ent.Owner, ref ev);

        if (_net.IsServer)
            _rmcNpc.WakeNPC(ent.Owner);

        var msg = Loc.GetString("rmc-sentry-faction-assigned", ("sentry", ent.Owner));
        _popup.PopupPredicted(msg, msg, ent.Owner, args.User);
    }

    private void OnClearFactionDoAfter(Entity<SentryTargetingComponent> ent, ref TurretClearFactionDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        _targeting.ClearFactionAssignment(ent);

        if (_net.IsServer)
        {
            ClearSideFactions(ent.Owner);
            _rmcNpc.SleepNPC(ent.Owner);
        }

        var msg = Loc.GetString("rmc-sentry-faction-cleared", ("sentry", ent.Owner));
        _popup.PopupPredicted(msg, msg, ent.Owner, args.User);
    }

    // Sets NpcFactionMember to match the turret's assigned factions.
    private void SyncNpcFaction(Entity<SentryTargetingComponent> ent)
    {
        _npcFaction.ClearFactions(ent.Owner);
        foreach (var faction in ent.Comp.FriendlyFactions)
            _npcFaction.AddFaction(ent.Owner, faction);
    }

    private void ClearSideFactions(EntityUid uid)
    {
        _npcFaction.ClearFactions(uid);
    }
}
