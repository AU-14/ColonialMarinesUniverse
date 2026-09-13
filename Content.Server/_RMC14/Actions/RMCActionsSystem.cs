using System.Collections.Immutable;
using System.Linq;
using Content.Shared._RMC14.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Actions;

public sealed partial class RMCActionsSystem : SharedRMCActionsSystem
{
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private RMCActionsManager _manager = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;

    private readonly Dictionary<(NetUserId User, EntProtoId Id), RMCActionOrderData> _toUpdate = new();
    private string _actionComponentName = string.Empty;

    public override void Initialize()
    {
        base.Initialize();

        _actionComponentName = _componentFactory.GetComponentName<ActionComponent>();
        _manager.OnLoaded += OnLoaded;

        SubscribeNetworkEvent<RMCActionOrderChangeEvent>(OnActionOrder);

        SubscribeLocalEvent<RMCActionOrderComponent, ComponentStartup>(OnOrderStartup); // CMU14
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

        FilterInvalidActions(msg.Actions);
        FilterInvalidActions(msg.HiddenActions);

        var visibleActions = msg.Actions.ToHashSet();
        msg.HiddenActions.RemoveAll(visibleActions.Contains);

        _toUpdate[(args.SenderSession.UserId, order.Id)] = new RMCActionOrderData(
            msg.Actions.ToImmutableArray(),
            msg.HiddenActions.ToImmutableArray(),
            msg.HiddenActionsKnown);
    }

    private void FilterInvalidActions(List<EntProtoId> actions)
    {
        for (var i = actions.Count - 1; i >= 0; i--)
        {
            var action = actions[i];
            if (!_prototypes.TryIndex<EntityPrototype>(action, out var prototype) ||
                !prototype.Components.ContainsKey(_actionComponentName))
            {
                actions.RemoveAt(i);
            }
        }
    }

    // CMU14 method
    private void OnOrderStartup(Entity<RMCActionOrderComponent> ent, ref ComponentStartup args)
    {
        // Entities swap this component out to change which order they use, e.g. queen maturing into extra abilities.
        if (_player.TryGetSessionByEntity(ent, out var player))
            LoadOrder(ent, player);
    }

    // CMU14 method
    private void OnOrderAttached(Entity<RMCActionOrderComponent> ent, ref PlayerAttachedEvent args)
    {
        LoadOrder(ent, args.Player);
    }

    // CMU14 method
    private void LoadOrder(Entity<RMCActionOrderComponent> ent, ICommonSession player)
    {
        if (_manager.GetOrder(player.UserId, ent.Comp.Id) is not { } order)
            return;

        ent.Comp.Order = order.Actions;
        ent.Comp.HiddenActions = order.HiddenActions;
        ent.Comp.HiddenActionsKnown = order.HiddenActionsKnown;
        Dirty(ent);

        // The actions saved under this id are not the ones the client sorted itself by, so make it sort again.
        var ev = new RMCActionOrderLoadedEvent(
            order.Actions.ToList(),
            order.HiddenActions.ToList(),
            order.HiddenActionsKnown);
        RaiseNetworkEvent(ev, player);
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
