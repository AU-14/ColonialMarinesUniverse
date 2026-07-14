using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Shared._CMU14.BalanceRating;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14.util;
using Content.Shared.EntityList;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.BalanceRating;

public sealed partial class CMUBalanceRatingSystem : EntitySystem
{
    public static readonly ProtoId<EntityListPrototype> WeaponTargets = "CMUBalanceRatingWeapons";
    public static readonly ProtoId<EntityListPrototype> XenoTargets = "CMUBalanceRatingXenos";

    public static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MinimumDuration = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan MaximumDuration = TimeSpan.FromMinutes(2);

    private const int PersistenceAttempts = 5;
    private static readonly TimeSpan AutomaticLobbyGracePeriod = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PersistenceRetryDelay = TimeSpan.FromSeconds(1);

    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IComponentFactory _components = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private ITaskManager _task = default!;

    private readonly ISawmill _sawmill = Logger.GetSawmill("cmu.balance_rating");

    private ActiveRating? _active;
    private readonly CMUBalanceRatingSchedule _automaticSchedule = new(1);
    private int? _automaticPendingRoundId;
    private TimeSpan _automaticStartTime;
    private bool _automaticEnabled;
    private bool _cancelStarting;
    private int _lastAutomaticAttemptRoundId;
    private bool _starting;

