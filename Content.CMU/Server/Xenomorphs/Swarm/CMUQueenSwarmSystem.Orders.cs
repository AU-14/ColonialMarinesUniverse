using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared.CMU14.Xenomorphs.Swarm;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Xenomorphs.Swarm;

public sealed partial class CMUQueenSwarmSystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private const int MaxFlags = 64;
    private const int MaxWaypoints = 8;

    private void InitializeOrders()
    {
        SubscribeLocalEvent<CMUQueenSwarmComponent, CMUSwarmMenuActionEvent>(OnMenu);
        SubscribeLocalEvent<CMUQueenSwarmComponent, CMUPlaceSwarmOrderActionEvent>(OnPlaceOrder);
        SubscribeLocalEvent<CMUQueenSwarmComponent, XenoOvipositorChangedEvent>(OnOviChanged);
        Subs.BuiEvents<CMUQueenSwarmComponent>(CMUSwarmUiKey.Key, subs =>
        {
            subs.Event<CMUSwarmCommandMessage>(OnCommand);
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
        });
    }

    private void OnOviChanged(Entity<CMUQueenSwarmComponent> ent, ref XenoOvipositorChangedEvent args)
    {
        SetCommandAccess(ent, args.Attached);
    }

    private void SetCommandAccess(EntityUid queen, bool attached)
    {
        var commander = EnsureComp<CMUSwarmCommanderComponent>(queen);
        foreach (var action in commander.Actions)
            _actions.SetEnabled(action, attached);
        if (attached)
            return;

        commander.Selection.Clear();
        commander.Pending = null;
        _ui.CloseUi(queen, CMUSwarmUiKey.Key);
        // Hive orders deliberately outlive this queen's access to the command UI.
    }

    private void OnMenu(Entity<CMUQueenSwarmComponent> ent, ref CMUSwarmMenuActionEvent args)
    {
        if (args.Handled || !TryController(ent, out _, out var controller))
            return;
        args.Handled = true;
        UpdateSwarmUi(ent, controller);
        _ui.TryOpenUi(ent.Owner, CMUSwarmUiKey.Key, ent.Owner);
    }

    private void OnUiOpened(Entity<CMUQueenSwarmComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.Actor != ent.Owner || !TryController(ent, out _, out var controller))
        {
            _ui.CloseUi(ent.Owner, CMUSwarmUiKey.Key);
            return;
        }
        UpdateSwarmUi(ent, controller);
    }

    private void OnCommand(Entity<CMUQueenSwarmComponent> ent, ref CMUSwarmCommandMessage args)
    {
        if (args.Actor != ent.Owner || !TryController(ent, out var hive, out var controller) ||
            args.Members.Count > 256 || args.Squad is < 0 or > 4 || !Enum.IsDefined(args.Command) ||
            !Enum.IsDefined(args.Order) || !Enum.IsDefined(args.Aggression) || !Enum.IsDefined(args.Structure))
            return;

        var commander = EnsureComp<CMUSwarmCommanderComponent>(ent);
        commander.Pending = null;
        if (commander.NextCommand > _timing.CurTime)
            return;
        commander.NextCommand = _timing.CurTime + TimeSpan.FromMilliseconds(100);
        commander.Selection.Clear();
        var unique = new HashSet<EntityUid>();
        foreach (var net in args.Members)
        {
            if (!TryGetEntity(net, out var uid) || uid is not { } member || !unique.Add(member) ||
                !controller.Members.Contains(member) || !_mobState.IsAlive(member) ||
                _hive.GetHive(member)?.Owner != hive || HasComp<ActorComponent>(member))
                continue;
            commander.Selection.Add(member);
        }

        if (args.Command == CMUSwarmCommand.ClearMarkers)
        {
            foreach (var flag in controller.Flags)
            {
                if (TryComp<CMUSwarmOrderFlagComponent>(flag, out var marker) && IsPlayerMarker(marker.Kind))
                    QueueDel(flag);
            }
        }
        else if (args.Command == CMUSwarmCommand.Prepare)
        {
            if (args.Order == CMUSwarmOrderKind.Idle ||
                (commander.Selection.Count == 0 && !IsPlayerMarker(args.Order)))
                return;
            commander.Pending = args.Order;
            commander.Structure = args.Structure;
            commander.Append = args.Append;
        }
        else
        {
            foreach (var member in commander.Selection)
            {
                var agent = GetAgent(member, controller);
                switch (args.Command)
                {
                    case CMUSwarmCommand.AssignSquad:
                        agent.Squad = args.Squad;
                        break;
                    case CMUSwarmCommand.Aggression:
                        agent.Aggression = args.Aggression;
                        StopAgent(member, agent);
                        break;
                    case CMUSwarmCommand.Release:
                        SetOrder(member, agent, new CMUSwarmOrder { Kind = CMUSwarmOrderKind.Idle });
                        agent.Home = Transform(member).Coordinates;
                        break;
                }
            }
        }
        controller.NextThink = TimeSpan.Zero;
        UpdateSwarmUi(ent, controller);
    }

    private void OnPlaceOrder(Entity<CMUQueenSwarmComponent> ent, ref CMUPlaceSwarmOrderActionEvent args)
    {
        if (args.Handled || !TryController(ent, out var hive, out var controller) ||
            !TryComp<CMUSwarmCommanderComponent>(ent, out var commander) ||
            commander.Pending is not { } kind || kind == CMUSwarmOrderKind.Follow || !CanCommandAt(ent, args.Target))
            return;

        commander.Pending = null;
        PruneFlags(controller);
        if (controller.Flags.Count >= MaxFlags)
        {
            _popup.PopupCursor(Loc.GetString("cmu-swarm-flag-limit"), ent);
            return;
        }

        var selected = commander.Selection.FindAll(member => !TerminatingOrDeleted(member) &&
            _mobState.IsAlive(member) && _hive.GetHive(member)?.Owner == hive && !HasComp<ActorComponent>(member));
        if (!IsPlayerMarker(kind) && selected.Count == 0)
            return;

        if (kind == CMUSwarmOrderKind.Patrol && commander.Append &&
            GetAgent(selected[0], controller).Order is { Kind: CMUSwarmOrderKind.Patrol } route &&
            route.Flags.Count >= MaxWaypoints)
        {
            _popup.PopupCursor(Loc.GetString("cmu-swarm-patrol-limit"), ent);
            return;
        }

        var flag = CreateFlag(hive, controller, kind, args.Target);
        if (!IsPlayerMarker(kind))
        {
            var order = new CMUSwarmOrder { Kind = kind, Structure = commander.Structure };
            // Copy the route, not the shared order. Editing a subset must leave unselected units alone.
            if (kind == CMUSwarmOrderKind.Patrol && commander.Append &&
                GetAgent(selected[0], controller).Order is { Kind: CMUSwarmOrderKind.Patrol } previous)
            {
                foreach (var waypoint in previous.Flags)
                {
                    if (!TerminatingOrDeleted(waypoint) && order.Flags.Count < MaxWaypoints - 1)
                        order.Flags.Add(waypoint);
                }
            }
            order.Flags.Add(flag);
            foreach (var member in selected)
                SetOrder(member, GetAgent(member, controller), order);
        }
        controller.NextThink = TimeSpan.Zero;
        UpdateSwarmUi(ent, controller);
        args.Handled = true;
    }

    private EntityUid CreateFlag(EntityUid hive, CMUSwarmControllerComponent controller,
        CMUSwarmOrderKind kind, EntityCoordinates coordinates)
    {
        var flag = Spawn(controller.MarkerPrototype, coordinates);
        var data = EnsureComp<CMUSwarmOrderFlagComponent>(flag);
        data.Hive = hive;
        data.Kind = kind;
        data.Number = controller.Flags.Count + 1;
        Dirty(flag, data);
        controller.Flags.Add(flag);
        return flag;
    }

    private void PruneFlags(CMUSwarmControllerComponent controller)
    {
        var used = new HashSet<EntityUid>();
        if (controller.Marker is { } marker)
            used.Add(marker);
        foreach (var member in controller.Members)
        {
            if (TryComp<CMUSwarmAgentComponent>(member, out var agent) && agent.Order is { } order &&
                _mobState.IsAlive(member))
                used.UnionWith(order.Flags);
        }
        for (var i = controller.Flags.Count - 1; i >= 0; i--)
        {
            var flag = controller.Flags[i];
            if (!TerminatingOrDeleted(flag) && (used.Contains(flag) ||
                TryComp<CMUSwarmOrderFlagComponent>(flag, out var data) && IsPlayerMarker(data.Kind)))
                continue;
            QueueDel(flag);
            controller.Flags.RemoveAt(i);
        }
    }

    private static bool IsPlayerMarker(CMUSwarmOrderKind kind) =>
        kind is CMUSwarmOrderKind.Rally or CMUSwarmOrderKind.Attack or CMUSwarmOrderKind.Defend;

    private CMUSwarmAgentComponent GetAgent(EntityUid member, CMUSwarmControllerComponent controller)
    {
        var agent = EnsureComp<CMUSwarmAgentComponent>(member);
        if (agent.Number == 0)
            agent.Number = ++controller.NextNumber;
        agent.Home ??= Transform(member).Coordinates;
        return agent;
    }

    private void SetOrder(EntityUid member, CMUSwarmAgentComponent agent, CMUSwarmOrder order)
    {
        StopAgent(member, agent);
        agent.Order = order;
        agent.FormationSlot = order.NextSlot++;
        agent.Waypoint = 0;
        agent.RestAt = TimeSpan.Zero;
        agent.PauseUntil = TimeSpan.Zero;
        agent.NextWork = TimeSpan.Zero;
        agent.MoveUntil = TimeSpan.Zero;
    }

    private void StopAgent(EntityUid member, CMUSwarmAgentComponent agent)
    {
        CancelWork(member, agent);
        Wake(member, agent);
        agent.Destination = null;
        RemComp<Content.Server.NPC.Components.NPCMeleeCombatComponent>(member);
        RemComp<CMUSwarmTraversalComponent>(member);
        _steering.Unregister(member);
    }

    private void UpdateSwarmUi(EntityUid queen, CMUSwarmControllerComponent controller)
    {
        var members = new List<CMUSwarmMemberInfo>();
        foreach (var member in controller.Members)
        {
            if (TerminatingOrDeleted(member) || !_mobState.IsAlive(member))
                continue;
            var agent = GetAgent(member, controller);
            members.Add(new CMUSwarmMemberInfo(GetNetEntity(member), agent.Number, agent.Squad,
                agent.Order?.Kind ?? CMUSwarmOrderKind.Guard, agent.Aggression,
                HasComp<XenoRestingComponent>(member), WorkRunning(member)));
        }
        _ui.SetUiState(queen, CMUSwarmUiKey.Key, new CMUSwarmUiState(members,
            Math.Max(0, (int) Math.Ceiling((controller.NextSummon - _timing.CurTime).TotalSeconds))));
    }

    private void UpdateCommanders()
    {
        var query = EntityQueryEnumerator<CMUQueenSwarmComponent, CMUSwarmCommanderComponent>();
        while (query.MoveNext(out var queen, out _, out var commander))
        {
            if (!_ui.IsUiOpen(queen, CMUSwarmUiKey.Key) || commander.NextUi > _timing.CurTime)
                continue;
            commander.NextUi = _timing.CurTime + TimeSpan.FromSeconds(2);
            if (!TryController(queen, out _, out var controller))
            {
                SetCommandAccess(queen, false);
                continue;
            }
            UpdateSwarmUi(queen, controller);
        }
    }
}
