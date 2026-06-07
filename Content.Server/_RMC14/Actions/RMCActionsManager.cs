using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._RMC14.Actions;
using Robust.Shared.Asynchronous;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Serilog;

namespace Content.Server._RMC14.Actions;

public sealed partial class RMCActionsManager : IPostInjectInit
{
    private const string HiddenActionsMarker = "__rmc_hidden_actions_v1";
    private const string HiddenActionPrefix = "hidden:";

    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private UserDbDataManager _userDb = default!;
    [Dependency] private ITaskManager _task = default!;

    public event Action<ICommonSession, Dictionary<EntProtoId, RMCActionOrderData>?>? OnLoaded;

    private ISawmill _log = null!;
    private readonly Dictionary<NetUserId, Dictionary<EntProtoId, RMCActionOrderData>> _actionOrders = new();

    private async Task LoadData(ICommonSession player, CancellationToken cancel)
    {
        // TODO RMC14 read the migration.yml file to map old ids to new ones if necessary, otherwise ordering data is lost
        var orders = await _db.GetAllActionOrders(player.UserId);
        orders ??= new Dictionary<string, List<string>>();

        _actionOrders[player.UserId] = orders.ToDictionary(
            kvp => new EntProtoId(kvp.Key),
            kvp => ParseOrder(kvp.Value)
        );

        _task.RunOnMainThread(() => OnLoaded?.Invoke(player, _actionOrders.GetValueOrDefault(player.UserId)));
    }

    private void ClientDisconnected(ICommonSession player)
    {
        _actionOrders.Remove(player.UserId);
    }

    public RMCActionOrderData? GetOrder(NetUserId player, EntProtoId id)
    {
        var orders = _actionOrders.GetValueOrDefault(player);
        if (orders == null || !orders.TryGetValue(id, out var order))
            return null;

        return order;
    }

    public async void SetOrder(NetUserId player, EntProtoId id, RMCActionOrderData order)
    {
        try
        {
            _actionOrders.GetOrNew(player)[id] = order;
            await _db.SetActionOrder(player, id, SerializeOrder(order));
        }
        catch (Exception e)
        {
            _log.Error($"Error setting order of actions for player {player} with id {id}:\n{e}");
        }
    }

    private static RMCActionOrderData ParseOrder(IEnumerable<string> entries)
    {
        var actions = ImmutableArray.CreateBuilder<EntProtoId>();
        var hiddenActions = ImmutableArray.CreateBuilder<EntProtoId>();
        var hiddenActionsKnown = false;

        foreach (var entry in entries)
        {
            if (entry == HiddenActionsMarker)
            {
                hiddenActionsKnown = true;
                continue;
            }

            if (entry.StartsWith(HiddenActionPrefix, StringComparison.Ordinal))
            {
                hiddenActionsKnown = true;
                hiddenActions.Add(new EntProtoId(entry[HiddenActionPrefix.Length..]));
                continue;
            }

            actions.Add(new EntProtoId(entry));
        }

        return new RMCActionOrderData(actions.ToImmutable(), hiddenActions.ToImmutable(), hiddenActionsKnown);
    }

    private static List<string> SerializeOrder(RMCActionOrderData order)
    {
        var entries = order.Actions.Select(action => action.Id).ToList();
        if (!order.HiddenActionsKnown)
            return entries;

        entries.Add(HiddenActionsMarker);
        entries.AddRange(order.HiddenActions.Select(action => $"{HiddenActionPrefix}{action.Id}"));
        return entries;
    }

    public void PostInject()
    {
        _log = _logManager.GetSawmill("rmc_actions");
        _userDb.AddOnLoadPlayer(LoadData);
        _userDb.AddOnPlayerDisconnect(ClientDisconnected);
    }
}
