using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Fob;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Fob;

/// <summary>
/// Authoritative reserve and relay-job lifecycle. Map selection, repair DoAfters, controls, and weapons
/// integrate through these server-only methods; this system does not change any existing defense or flight rules.
/// </summary>
public sealed partial class CMUFobReserveSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    // Keep deleted/closed networks as tombstones until round cleanup: replacing hardware cannot mint a new bank.
    private readonly Dictionary<string, EntityUid> _networks = new(StringComparer.OrdinalIgnoreCase);
    private bool _enabled;
    private bool _roundConfigured;

    public bool Enabled => _enabled;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<CMUFobNetworkComponent, ComponentShutdown>(OnNetworkShutdown);
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        if (_roundConfigured)
            return;

        _enabled = _config.GetCVar(CCVars.CMUFobReserveEnabled);
        _roundConfigured = true;
    }

    private void OnRoundCleanup(RoundRestartCleanupEvent ev)
    {
        foreach (var uid in _networks.Values)
        {
            if (!TryComp<CMUFobNetworkComponent>(uid, out var comp))
                continue;

            CloseNetwork((uid, comp));
            QueueDel(uid);
        }

        var equipment = EntityQueryEnumerator<CMUFobInstructionsComponent>();
        while (equipment.MoveNext(out var uid, out _))
            RemCompDeferred<CMUFobInstructionsComponent>(uid);
        _networks.Clear();
        _enabled = false;
        _roundConfigured = false;
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New != GameRunLevel.PostRound)
            return;

        foreach (var uid in _networks.Values)
        {
            if (TryComp<CMUFobNetworkComponent>(uid, out var comp))
                CloseNetwork((uid, comp));
        }

        _enabled = false;
    }

    /// <summary>
    /// Called once by the approved landing/site selector. The site must be on a live, unpaused map.
    /// Another site or a replacement console cannot create a second network for this faction in this round.
    /// </summary>
    public bool TryCreateNetwork(EntityUid site, string faction, out Entity<CMUFobNetworkComponent> network, string? siteId = null)
    {
        network = default;
        if (!_enabled || string.IsNullOrWhiteSpace(faction) || !Exists(site) || TerminatingOrDeleted(site)
            || MetaData(site).EntityPaused || Transform(site).MapUid is not { } map)
            return false;

        faction = faction.Trim();
        if (_networks.ContainsKey(faction))
            return false;

        // The selected center can be under a landed dropship. Keep the logical bank attached to
        // the ground map, never the overlapping shuttle grid or the movable physical terminal.
        var uid = SpawnAttachedTo("CMUFobNetwork", new EntityCoordinates(map, _transform.GetMapCoordinates(site).Position));
        var comp = Comp<CMUFobNetworkComponent>(uid);
        comp.Faction = faction;
        comp.Map = map;
        comp.StoredReserve = TimeSpan.FromTicks(Math.Clamp(comp.StartingReserve.Ticks, 0, comp.Capacity.Ticks));
        comp.State = comp.StoredReserve > TimeSpan.Zero ? CMUFobReserveState.Standby : CMUFobReserveState.Empty;
        comp.NextJobAt = _timing.CurTime;
        _networks.Add(faction, uid);
        network = (uid, comp);
        BeginTrial(network, site, siteId);
        Notify(network);
        Log.Info($"Created FOB reserve for {faction} on {ToPrettyString(map)}.");
        return true;
    }

    /// <summary>Returns the existing round-owned network without creating or replenishing it.</summary>
    public bool TryGetNetwork(string faction, out Entity<CMUFobNetworkComponent> network)
    {
        network = default;
        if (!_networks.TryGetValue(faction.Trim(), out var uid)
            || !TryComp<CMUFobNetworkComponent>(uid, out var comp) || TerminatingOrDeleted(uid))
            return false;

        network = (uid, comp);
        return true;
    }

    private bool CanUse(Entity<CMUFobNetworkComponent> network)
    {
        return _enabled && !TerminatingOrDeleted(network) && !MetaData(network).EntityPaused
            && network.Comp.State != CMUFobReserveState.Closed
            && _networks.TryGetValue(network.Comp.Faction, out var registered) && registered == network.Owner
            && Transform(network).MapUid == network.Comp.Map;
    }

    /// <summary>Remaining reserve at this instant, frozen at the map's pause time while paused.</summary>
    public TimeSpan GetRemaining(Entity<CMUFobNetworkComponent> network)
    {
        if (network.Comp.DepletesAt is not { } deadline)
            return network.Comp.StoredReserve;

        var now = _timing.CurTime - _metadata.GetPauseTime(network);
        return deadline > now ? deadline - now : TimeSpan.Zero;
    }

    public bool TryActivate(Entity<CMUFobNetworkComponent> network)
    {
        if (!CanUse(network))
            return false;

        Refresh(network);
        var comp = network.Comp;
        var now = _timing.CurTime;
        if (comp.State is not CMUFobReserveState.Standby || comp.StoredReserve <= TimeSpan.Zero
            || now < comp.RestartAvailableAt)
            return false;

        comp.DepletesAt = now + comp.StoredReserve;
        comp.StartupEndsAt = now + comp.StartupDuration;
        comp.MinimumActiveUntil = now + comp.MinimumActivation;
        comp.State = CMUFobReserveState.Starting;
        comp.ActivationGeneration++;
        Comp<CMUFobTrialComponent>(network).Activations++;
        Notify(network);
        CheckWarnings(network);
        TrialLog(network, "activated");
        return true;
    }

    /// <summary>Recheck the deadline at the point of firing, regardless of system update order.</summary>
    public bool IsActive(Entity<CMUFobNetworkComponent> network)
    {
        if (!CanUse(network))
            return false;
        Refresh(network);
        return network.Comp.State == CMUFobReserveState.Active && GetRemaining(network) > TimeSpan.Zero;
    }

    public bool TryDeactivate(Entity<CMUFobNetworkComponent> network)
    {
        if (!CanUse(network))
            return false;

        Refresh(network);
        if (network.Comp.DepletesAt == null || _timing.CurTime < network.Comp.MinimumActiveUntil)
            return false;

        Stop(network, GetRemaining(network), _timing.CurTime);
        return true;
    }

    /// <summary>
    /// Called when the physical control is disabled. Pay the minimum activation even when hardware is destroyed;
    /// repairs use the same network and cannot refund the activation or clear the restart deadline.
    /// </summary>
    public void DisableControl(Entity<CMUFobNetworkComponent> network)
    {
        if (!CanUse(network))
            return;

        Refresh(network);
        if (network.Comp.DepletesAt == null)
            return;

        var remaining = GetRemaining(network);
        if (network.Comp.MinimumActiveUntil is { } minimum && minimum > _timing.CurTime)
        {
            Comp<CMUFobTrialComponent>(network).ControlLossChargeSeconds += Math.Min(remaining.TotalSeconds, (minimum - _timing.CurTime).TotalSeconds);
            remaining -= minimum - _timing.CurTime;
        }

        Stop(network, remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, _timing.CurTime);
    }

    private void Stop(Entity<CMUFobNetworkComponent> network, TimeSpan remaining, TimeSpan stoppedAt)
    {
        AccountTrial(network);
        if (remaining <= TimeSpan.Zero && network.Comp.DepletesAt != null)
        {
            var trial = Comp<CMUFobTrialComponent>(network);
            trial.Depletions++;
            if ((trial.WarningMask & 4) == 0)
            {
                trial.WarningMask |= 7;
                EmitWarning(network, trial, 0);
            }
        }
        var comp = network.Comp;
        comp.StoredReserve = remaining;
        comp.DepletesAt = null;
        comp.StartupEndsAt = null;
        comp.MinimumActiveUntil = null;
        comp.RestartAvailableAt = stoppedAt + comp.RestartDelay;
        comp.State = remaining > TimeSpan.Zero ? CMUFobReserveState.Standby : CMUFobReserveState.Empty;
        Notify(network);
        TrialLog(network, "stopped");
    }

    /// <summary>
    /// Offers two alternatives selected by the map's route/anchor policy. The backend validates identity,
    /// map, ownership and reuse deadlines, but does not claim that a pair is tactically fair.
    /// </summary>
    public bool TryOfferRelays(
        Entity<CMUFobNetworkComponent> network,
        EntityUid first,
        EntityUid second,
        out CMUFobRelayJob job)
    {
        job = default;
        if (!CanUse(network))
            return false;

        Refresh(network);
        if (network.Comp.JobExpiresAt != null || _timing.CurTime < network.Comp.NextJobAt || first == second
            || !IsAvailableRelay(network, first, out var a) || !IsAvailableRelay(network, second, out var b))
            return false;

        var generation = ++network.Comp.JobGeneration;
        job = new CMUFobRelayJob(network, generation);
        network.Comp.FirstRelay = first;
        network.Comp.SecondRelay = second;
        network.Comp.JobExpiresAt = _timing.CurTime + network.Comp.JobLifetime;
        a.Network = network.Owner;
        b.Network = network.Owner;
        a.JobGeneration = generation;
        b.JobGeneration = generation;
        var changed = new CMUFobRelayChangedEvent();
        RaiseLocalEvent(first, ref changed);
        RaiseLocalEvent(second, ref changed);
        EnsureComp<CMUFobInstructionsComponent>(first);
        EnsureComp<CMUFobInstructionsComponent>(second);
        Comp<CMUFobTrialComponent>(network).Offers++;
        TrialLog(network, "relay_offered", new { generation, first = first.Id, second = second.Id });
        MarkersChanged();
        AnnounceRelayPair(network, first, second);
        Log.Info($"FOB {network.Comp.Faction} offered relay job {generation}: {first}, {second}.");
        return true;
    }

    private bool IsAvailableRelay(
        Entity<CMUFobNetworkComponent> network,
        EntityUid uid,
        out CMUFobRelayComponent relay)
    {
        return TryComp(uid, out relay!) && !TerminatingOrDeleted(uid) && !MetaData(uid).EntityPaused
            && Transform(uid).MapUid == network.Comp.Map && relay.Network == null
            && _timing.CurTime >= relay.ReusableAt;
    }

    /// <summary>An assigned relay remains visible even when the bank is full.</summary>
    public bool TryGetRelayAssignment(EntityUid relay, out CMUFobRelayJob job)
    {
        job = default;
        if (!TryComp<CMUFobRelayComponent>(relay, out var endpoint) || endpoint.Network is not { } uid
            || !TryComp<CMUFobNetworkComponent>(uid, out var comp) || !CanUse((uid, comp)))
            return false;

        Refresh((uid, comp));
        if (comp.JobExpiresAt == null || endpoint.Network != uid || endpoint.JobGeneration != comp.JobGeneration
            || (comp.FirstRelay != relay && comp.SecondRelay != relay) || TerminatingOrDeleted(relay)
            || MetaData(relay).EntityPaused || Transform(relay).MapUid != comp.Map)
            return false;

        job = new CMUFobRelayJob(uid, comp.JobGeneration);
        return true;
    }

    /// <summary>Returns a currently redeemable job and its actual reward, without consuming the offer.</summary>
    public bool TryGetRelayJob(EntityUid relay, out CMUFobRelayJob job, out TimeSpan reward)
    {
        reward = TimeSpan.Zero;
        if (!TryGetRelayAssignment(relay, out job)) return false;
        var uid = job.Network;
        var comp = Comp<CMUFobNetworkComponent>(uid);
        var headroom = comp.Capacity - GetRemaining((uid, comp));
        if (headroom <= TimeSpan.Zero)
            return false;

        job = new CMUFobRelayJob(uid, comp.JobGeneration);
        reward = headroom < comp.RelayReward ? headroom : comp.RelayReward;
        return reward > TimeSpan.Zero;
    }

    /// <summary>
    /// Redeems an endpoint only after an authoritative repair interaction has completed and validated its user/tool.
    /// Capture the complete job token when that interaction starts. This is not a player-accessible network message.
    /// The first successful completion consumes both alternatives before notifying other systems.
    /// </summary>
    public bool TryCompleteRelay(EntityUid relay, CMUFobRelayJob job, out TimeSpan awarded)
    {
        awarded = TimeSpan.Zero;
        if (!TryGetRelayJob(relay, out var current, out var reward) || current != job)
            return false;

        Entity<CMUFobNetworkComponent> network = (job.Network, Comp<CMUFobNetworkComponent>(job.Network));
        var comp = network.Comp;
        awarded = reward;
        var trial = Comp<CMUFobTrialComponent>(network);
        trial.Completions++;
        trial.AwardedSeconds += awarded.TotalSeconds;
        trial.CappedSeconds += Math.Max(0, (comp.RelayReward - awarded).TotalSeconds);
        var remaining = GetRemaining(network) + awarded;
        if (comp.DepletesAt != null)
            comp.DepletesAt = _timing.CurTime + remaining;
        else
        {
            comp.StoredReserve = remaining;
            comp.State = CMUFobReserveState.Standby;
        }

        FinishJob(network, _timing.CurTime, comp.RelayInterval);
        RearmWarnings(network);
        Notify(network);
        TrialLog(network, "relay_completed", new { job.Generation, relay = relay.Id, awardedSeconds = awarded.TotalSeconds });
        Log.Info($"FOB {comp.Faction} redeemed job {job.Generation} at {relay}: +{awarded.TotalSeconds}s.");
        return true;
    }

    private void FinishJob(Entity<CMUFobNetworkComponent> network, TimeSpan resolvedAt, TimeSpan nextDelay)
    {
        var first = network.Comp.FirstRelay;
        var second = network.Comp.SecondRelay;
        // Consume the whole offer before cancellation callbacks can re-enter the reserve system.
        network.Comp.FirstRelay = null;
        network.Comp.SecondRelay = null;
        network.Comp.JobExpiresAt = null;
        network.Comp.NextJobAt = resolvedAt + nextDelay;
        ReleaseRelay(first, network, resolvedAt);
        ReleaseRelay(second, network, resolvedAt);
        MarkersChanged();
    }

    private void ReleaseRelay(EntityUid? uid, Entity<CMUFobNetworkComponent> network, TimeSpan resolvedAt)
    {
        if (uid is not { } relay || !TryComp<CMUFobRelayComponent>(relay, out var comp)
            || comp.Network != network.Owner || comp.JobGeneration != network.Comp.JobGeneration)
            return;

        comp.Network = null;
        comp.ReusableAt = resolvedAt + network.Comp.RelayReuseDelay;
        var changed = new CMUFobRelayChangedEvent();
        RaiseLocalEvent(relay, ref changed);
    }

    private void Refresh(Entity<CMUFobNetworkComponent> network)
    {
        var comp = network.Comp;
        var now = _timing.CurTime;
        AccountTrial(network);
        CheckWarnings(network);
        if (comp.DepletesAt is { } depleted && now >= depleted)
            Stop(network, TimeSpan.Zero, depleted);
        else if (comp.State == CMUFobReserveState.Starting && now >= comp.StartupEndsAt)
        {
            comp.State = CMUFobReserveState.Active;
            Notify(network);
        }

        if (comp.JobExpiresAt is { } expired && now >= expired)
        {
            FinishJob(network, expired, comp.ExpiredJobDelay);
            Comp<CMUFobTrialComponent>(network).Expiries++;
            TrialLog(network, "relay_expired");
            Log.Info($"FOB {comp.Faction} relay job {comp.JobGeneration} expired.");
        }
        var trial = Comp<CMUFobTrialComponent>(network);
        if (now >= trial.NextSummaryAt)
        {
            trial.NextSummaryAt = now + TimeSpan.FromMinutes(1);
            TrialLog(network, "sample");
        }
    }

    /// <summary>Final faction withdrawal/round outcome. Closing cannot be undone by replacing hardware.</summary>
    /// <summary>One physical backup activation per round-owned bank, including replacement generators.</summary>
    public bool TryUseBackup(Entity<CMUFobNetworkComponent> network)
    {
        if (!CanUse(network)) return false;
        Refresh(network);
        var comp = network.Comp;
        var remaining = GetRemaining(network);
        if (comp.BackupUsed || remaining >= comp.Capacity) return false;
        var award = TimeSpan.FromSeconds(Math.Min(120, (comp.Capacity - remaining).TotalSeconds));
        comp.BackupUsed = true;
        if (comp.DepletesAt != null) comp.DepletesAt += award;
        else
        {
            comp.StoredReserve = remaining + award;
            comp.State = CMUFobReserveState.Standby;
        }
        RearmWarnings(network);
        Notify(network);
        TrialLog(network, "backup", new { awardedSeconds = award.TotalSeconds });
        return true;
    }

    public void CloseNetwork(Entity<CMUFobNetworkComponent> network)
    {
        if (network.Comp.State == CMUFobReserveState.Closed)
            return;

        AccountTrial(network);
        if (TryComp<CMUFobTrialComponent>(network, out var trial) && !trial.Finalized)
        {
            TrialLog(network, "closed", new { outcome = _ticker.RunLevel.ToString() });
            trial.Finalized = true;
        }
        FinishJob(network, _timing.CurTime, TimeSpan.Zero);
        network.Comp.StoredReserve = TimeSpan.Zero;
        network.Comp.DepletesAt = null;
        network.Comp.StartupEndsAt = null;
        network.Comp.MinimumActiveUntil = null;
        network.Comp.State = CMUFobReserveState.Closed;
        Notify(network);
    }

    private void OnNetworkShutdown(Entity<CMUFobNetworkComponent> network, ref ComponentShutdown args)
    {
        CloseNetwork(network);
    }

    private void Notify(Entity<CMUFobNetworkComponent> network)
    {
        var ev = new CMUFobReserveChangedEvent(network.Comp.State, GetRemaining(network));
        RaiseLocalEvent(network, ref ev);
    }

    public override void Update(float frameTime)
    {
        if (!_enabled)
            return;

        // Only the handful of round-owned banks are queried; never scan all defenses, players, or relay anchors.
        var query = EntityQueryEnumerator<CMUFobNetworkComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (CanUse((uid, comp)))
                Refresh((uid, comp));
        }
    }
}
