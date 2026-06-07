using System.Collections.Immutable;
using System.Linq;
using Content.Server.Actions;
using Content.Shared._RMC14.Actions;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Actions;

public sealed partial class RMCActionsSystem : SharedRMCActionsSystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private RMCActionsManager _manager = default!;

    private readonly HashSet<EntProtoId> _actionsPresent = new();
    private readonly Dictionary<(NetUserId User, EntProtoId Id), RMCActionOrderData> _toUpdate = new();

    public override void Initialize()
    {
        base.Initialize();

        _manager.OnLoaded += OnLoaded;

        SubscribeNetworkEvent<RMCActionOrderChangeEvent>(OnActionOrder);

        SubscribeLocalEvent<RMCActionOrderComponent, PlayerAttachedEvent>(OnOrderAttached);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _manager.OnLoaded -= OnLoaded;
    }

    private void OnActionOrder(RMCActionOrderChangeEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } ent)
            return;

        if (!TryComp(ent, out RMCActionOrderComponent? order) ||
            string.IsNullOrWhiteSpace(order.Id))
        {
            return;
        }

        _actionsPresent.Clear();
        foreach (var action in _actions.GetActions(ent))
        {
            if (Prototype(action)?.ID is not { } proto)
                continue;

            _actionsPresent.Add(proto);
        }

        FilterUnavailableActions(msg.Actions);
        FilterUnavailableActions(msg.HiddenActions);

        var visibleActions = msg.Actions.ToHashSet();
        msg.HiddenActions.RemoveAll(visibleActions.Contains);

        _toUpdate[(args.SenderSession.UserId, order.Id)] = new RMCActionOrderData(
            msg.Actions.ToImmutableArray(),
            msg.HiddenActions.ToImmutableArray(),
            msg.HiddenActionsKnown);
    }

    private void FilterUnavailableActions(List<EntProtoId> actions)
    {
        for (var i = actions.Count - 1; i >= 0; i--)
        {
            var action = actions[i];
            if (!_actionsPresent.Contains(action))
                actions.RemoveAt(i);
        }
    }

    private void OnOrderAttached(Entity<RMCActionOrderComponent> ent, ref PlayerAttachedEvent args)
    {
        if (_manager.GetOrder(args.Player.UserId, ent.Comp.Id) is not { } order)
            return;

        ent.Comp.Order = order.Actions;
        ent.Comp.HiddenActions = order.HiddenActions;
        ent.Comp.HiddenActionsKnown = order.HiddenActionsKnown;
        Dirty(ent);
    }

    private void OnLoaded(ICommonSession user, Dictionary<EntProtoId, RMCActionOrderData>? allActions)
    {
        if (user.Status != SessionStatus.Connected && user.Status != SessionStatus.InGame)
            return;

        if (!TryComp(user.AttachedEntity, out RMCActionOrderComponent? order) ||
            allActions == null ||
            !allActions.TryGetValue(order.Id, out var actions))
        {
            return;
        }

        order.Order = actions.Actions;
        order.HiddenActions = actions.HiddenActions;
        order.HiddenActionsKnown = actions.HiddenActionsKnown;
        Dirty(user.AttachedEntity.Value, order);
        var ev = new RMCActionOrderLoadedEvent(
            actions.Actions.ToList(),
            actions.HiddenActions.ToList(),
            actions.HiddenActionsKnown);
        RaiseNetworkEvent(ev, user);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        try
        {
            foreach (var ((user, id), order) in _toUpdate)
            {
                try
                {
                    _manager.SetOrder(user, id, order);
                }
                catch (Exception e)
                {
                    Log.Error($"Error saving action order for {user}:\n{e}");
                }
            }
        }
        finally
        {
            _toUpdate.Clear();
        }
    }
}
