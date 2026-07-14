using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Shared._CMU14.BalanceRating;
using Content.Shared.Eui;
using Robust.Shared.Asynchronous;
using Robust.Shared.Log;

namespace Content.Server._CMU14.BalanceRating;

public sealed partial class CMUBalanceRatingEui : BaseEui
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);
    private static readonly SemaphoreSlim QueryGate = new(1, 1);

    private static CMUBalanceRatingDashboard? _cachedDashboard;
    private static DateTime _cacheExpiresAt;

    [Dependency] private IEntitySystemManager _systems = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private ITaskManager _task = default!;

    private readonly ISawmill _sawmill = Logger.GetSawmill("cmu.balance_rating.statistics");
    private readonly CMUBalanceRatingSystem _ratings;
    private CMUBalanceRatingDashboard _dashboard = new([], 0, 0);
    private bool _closed;
    private bool _loading;

    public CMUBalanceRatingEui()
    {
        IoCManager.InjectDependencies(this);
        _ratings = _systems.GetEntitySystem<CMUBalanceRatingSystem>();
    }

    public override void Opened()
    {
        base.Opened();

        _closed = false;
        LoadFromDb();
    }

    public override void Closed()
    {
        base.Closed();

        _closed = true;
    }

    public override EuiStateBase GetNewState()
    {
        return new CMUBalanceRatingEuiState(_dashboard);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is CMUBalanceRatingRefreshMessage)
            LoadFromDb();
    }

    private async void LoadFromDb()
    {
        if (_loading || _closed)
            return;

        _loading = true;

        try
        {
            var dashboard = await GetCachedDashboard();
            _task.RunOnMainThread(() =>
            {
                _loading = false;
                if (_closed)
                    return;

                _dashboard = EnrichTargetNames(dashboard);
                StateDirty();
            });
        }
        catch (Exception e)
        {
            _task.RunOnMainThread(() => _loading = false);
            _sawmill.Error($"Failed to load CMU balance rating statistics:\n{e}");
        }
    }

    private async Task<CMUBalanceRatingDashboard> GetCachedDashboard()
    {
        await QueryGate.WaitAsync();

        try
        {
            var now = DateTime.UtcNow;
            if (_cachedDashboard != null && now < _cacheExpiresAt)
                return _cachedDashboard;

            _cachedDashboard = await _db.GetCMUBalanceRatingDashboard();
            _cacheExpiresAt = DateTime.UtcNow + CacheDuration;
            return _cachedDashboard;
        }
        finally
        {
            QueryGate.Release();
        }
    }

    private CMUBalanceRatingDashboard EnrichTargetNames(CMUBalanceRatingDashboard dashboard)
    {
        var entries = new List<CMUBalanceRatingStatisticsEntry>(dashboard.Entries.Count);
        foreach (var entry in dashboard.Entries)
        {
            var targetName = entry.TargetName;
            if (_ratings.TryGetTargetName(entry.Target, entry.TargetId, out var resolvedName))
                targetName = resolvedName;

            if (string.IsNullOrWhiteSpace(targetName))
                targetName = entry.TargetId;

            entries.Add(entry with { TargetName = targetName });
        }

        return new CMUBalanceRatingDashboard(entries, dashboard.TotalPolls, dashboard.TotalResponses);
    }
}
