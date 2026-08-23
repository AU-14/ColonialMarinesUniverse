using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Logs;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Server.Players.JobWhitelist;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Administration;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Eui;
using Robust.Server.Player;
using Robust.Shared.Network;

namespace Content.Server._CMU14.Yautja;

public sealed class YautjaClanAdminEui : BaseEui
{
    public const AdminFlags RequiredAdminFlag = AdminFlags.Clans;

    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private YautjaClanManager _clanManager = default!;
    [Dependency] private YautjaRankManager _rankManager = default!;
    [Dependency] private JobWhitelistManager _jobWhitelist = default!;
    [Dependency] private UserDbDataManager _userDb = default!;
    [Dependency] private IPlayerLocator _playerLocator = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;

    private string _statusMessage = "";
    private string _inspectedPlayer = "";
    private string _inspectedSummary = "";
    private readonly YautjaClanAdminStateStore _stateStore = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private bool _closed;

    public YautjaClanAdminEui()
    {
        IoCManager.InjectDependencies(this);
    }

    public override void Opened()
    {
        base.Opened();
        _closed = false;
        _admin.OnPermsChanged += OnAdminPermsChanged;

        if (!_admin.HasAdminFlag(Player, RequiredAdminFlag))
        {
            Close();
            return;
        }

        _ = RefreshInitialStateAsync();
    }

    public override void Closed()
    {
        base.Closed();
        _closed = true;
        _admin.OnPermsChanged -= OnAdminPermsChanged;
    }

    public override EuiStateBase GetNewState()
    {
        return _stateStore.GetForDelivery();
    }

