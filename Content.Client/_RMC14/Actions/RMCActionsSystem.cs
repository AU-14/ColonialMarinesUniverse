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
        _sortEnt = null;
    }

    private void OnClientActionChanged(EntityUid action)
    {
        _sortEnt = null;
    }

    public void ActionsChanged(List<EntityUid?> actions)
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

        if (_player.LocalEntity is { } player &&
            TryComp(player, out RMCActionOrderComponent? order))
        {
            _localOrderId = order.Id;
            _localOrder = actionPrototypes.ToImmutableArray();
            _sortEnt = player;
        }

        var ev = new RMCActionOrderChangeEvent(actionPrototypes);
        RaiseNetworkEvent(ev);
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
            !TryGetOrder(orderComp, out var order))
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

        var actions = new Entity<ActionComponent>[order.Length];
        var extraActions = new List<Entity<ActionComponent>>();
        foreach (var action in clientActions)
        {
            if (!TryComp(action, out MetaDataComponent? meta) ||
                meta.EntityPrototype is not { } prototype)
            {
                extraActions.Add(action);
                continue;
            }

            var index = order.IndexOf(prototype.ID);
            if (index < 0)
            {
                extraActions.Add(action);
                continue;
            }

            actions[index] = action;
        }

        var assignments = new List<ActionsSystem.SlotAssignment>();
        var allActions = actions.Concat(extraActions).Where(a => a != default).ToArray();
        for (var i = 0; i < allActions.Length; i++)
        {
            assignments.Add(new ActionsSystem.SlotAssignment(0, (byte) i, allActions[i]));
        }

        _actions.SetAssignments(assignments);
    }

    private bool TryGetOrder(RMCActionOrderComponent orderComp, out ImmutableArray<EntProtoId> order)
    {
        if (_localOrderId == orderComp.Id &&
            _localOrder is { Length: > 0 } localOrder)
        {
            order = localOrder;
            return true;
        }

        if (orderComp.Order is { Length: > 0 } componentOrder)
        {
            order = componentOrder;
            return true;
        }

        order = default;
        return false;
    }
}
