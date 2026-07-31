using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._CMU14.BalanceRating;
using Content.Shared._CMU14.RoundStatistics;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    public async Task<long> CreateCMUBalanceRatingPoll(int roundId, CMUBalanceRatingTarget target,
        string targetId, CMUBalanceRatingMetric metric, Guid? createdBy, DateTime openedAt)
    {
        await using var db = await GetDb();
        var poll = new CMUBalanceRatingPoll
        {
            RoundId = roundId,
            Target = target.ToString(),
            TargetId = targetId,
            Metric = metric.ToString(),
            CreatedById = createdBy,
            OpenedAt = NormalizeCMUInputTime(openedAt),
        };
        db.DbContext.CMUBalanceRatingPolls.Add(poll);
        await db.DbContext.SaveChangesAsync();
        return poll.Id;
    }

    public async Task AddCMUBalanceRatingResponse(long pollId, Guid playerId, byte rating, DateTime recordedAt)
    {
        if (rating is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(rating));
        await using var db = await GetDb();
        if (!await db.DbContext.CMUBalanceRatingPolls.AnyAsync(poll => poll.Id == pollId) ||
            await db.DbContext.CMUBalanceRatingResponses.AnyAsync(response => response.PollId == pollId && response.PlayerId == playerId))
            return;
        db.DbContext.CMUBalanceRatingResponses.Add(new CMUBalanceRatingResponse
        {
            PollId = pollId,
            PlayerId = playerId,
            Rating = rating,
            RecordedAt = NormalizeCMUInputTime(recordedAt),
        });
        await db.DbContext.SaveChangesAsync();
    }

    public async Task CloseCMUBalanceRatingPoll(long pollId, DateTime closedAt)
    {
        await using var db = await GetDb();
        var poll = await db.DbContext.CMUBalanceRatingPolls.SingleOrDefaultAsync(candidate => candidate.Id == pollId);
        if (poll == null || poll.ClosedAt != null)
            return;
        poll.ClosedAt = NormalizeCMUInputTime(closedAt);
        await db.DbContext.SaveChangesAsync();
    }

    public async Task DeleteCMUBalanceRatingPoll(long pollId)
    {
        await using var db = await GetDb();
        var poll = await db.DbContext.CMUBalanceRatingPolls.SingleOrDefaultAsync(candidate => candidate.Id == pollId);
        if (poll == null)
            return;
        db.DbContext.CMUBalanceRatingPolls.Remove(poll);
        await db.DbContext.SaveChangesAsync();
    }

    public async Task<CMUBalanceRatingDashboard> GetCMUBalanceRatingDashboard(CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);
        var polls = await db.DbContext.CMUBalanceRatingPolls.AsNoTracking().Include(poll => poll.Responses).ToListAsync(cancel);
        var entries = polls.GroupBy(poll => (poll.Target, poll.TargetId, poll.Metric)).Select(group =>
        {
            var responses = group.SelectMany(poll => poll.Responses).ToList();
            return new CMUBalanceRatingStatisticsEntry(
                Enum.Parse<CMUBalanceRatingTarget>(group.Key.Target),
                Enum.Parse<CMUBalanceRatingMetric>(group.Key.Metric),
                group.Key.TargetId,
                group.Key.TargetId,
                group.Count(),
                responses.Count(response => response.Rating == 1),
                responses.Count(response => response.Rating == 2),
                responses.Count(response => response.Rating == 3),
                responses.Count(response => response.Rating == 4),
                responses.Count(response => response.Rating == 5),
                responses.Count == 0 ? group.Max(poll => poll.OpenedAt) : responses.Max(response => response.RecordedAt));
        }).ToList();
        return new CMUBalanceRatingDashboard(entries, polls.Count, entries.Sum(entry => entry.Responses));
    }

    public async Task UpsertCMURoundOutcome(CMURoundOutcomeRecord record)
    {
        await using var db = await GetDb();
        var outcome = await db.DbContext.CMURoundOutcomes.FirstOrDefaultAsync(candidate => candidate.RoundId == record.RoundId)
            ?? db.DbContext.CMURoundOutcomes.Add(new CMURoundOutcome { RoundId = record.RoundId }).Entity;
        outcome.PresetId = record.Preset.ToString();
        outcome.Winner = record.Winner.ToString();
        outcome.Outcome = record.Outcome.ToString();
        outcome.Source = record.Source;
        outcome.SelectedThreatId = record.SelectedThreatId;
        outcome.PlanetId = record.PlanetId;
        outcome.GovforPlatoonId = record.GovforPlatoonId;
        outcome.OpforPlatoonId = record.OpforPlatoonId;
        outcome.PlayerCount = record.PlayerCount;
        outcome.DurationSeconds = record.DurationSeconds;
        outcome.RecordedAt = record.RecordedAt.ToUniversalTime();
        await db.DbContext.SaveChangesAsync();
    }

    public async Task<CMURoundStatisticsDashboard> GetCMURoundStatisticsDashboard(int recentRounds, CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);
        var records = await db.DbContext.CMURoundOutcomes.AsNoTracking().OrderByDescending(outcome => outcome.RecordedAt).Take(Math.Max(0, recentRounds)).ToListAsync(cancel);
        var recent = records.Select(outcome => new CMURoundOutcomeRecord(
            outcome.RoundId,
            Enum.TryParse(outcome.PresetId, out CMURoundStatisticsPreset preset) ? preset : CMURoundStatisticsPreset.DistressSignal,
            Enum.TryParse(outcome.Winner, out CMURoundStatisticsWinner winner) ? winner : CMURoundStatisticsWinner.Unknown,
            Enum.TryParse(outcome.Outcome, out CMURoundStatisticsOutcome result) ? result : CMURoundStatisticsOutcome.Unknown,
            outcome.Source, outcome.SelectedThreatId, outcome.PlanetId, outcome.GovforPlatoonId, outcome.OpforPlatoonId,
            outcome.PlayerCount, outcome.DurationSeconds, outcome.RecordedAt)).ToList();
        return new CMURoundStatisticsDashboard([], recent);
    }

    private static DateTime NormalizeCMUInputTime(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }
}
