using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._RMC14.Xenonids.JoinXeno;

public sealed partial class LarvaPoolPreferenceManager : IPostInjectInit
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private UserDbDataManager _userDb = default!;

    private readonly Dictionary<NetUserId, HashSet<string>> _optOuts = [];
    private ISawmill _log = default!;

    private async Task LoadData(ICommonSession player, CancellationToken cancel)
    {
        var optOuts = await _db.GetLarvaPoolOptOuts(player.UserId);
        cancel.ThrowIfCancellationRequested();
        _optOuts[player.UserId] = optOuts;
    }

    private void ClientDisconnected(ICommonSession player)
    {
        _optOuts.Remove(player.UserId);
    }

    public bool IsLoaded(NetUserId player)
    {
        return _optOuts.ContainsKey(player);
    }

    public bool TryGetOptedIn(NetUserId player, string hiveId, out bool optedIn)
    {
        if (!_optOuts.TryGetValue(player, out var optOuts))
        {
            optedIn = false;
            return false;
        }

        optedIn = !optOuts.Contains(hiveId);
        return true;
    }

    public bool SetOptedIn(NetUserId player, string hiveId, bool optedIn)
    {
        if (!_optOuts.TryGetValue(player, out var optOuts))
            return false;

        var changed = optedIn
            ? optOuts.Remove(hiveId)
            : optOuts.Add(hiveId);
        if (!changed)
            return true;

        PersistOptIn(player, hiveId, optedIn);
        return true;
    }

    private async void PersistOptIn(NetUserId player, string hiveId, bool optedIn)
    {
        try
        {
            await _db.SetLarvaPoolOptIn(player, hiveId, optedIn);
        }
        catch (Exception e)
        {
            _log.Error($"Error saving larva pool preference for player {player} and hive {hiveId}:\n{e}");
        }
    }

    void IPostInjectInit.PostInject()
    {
        _log = _logManager.GetSawmill("larva_pool_preferences");
        _userDb.AddOnLoadPlayer(LoadData);
        _userDb.AddOnPlayerDisconnect(ClientDisconnected);
    }
}
