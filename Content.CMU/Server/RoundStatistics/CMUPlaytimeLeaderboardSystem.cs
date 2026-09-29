using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Preferences.Managers;
using Content.Shared.CMU14.RoundStatistics;
using Content.Shared.GameTicking;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Roles;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.RoundStatistics;

internal sealed record CMUPlaytimeTop(
    string Role,
    string Tracker,
    int PlayerCount,
    double TotalHours,
    List<CMUPlaytimeLeaderboardRow> Rows);

internal sealed record CMUPlaytimeChampion(
    Guid PlayerId,
    string Player,
    double Hours);

internal sealed record CMUPlaytimeLeaderboardSnapshot(
    List<string> XenoTrackers,
    List<string> GovforTrackers,
    int Players,
    List<CMUPlaytimeTop> Xeno,
    List<CMUPlaytimeTop> Govfor,
    List<CMUPlaytimeChampion> XenoChampions,
    List<CMUPlaytimeChampion> GovforChampions,
    ILookup<string, CMUPlaytimeLeaderboardRow> Rows,
    Dictionary<Guid, string> Tags);

public sealed class CMUPlaytimeLeaderboardSystem : EntitySystem
{
    private const int TopPerRole = 3;
    private const int ChampionsPerSide = 10;
    private const string DefaultPrefix = "XX";

    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly PlayTimeTrackingManager _playTime = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;

    private readonly ISawmill _sawmill = Logger.GetSawmill("cmu.playtime_leaderboard");

    private Task<CMUPlaytimeLeaderboardSnapshot>? _load;
    private DateTime _lastLoad = DateTime.MinValue;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    // Callers run on the main thread, so the cache fields need no locking
    internal Task<CMUPlaytimeLeaderboardSnapshot> GetSnapshot()
    {
        if (_load is { IsFaulted: true }
            || DateTime.UtcNow - _lastLoad >= CMUPlaytimeLeaderboardShared.RefreshCooldown)
        {
            _lastLoad = DateTime.UtcNow;
            _load = null;
        }

        return _load ??= LoadSnapshot();
    }

    // Main thread only: reads the live playtime and preferences caches
    internal CMUPlaytimeLeaderboard Bake(CMUPlaytimeLeaderboardSnapshot snapshot, ICommonSession player)
    {
        var times = _playTime.GetPlayTimes(player);
        var youTag = GetXenoTag(player.UserId);

        return new CMUPlaytimeLeaderboard(
            snapshot.Players,
            BakeChampions(snapshot.XenoChampions, snapshot.Tags, snapshot.XenoTrackers, times, player, youTag),
            BakeChampions(snapshot.GovforChampions, snapshot.Tags, snapshot.GovforTrackers, times, player, string.Empty),
            BakeRoles(snapshot.Xeno, snapshot, times, player, youTag),
            BakeRoles(snapshot.Govfor, snapshot, times, player, string.Empty));
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _load = null;
        _lastLoad = DateTime.MinValue;
    }

    private async Task<CMUPlaytimeLeaderboardSnapshot> LoadSnapshot()
    {
        // Runs before the first await, so prototype and Loc reads stay on the main thread
        var (xenoTrackers, govforTrackers) = GetTrackers();
        var roleNames = GetRoleNames();

        var rows = await _db.GetCMUPlaytimeLeaderboardRows(xenoTrackers.Concat(govforTrackers).ToList());
        var byTracker = rows.ToLookup(row => row.Tracker);
        var xenoTops = BuildTops(byTracker, roleNames, xenoTrackers);
        var govforTops = BuildTops(byTracker, roleNames, govforTrackers);
        var xenoChampions = BuildChampions(rows, xenoTrackers.ToHashSet());
        var govforChampions = BuildChampions(rows, govforTrackers.ToHashSet());

        // Tags are fetched only for players that made a board, not everyone with hours
        var tagIds = xenoTops
            .SelectMany(top => top.Rows)
            .Select(row => row.PlayerId)
            .Concat(xenoChampions.Select(champion => champion.PlayerId))
            .Distinct()
            .ToList();
        var tagPairs = await Task.WhenAll(tagIds.Select(async id => (Id: id, Tag: await GetXenoTag(id))));
        var tags = tagPairs.ToDictionary(pair => pair.Id, pair => pair.Tag);

        return new CMUPlaytimeLeaderboardSnapshot(
            xenoTrackers,
            govforTrackers,
            rows.Select(row => row.PlayerId).Distinct().Count(),
            xenoTops,
            govforTops,
            xenoChampions,
            govforChampions,
            byTracker,
            tags);
    }

    private (List<string> Xeno, List<string> Govfor) GetTrackers()
    {
        var xeno = new List<string>();
        var govfor = new List<string>();

        foreach (var tracker in _prototype.EnumeratePrototypes<PlayTimeTrackerPrototype>())
        {
            if (tracker.IsXeno)
                xeno.Add(tracker.ID);
            else if (tracker.IsHumanoid)
                govfor.Add(tracker.ID);
        }

        return (xeno, govfor);
    }

    private Dictionary<string, string> GetRoleNames()
    {
        var names = new Dictionary<string, string>();

        foreach (var job in _prototype.EnumeratePrototypes<JobPrototype>())
        {
            if (string.IsNullOrEmpty(job.PlayTimeTracker))
                continue;

            var name = Loc.TryGetString(job.Name, out var text) ? text : job.Name;
            names.TryAdd(job.PlayTimeTracker, name);
        }

        return names;
    }

