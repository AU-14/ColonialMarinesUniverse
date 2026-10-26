using Content.Client.Eui;
using Content.Shared.CMU14.RoundStatistics;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client.CMU14.RoundStatistics;

[UsedImplicitly]
public sealed class CMURoundStatisticsEui : BaseEui
{
    private CMURoundStatisticsWindow? _window;

    public override void Opened()
    {
        base.Opened();

        _window = new CMURoundStatisticsWindow();
        _window.OnRefresh += OnRefresh;
        _window.OnQuery += OnQuery;
        _window.OnClose += OnWindowClosed;
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();

        if (_window != null)
        {
            _window.OnRefresh -= OnRefresh;
            _window.OnQuery -= OnQuery;
            _window.OnClose -= OnWindowClosed;
        }

        _window?.Close();
        _window = null;
    }

    public override void HandleState(EuiStateBase state)
    {
        base.HandleState(state);

        if (state is CMURoundStatisticsEuiState s)
            _window?.UpdateDashboard(s.Dashboard, s.Loading, s.Failed);
    }

    private void OnRefresh()
    {
        SendMessage(new CMURoundStatisticsRefreshMsg());
    }

    private void OnQuery(CMURoundStatisticsQuery query) => SendMessage(new CMURoundStatisticsQueryMsg(query));

    private void OnWindowClosed() => SendMessage(new CloseEuiMessage());
}