    public override async void HandleMessage(EuiMessageBase msg)
    {
        if (!_admin.HasAdminFlag(Player, RequiredAdminFlag))
        {
            Close();
            return;
        }

        base.HandleMessage(msg);
        if (msg is CloseEuiMessage)
            return;

        await _operationGate.WaitAsync();
        try
        {
            if (!_admin.HasAdminFlag(Player, RequiredAdminFlag))
            {
                Close();
                return;
            }

            if (!_stateStore.CanStartMutation)
            {
                if (!_stateStore.NeedsMutationRecovery)
                    return;

                if (!await RefreshStateAsync())
                    return;

                // Publish the recovered acknowledgement in its own state cycle
                // before accepting another mutation.
                return;
            }

            try
            {
                switch (msg)
                {
                    case YautjaClanAdminRefreshMessage:
                        break;
                    case YautjaClanAdminCreateClanMessage create:
                        await CreateClan(create);
                        break;
                    case YautjaClanAdminUpdateClanMessage update:
                        await UpdateClan(update);
                        break;
                    case YautjaClanAdminDeleteClanMessage delete:
                        await DeleteClan(delete);
                        break;
                    case YautjaClanAdminRemoveMemberMessage removeMember:
                        await RemoveMember(removeMember);
                        break;
                    case YautjaClanAdminClearWhitelistMessage clearWhitelist:
                        await ClearWhitelist(clearWhitelist);
                        break;
                    case YautjaClanAdminSetMembershipMessage membership:
                        await SetMembership(membership);
                        break;
                    case YautjaClanAdminSetRankMessage rank:
                        await SetRank(rank);
                        break;
                    case YautjaClanAdminSetWhitelistMessage whitelist:
                        await SetWhitelist(whitelist);
                        break;
                    case YautjaClanAdminInspectMessage inspect:
                        await Inspect(inspect);
                        break;
                }
            }
            catch (Exception e)
            {
                _statusMessage = e.Message;
                Logger.GetSawmill("cmu.yautja.clan_admin").Error($"Yautja clan admin action failed:\n{e}");
            }

            await RefreshStateAsync();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task RefreshInitialStateAsync()
    {
        await _operationGate.WaitAsync();
        try
        {
            await RefreshStateAsync();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task<bool> RefreshStateAsync()
    {
        try
        {
            var clans = await _db.GetYautjaClansAsync();
            var memberRecords = await _db.GetYautjaClanMembersAsync();
            var whitelistHolders = await _db.GetYautjaWhitelistHoldersAsync();
            var memberStates = new Dictionary<Guid, YautjaClanAdminMemberState>(memberRecords.Count);
            foreach (var member in memberRecords)
            {
                var playerId = new NetUserId(member.PlayerUserId);
                var whitelistFlags = (YautjaWhitelistFlags) await _db.GetYautjaWhitelistFlagsAsync(member.PlayerUserId);
                memberStates.Add(
                    member.PlayerUserId,
                    ToMemberState(
                        member,
                        GetPlayerName(playerId),
                        _players.TryGetSessionById(playerId, out _),
                        whitelistFlags));
            }
            var clanStates = new List<YautjaClanAdminClanState>(clans.Count);

            foreach (var clan in clans)
            {
                var clanMemberStates = memberRecords
                    .Where(member => member.ClanId == clan.Id)
                    .Select(member => memberStates[member.PlayerUserId])
                    .OrderByDescending(member => member.Rank)
                    .ThenBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                clanStates.Add(new YautjaClanAdminClanState(
                    clan.Id,
                    clan.Name,
                    clan.Description,
                    clan.Honor,
                    clan.Color,
                    clanMemberStates.Count,
                    clanMemberStates));
            }

            var clanlessPlayers = memberRecords
                .Where(IsClanless)
                .Select(member => memberStates[member.PlayerUserId])
                .ToList();
            clanlessPlayers.AddRange(whitelistHolders
                .Where(holder => !memberStates.ContainsKey(holder.PlayerUserId))
                .Select(holder =>
                {
                    var playerId = new NetUserId(holder.PlayerUserId);
                    return new YautjaClanAdminMemberState(
                        playerId,
                        holder.Name,
                        YautjaClanManager.SanitizeStoredRank(holder.Rank),
                        _players.TryGetSessionById(playerId, out _),
                        (YautjaWhitelistFlags) holder.WhitelistFlags);
                }));
            clanlessPlayers = clanlessPlayers
                .OrderByDescending(member => member.Rank)
                .ThenBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var publishedState = _stateStore.PublishFreshSnapshot(
                clanStates,
                _inspectedPlayer,
                _inspectedSummary,
                _statusMessage,
                clanlessPlayers);
            _statusMessage = publishedState.StatusMessage;
        }
        catch (Exception e)
        {
            _statusMessage = e.Message;
            Logger.GetSawmill("cmu.yautja.clan_admin").Error($"Yautja clan admin state refresh failed:\n{e}");
            _stateStore.PublishRefreshFailure(_statusMessage);

            if (!_closed && !IsShutDown)
                StateDirty();
            return false;
        }

        if (!_closed && !IsShutDown)
            StateDirty();
        return true;
    }

    private async Task CreateClan(YautjaClanAdminCreateClanMessage message)
    {
        if (!YautjaClanAdminValidation.TryNormalize(
                message.Name,
                message.Description,
                message.Color,
                out var fields,
                out var error))
        {
            _statusMessage = error == YautjaClanAdminValidationError.InvalidColor
                ? Loc.GetString("cmu-yautja-clan-admin-invalid-color")
                : Loc.GetString("cmu-yautja-clan-admin-invalid-clan");
            return;
        }

        var id = await _db.CreateYautjaClanAsync(fields.Name, fields.Description, 0, fields.Color);
        _statusMessage = Loc.GetString("cmu-yautja-clan-admin-created", ("id", id));
        _stateStore.StageMutation(id, YautjaClanAdminMutationKind.Created, _statusMessage);
        _adminLog.Add(LogType.AdminCommands, LogImpact.Medium,
            $"{Player.Name} created Yautja clan {id} ({message.Name}).");
    }

    internal static YautjaClanAdminMemberState ToMemberState(
        YautjaClanMemberRecord member,
        string name,
        bool online,
        YautjaWhitelistFlags whitelistFlags = YautjaWhitelistFlags.None)
    {
        return new(
            new NetUserId(member.PlayerUserId),
            name,
            YautjaClanManager.SanitizeStoredRank(member.Rank),
            online,
            whitelistFlags);
    }

    internal static YautjaClanMemberRecord RemoveFromClan(YautjaClanMemberRecord member)
    {
        return member with { ClanId = null };
    }

    internal static bool IsClanless(YautjaClanMemberRecord member)
    {
        return member.ClanId == null;
    }

    private string GetPlayerName(NetUserId userId)
    {
        return _players.TryGetSessionById(userId, out var session)
            ? session.Name
            : userId.ToString();
    }

    private async Task UpdateClan(YautjaClanAdminUpdateClanMessage message)
    {
        if (!YautjaClanAdminValidation.TryNormalize(
                message.Name,
                message.Description,
                message.Color,
                out var fields,
                out var error))
        {
            _statusMessage = error == YautjaClanAdminValidationError.InvalidColor
                ? Loc.GetString("cmu-yautja-clan-admin-invalid-color")
                : Loc.GetString("cmu-yautja-clan-admin-invalid-clan");
            return;
        }

        if (!await _db.UpdateYautjaClanAsync(
                message.ClanId,
                fields.Name,
                fields.Description,
                fields.Color))
        {
            _statusMessage = Loc.GetString("cmu-yautja-clan-admin-clan-not-found");
            return;
        }

        _statusMessage = Loc.GetString("cmu-yautja-clan-admin-updated", ("id", message.ClanId));
        _stateStore.StageMutation(message.ClanId, YautjaClanAdminMutationKind.Updated, _statusMessage);
        _adminLog.Add(
            LogType.AdminCommands,
            LogImpact.Medium,
            $"{Player.Name} updated Yautja clan {message.ClanId} ({fields.Name}).");
    }

    private async Task DeleteClan(YautjaClanAdminDeleteClanMessage message)
    {
        var result = await _db.DeactivateYautjaClanAsync(message.ClanId);
        if (!result.Succeeded)
        {
            _statusMessage = Loc.GetString("cmu-yautja-clan-admin-clan-not-found");
            return;
        }

        foreach (var detachedPlayer in result.DetachedPlayers)
        {
            var userId = new NetUserId(detachedPlayer);
            await _rankManager.Refresh(userId);
            await _jobWhitelist.RefreshYautjaWhitelist(userId);
        }

        _statusMessage = Loc.GetString(
            "cmu-yautja-clan-admin-deleted",
            ("id", message.ClanId),
            ("members", result.DetachedPlayers.Count));
        _stateStore.StageMutation(message.ClanId, YautjaClanAdminMutationKind.Deleted, _statusMessage);
        _adminLog.Add(
            LogType.AdminCommands,
            LogImpact.Medium,
            $"{Player.Name} deleted Yautja clan {message.ClanId} and detached {result.DetachedPlayers.Count} members.");
    }

    private async Task RemoveMember(YautjaClanAdminRemoveMemberMessage message)
    {
        var existing = await _db.GetYautjaClanMemberAsync(message.PlayerId.UserId);
        if (existing == null || IsClanless(existing))
        {
            _statusMessage = Loc.GetString("cmu-yautja-clan-admin-member-not-found");
            return;
        }

        if (!await _db.UpsertYautjaClanMemberAsync(RemoveFromClan(existing)))
        {
            _statusMessage = Loc.GetString("cmu-yautja-clan-admin-member-not-found");
            return;
        }

        await _rankManager.Refresh(message.PlayerId);
        await _jobWhitelist.RefreshYautjaWhitelist(message.PlayerId);
        var playerName = GetPlayerName(message.PlayerId);
        _statusMessage = Loc.GetString("cmu-yautja-clan-admin-member-removed", ("player", playerName));
        _adminLog.Add(
            LogType.AdminCommands,
            LogImpact.Medium,
            $"{Player.Name} removed Yautja player {playerName} ({message.PlayerId}) from a clan.");
    }

    private async Task ClearWhitelist(YautjaClanAdminClearWhitelistMessage message)
    {
        await WaitForPlayerDataLoad(message.PlayerId);
        await _db.SetYautjaWhitelistFlagsAsync(message.PlayerId.UserId, (int) YautjaWhitelistFlags.None);
        await _rankManager.Refresh(message.PlayerId);
        await _jobWhitelist.RefreshYautjaWhitelist(message.PlayerId);
        var playerName = GetPlayerName(message.PlayerId);
        _statusMessage = Loc.GetString("cmu-yautja-clan-admin-whitelist-cleared", ("player", playerName));
        _adminLog.Add(
            LogType.AdminCommands,
            LogImpact.Medium,
            $"{Player.Name} cleared Yautja whitelist flags for {playerName} ({message.PlayerId}).");
    }

    private async Task SetMembership(YautjaClanAdminSetMembershipMessage message)
    {
        var player = await FindPlayer(message.Player);
        if (player == null)
            return;
        if (!YautjaRankManager.IsPersistentRank(message.Rank))
        {
            _statusMessage = Loc.GetString("cmu-yautja-clan-admin-invalid-rank");
            return;
        }

        if (!int.TryParse(message.ClanId.Trim(), out var clanId))
        {
            _statusMessage = Loc.GetString("cmu-yautja-clan-admin-invalid-clan-id");
            return;
        }

        var existing = await _db.GetYautjaClanMemberAsync(player.UserId.UserId);
        if (!await _db.UpsertYautjaClanMemberAsync(new YautjaClanMemberRecord(
            player.UserId.UserId,
            clanId,
            (int) message.Rank,
            (int) YautjaClanManager.PermissionsForRank(message.Rank),
            existing?.Honor ?? 0,
            false)))
        {
            _statusMessage = Loc.GetString("cmu-yautja-clan-admin-invalid-clan-id");
            return;
        }

        await _rankManager.Refresh(player.UserId);
        await _jobWhitelist.RefreshYautjaWhitelist(player.UserId);
        _statusMessage = Loc.GetString("cmu-yautja-clan-admin-membership-updated", ("player", player.Username));
        _adminLog.Add(LogType.AdminCommands, LogImpact.Medium,
            $"{Player.Name} set Yautja clan membership for {player.Username} ({player.UserId}) to clan {clanId} at rank {message.Rank}.");
    }

    private async Task SetRank(YautjaClanAdminSetRankMessage message)
    {
        var player = await FindPlayer(message.Player);
        if (player == null)
            return;
        if (!YautjaRankManager.IsPersistentRank(message.Rank))
        {
            _statusMessage = Loc.GetString("cmu-yautja-clan-admin-invalid-rank");
            return;
        }

        await _rankManager.Set(player.UserId, message.Rank);
        await _jobWhitelist.RefreshYautjaWhitelist(player.UserId);
        _statusMessage = Loc.GetString("cmu-yautja-clan-admin-rank-updated", ("player", player.Username));
        _adminLog.Add(LogType.AdminCommands, LogImpact.Medium,
            $"{Player.Name} set Yautja rank for {player.Username} ({player.UserId}) to {message.Rank}.");
    }

    private async Task SetWhitelist(YautjaClanAdminSetWhitelistMessage message)
    {
        var player = await FindPlayer(message.Player);
        if (player == null)
            return;
        await WaitForPlayerDataLoad(player.UserId);
        const YautjaWhitelistFlags knownFlags =
            YautjaWhitelistFlags.Yautja |
            YautjaWhitelistFlags.Legacy |
            YautjaWhitelistFlags.Council |
            YautjaWhitelistFlags.CouncilLegacy |
            YautjaWhitelistFlags.Leader;
        if ((message.Flags & ~knownFlags) != YautjaWhitelistFlags.None)
        {
            _statusMessage = Loc.GetString("cmu-yautja-clan-admin-invalid-whitelist");
            return;
        }

        await _db.SetYautjaWhitelistFlagsAsync(player.UserId.UserId, (int) message.Flags);
        await _rankManager.Refresh(player.UserId);
        await _jobWhitelist.RefreshYautjaWhitelist(player.UserId);
        _statusMessage = Loc.GetString("cmu-yautja-clan-admin-whitelist-updated", ("player", player.Username));
        _adminLog.Add(LogType.AdminCommands, LogImpact.Medium,
            $"{Player.Name} set Yautja whitelist flags for {player.Username} ({player.UserId}) to {message.Flags}.");
    }

    private async Task WaitForPlayerDataLoad(NetUserId userId)
    {
        if (_players.TryGetSessionById(userId, out var session) && !_userDb.IsLoadComplete(session))
            await _userDb.WaitLoadComplete(session);
    }

    private async Task Inspect(YautjaClanAdminInspectMessage message)
    {
        var player = await FindPlayer(message.Player);
        if (player == null)
            return;

        var resolution = await _clanManager.Resolve(player.UserId);
        var clan = resolution.ClanId is { } clanId
            ? (await _db.GetYautjaClanAsync(clanId))?.Name ?? clanId.ToString()
            : "none";
        _inspectedPlayer = player.Username;
        _inspectedSummary = Loc.GetString(
            "cmu-yautja-clan-admin-inspection",
            ("rank", resolution.Rank),
            ("clan", clan),
            ("permissions", resolution.Permissions),
            ("whitelist", resolution.WhitelistFlags),
            ("legacy", resolution.IsLegacy));
        _statusMessage = Loc.GetString("cmu-yautja-clan-admin-inspected", ("player", player.Username));
    }

    private async Task<LocatedPlayerData?> FindPlayer(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _statusMessage = Loc.GetString("cmu-yautja-clan-admin-player-required");
            return null;
        }

        var found = await _playerLocator.LookupIdByNameOrIdAsync(query.Trim());
        if (found == null)
        {
            _statusMessage = Loc.GetString("cmu-yautja-clan-admin-player-not-found", ("player", query));
            return null;
        }

        return found;
    }

    private void OnAdminPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player == Player && !_admin.HasAdminFlag(Player, RequiredAdminFlag))
            Close();
    }
}