    // Synchronous twin of the DB fetch below, using the main-thread prefs cache
    private string GetXenoTag(NetUserId userId)
    {
        var profile = _prefs.GetPreferences(userId).SelectedCharacter;
        var prefix = profile.XenoPrefix.Length > 0 ? profile.XenoPrefix : DefaultPrefix;
        return profile.XenoPostfix.Length > 0 ? $"({prefix}-{profile.XenoPostfix})" : $"({prefix})";
    }

    // The preferences cache is not thread safe, so tags always come from the database
    private async Task<string> GetXenoTag(Guid playerGuid)
    {
        try
        {
            var raw = await _db.GetPlayerPreferencesAsync(new NetUserId(playerGuid), CancellationToken.None);
            if (raw == null)
                return string.Empty;

            var profile = ((ServerPreferencesManager) _prefs).ConvertPreferences(raw).SelectedCharacter;
            var prefix = profile.XenoPrefix.Length > 0 ? profile.XenoPrefix : DefaultPrefix;
            return profile.XenoPostfix.Length > 0 ? $"({prefix}-{profile.XenoPostfix})" : $"({prefix})";
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Failed to load xeno name tag for {playerGuid}:\n{e}");
            return string.Empty;
        }
    }

    private static List<CMUPlaytimeTop> BuildTops(
        ILookup<string, CMUPlaytimeLeaderboardRow> rows,
        Dictionary<string, string> roleNames,
        IReadOnlyList<string> trackers)
    {
        return trackers
            .Select(tracker => rows[tracker].ToList())
            .Where(trackerRows => trackerRows.Count > 0)
            .Select(trackerRows => new CMUPlaytimeTop(
                roleNames.GetValueOrDefault(trackerRows[0].Tracker, trackerRows[0].Tracker),
                trackerRows[0].Tracker,
                trackerRows.Count,
                trackerRows.Sum(row => row.Hours),
                trackerRows
                    .OrderByDescending(row => row.Hours)
                    .Take(TopPerRole)
                    .ToList()))
            .OrderBy(top => top.Role, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<CMUPlaytimeChampion> BuildChampions(
        List<CMUPlaytimeLeaderboardRow> rows,
        HashSet<string> trackers)
    {
        return rows
            .Where(row => trackers.Contains(row.Tracker))
            .GroupBy(row => row.PlayerId)
            .Select(group => new CMUPlaytimeChampion(
                group.Key,
                group.First().Player,
                group.Sum(row => row.Hours)))
            .OrderByDescending(champion => champion.Hours)
            .Take(ChampionsPerSide)
            .ToList();
    }

    private static List<CMUPlaytimeRoleLeaderboard> BakeRoles(
        List<CMUPlaytimeTop> tops,
        CMUPlaytimeLeaderboardSnapshot snapshot,
        IReadOnlyDictionary<string, TimeSpan> times,
        ICommonSession player,
        string youTag)
    {
        var userId = player.UserId.UserId;
        var roles = new List<CMUPlaytimeRoleLeaderboard>();

        foreach (var top in tops)
        {
            var entries = new List<CMUPlaytimeLeaderboardEntry>();
            var inBoard = false;

            for (var i = 0; i < top.Rows.Count; i++)
            {
                var row = top.Rows[i];
                var isYou = row.PlayerId == userId;
                inBoard |= isYou;
                entries.Add(new CMUPlaytimeLeaderboardEntry(
                    i + 1,
                    row.Player,
                    row.Hours,
                    snapshot.Tags.GetValueOrDefault(row.PlayerId, string.Empty),
                    isYou));
            }

            // The viewer's own row, when they have hours but missed the top
            if (!inBoard
                && times.GetValueOrDefault(top.Tracker).TotalHours > 0)
            {
                var hours = times[top.Tracker].TotalHours;
                var rank = 1 + snapshot.Rows[top.Tracker].Count(row => row.Hours > hours);
                entries.Add(new CMUPlaytimeLeaderboardEntry(rank, player.Name, hours, youTag, true));
            }

            roles.Add(new CMUPlaytimeRoleLeaderboard(top.Role, top.PlayerCount, top.TotalHours, entries));
        }

        return roles;
    }

    private static List<CMUPlaytimeLeaderboardEntry> BakeChampions(
        List<CMUPlaytimeChampion> champions,
        Dictionary<Guid, string> tags,
        List<string> trackers,
        IReadOnlyDictionary<string, TimeSpan> times,
        ICommonSession player,
        string youTag)
    {
        var userId = player.UserId.UserId;
        var entries = new List<CMUPlaytimeLeaderboardEntry>();
        var inBoard = false;

        for (var i = 0; i < champions.Count; i++)
        {
            var champion = champions[i];
            var isYou = champion.PlayerId == userId;
            inBoard |= isYou;
            entries.Add(new CMUPlaytimeLeaderboardEntry(
                i + 1,
                champion.Player,
                champion.Hours,
                tags.GetValueOrDefault(champion.PlayerId, string.Empty),
                isYou));
        }

        if (!inBoard)
        {
            var hours = trackers.Sum(tracker => times.GetValueOrDefault(tracker).TotalHours);
            if (hours > 0)
                entries.Add(new CMUPlaytimeLeaderboardEntry(0, player.Name, hours, youTag, true));
        }

        return entries;
    }
}
