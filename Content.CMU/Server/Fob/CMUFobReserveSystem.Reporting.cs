using System.Text.Json;
using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Radio.EntitySystems;
using Content.Shared._RMC14.Marines;
using Content.Shared.CMU14.Fob;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Fob;

public sealed partial class CMUFobReserveSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private CMUFobDefenseSystem _defense = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private GameTicker _ticker = default!;

    private static readonly (int Level, int Bit)[] WarningThresholds = [(120, 1), (60, 2), (0, 4)];
    private static readonly SoundSpecifier ReserveWarning = new SoundPathSpecifier("/Audio/Effects/double_beep.ogg");
    private static readonly ProtoId<RadioChannelPrototype> CommandChannel = "radioGovforCommand";

    private void BeginTrial(Entity<CMUFobNetworkComponent> network, EntityUid site, string? siteId)
    {
        var trial = AddComp<CMUFobTrialComponent>(network);
        trial.Site = siteId ?? Name(site);
        trial.LastAccountedAt = _timing.CurTime;
        trial.NextSummaryAt = _timing.CurTime + TimeSpan.FromMinutes(1);
        TrialLog(network, "created");
        if (network.Comp.Faction == "govfor")
            _radio.SendRadioMessage(network, Loc.GetString("cmu-fob-trial-announcement"), CommandChannel, network, null);
    }

    // Account to the actual deadline, not the frame that notices it. Pausable timestamps exclude pauses.
    private void AccountTrial(Entity<CMUFobNetworkComponent> network)
    {
        if (!TryComp<CMUFobTrialComponent>(network, out var trial) || trial.Finalized)
            return;
        var now = _timing.CurTime - _metadata.GetPauseTime(network);
        if (network.Comp.DepletesAt is { } deadline)
        {
            var end = now < deadline ? now : deadline;
            var start = trial.LastAccountedAt;
            var startupEnd = network.Comp.StartupEndsAt ?? start;
            var startup = (startupEnd < end ? startupEnd : end) - start;
            var active = end - (startupEnd > start ? startupEnd : start);
            trial.StartupSeconds += Math.Max(0, startup.TotalSeconds);
            trial.ActiveSeconds += Math.Max(0, active.TotalSeconds);
        }
        trial.LastAccountedAt = now;
    }

    private void CheckWarnings(Entity<CMUFobNetworkComponent> network)
    {
        if (!TryComp<CMUFobTrialComponent>(network, out var trial) || trial.Finalized
            || network.Comp.DepletesAt == null)
            return;
        var seconds = GetRemaining(network).TotalSeconds;
        int? warning = null;
        // A delayed frame emits only the most urgent crossed threshold. All crossed levels are consumed.
        foreach (var (level, bit) in WarningThresholds)
        {
            if (seconds > level || (trial.WarningMask & bit) != 0)
                continue;
            trial.WarningMask |= bit;
            warning = level;
        }
        if (warning is not { } crossed)
            return;
        EmitWarning(network, trial, crossed);
    }

    private void EmitWarning(Entity<CMUFobNetworkComponent> network, CMUFobTrialComponent trial, int crossed)
    {
        trial.WarningCount++;
        var message = Loc.GetString(crossed == 0 ? "cmu-fob-warning-empty" : "cmu-fob-warning-low",
            ("seconds", crossed));
        _audio.PlayPvs(ReserveWarning, network);
        var localMarines = Filter.Empty().AddWhereAttachedEntity(uid =>
            TryComp<MarineComponent>(uid, out var marine) && marine.Faction == network.Comp.Faction
            && Transform(uid).MapUid == network.Comp.Map
            && (_transform.GetWorldPosition(uid) - _transform.GetWorldPosition(network)).LengthSquared() <= 49);
        _popup.PopupEntity(message, network, localMarines, true, PopupType.MediumCaution);
        // Normal tower/range/jamming checks apply. Never add the clear-transmission exemption.
        if (network.Comp.Faction == "govfor")
            _radio.SendRadioMessage(network, message, CommandChannel, network, null);
        var ev = new CMUFobWarningEvent(crossed);
        RaiseLocalEvent(network, ref ev);
        TrialLog(network, "warning", crossed);
    }

    private void RearmWarnings(Entity<CMUFobNetworkComponent> network)
    {
        if (!TryComp<CMUFobTrialComponent>(network, out var trial))
            return;
        var remaining = GetRemaining(network).TotalSeconds;
        if (remaining >= 135) trial.WarningMask &= ~1;
        if (remaining >= 75) trial.WarningMask &= ~2;
        if (remaining > 0) trial.WarningMask &= ~4;
    }

    /// <summary>Nearby equipment exposes its support mode to both sides, never the reserve timer.</summary>
    public string GetPublicState(Entity<CMUFobNetworkComponent> network)
        => Loc.GetString($"cmu-fob-equipment-{network.Comp.State.ToString().ToLowerInvariant()}");

    public void RecordGunUse(EntityUid network, int shots = 0, double damage = 0, bool broken = false)
    {
        if (!_enabled || !TryComp<CMUFobTrialComponent>(network, out var trial) || trial.Finalized)
            return;
        trial.Shots += Math.Max(0, shots);
        trial.GunDamage += Math.Max(0, damage);
        if (broken) trial.GunBreaks++;
    }

    public void RecordDeparture(Entity<CMUFobNetworkComponent> network, int boardedMarines, int boardedXenos, int groundMarines, bool hijacked)
    {
        if (!_enabled || !TryComp<CMUFobTrialComponent>(network, out var trial) || trial.Finalized)
            return;
        trial.Departures++;
        TrialLog(network, "departure", new { boardedMarines, boardedXenos, groundMarines, hijacked });
    }

    public void RecordLanding(Entity<CMUFobNetworkComponent> network, string? arrivalSite)
    {
        if (!_enabled || !TryComp<CMUFobTrialComponent>(network, out var trial) || trial.Finalized)
            return;
        trial.Landings++;
        TrialLog(network, "landing", new { arrivalSite });
    }

    private void TrialLog(Entity<CMUFobNetworkComponent> network, string kind, object? detail = null)
    {
        if (!TryComp<CMUFobTrialComponent>(network, out var trial))
            return;
        Log.Info("CMU_FOB " + JsonSerializer.Serialize(new
        {
            kind, round = _ticker.RoundId, network = network.Owner.Id, trial.Site, network.Comp.Faction,
            map = network.Comp.Map.Id, mapName = Name(network.Comp.Map), elapsedSeconds = (long) (_timing.CurTime - _ticker.RoundStartTimeSpan).TotalSeconds,
            state = network.Comp.State.ToString(), remainingSeconds = (long) GetRemaining(network).TotalSeconds,
            trial.Offers, trial.Completions, trial.Expiries, trial.Activations, trial.Depletions,
            trial.AwardedSeconds, trial.CappedSeconds, trial.StartupSeconds, trial.ActiveSeconds,
            trial.ControlLossChargeSeconds, trial.Shots, trial.GunDamage, trial.GunBreaks, trial.Landings, trial.Departures,
            guns = _defense.GetGunStatus(network).Select(gun => new { condition = gun.Condition.ToString(), gun.Ammunition, gun.Damage }), detail,
        }));
    }

    private void MarkersChanged()
    {
        var ev = new CMUFobMarkersChangedEvent();
        RaiseLocalEvent(ref ev);
    }
}
