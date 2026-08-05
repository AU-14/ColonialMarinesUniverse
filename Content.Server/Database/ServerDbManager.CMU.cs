using System;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._CMU14.BalanceRating;
using Content.Shared._CMU14.RoundStatistics;

namespace Content.Server.Database;

public partial interface IServerDbManager
{
    Task<long> CreateCMUBalanceRatingPoll(int roundId, CMUBalanceRatingTarget target, string targetId,
        CMUBalanceRatingMetric metric, Guid? createdBy, DateTime openedAt);
    Task AddCMUBalanceRatingResponse(long pollId, Guid playerId, byte rating, DateTime recordedAt);
    Task CloseCMUBalanceRatingPoll(long pollId, DateTime closedAt);
    Task DeleteCMUBalanceRatingPoll(long pollId);
    Task<CMUBalanceRatingDashboard> GetCMUBalanceRatingDashboard(CancellationToken cancel = default);
    Task UpsertCMURoundOutcome(CMURoundOutcomeRecord record);
    Task<CMURoundStatisticsDashboard> GetCMURoundStatisticsDashboard(int recentRounds, CancellationToken cancel = default);
}

public sealed partial class ServerDbManager
{
    public Task<long> CreateCMUBalanceRatingPoll(int roundId, CMUBalanceRatingTarget target, string targetId,
        CMUBalanceRatingMetric metric, Guid? createdBy, DateTime openedAt)
        => RunDbCommand(() => _db.CreateCMUBalanceRatingPoll(roundId, target, targetId, metric, createdBy, openedAt));
    public Task AddCMUBalanceRatingResponse(long pollId, Guid playerId, byte rating, DateTime recordedAt)
        => RunDbCommand(() => _db.AddCMUBalanceRatingResponse(pollId, playerId, rating, recordedAt));
    public Task CloseCMUBalanceRatingPoll(long pollId, DateTime closedAt)
        => RunDbCommand(() => _db.CloseCMUBalanceRatingPoll(pollId, closedAt));
    public Task DeleteCMUBalanceRatingPoll(long pollId)
        => RunDbCommand(() => _db.DeleteCMUBalanceRatingPoll(pollId));
    public Task<CMUBalanceRatingDashboard> GetCMUBalanceRatingDashboard(CancellationToken cancel = default)
        => RunDbCommand(() => _db.GetCMUBalanceRatingDashboard(cancel));
    public Task UpsertCMURoundOutcome(CMURoundOutcomeRecord record)
        => RunDbCommand(() => _db.UpsertCMURoundOutcome(record));
    public Task<CMURoundStatisticsDashboard> GetCMURoundStatisticsDashboard(int recentRounds, CancellationToken cancel = default)
        => RunDbCommand(() => _db.GetCMURoundStatisticsDashboard(recentRounds, cancel));
}
