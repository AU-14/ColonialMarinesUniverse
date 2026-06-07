using System.Collections.Immutable;
using System.Linq;
using Content.Client.Actions;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Actions.Components;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._RMC14.Actions;

public sealed partial class RMCActionsSystem : SharedRMCActionsSystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private IPlayerManager _player = default!;

    private EntityUid? _sortEnt;
    private EntProtoId? _localOrderId;
    private ImmutableArray<EntProtoId>? _localOrder;
    private ImmutableArray<EntProtoId> _localHiddenActions = ImmutableArray<EntProtoId>.Empty;
    private bool _localHiddenActionsKnown;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RMCActionOrderLoadedEvent>(OnActionOrderLoaded);
        SubscribeLocalEvent<RMCActionOrderComponent, AfterAutoHandleStateEvent>(OnActionOrderState);

        _actions.OnActionAdded += OnClientActionChanged;
        _actions.OnActionRemoved += OnClientActionChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _actions.OnActionAdded -= OnClientActionChanged;
        _actions.OnActionRemoved -= OnClientActionChanged;
    }

    private void OnActionOrderLoaded(RMCActionOrderLoadedEvent ev)
    {
        if (_player.LocalEntity is { } player &&
            TryComp(player, out RMCActionOrderComponent? order))
        {
            _localOrderId = order.Id;
            _localOrder = ev.Actions.ToImmutableArray();
            _localHiddenActions = ev.HiddenActions.ToImmutableArray();
            _localHiddenActionsKnown = ev.HiddenActionsKnown;
        }

        // Re-trigger reordering
        _sortEnt = null;
    }

    private void OnActionOrderState(Entity<RMCActionOrderComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_player.LocalEntity != ent.Owner)
            return;

        _localOrderId = ent.Comp.Id;
        _localOrder = ent.Comp.Order;
        _localHiddenActions = ent.Comp.HiddenActions ?? ImmutableArray<EntProtoId>.Empty;
        _localHiddenActionsKnown = ent.Comp.HiddenActionsKnown;
        _sortEnt = null;
    }

    private void OnClientActionChanged(EntityUid action)
    {
        _sortEnt = null;
    }

    public void ActionsChanged(List<EntityUid?> actions)
    {
        var actionPrototypes = GetActionPrototypes(actions);
        var clientActionPrototypes = GetActionPrototypes(_actions.GetClientActions().Select(action => (EntityUid?) action.Owner));
        var hiddenActions = GetHiddenActionPrototypes(clientActionPrototypes, actionPrototypes);

        if (_player.LocalEntity is { } player &&
            TryComp(player, out RMCActionOrderComponent? order))
        {
            _localOrderId = order.Id;
            _localOrder = actionPrototypes.ToImmutableArray();
            _localHiddenActions = hiddenActions.ToImmutableArray();
            _localHiddenActionsKnown = true;
            _sortEnt = player;
        }

        var ev = new RMCActionOrderChangeEvent(actionPrototypes, hiddenActions, true);
        RaiseNetworkEvent(ev);
    }

    private List<EntProtoId> GetActionPrototypes(IEnumerable<EntityUid?> actions)
    {
        var actionPrototypes = new List<EntProtoId>();
        foreach (var action in actions)
        {
            if (action is not { } actionUid || !Exists(actionUid))
                continue;

            if (!TryComp(actionUid, out MetaDataComponent? meta) ||
                meta.EntityPrototype is not { } prototype)
            {
                continue;
            }

            actionPrototypes.Add(prototype.ID);
        }

        return actionPrototypes;
    }

    internal static List<EntProtoId> GetHiddenActionPrototypes(
        IReadOnlyCollection<EntProtoId> currentActions,
        IReadOnlyCollection<EntProtoId> visibleActions)
    {
        var visible = visibleActions.ToHashSet();
        var hidden = new List<EntProtoId>();

        foreach (var action in currentActions)
        {
            if (!visible.Contains(action))
                hidden.Add(action);
        }

        return hidden;
    }

    private void SortDefault(EntityUid player)
    {
        if (!TryComp(player, out XenoComponent? xeno))
            return;

        foreach (var (_, actionId) in xeno.Actions)
        {
            if (!actionId.IsValid())
                return;
        }

        _sortEnt = player;

        var actions = new List<Entity<ActionComponent>>();
        foreach (var action in _actions.GetActions(player))
        {
            actions.Add(action);
        }

        var xenoActions = xeno.Actions.Values.ToList();
        actions.Sort((a, b) =>
        {
            var aXeno = xenoActions.FindIndex(e => e == a.Owner);
            var bXeno = xenoActions.FindIndex(e => e == b.Owner);
            if (aXeno != -1 && bXeno != -1)
                return aXeno - bXeno;

            return ActionsSystem.ActionComparer((a, a), (b, b));
        });

        var assignments = actions.Select((t, i) => new ActionsSystem.SlotAssignment(0, (byte) i, t)).ToList();
        _actions.SetAssignments(assignments);
    }

    public override void Update(float frameTime)
    {
        if (_player.LocalEntity is not { } player)
            return;

        if (_sortEnt == player)
            return;

        _sortEnt = null;

        if (!TryComp(player, out RMCActionOrderComponent? orderComp) ||
            !TryGetOrder(orderComp, out var order, out var hiddenActions))
        {
            SortDefault(player);
            return;
        }

        var clientActions = _actions.GetClientActions().ToArray();
        foreach (var action in clientActions)
        {
            if (!action.Owner.IsValid())
                return;
        }

        _sortEnt = player;

        if (hiddenActions == null)
        {
            hiddenActions = UpgradeHistoricalHiddenActions(orderComp.Id, order, clientActions);
        }

        var allActions = ReconcileActionOrder(
            clientActions,
            order,
            hiddenActions,
            action => Prototype(action)?.ID);

        var assignments = new List<ActionsSystem.SlotAssignment>();
        for (var i = 0; i < allActions.Count; i++)
        {
            assignments.Add(new ActionsSystem.SlotAssignment(0, (byte) i, allActions[i]));
        }

        _actions.SetAssignments(assignments);
    }

    private ImmutableArray<EntProtoId> UpgradeHistoricalHiddenActions(
        EntProtoId orderId,
        ImmutableArray<EntProtoId> order,
        IReadOnlyCollection<Entity<ActionComponent>> clientActions)
    {
        var currentActionPrototypes = GetActionPrototypes(clientActions.Select(action => (EntityUid?) action.Owner));
        var hiddenActions = GetHiddenActionPrototypes(currentActionPrototypes, order).ToImmutableArray();

        _localOrderId = orderId;
        _localOrder = order;
        _localHiddenActions = hiddenActions;
        _localHiddenActionsKnown = true;

        var ev = new RMCActionOrderChangeEvent(order.ToList(), hiddenActions.ToList(), true);
        RaiseNetworkEvent(ev);

        return hiddenActions;
    }

    internal static List<TAction> ReconcileActionOrder<TAction>(
        IReadOnlyList<TAction> currentActions,
        ImmutableArray<EntProtoId> order,
        ImmutableArray<EntProtoId>? hiddenActions,
        Func<TAction, EntProtoId?> prototypeSelector)
    {
        var used = new bool[currentActions.Count];
        var ordered = new List<TAction>();

        foreach (var orderedPrototype in order)
        {
            for (var i = 0; i < currentActions.Count; i++)
            {
                if (used[i] ||
                    prototypeSelector(currentActions[i]) != orderedPrototype)
                {
                    continue;
                }

                ordered.Add(currentActions[i]);
                used[i] = true;
                break;
            }
        }

        HashSet<EntProtoId>? hidden = null;
        if (hiddenActions is { } knownHiddenActions)
            hidden = knownHiddenActions.ToHashSet();

        for (var i = 0; i < currentActions.Count; i++)
        {
            if (used[i])
                continue;

            var prototype = prototypeSelector(currentActions[i]);
            if (prototype == null)
            {
                ordered.Add(currentActions[i]);
                continue;
            }

            if (hidden == null ||
                hidden.Contains(prototype.Value))
            {
                continue;
            }

            ordered.Add(currentActions[i]);
        }

        return ordered;
    }

    private bool TryGetOrder(
        RMCActionOrderComponent orderComp,
        out ImmutableArray<EntProtoId> order,
        out ImmutableArray<EntProtoId>? hiddenActions)
    {
        if (_localOrderId == orderComp.Id &&
            _localOrder is { } localOrder)
        {
            order = localOrder;
            hiddenActions = _localHiddenActionsKnown ? _localHiddenActions : null;
            return true;
        }

        if (orderComp.Order is { } componentOrder)
        {
            order = componentOrder;
            hiddenActions = orderComp.HiddenActionsKnown
                ? orderComp.HiddenActions ?? ImmutableArray<EntProtoId>.Empty
                : null;
            return true;
        }

        order = default;
        hiddenActions = null;
        return false;
    }
}
