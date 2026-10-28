using System;
using System.Threading;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Shared.CMU14.RoundStatistics;
using Content.Shared.Eui;
using Robust.Shared.Asynchronous;
using Robust.Shared.Log;

namespace Content.Server.CMU14.RoundStatistics;

public sealed partial class CMURoundStatisticsEui : BaseEui
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private ITaskManager _task = default!;

    private readonly ISawmill _sawmill = Logger.GetSawmill("cmu.round_statistics");
    private readonly CancellationTokenSource _cancel = new();
    private CMURoundStatisticsDashboard _dashboard = new(new(), new());
    private CMURoundStatisticsQuery _query = new();
    private int _revision;
    private bool _loading;
    private bool _failed;
    private bool _closed;

    public CMURoundStatisticsEui()
    {
        IoCManager.InjectDependencies(this);
    }

    public override void Opened()
    {
        base.Opened();
        RequestLoad();
    }

    public override void Closed()
    {
        _closed = true;
        _cancel.Cancel();
        _cancel.Dispose();
        base.Closed();
    }

    public override EuiStateBase GetNewState() => new CMURoundStatisticsEuiState(_dashboard, _loading, _failed);

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (msg is CMURoundStatisticsQueryMsg request)
        {
            _query = request.Query;
            RequestLoad();
        }
        else if (msg is CMURoundStatisticsRefreshMsg)
            RequestLoad();
    }

    private void RequestLoad()
    {
        _revision++;
        if (!_loading && !_closed)
            Load();
    }

    private async void Load()
    {
        _loading = true;
        _failed = false;
        StateDirty();
        var revision = _revision;
        CMURoundStatisticsDashboard? dashboard = null;
        try
        {
            dashboard = await _db.GetCMURoundStatisticsDashboard(_query, _cancel.Token);
        }
        catch (OperationCanceledException) when (_closed)
        {
            return;
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to load CMU round statistics dashboard:\n{e}");
        }

        _task.RunOnMainThread(() =>
        {
            if (_closed)
                return;
            _loading = false;
            // Coalesce requests while a query is in flight and discard stale responses.
            if (revision != _revision)
            {
                Load();
                return;
            }
            if (dashboard != null)
                _dashboard = dashboard;
            _failed = dashboard == null;
            StateDirty();
        });
    }
}
