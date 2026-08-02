using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Administration.Logs;
using Content.Server.EUI;
using Content.Server.Players.JobWhitelist;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Eui;
using Robust.Server.Player;
using Robust.Shared.Network;

namespace Content.Server._CMU14.Yautja;

public sealed class YautjaClanInfoEui : BaseEui
{
    [Dependency] private YautjaClanManager _clanManager = default!;
    [Dependency] private YautjaRankManager _rankManager = default!;
    [Dependency] private JobWhitelistManager _jobWhitelist = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IPlayerManager _players = default!;

    private string _statusMessage = "";
    private int? _selectedClanId;
    private YautjaClanInfoEuiState _state = EmptyState();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private bool _closed;

    public YautjaClanInfoEui()
    {
        IoCManager.InjectDependencies(this);
    }

    public override void Opened()
    {
        base.Opened();
        _closed = false;
        _ = RefreshInitialStateAsync();
    }

    public override void Closed()
    {
        base.Closed();
        _closed = true;
    }

    public override EuiStateBase GetNewState()
    {
        return _state;
    }

    public override async void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (msg is CloseEuiMessage || _closed || IsShutDown)
            return;

        await _operationGate.WaitAsync();
        try
        {
            if (_closed || IsShutDown || !await CanViewAsync())
                return;

            try
            {
                switch (msg)
                {
                    case YautjaClanInfoInitializeMessage:
                    case YautjaClanInfoRefreshMessage:
                        break;
                    case YautjaClanInfoSelectClanMessage selectClan:
                        _selectedClanId = selectClan.ClanId;
                        break;
                    case YautjaClanInfoSetRankMessage setRank:
                        await SetRankAsync(setRank);
                        break;
                    case YautjaClanInfoSetAncientMessage setAncient:
                        await SetAncientAsync(setAncient);
                        break;
                    case YautjaClanInfoUpdateDescriptionMessage description:
                        await UpdateDescriptionAsync(description);
                        break;
                    case YautjaClanInfoUpdateAppearanceMessage appearance:
                        await UpdateAppearanceAsync(appearance);
                        break;
                    case YautjaClanInfoSetHonorMessage honor:
                        await SetHonorAsync(honor);
                        break;
                    case YautjaClanInfoPurgeMemberMessage purge:
                        await PurgeMemberAsync(purge);
                        break;
                    case YautjaClanInfoDeleteClanMessage deleteClan:
                        await DeleteClanAsync(deleteClan);
                        break;
                    case YautjaClanInfoMoveMemberMessage moveMember:
                        await MoveMemberAsync(moveMember);
                        break;
                }
            }
            catch (Exception e)
            {
                _statusMessage = Loc.GetString("cmu-yautja-clan-info-action-denied");
                Logger.GetSawmill("cmu.yautja.clan_info").Error($"Yautja clan information action failed:\n{e}");
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

    private async Task<bool> CanViewAsync()
    {
        var view = await _clanManager.GetView(Player.UserId, _selectedClanId);
        if (YautjaClanPolicy.CanView(ToViewer(view)))
            return true;

        Close();
        return false;
    }

    private async Task RefreshStateAsync()
    {
        try
        {
            var view = await _clanManager.GetView(Player.UserId, _selectedClanId);
            var viewer = ToViewer(view);
            if (!YautjaClanPolicy.CanView(viewer))
            {
                Close();
                return;
            }

            if (_selectedClanId == null &&
                YautjaClanPolicy.HasPermission(viewer.Permissions, YautjaClanPermission.AdminView))
            {
                _selectedClanId = view.Viewer.ClanId;
                if (_selectedClanId != view.ClanId)
                    view = await _clanManager.GetView(Player.UserId, _selectedClanId);
            }

            _selectedClanId = view.ClanId;
            _state = BuildState(view, viewer);
            if (!_closed && !IsShutDown)
                StateDirty();
        }
        catch (Exception e)
        {
            _statusMessage = Loc.GetString("cmu-yautja-clan-info-action-denied");
            Logger.GetSawmill("cmu.yautja.clan_info").Error($"Yautja clan information refresh failed:\n{e}");
        }
    }

    private YautjaClanInfoEuiState BuildState(YautjaClanView view, YautjaClanMemberSnapshot viewer)
    {
        var members = view.Members
            .OrderByDescending(member => member.Rank)
            .ThenBy(member => GetPlayerName(member.PlayerId))
            .Select(member =>
            {
                var canManage = YautjaClanPolicy.GetNormalAssignableRanks().Any(requestedRank =>
                    YautjaClanPolicy.CanModifyRank(
                        viewer,
                        member,
                        requestedRank,
                        view.Members.Count,
                        view.Members.Count(candidate => candidate.Rank == requestedRank)));
                var canSetAncient = YautjaClanPolicy.CanSetAncient(viewer, member, true) ||
                                    YautjaClanPolicy.CanSetAncient(viewer, member, false);
                var canMove = YautjaClanPolicy.CanMove(viewer, member);
                return new YautjaClanInfoMemberState(
                    member.PlayerId,
                    GetPlayerName(member.PlayerId),
                    member.Rank,
                    YautjaRankMetadata.For(member.Rank).IconState,
                    member.Honor,
                    _players.TryGetSessionById(member.PlayerId, out _),
                    canManage,
                    canSetAncient,
                    canMove);
            })
            .ToList();

        var canEditDescription = view.ClanId is { } descriptionClanId &&
                                 YautjaClanPolicy.CanManageClan(
                                     viewer,
                                     descriptionClanId,
                                     YautjaClanPermission.UserModify);
        var canEditAppearance = YautjaClanPolicy.HasPermission(
                                    viewer.Permissions,
                                    YautjaClanPermission.AdminView) &&
                                YautjaClanPolicy.HasPermission(
                                    viewer.Permissions,
                                    YautjaClanPermission.AdminModify);
        var canSetHonor = view.ClanId is not null &&
                          YautjaClanPolicy.HasPermission(
                              viewer.Permissions,
                              YautjaClanPermission.AdminManager);
        var canPurge = YautjaClanPolicy.HasPermission(
            viewer.Permissions,
            YautjaClanPermission.AdminManager);

        return new YautjaClanInfoEuiState(
            view.ClanId,
            view.ClanName,
            view.ClanDescription,
            view.ClanHonor,
            view.ClanColor,
            viewer.Rank,
            viewer.Permissions,
            view.AvailableClans.ToList(),
            canEditDescription,
            canEditAppearance,
            canSetHonor,
            canPurge,
            canSetHonor,
            YautjaClanPolicy.GetNormalAssignableRanks().Any(requestedRank =>
                view.Members.Any(member =>
                    YautjaClanPolicy.CanModifyRank(
                        viewer,
                        member,
                        requestedRank,
                        view.Members.Count,
                        view.Members.Count(candidate => candidate.Rank == requestedRank)))),
            view.Members.Any(member => YautjaClanPolicy.CanSetAncient(viewer, member, true)),
            view.Members.Any(member => YautjaClanPolicy.CanMove(viewer, member)),
            members,
            _statusMessage);
    }

    private async Task SetRankAsync(YautjaClanInfoSetRankMessage message)
    {
        var result = await _clanManager.SetRank(Player.UserId, message.Target, message.Rank);
        _statusMessage = result.Succeeded
            ? Loc.GetString("cmu-yautja-clan-info-rank-updated")
            : result.Error ?? Loc.GetString("cmu-yautja-clan-info-action-denied");
        if (!result.Succeeded)
            return;

        await _rankManager.Refresh(message.Target);
        await _jobWhitelist.RefreshYautjaWhitelist(message.Target);
        _adminLog.Add(
            LogType.Action,
            LogImpact.Medium,
            $"{Player.Name} changed Yautja rank for {message.Target} to {message.Rank}.");
    }

    private async Task SetAncientAsync(YautjaClanInfoSetAncientMessage message)
    {
        var result = await _clanManager.SetAncient(Player.UserId, message.Target, message.Enabled);
        _statusMessage = result.Succeeded
            ? Loc.GetString("cmu-yautja-clan-info-ancient-updated")
            : result.Error ?? Loc.GetString("cmu-yautja-clan-info-action-denied");
        if (!result.Succeeded)
            return;

        await _rankManager.Refresh(message.Target);
        await _jobWhitelist.RefreshYautjaWhitelist(message.Target);
        _adminLog.Add(
            LogType.Action,
            LogImpact.Medium,
            $"{Player.Name} { (message.Enabled ? "made" : "demoted") } Yautja {message.Target} { (message.Enabled ? "Ancient" : "from Ancient") }.");
    }

    private async Task UpdateDescriptionAsync(YautjaClanInfoUpdateDescriptionMessage message)
    {
        var result = await _clanManager.UpdateDescription(Player.UserId, message.ClanId, message.Description);
        _statusMessage = result.Succeeded
            ? Loc.GetString("cmu-yautja-clan-info-description-updated")
            : result.Error ?? Loc.GetString("cmu-yautja-clan-info-action-denied");
    }

    private async Task UpdateAppearanceAsync(YautjaClanInfoUpdateAppearanceMessage message)
    {
        var result = await _clanManager.UpdateAppearance(Player.UserId, message.ClanId, message.Name, message.Color);
        _statusMessage = result.Succeeded
            ? Loc.GetString("cmu-yautja-clan-info-appearance-updated")
            : result.Error ?? Loc.GetString("cmu-yautja-clan-info-action-denied");
    }

    private async Task SetHonorAsync(YautjaClanInfoSetHonorMessage message)
    {
        var result = await _clanManager.SetClanHonor(Player.UserId, message.ClanId, message.Honor);
        _statusMessage = result.Succeeded
            ? Loc.GetString("cmu-yautja-clan-info-honor-updated")
            : result.Error ?? Loc.GetString("cmu-yautja-clan-info-action-denied");
    }

    private async Task PurgeMemberAsync(YautjaClanInfoPurgeMemberMessage message)
    {
        var result = await _clanManager.PurgeMember(Player.UserId, message.Target);
        _statusMessage = result.Succeeded
            ? Loc.GetString("cmu-yautja-clan-info-member-purged")
            : result.Error ?? Loc.GetString("cmu-yautja-clan-info-action-denied");
        if (!result.Succeeded)
            return;

        foreach (var affectedPlayer in result.AffectedPlayers ?? [])
        {
            await _rankManager.Refresh(affectedPlayer);
            await _jobWhitelist.RefreshYautjaWhitelist(affectedPlayer);
        }

        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{Player.Name} purged Yautja clan profile {message.Target}.");
    }

    private async Task DeleteClanAsync(YautjaClanInfoDeleteClanMessage message)
    {
        var result = await _clanManager.DeleteClan(Player.UserId, message.ClanId);
        _statusMessage = result.Succeeded
            ? Loc.GetString("cmu-yautja-clan-info-clan-deleted")
            : result.Error ?? Loc.GetString("cmu-yautja-clan-info-action-denied");
        if (!result.Succeeded)
            return;

        _selectedClanId = null;
        foreach (var affectedPlayer in result.AffectedPlayers ?? [])
        {
            await _rankManager.Refresh(affectedPlayer);
            await _jobWhitelist.RefreshYautjaWhitelist(affectedPlayer);
        }

        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{Player.Name} deleted Yautja clan {message.ClanId}.");
    }

    private async Task MoveMemberAsync(YautjaClanInfoMoveMemberMessage message)
    {
        var result = await _clanManager.MoveMember(Player.UserId, message.Target, message.ClanId);
        _statusMessage = result.Succeeded
            ? Loc.GetString("cmu-yautja-clan-info-member-moved")
            : result.Error ?? Loc.GetString("cmu-yautja-clan-info-action-denied");
        if (!result.Succeeded)
            return;

        await _rankManager.Refresh(message.Target);
        await _jobWhitelist.RefreshYautjaWhitelist(message.Target);
        _adminLog.Add(
            LogType.Action,
            LogImpact.Medium,
            $"{Player.Name} moved Yautja {message.Target} to clan {message.ClanId?.ToString() ?? "none"}.");
    }

    private YautjaClanMemberSnapshot ToViewer(YautjaClanView view)
    {
        return new YautjaClanMemberSnapshot(
            Player.UserId,
            view.Viewer.ClanId,
            view.Viewer.Rank,
            view.Viewer.Permissions,
            view.Viewer.IsLegacy,
            view.Viewer.Honor);
    }

    private static YautjaClanInfoEuiState EmptyState()
    {
        return new YautjaClanInfoEuiState(
            null,
            "",
            "",
            0,
            "",
            YautjaRank.Blooded,
            YautjaClanPermission.None,
            [],
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            [],
            "");
    }

    private string GetPlayerName(NetUserId userId)
    {
        if (_players.TryGetSessionById(userId, out var session))
            return session.Name;

        return _players.TryGetPlayerData(userId, out var data)
            ? data.UserName
            : userId.ToString();
    }
}
