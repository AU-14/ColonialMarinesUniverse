using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.CMU14.Round.Roles;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Lobby;

/// <summary>Changes live available slots, or queues changes until the lobby's maps and job rules have loaded.</summary>
public sealed partial class CMUEventSlotsSystem : EntitySystem
{
    [Dependency] private CMUEventAccessSystem _access = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private StationJobsSystem _jobs = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private AuRoundSystem _round = default!;
    [Dependency] private StationSystem _stations = default!;
    [Dependency] private CMUEventJobTargetsSystem _targets = default!;
    [Dependency] private GameTicker _ticker = default!;

    private sealed record PendingChange(string Side, string Role, int Amount, bool Set);
    private readonly List<PendingChange> _pending = new();
    public event Action? Changed;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMURoundJobsReadyEvent>(OnJobsReady);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => ClearPending());
    }

    public void ClearPending()
    {
        _pending.Clear();
        Changed?.Invoke();
    }

    public bool TryChange(string selector, string role, int amount, bool set, out string message)
    {
        if (!_access.Enabled)
        {
            message = Loc.GetString("cmu-event-disabled");
            return false;
        }
        if (!TryResolveJob(selector, role, out var job, out message))
            return false;

        if ((set && amount < -1) || (!set && amount == 0))
        {
            message = Loc.GetString("cmu-event-slots-invalid-amount");
            return false;
        }

        if (_ticker.RunLevel == GameRunLevel.PreRoundLobby)
        {
            _pending.Add(new PendingChange(selector, job.ID, amount, set));
            Changed?.Invoke();
            message = Loc.GetString("cmu-event-slots-queued", ("side", selector), ("job", job.LocalizedName));
            return true;
        }

        return TryApply(selector, job, amount, set, out message);
    }

    private bool TryResolveJob(string selector, string role, out JobPrototype job, out string message)
    {
        job = default!;
        if (!_targets.TryResolveSide(selector, out var side))
        {
            message = Loc.GetString("cmu-event-invalid-side", ("side", selector));
            return false;
        }

        var matches = _targets.GetJobs(side, role);
        if (matches.Count != 1)
        {
            message = Loc.GetString("cmu-event-slots-one-job", ("role", role), ("side", selector));
            return false;
        }

        job = matches[0];
        message = string.Empty;
        return true;
    }

    private bool TryFindStation(JobPrototype job, out EntityUid station)
    {
        // Use the same ship/planet routing as AddJobsRuleSystem. Other maps may retain closed
        // copies of the same job, so job-ID presence alone does not identify the active station.
        var configured = new HashSet<EntityUid>();
        var planet = _round.GetSelectedPlanet();
        if (planet != null && job.RoundSide is RoundJobSide.Govfor or RoundJobSide.Opfor)
        {
            var inShip = job.RoundSide == RoundJobSide.Govfor ? planet.GovforInShip : planet.OpforInShip;
            if (!inShip)
            {
                if (_stations.GetStationInMap(_ticker.DefaultMap) is { } main && HasComp<StationJobsComponent>(main))
                    configured.Add(main);
            }
            else
            {
                // Faction tags also belong to deployed dropships. Only the selected carrier's
                // loaded grids identify the station that owns this side's job slots.
                var preset = _ticker.CurrentPreset ?? _ticker.Preset;
                var ship = job.RoundSide == RoundJobSide.Govfor
                    ? _round.ResolveGovforShip(preset)
                    : _round.ResolveOpforShip(preset);
                if (!string.IsNullOrEmpty(ship) && _round.TryGetLoadedShipGrids(ship, job.RoundSide == RoundJobSide.Govfor ? "govfor" : "opfor", out var grids))
                {
                    foreach (var grid in grids)
                    {
                        if (_stations.GetOwningStation(grid) is { } owner && HasComp<StationJobsComponent>(owner))
                            configured.Add(owner);
                    }
                }
            }
        }

        if (configured.Count > 0)
        {
            station = configured.Count == 1 ? configured.First() : EntityUid.Invalid;
            return configured.Count == 1;
        }

        var exact = new List<EntityUid>();
        var sameSide = new List<EntityUid>();
        var query = EntityQueryEnumerator<StationJobsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var available = _jobs.GetJobs(uid, comp);
            if (available.ContainsKey(job.ID))
                exact.Add(uid);
            if (available.Keys.Any(id => _prototypes.TryIndex(id, out var other) && other.RoundSide == job.RoundSide))
                sameSide.Add(uid);
        }

        // A role may not be enabled on this map yet. Use the side's station only when unambiguous.
        var candidates = exact.Count == 0 ? sameSide : exact;
        station = candidates.Count == 1 ? candidates[0] : EntityUid.Invalid;
        return candidates.Count == 1;
    }

    private bool TryApply(string selector, JobPrototype job, int amount, bool set, out string message)
    {
        if (!TryFindStation(job, out var station))
        {
            message = Loc.GetString("cmu-event-slots-no-station", ("job", job.LocalizedName));
            return false;
        }

        var exists = _jobs.TryGetJobSlot(station, job, out var available);
        if (!set && exists && available == null)
        {
            message = Loc.GetString("cmu-event-slots-already-unlimited");
            return false;
        }

        var result = set ? amount : (long) (available ?? 0) + amount;
        var total = _jobs.GetJobs(station).Where(pair => pair.Key.Id != job.ID).Sum(pair => (long) (pair.Value ?? 0));
        if (result < -1 || (!set && result < 0) || result > int.MaxValue || total + Math.Max(result, 0) > int.MaxValue)
        {
            message = Loc.GetString("cmu-event-slots-invalid-amount");
            return false;
        }

        if (result == -1)
            _jobs.MakeJobUnlimited(station, job);
        else
            _jobs.TrySetJobSlot(station, job, (int) result, createSlot: true);

        // Keep round-start minimums and overflow bookkeeping consistent with the available slots.
        _jobs.SetEventRoundStartSlots(station, job.ID, (int) result, set, amount);
        var display = result == -1 ? Loc.GetString("cmu-event-slots-unlimited") : result.ToString();
        message = Loc.GetString("cmu-event-slots-changed", ("side", selector), ("job", job.LocalizedName), ("count", display));
        Changed?.Invoke();
        return true;
    }

    public List<string> Describe(string selector)
    {
        var lines = new List<string>();
        if (!_targets.TryResolveSide(selector, out var side))
        {
            lines.Add(Loc.GetString("cmu-event-invalid-side", ("side", selector)));
            return lines;
        }
        foreach (var pending in _pending)
        {
            if (!_targets.TryResolveSide(pending.Side, out var pendingSide) || pendingSide != side)
                continue;
            var name = _prototypes.TryIndex<JobPrototype>(pending.Role, out var pendingJob)
                ? pendingJob.LocalizedName : pending.Role;
            lines.Add(Loc.GetString("cmu-event-slots-pending", ("side", pending.Side), ("job", name),
                ("operation", pending.Set ? "set" : "add"), ("amount", pending.Amount)));
        }

        var query = EntityQueryEnumerator<StationJobsComponent>();
        while (query.MoveNext(out var station, out var comp))
        {
            foreach (var (id, amount) in _jobs.GetJobs(station, comp).OrderBy(pair => pair.Key.Id))
            {
                if (!_prototypes.TryIndex(id, out var job) || job.RoundSide != side)
                    continue;
                lines.Add($"{job.LocalizedName}: {amount?.ToString() ?? Loc.GetString("cmu-event-slots-unlimited")}");
            }
        }
        return lines;
    }

    private void OnJobsReady(CMURoundJobsReadyEvent ev)
    {
        if (!_access.Enabled)
        {
            ClearPending();
            return;
        }
        foreach (var pending in _pending)
        {
            if (TryResolveJob(pending.Side, pending.Role, out var job, out var error) &&
                TryApply(pending.Side, job, pending.Amount, pending.Set, out error))
                Log.Info(error);
            else
            {
                Log.Error(error);
                _chat.SendAdminAlert(error);
            }
        }
        ClearPending();
    }
}
