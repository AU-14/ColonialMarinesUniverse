using Content.Client.Eui;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Eui;

namespace Content.Client._CMU14.Yautja;

public sealed class YautjaClanInfoEui : BaseEui
{
    private YautjaClanInfoWindow? _window;

    public override void Opened()
    {
        base.Opened();

        _window = new YautjaClanInfoWindow();
        _window.OnClose += OnWindowClosed;
        _window.OnInitialize += OnInitialize;
        _window.OnRefresh += OnRefresh;
        _window.OnSetRank += OnSetRank;
        _window.OnSetAncient += OnSetAncient;
        _window.OnMoveMember += OnMoveMember;
        _window.OnSelectClan += OnSelectClan;
        _window.OnUpdateDescription += OnUpdateDescription;
        _window.OnUpdateAppearance += OnUpdateAppearance;
        _window.OnSetHonor += OnSetHonor;
        _window.OnPurgeMember += OnPurgeMember;
        _window.OnDeleteClan += OnDeleteClan;
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();

        if (_window == null)
            return;

        _window.OnClose -= OnWindowClosed;
        _window.OnInitialize -= OnInitialize;
        _window.OnRefresh -= OnRefresh;
        _window.OnSetRank -= OnSetRank;
        _window.OnSetAncient -= OnSetAncient;
        _window.OnMoveMember -= OnMoveMember;
        _window.OnSelectClan -= OnSelectClan;
        _window.OnUpdateDescription -= OnUpdateDescription;
        _window.OnUpdateAppearance -= OnUpdateAppearance;
        _window.OnSetHonor -= OnSetHonor;
        _window.OnPurgeMember -= OnPurgeMember;
        _window.OnDeleteClan -= OnDeleteClan;
        _window.Close();
        _window = null;
    }

    public override void HandleState(EuiStateBase state)
    {
        base.HandleState(state);

        if (state is YautjaClanInfoEuiState clanState)
            _window?.UpdateState(clanState);
    }

    private void OnInitialize()
    {
        SendMessage(new YautjaClanInfoInitializeMessage());
    }

    private void OnRefresh()
    {
        SendMessage(new YautjaClanInfoRefreshMessage());
    }

    private void OnSetRank(Robust.Shared.Network.NetUserId target, YautjaRank rank)
    {
        SendMessage(new YautjaClanInfoSetRankMessage(target, rank));
    }

    private void OnSetAncient(Robust.Shared.Network.NetUserId target, bool enabled)
    {
        SendMessage(new YautjaClanInfoSetAncientMessage(target, enabled));
    }

    private void OnWindowClosed()
    {
        SendMessage(new CloseEuiMessage());
    }

    private void OnMoveMember(Robust.Shared.Network.NetUserId target, int? clanId)
    {
        SendMessage(new YautjaClanInfoMoveMemberMessage(target, clanId));
    }

    private void OnSelectClan(int? clanId)
    {
        SendMessage(new YautjaClanInfoSelectClanMessage(clanId));
    }

    private void OnUpdateDescription(int clanId, string description)
    {
        SendMessage(new YautjaClanInfoUpdateDescriptionMessage(clanId, description));
    }

    private void OnUpdateAppearance(int clanId, string name, string color)
    {
        SendMessage(new YautjaClanInfoUpdateAppearanceMessage(clanId, name, color));
    }

    private void OnSetHonor(int clanId, int honor)
    {
        SendMessage(new YautjaClanInfoSetHonorMessage(clanId, honor));
    }

    private void OnPurgeMember(Robust.Shared.Network.NetUserId target)
    {
        SendMessage(new YautjaClanInfoPurgeMemberMessage(target));
    }

    private void OnDeleteClan(int clanId)
    {
        SendMessage(new YautjaClanInfoDeleteClanMessage(clanId));
    }
}
