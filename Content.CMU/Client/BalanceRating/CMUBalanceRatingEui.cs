using Content.Client.Eui;
using Content.Shared._CMU14.BalanceRating;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._CMU14.BalanceRating;

[UsedImplicitly]
public sealed class CMUBalanceRatingEui : BaseEui
{
    private CMUBalanceRatingStatisticsWindow? _window;

    public override void Opened()
    {
        base.Opened();

        _window = new CMUBalanceRatingStatisticsWindow();
        _window.OnClose += OnWindowClosed;
        _window.OnRefresh += OnRefresh;
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();

        if (_window != null)
        {
            _window.OnClose -= OnWindowClosed;
            _window.OnRefresh -= OnRefresh;
            _window.Close();
            _window = null;
        }
    }

    public override void HandleState(EuiStateBase state)
    {
        base.HandleState(state);

        if (state is CMUBalanceRatingEuiState ratingState)
            _window?.UpdateDashboard(ratingState.Dashboard);
    }

    private void OnRefresh()
    {
        SendMessage(new CMUBalanceRatingRefreshMessage());
    }

    private void OnWindowClosed()
    {
        SendMessage(new CloseEuiMessage());
    }
}
