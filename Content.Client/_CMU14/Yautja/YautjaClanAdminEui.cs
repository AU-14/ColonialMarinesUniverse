using Content.Client.Eui;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Eui;
using Robust.Shared.Network;

namespace Content.Client._CMU14.Yautja;

public sealed class YautjaClanAdminEui : BaseEui
{
    private YautjaClanAdminWindow? _window;

    public override void Opened()
    {
        base.Opened();

        _window = new YautjaClanAdminWindow();
        _window.OnClose += OnWindowClosed;
        _window.OnRefresh += OnRefresh;
        _window.OnCreateClan += OnCreateClan;
        _window.OnUpdateClan += OnUpdateClan;
        _window.OnDeleteClan += OnDeleteClan;
        _window.OnRemoveMember += OnRemoveMember;
        _window.OnClearWhitelist += OnClearWhitelist;
        _window.OnSetMembership += OnSetMembership;
        _window.OnSetRank += OnSetRank;
        _window.OnSetWhitelist += OnSetWhitelist;
        _window.OnInspect += OnInspect;
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();

        if (_window == null)
            return;

        _window.OnClose -= OnWindowClosed;
        _window.OnRefresh -= OnRefresh;
        _window.OnCreateClan -= OnCreateClan;
        _window.OnUpdateClan -= OnUpdateClan;
        _window.OnDeleteClan -= OnDeleteClan;
        _window.OnRemoveMember -= OnRemoveMember;
        _window.OnClearWhitelist -= OnClearWhitelist;
        _window.OnSetMembership -= OnSetMembership;
        _window.OnSetRank -= OnSetRank;
        _window.OnSetWhitelist -= OnSetWhitelist;
        _window.OnInspect -= OnInspect;
        _window.Close();
        _window = null;
    }

    public override void HandleState(EuiStateBase state)
    {
        base.HandleState(state);

        if (state is YautjaClanAdminEuiState adminState)
            _window?.UpdateState(adminState);
    }

    private void OnRefresh() => SendMessage(new YautjaClanAdminRefreshMessage());

    private void OnCreateClan(string name, string description, string color)
    {
        SendMessage(new YautjaClanAdminCreateClanMessage(name, description, color));
    }

    private void OnUpdateClan(int clanId, string name, string description, string color)
    {
        SendMessage(new YautjaClanAdminUpdateClanMessage(clanId, name, description, color));
    }

    private void OnDeleteClan(int clanId)
    {
        SendMessage(new YautjaClanAdminDeleteClanMessage(clanId));
    }

    private void OnRemoveMember(NetUserId playerId)
    {
        SendMessage(new YautjaClanAdminRemoveMemberMessage(playerId));
    }

    private void OnClearWhitelist(NetUserId playerId)
    {
        SendMessage(new YautjaClanAdminClearWhitelistMessage(playerId));
    }

    private void OnSetMembership(string player, string clanId, YautjaRank rank)
    {
        SendMessage(new YautjaClanAdminSetMembershipMessage(player, clanId, rank));
    }

    private void OnSetRank(string player, YautjaRank rank)
    {
        SendMessage(new YautjaClanAdminSetRankMessage(player, rank));
    }

    private void OnSetWhitelist(string player, YautjaWhitelistFlags flags)
    {
        SendMessage(new YautjaClanAdminSetWhitelistMessage(player, flags));
    }

    private void OnInspect(string player)
    {
        SendMessage(new YautjaClanAdminInspectMessage(player));
    }

    private void OnWindowClosed()
    {
        SendMessage(new CloseEuiMessage());
    }
}
