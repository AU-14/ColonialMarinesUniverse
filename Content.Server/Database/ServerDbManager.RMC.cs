using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._RMC14.LinkAccount;
using Content.Shared.Database;
using Robust.Shared.Network;

namespace Content.Server.Database;

public partial interface IServerDbManager
{
    Task<Guid?> GetLinkingCode(Guid player);
    Task SetLinkingCode(Guid player, Guid code);
    Task<bool> HasLinkedAccount(Guid player, CancellationToken cancel);
    Task<RMCPatron?> GetPatron(Guid player, CancellationToken cancel);
    Task<List<RMCPatron>> GetAllPatrons();
    Task SetGhostColor(Guid player, System.Drawing.Color? color);
    Task SetLobbyMessage(Guid player, string message);
    Task SetMarineShoutout(Guid player, string name);
    Task SetXenoShoutout(Guid player, string name);
    Task<(string Message, string User)?> GetRandomLobbyMessage();
    Task<(RoundEndShoutout? Marine, RoundEndShoutout? Xeno)> GetRandomShoutout();
    Task<List<string>> GetExcludedRoleTimers(Guid player, CancellationToken cancel);
    Task<bool> ExcludeRoleTimer(Guid player, string tracker);
    Task<bool> RemoveRoleTimerExclusion(Guid player, string tracker);
    Task AddCommendation(Guid giver,
        Guid receiver,
        string giverName,
        string receiverName,
        string name,
        string text,
        CommendationType type,
        int round);
    Task<List<RMCCommendation>> GetCommendationsReceived(Guid player,
        CommendationType? filterType = null,
        bool includePlayers = false);
    Task<List<RMCCommendation>> GetCommendationsGiven(Guid player,
        CommendationType? filterType = null,
        bool includePlayers = false);
    Task<List<RMCCommendation>> GetLastCommendations(int count,
        CommendationType? filterType = null,
        bool includePlayers = false);
    Task<RMCCommendation?> GetCommendationById(int commendationId, bool includePlayers = false);
    Task<List<RMCCommendation>> GetCommendationsByRound(int roundId,
        CommendationType? filterType = null,
        bool includePlayers = false);
    Task<RMCCommendation?> DeleteCommendationById(int commendationId,
        Guid deletedBy,
        DateTimeOffset deletedAt,
        bool includePlayers = false);
    Task<List<RMCCommendation>> DeleteCommendationsByRound(int roundId,
        CommendationType type,
        Guid deletedBy,
        DateTimeOffset deletedAt,
        Guid? giverId = null,
        Guid? receiverId = null,
        bool includePlayers = false);
    Task IncreaseInfects(Guid player);
    Task<Dictionary<string, List<string>>?> GetAllActionOrders(Guid player);
    Task SetActionOrder(Guid player, string id, List<string> actions);
    Task<HashSet<string>> GetLarvaPoolOptOuts(Guid player);
    Task SetLarvaPoolOptIn(Guid player, string hiveId, bool optedIn);
    Task AddChatBan(int? round,
        NetUserId target,
        (IPAddress, int)? addressRange,
        ImmutableTypedHwid? hwid,
        TimeSpan? duration,
        ChatType type,
        NetUserId admin,
        string reason);
    Task<List<RMCChatBans>> GetAllChatBans(Guid player);
    Task<List<RMCChatBans>> GetActiveChatBans(Guid player);
    Task<Guid?> TryPardonChatBan(int id, Guid? admin);
}

