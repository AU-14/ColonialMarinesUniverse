using Content.Client.Eui;
using Content.Shared._CMU14.RoundStatistics;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._CMU14.RoundStatistics;

[UsedImplicitly]
public sealed class CMURoundStatisticsEui : BaseEui
{
    private CMURoundStatisticsWindow? _window;

    public override void Opened()
    {
        base.Opened();

        _window = new CMURoundStatisticsWindow();
        _window.OnRefresh += OnRefresh;
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();

        if (_window != null)
            _window.OnRefresh -= OnRefresh;

        _window?.Close();
        _window = null;
    }

    public override void HandleState(EuiStateBase state)
    {
        base.HandleState(state);

        if (state is CMURoundStatisticsEuiState s)
            _window?.UpdateDashboard(s.Dashboard);
    }

    private void OnRefresh()
    {
        SendMessage(new CMURoundStatisticsRefreshMsg());
    }
}