    public bool HasActiveRating => _active != null || _starting;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnGameRunLevelChanged);
        SubscribeNetworkEvent<CMUBalanceRatingResponseEvent>(OnRatingResponse);
        Subs.CVar(_configuration,
            CMUBalanceRatingCVars.AutomaticEnabled,
            SetAutomaticEnabled,
            true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_active is { } active && _timing.RealTime >= active.EndTime)
            CloseActiveRating();

        UpdateAutomaticRating();
    }

    public Task<(bool Success, string Message)> TryStartRating(
        string targetId,
        CMUBalanceRatingMetric metric,
        TimeSpan duration,
        Guid? createdBy)
    {
        return TryStartRating(targetId, metric, duration, createdBy, false);
    }

    private async Task<(bool Success, string Message)> TryStartRating(
        string targetId,
        CMUBalanceRatingMetric metric,
        TimeSpan duration,
        Guid? createdBy,
        bool automatic)
    {
        if (_gameTicker.RunLevel != GameRunLevel.PreRoundLobby)
            return (false, Loc.GetString("cmu-balance-rating-command-lobby-only"));

        if (_gameTicker.RoundId <= 0)
            return (false, Loc.GetString("cmu-balance-rating-command-round-unavailable"));

        if (HasActiveRating)
            return (false, Loc.GetString("cmu-balance-rating-command-already-active"));

        if (duration < MinimumDuration || duration > MaximumDuration)
        {
            return (false, Loc.GetString("cmu-balance-rating-command-duration-range",
                ("minimum", (int) MinimumDuration.TotalSeconds),
                ("maximum", (int) MaximumDuration.TotalSeconds)));
        }

        if (!Enum.IsDefined(metric))
            return (false, Loc.GetString("cmu-balance-rating-command-invalid-metric"));

        if (!TryGetTarget(targetId, out var target))
            return (false, Loc.GetString("cmu-balance-rating-command-invalid-target", ("target", targetId)));

        if (!target.AllowsMetric(metric))
            return (false, Loc.GetString("cmu-balance-rating-command-map-fun-only"));

        var eligible = GetEligiblePlayers();
        if (eligible.Count == 0)
            return (false, Loc.GetString("cmu-balance-rating-command-no-players"));

        _cancelStarting = false;
        _starting = true;
        var roundId = _gameTicker.RoundId;

        try
        {
            var openedAt = DateTime.UtcNow;
            var pollId = await _db.CreateCMUBalanceRatingPoll(
                roundId,
                target.Target,
                target.Id,
                metric,
                createdBy,
                openedAt);

            var result = await RunOnMainThread(() => FinishStartingRating(
                pollId,
                roundId,
                target,
                metric,
                duration,
                eligible,
                automatic));

            if (result.DiscardPoll)
                await DeleteAbortedPoll(pollId);

            return (result.Success, result.Message);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to open a lobby balance rating for {targetId}:\n{e}");
            return (false, Loc.GetString("cmu-balance-rating-command-database-error"));
        }
        finally
        {
            await RunOnMainThread(() =>
            {
                _starting = false;
                _cancelStarting = false;
                return true;
            });
        }
    }

    private StartResult FinishStartingRating(
        long pollId,
        int roundId,
        CMUBalanceRatingTargetOption target,
        CMUBalanceRatingMetric metric,
        TimeSpan duration,
        HashSet<NetUserId> eligible,
        bool automatic)
    {
        if (_cancelStarting || automatic && !_automaticEnabled)
        {
            return new StartResult(
                false,
                true,
                Loc.GetString("cmu-balance-rating-command-start-cancelled"));
        }

        if (_gameTicker.RunLevel != GameRunLevel.PreRoundLobby || _gameTicker.RoundId != roundId)
        {
            return new StartResult(
                false,
                true,
                Loc.GetString("cmu-balance-rating-command-lobby-ended"));
        }

        var active = new ActiveRating(
            pollId,
            target.Target,
            metric,
            target.Id,
            target.Name,
            _timing.RealTime + duration,
            eligible);
        _active = active;
        ScheduleNextAutomaticRating();

        var opened = new CMUBalanceRatingOpenEvent(
            active.PollId,
            active.Target,
            active.Metric,
            active.TargetId,
            active.TargetName,
            duration);

        foreach (var session in _players.Sessions)
        {
            if (eligible.Contains(session.UserId))
                RaiseNetworkEvent(opened, session.Channel);
        }

        _sawmill.Info($"Opened lobby rating poll {pollId} for {target.Target} {target.Id} / {metric} to {eligible.Count} players.");
        return new StartResult(
            true,
            false,
            Loc.GetString("cmu-balance-rating-command-started",
                ("target", target.Name),
                ("metric", metric.ToString()),
                ("players", eligible.Count)));
    }

    public bool CancelActiveRating()
    {
        if (_starting)
        {
            _cancelStarting = true;
            return true;
        }

        if (_active == null)
            return false;

        CloseActiveRating();
        return true;
    }

    public IEnumerable<CMUBalanceRatingTargetOption> GetTargets()
    {
        foreach (var target in GetEntityTargets(CMUBalanceRatingTarget.Weapon, WeaponTargets))
            yield return target;

        foreach (var target in GetEntityTargets(CMUBalanceRatingTarget.Xeno, XenoTargets))
            yield return target;

        foreach (var target in GetMapTargets())
            yield return target;
    }

    public bool TryGetTarget(string targetId, out CMUBalanceRatingTargetOption target)
    {
        foreach (var option in GetTargets())
        {
            if (!option.Id.Equals(targetId, StringComparison.OrdinalIgnoreCase))
                continue;

            target = option;
            return true;
        }

        target = default;
        return false;
    }

    internal bool TryGetTargetName(
        CMUBalanceRatingTarget target,
        string targetId,
        out string targetName)
    {
        if (target != CMUBalanceRatingTarget.Map)
        {
            if (_prototypes.TryIndex<EntityPrototype>(targetId, out var prototype))
            {
                targetName = prototype.Name;
                return true;
            }

            targetName = string.Empty;
            return false;
        }

        var separator = targetId.IndexOf('/');
        if (separator <= 0 || separator == targetId.Length - 1)
        {
            targetName = string.Empty;
            return false;
        }

        var planetId = targetId[..separator];
        var presetId = targetId[(separator + 1)..];
        if (!_prototypes.TryIndex<EntityPrototype>(planetId, out var planetPrototype) ||
            !planetPrototype.TryComp(out RMCPlanetMapPrototypeComponent? planet, _components) ||
            !_prototypes.TryIndex<GamePresetPrototype>(presetId, out var preset))
        {
            targetName = string.Empty;
            return false;
        }

        targetName = GetMapTargetName(planetPrototype, planet, preset);
        return true;
    }

    private IEnumerable<CMUBalanceRatingTargetOption> GetEntityTargets(
        CMUBalanceRatingTarget target,
        ProtoId<EntityListPrototype> listId)
    {
        if (!_prototypes.TryIndex(listId, out var list))
            yield break;

        foreach (var prototype in list.Entities(_prototypes)
                     .Where(prototype => !prototype.Abstract)
                     .DistinctBy(prototype => prototype.ID))
        {
            yield return new CMUBalanceRatingTargetOption(target, prototype.ID, prototype.Name);
        }
    }

    private IEnumerable<CMUBalanceRatingTargetOption> GetMapTargets()
    {
        foreach (var preset in _prototypes.EnumeratePrototypes<GamePresetPrototype>()
                     .Where(preset => preset.ShowInVote))
        {
            var planetIds = GetPresetPlanetIds(preset);
            foreach (var planetId in planetIds)
            {
                if (!_prototypes.TryIndex<EntityPrototype>(planetId, out var planetPrototype) ||
                    !planetPrototype.TryComp(out RMCPlanetMapPrototypeComponent? planet, _components) ||
                    !planet.InRotation)
                {
                    continue;
                }

                var id = $"{planetPrototype.ID}/{preset.ID}";
                var name = GetMapTargetName(planetPrototype, planet, preset);
                yield return new CMUBalanceRatingTargetOption(CMUBalanceRatingTarget.Map, id, name);
            }
        }
    }

    private IEnumerable<string> GetPresetPlanetIds(GamePresetPrototype preset)
    {
        if (!string.IsNullOrWhiteSpace(preset.PlanetPool) &&
            _prototypes.TryIndex<GamePlanetPoolPrototype>(preset.PlanetPool, out var pool))
        {
            return pool.Planets;
        }

        return preset.SupportedPlanets ?? [];
    }

    private static string GetMapTargetName(
        EntityPrototype planetPrototype,
        RMCPlanetMapPrototypeComponent planet,
        GamePresetPrototype preset)
    {
        var planetName = string.IsNullOrWhiteSpace(planet.VoteName)
            ? planetPrototype.Name
            : planet.VoteName;
        return $"{planetName} — {preset.ModeTitle}";
    }

    private void OnGameRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        var lobbyRestarted = ev.Old == GameRunLevel.PreRoundLobby &&
                             ev.New == GameRunLevel.PreRoundLobby;
        if (ev.New != GameRunLevel.PreRoundLobby || lobbyRestarted)
        {
            _automaticPendingRoundId = null;
            _cancelStarting = _starting;
            CloseActiveRating();
        }

        if (_automaticEnabled &&
            ev.New == GameRunLevel.InRound &&
            _automaticSchedule.CountRound(_gameTicker.RoundId))
        {
            _sawmill.Debug($"Automatic lobby rating is due after round {_gameTicker.RoundId}.");
        }
    }

    private void SetAutomaticEnabled(bool enabled)
    {
        if (_automaticEnabled == enabled)
            return;

        _automaticEnabled = enabled;
        _automaticPendingRoundId = null;

        if (enabled)
            ScheduleNextAutomaticRating();
    }

    private void ScheduleNextAutomaticRating()
    {
        if (!_automaticEnabled)
            return;

        var interval = GetAutomaticRoundInterval();
        _automaticSchedule.Reset(interval);
        _automaticPendingRoundId = null;
        _sawmill.Debug($"Next automatic lobby rating is scheduled after {interval} rounds.");
    }

    private int GetAutomaticRoundInterval()
    {
        var minimum = Math.Max(1, _configuration.GetCVar(CMUBalanceRatingCVars.AutomaticMinimumRounds));
        var maximum = Math.Max(minimum, _configuration.GetCVar(CMUBalanceRatingCVars.AutomaticMaximumRounds));

        if (maximum == int.MaxValue)
            return _random.Next(minimum - 1, maximum) + 1;

        return _random.Next(minimum, maximum + 1);
    }

    private void UpdateAutomaticRating()
    {
        if (!_automaticEnabled ||
            !_automaticSchedule.Due ||
            _gameTicker.RunLevel != GameRunLevel.PreRoundLobby ||
            _gameTicker.RoundId <= 0)
        {
            return;
        }

        var roundId = _gameTicker.RoundId;
        if (_lastAutomaticAttemptRoundId == roundId)
            return;

        if (_automaticPendingRoundId != roundId)
        {
            _automaticPendingRoundId = roundId;
            _automaticStartTime = _timing.RealTime + AutomaticLobbyGracePeriod;
            return;
        }

        if (_timing.RealTime < _automaticStartTime)
            return;

        _automaticPendingRoundId = null;
        _lastAutomaticAttemptRoundId = roundId;

        if (HasActiveRating)
            return;

        _ = StartAutomaticRating(roundId);
    }

    private async Task StartAutomaticRating(int roundId)
    {
        try
        {
            if (!_automaticEnabled ||
                !_automaticSchedule.Due ||
                _gameTicker.RunLevel != GameRunLevel.PreRoundLobby ||
                _gameTicker.RoundId != roundId ||
                HasActiveRating)
            {
                return;
            }

            var targets = GetTargets().ToList();
            if (targets.Count == 0)
            {
                _sawmill.Warning("Automatic lobby rating could not start because no valid targets are configured.");
                return;
            }

            var target = _random.Pick(targets);
            var metric = SelectAutomaticMetric(target.Target, _random.Prob(0.5f));
            var result = await TryStartRating(target.Id, metric, DefaultDuration, null, true);
            if (!result.Success)
            {
                _sawmill.Info($"Automatic lobby rating for round {roundId} did not start: {result.Message}");
            }
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to automatically open a lobby balance rating for round {roundId}:\n{e}");
        }
    }

    internal static CMUBalanceRatingMetric SelectAutomaticMetric(
        CMUBalanceRatingTarget target,
        bool chooseFun)
    {
        return target == CMUBalanceRatingTarget.Map || chooseFun
            ? CMUBalanceRatingMetric.Fun
            : CMUBalanceRatingMetric.Power;
    }

    private async void OnRatingResponse(CMUBalanceRatingResponseEvent ev, EntitySessionEventArgs args)
    {
        var active = _active;
        var session = args.SenderSession;

        if (active == null ||
            ev.PollId != active.PollId ||
            ev.Rating is < 1 or > 5 ||
            _gameTicker.RunLevel != GameRunLevel.PreRoundLobby ||
            _timing.RealTime > active.EndTime ||
            !active.EligiblePlayers.Contains(session.UserId) ||
            !active.RespondedPlayers.Add(session.UserId))
        {
            return;
        }

        var playerId = session.UserId;
        var persisted = await PersistResponse(active.PollId, playerId.UserId, ev.Rating);
        _task.RunOnMainThread(() => FinishRatingResponse(active, playerId, persisted));
    }

    private void FinishRatingResponse(ActiveRating active, NetUserId playerId, bool persisted)
    {
        if (persisted)
        {
            if (_players.TryGetSessionById(playerId, out var currentSession))
                RaiseNetworkEvent(new CMUBalanceRatingCloseEvent(active.PollId), currentSession.Channel);

            return;
        }

        active.RespondedPlayers.Remove(playerId);
        _sawmill.Error($"Failed to save response from {playerId} for balance rating poll {active.PollId} after {PersistenceAttempts} attempts.");

        if (_active != active ||
            _gameTicker.RunLevel != GameRunLevel.PreRoundLobby ||
            _timing.RealTime >= active.EndTime ||
            !_players.TryGetSessionById(playerId, out var retrySession))
        {
            return;
        }

        RaiseNetworkEvent(new CMUBalanceRatingOpenEvent(
            active.PollId,
            active.Target,
            active.Metric,
            active.TargetId,
            active.TargetName,
            active.EndTime - _timing.RealTime), retrySession.Channel);
    }

    private async Task<bool> PersistResponse(long pollId, Guid playerId, byte rating)
    {
        for (var attempt = 1; attempt <= PersistenceAttempts; attempt++)
        {
            try
            {
                await _db.AddCMUBalanceRatingResponse(pollId, playerId, rating, DateTime.UtcNow);
                return true;
            }
            catch (Exception e)
            {
                if (attempt == PersistenceAttempts)
                {
                    _sawmill.Error($"Attempt {attempt} to save a response for balance rating poll {pollId} failed:\n{e}");
                    break;
                }

                _sawmill.Warning($"Attempt {attempt} to save a response for balance rating poll {pollId} failed; retrying: {e.Message}");
                await Task.Delay(PersistenceRetryDelay);
            }
        }

        return false;
    }

    private HashSet<NetUserId> GetEligiblePlayers()
    {
        var players = new HashSet<NetUserId>();
        foreach (var session in _players.Sessions)
        {
            if (session.Status is not (SessionStatus.Connected or SessionStatus.InGame) ||
                !_gameTicker.PlayerGameStatuses.TryGetValue(session.UserId, out var status) ||
                status == PlayerGameStatus.JoinedGame)
            {
                continue;
            }

            players.Add(session.UserId);
        }

        return players;
    }

    private void CloseActiveRating()
    {
        if (_active is not { } active)
            return;

        _active = null;
        var closed = new CMUBalanceRatingCloseEvent(active.PollId);

        foreach (var session in _players.Sessions)
        {
            if (active.EligiblePlayers.Contains(session.UserId))
                RaiseNetworkEvent(closed, session.Channel);
        }

        _ = PersistClosedPoll(active.PollId);
    }

    private async Task PersistClosedPoll(long pollId)
    {
        for (var attempt = 1; attempt <= PersistenceAttempts; attempt++)
        {
            try
            {
                await _db.CloseCMUBalanceRatingPoll(pollId, DateTime.UtcNow);
                return;
            }
            catch (Exception e)
            {
                if (attempt == PersistenceAttempts)
                {
                    _sawmill.Error($"Failed to close balance rating poll {pollId} after {attempt} attempts:\n{e}");
                    return;
                }

                _sawmill.Warning($"Attempt {attempt} to close balance rating poll {pollId} failed; retrying: {e.Message}");
                await Task.Delay(PersistenceRetryDelay);
            }
        }
    }

    private async Task DeleteAbortedPoll(long pollId)
    {
        for (var attempt = 1; attempt <= PersistenceAttempts; attempt++)
        {
            try
            {
                await _db.DeleteCMUBalanceRatingPoll(pollId);
                return;
            }
            catch (Exception e)
            {
                if (attempt == PersistenceAttempts)
                {
                    _sawmill.Error($"Failed to discard aborted balance rating poll {pollId} after {attempt} attempts:\n{e}");
                    return;
                }

                _sawmill.Warning($"Attempt {attempt} to discard aborted balance rating poll {pollId} failed; retrying: {e.Message}");
                await Task.Delay(PersistenceRetryDelay);
            }
        }
    }

    private Task<T> RunOnMainThread<T>(Func<T> action)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _task.RunOnMainThread(() =>
        {
            try
            {
                completion.SetResult(action());
            }
            catch (Exception e)
            {
                completion.SetException(e);
            }
        });

        return completion.Task;
    }

    private readonly record struct StartResult(bool Success, bool DiscardPoll, string Message);

    private sealed class ActiveRating(
        long pollId,
        CMUBalanceRatingTarget target,
        CMUBalanceRatingMetric metric,
        string targetId,
        string targetName,
        TimeSpan endTime,
        HashSet<NetUserId> eligiblePlayers)
    {
        public readonly long PollId = pollId;
        public readonly CMUBalanceRatingTarget Target = target;
        public readonly CMUBalanceRatingMetric Metric = metric;
        public readonly string TargetId = targetId;
        public readonly string TargetName = targetName;
        public readonly TimeSpan EndTime = endTime;
        public readonly HashSet<NetUserId> EligiblePlayers = eligiblePlayers;
        public readonly HashSet<NetUserId> RespondedPlayers = new();
    }
}

public readonly record struct CMUBalanceRatingTargetOption(
    CMUBalanceRatingTarget Target,
    string Id,
    string Name)
{
    public bool AllowsMetric(CMUBalanceRatingMetric metric)
    {
        return Target != CMUBalanceRatingTarget.Map || metric == CMUBalanceRatingMetric.Fun;
    }
}