public sealed partial class ServerDbManager
{
    public Task<Guid?> GetLinkingCode(Guid player)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetLinkingCode(player));
    }

    public Task SetLinkingCode(Guid player, Guid code)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.SetLinkingCode(player, code));
    }

    public Task<bool> HasLinkedAccount(Guid player, CancellationToken cancel)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.HasLinkedAccount(player, cancel));
    }

    public Task<RMCPatron?> GetPatron(Guid player, CancellationToken cancel)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetPatron(player, cancel));
    }

    public Task<List<RMCPatron>> GetAllPatrons()
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetAllPatrons());
    }

    public Task SetGhostColor(Guid player, System.Drawing.Color? color)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.SetGhostColor(player, color));
    }

    public Task SetLobbyMessage(Guid player, string message)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.SetLobbyMessage(player, message));
    }

    public Task SetMarineShoutout(Guid player, string name)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.SetMarineShoutout(player, name));
    }

    public Task SetXenoShoutout(Guid player, string name)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.SetXenoShoutout(player, name));
    }

    public Task<(string Message, string User)?> GetRandomLobbyMessage()
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetRandomLobbyMessage());
    }

    public Task<(RoundEndShoutout? Marine, RoundEndShoutout? Xeno)> GetRandomShoutout()
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetRandomShoutout());
    }

    public Task<List<string>> GetExcludedRoleTimers(Guid player, CancellationToken cancel)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetExcludedRoleTimers(player, cancel));
    }

    public Task<bool> ExcludeRoleTimer(Guid player, string tracker)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.ExcludeRoleTimer(player, tracker));
    }

    public Task<bool> RemoveRoleTimerExclusion(Guid player, string tracker)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.RemoveRoleTimerExclusion(player, tracker));
    }

    public Task AddCommendation(Guid giver,
        Guid receiver,
        string giverName,
        string receiverName,
        string name,
        string text,
        CommendationType type,
        int round)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.AddCommendation(giver, receiver, giverName, receiverName, name, text, type, round));
    }

    public Task<List<RMCCommendation>> GetCommendationsReceived(Guid player,
        CommendationType? filterType = null,
        bool includePlayers = false)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetCommendationsReceived(player, filterType, includePlayers));
    }

    public Task<List<RMCCommendation>> GetCommendationsGiven(Guid player,
        CommendationType? filterType = null,
        bool includePlayers = false)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetCommendationsGiven(player, filterType, includePlayers));
    }

    public Task<List<RMCCommendation>> GetLastCommendations(int count,
        CommendationType? filterType = null,
        bool includePlayers = false)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetLastCommendations(count, filterType, includePlayers));
    }

    public Task<RMCCommendation?> GetCommendationById(int commendationId, bool includePlayers = false)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetCommendationById(commendationId, includePlayers));
    }

    public Task<List<RMCCommendation>> GetCommendationsByRound(int roundId,
        CommendationType? filterType = null,
        bool includePlayers = false)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetCommendationsByRound(roundId, filterType, includePlayers));
    }

    public Task<RMCCommendation?> DeleteCommendationById(int commendationId,
        Guid deletedBy,
        DateTimeOffset deletedAt,
        bool includePlayers = false)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.DeleteCommendationById(commendationId, deletedBy, deletedAt, includePlayers));
    }

    public Task<List<RMCCommendation>> DeleteCommendationsByRound(int roundId,
        CommendationType type,
        Guid deletedBy,
        DateTimeOffset deletedAt,
        Guid? giverId = null,
        Guid? receiverId = null,
        bool includePlayers = false)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.DeleteCommendationsByRound(
            roundId,
            type,
            deletedBy,
            deletedAt,
            giverId,
            receiverId,
            includePlayers));
    }

    public Task IncreaseInfects(Guid player)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.IncreaseInfects(player));
    }

    public Task<Dictionary<string, List<string>>?> GetAllActionOrders(Guid player)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetActionOrder(player));
    }

    public Task SetActionOrder(Guid player, string id, List<string> actions)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.SetActionOrder(player, id, actions));
    }

    public Task<HashSet<string>> GetLarvaPoolOptOuts(Guid player)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetLarvaPoolOptOuts(player));
    }

    public Task SetLarvaPoolOptIn(Guid player, string hiveId, bool optedIn)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.SetLarvaPoolOptIn(player, hiveId, optedIn));
    }

    public Task AddChatBan(int? round,
        NetUserId target,
        (IPAddress, int)? addressRange,
        ImmutableTypedHwid? hwid,
        TimeSpan? duration,
        ChatType type,
        NetUserId admin,
        string reason)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.AddChatBan(round, target, addressRange, hwid, duration, type, admin, reason));
    }

    public Task<List<RMCChatBans>> GetAllChatBans(Guid player)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetAllChatBans(player));
    }

    public Task<List<RMCChatBans>> GetActiveChatBans(Guid player)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetActiveChatBans(player));
    }

    public Task<Guid?> TryPardonChatBan(int id, Guid? admin)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.TryPardonChatBan(id, admin));
    }
}
