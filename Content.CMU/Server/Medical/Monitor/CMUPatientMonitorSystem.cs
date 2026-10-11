using Content.Server.GameTicking;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Defibrillator;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.Medical.Monitor;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Medical;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.StatusEffectNew;
using Content.Shared.Temperature.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Medical.Monitor;

public sealed partial class CMUPatientMonitorSystem : SharedCMUPatientMonitorSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private CMUDefibChargeSystem _charge = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private CMUDefibVoiceSystem _voice = default!;
    [Dependency] private CMUDefibPaddlesSystem _paddles = default!;
    [Dependency] private SharedDefibrillatorSystem _defib = default!;
    [Dependency] private RMCDefibrillatorSystem _rmcDefib = default!;

    private const int MaxLogEntries = 30;
    private bool _revivedDuringShock;
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUPatientMonitorComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CMUPatientMonitorComponent, RMCDefibrillatorDamageModifyEvent>(OnShock);
        SubscribeLocalEvent<CMUPatientMonitorComponent, RMCDefibrillatorRevivedEvent>(OnRevived);
        SubscribeLocalEvent<CMUPatientMonitorComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUPatientMonitorComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<CMUPatientMonitorComponent, CMUDefibPromptPlayedEvent>(OnPromptPlayed);
        SubscribeLocalEvent<CMUPatientMonitorComponent, DefibrillatorZapDoAfterEvent>(OnZapDoAfter);

        Subs.BuiEvents<CMUPatientMonitorComponent>(CMUPatientMonitorUi.Key, subs =>
        {
            subs.Event<CMUPatientMonitorEnergyMsg>(OnEnergyMsg);
            subs.Event<CMUPatientMonitorNibpMsg>(OnNibpMsg);
            subs.Event<CMUPatientMonitorSilenceMsg>(OnSilenceMsg);
            subs.Event<CMUPatientMonitorDisconnectMsg>(OnDisconnectMsg);
            subs.Event<CMUPatientMonitorAnalyzeMsg>(OnAnalyzeMsg);
            subs.Event<CMUPatientMonitorChargeMsg>(OnChargeMsg);
            subs.Event<CMUPatientMonitorShockMsg>(OnShockMsg);
        });
    }

    #region Leads

    protected override void Attach(Entity<CMUPatientMonitorComponent> ent, EntityUid user, EntityUid patient)
    {
        if (!InRange(ent, patient, ent.Comp.Range))
            return;

        if (!_skills.HasAllSkills(user, ent.Comp.SkillRequired))
        {
            _popup.PopupEntity(Loc.GetString("cmu-monitor-attach-no-skill"), user, user);
            return;
        }

        if (ent.Comp.AttachedTo is { } old && old != patient)
            Detach(ent, null);

        ent.Comp.AttachedTo = patient;
        ent.Comp.AttachedAt = _timing.CurTime;
        ent.Comp.NextUpdate = _timing.CurTime;
        ent.Comp.NextNibp = _timing.CurTime;
        ent.Comp.NibpSystolic = null;
        ent.Comp.NibpDiastolic = null;
        ent.Comp.NibpTime = null;
        ent.Comp.LastRhythm = CMUMonitorRhythm.None;
        Dirty(ent);

        _audio.PlayPvs(ent.Comp.AttachSound, ent);
        _popup.PopupEntity(Loc.GetString("cmu-monitor-attach-self", ("patient", patient)), patient, user);
        _popup.PopupEntity(Loc.GetString("cmu-monitor-attach-others", ("user", user), ("patient", patient)),
            patient, Filter.PvsExcept(user), true);

        AddLog(ent, Loc.GetString("cmu-monitor-log-attached", ("patient", patient)));
        _ui.OpenUi(ent.Owner, CMUPatientMonitorUi.Key, user);
        UpdateMonitor(ent);
    }

    protected override void Detach(Entity<CMUPatientMonitorComponent> ent, EntityUid? user)
    {
        if (ent.Comp.AttachedTo is not { } patient)
            return;

        ent.Comp.AttachedTo = null;
        ent.Comp.AnalyzeAt = null;
        DisarmCharge(ent);
        ent.Comp.DisplayHeartRate = 0;
        ent.Comp.DisplayRhythm = CMUMonitorRhythm.None;
        Dirty(ent);
        SetFlatline(ent, false);

        AddLog(ent, Loc.GetString("cmu-monitor-log-leads-off"));

        if (!TerminatingOrDeleted(patient))
        {
            var message = user != null
                ? Loc.GetString("cmu-monitor-detach", ("patient", patient))
                : Loc.GetString("cmu-monitor-leads-pulled", ("patient", patient));
            _popup.PopupEntity(message, patient, PopupType.Small);
        }

        UpdateMonitor(ent);
    }

    private void OnToggled(Entity<CMUPatientMonitorComponent> ent, ref ItemToggledEvent args)
    {
        if (args.Activated)
        {
            // Take a fresh blood pressure as soon as it comes on.
            ent.Comp.NextNibp = _timing.CurTime;
        }
        else
        {
            DisarmCharge(ent, true);
            ent.Comp.AnalyzeAt = null;
            _voice.CancelPrompts(ent);
            GoDark(ent);
        }

        UpdateMonitor(ent);
    }

    /// <summary>
    /// Clears everything the screen and speaker were showing, e.g. when it's switched off.
    /// </summary>
    private void GoDark(Entity<CMUPatientMonitorComponent> ent)
    {
        SetFlatline(ent, false);
        StopAlarm(ent);
        ent.Comp.WarningActive = false;
        ent.Comp.LastRhythm = CMUMonitorRhythm.None;

        if (ent.Comp.DisplayHeartRate == 0 && ent.Comp.DisplayRhythm == CMUMonitorRhythm.None)
            return;

        ent.Comp.DisplayHeartRate = 0;
        ent.Comp.DisplayRhythm = CMUMonitorRhythm.None;
        Dirty(ent);
    }

    private bool IsOn(EntityUid monitor)
    {
        return _toggle.IsActivated(monitor);
    }

    private void OnShutdown(Entity<CMUPatientMonitorComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.AttachedTo = null;
        SetFlatline(ent, false);
    }

    #endregion

    #region UI

    private void OnUiOpened(Entity<CMUPatientMonitorComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateMonitor(ent);
    }

    private void OnEnergyMsg(Entity<CMUPatientMonitorComponent> ent, ref CMUPatientMonitorEnergyMsg args)
    {
        _charge.StepJoules(ent, args.Raise, args.Actor);
        UpdateMonitor(ent);
    }

    private void OnNibpMsg(Entity<CMUPatientMonitorComponent> ent, ref CMUPatientMonitorNibpMsg args)
    {
        if (ent.Comp.AttachedTo is not { } patient || !IsOn(ent))
            return;

        TakeNibp(ent, patient);
        UpdateMonitor(ent);
    }

    private void OnSilenceMsg(Entity<CMUPatientMonitorComponent> ent, ref CMUPatientMonitorSilenceMsg args)
    {
        ent.Comp.AlarmsSilenced = !ent.Comp.AlarmsSilenced;
        Dirty(ent);
        UpdateMonitor(ent);
    }

    private void OnDisconnectMsg(Entity<CMUPatientMonitorComponent> ent, ref CMUPatientMonitorDisconnectMsg args)
    {
        Detach(ent, args.Actor);
    }

    private void OnAnalyzeMsg(Entity<CMUPatientMonitorComponent> ent, ref CMUPatientMonitorAnalyzeMsg args)
    {
        if (ent.Comp.AttachedTo == null || ent.Comp.AnalyzeAt != null || !IsOn(ent))
            return;

        ent.Comp.AnalyzeAt = _timing.CurTime + ent.Comp.AnalyzeDuration;
        _voice.AnnounceAnalyzing(ent);
        AddLog(ent, Loc.GetString("cmu-monitor-log-analyzing"));
        UpdateMonitor(ent);
    }

    #region Hands-free shock

    private void OnChargeMsg(Entity<CMUPatientMonitorComponent> ent, ref CMUPatientMonitorChargeMsg args)
    {
        if (ent.Comp.AttachedTo is not { } patient ||
            !IsOn(ent) ||
            ent.Comp.ChargedAt != null ||
            ent.Comp.ChargeExpiresAt != null ||
            !TryComp<DefibrillatorComponent>(ent, out var defib) ||
            !CanOperate(ent, args.Actor) ||
            !_defib.CanZap((ent.Owner, defib), patient, args.Actor))
        {
            return;
        }

        var user = args.Actor;
        var delay = defib.DoAfterDuration +
                    defib.SkillMultiplierDuration * _skills.GetSkillDelayMultiplier(user, defib.Skill);

        ent.Comp.ChargedAt = _timing.CurTime + delay;
        _rmcDefib.StartChargingAudio((ent.Owner, defib), user);

        // Reuses the charge prompts, e.g. "stand clear" just before it's ready.
        var started = new CMUDefibZapStartedEvent(user, patient, delay);
        RaiseLocalEvent(ent, ref started);

        AddLog(ent, Loc.GetString("cmu-monitor-log-charging", ("joules", _charge.GetJoules(ent))));
        UpdateMonitor(ent);
    }

    private void OnShockMsg(Entity<CMUPatientMonitorComponent> ent, ref CMUPatientMonitorShockMsg args)
    {
        if (ent.Comp.AttachedTo is not { } patient ||
            ent.Comp.ChargeExpiresAt == null ||
            !TryComp<DefibrillatorComponent>(ent, out var defib) ||
            !CanOperate(ent, args.Actor))
        {
            return;
        }

        ent.Comp.ChargeExpiresAt = null;
        _paddles.SetPaddlesOut(ent, false);

        var user = args.Actor;
        if (_defib.CanZap((ent.Owner, defib), patient, user))
        {
            QuietForShock(ent, defib);
            _revivedDuringShock = false;
            _defib.Zap((ent.Owner, defib), patient, user);

            // The shock plays its sounds as if the user predicted them, which they can't from a UI key.
            var toUser = Filter.Entities(user);
            _audio.PlayEntity(defib.ZapSound, toUser, ent, false);
            _audio.PlayEntity(_revivedDuringShock ? defib.SuccessSound : defib.FailureSound, toUser, ent, false);
        }

        UpdateMonitor(ent);
    }

    private bool CanOperate(EntityUid monitor, EntityUid user)
    {
        if (!TryComp<RequiresSkillComponent>(monitor, out var requires) ||
            _skills.HasAllSkills(user, requires.Skills))
        {
            return true;
        }

        _popup.PopupEntity(Loc.GetString("cmu-monitor-shock-no-skill"), user, user);
        return false;
    }

    private void FinishCharging(Entity<CMUPatientMonitorComponent> ent)
    {
        ent.Comp.ChargedAt = null;
        ent.Comp.ChargeExpiresAt = _timing.CurTime + ent.Comp.ChargeDumpTime;

        if (TryComp<DefibrillatorComponent>(ent, out var defib))
        {
            _rmcDefib.StopChargingAudio((ent.Owner, defib));
            _audio.PlayPvs(defib.ReadySound, ent);
            QuietAlarms(ent, defib.ReadySound);
        }

        AddLog(ent, Loc.GetString("cmu-monitor-log-charged"), true);
    }

    /// <summary>
    /// Cancels a charge in progress or dumps a finished one.
    /// </summary>
    private void DisarmCharge(Entity<CMUPatientMonitorComponent> ent, bool log = false)
    {
        var hadCharge = ent.Comp.ChargedAt != null || ent.Comp.ChargeExpiresAt != null;
        if (ent.Comp.ChargedAt != null && TryComp<DefibrillatorComponent>(ent, out var defib))
        {
            _rmcDefib.StopChargingAudio((ent.Owner, defib));
            _voice.CancelPrompts(ent);
        }

        ent.Comp.ChargedAt = null;
        ent.Comp.ChargeExpiresAt = null;

        if (hadCharge)
            _paddles.SetPaddlesOut(ent, false);

        if (log && hadCharge)
            AddLog(ent, Loc.GetString("cmu-monitor-log-charge-dumped"));
    }

    #endregion

    /// <summary>
    /// Finishes an analysis: announces whether a shock is advised and, if so, selects the lowest energy expected
    /// to bring the patient back.
    /// </summary>
    private void FinishAnalysis(Entity<CMUPatientMonitorComponent> ent, EntityUid patient)
    {
        ent.Comp.AnalyzeAt = null;

        var advice = _charge.Analyze(ent, patient);
        _voice.Announce(ent, advice);

        if (!advice.Shockable)
        {
            AddLog(ent, Loc.GetString("cmu-monitor-log-no-shock-advised"));
            return;
        }

        _charge.SetAdvisedJoules(ent, advice.Joules);
        AddLog(ent, Loc.GetString(advice.Sufficient ? "cmu-monitor-log-shock-advised" : "cmu-monitor-log-shock-advised-max",
            ("joules", advice.Joules)), true);
    }

    #endregion

    #region Code summary

    private void OnShock(Entity<CMUPatientMonitorComponent> ent, ref RMCDefibrillatorDamageModifyEvent args)
    {
        if (args.Cancelled)
            return;

        AddLog(ent, Loc.GetString("cmu-monitor-log-shock", ("joules", _charge.GetJoules(ent))), true);
    }

    private void OnRevived(Entity<CMUPatientMonitorComponent> ent, ref RMCDefibrillatorRevivedEvent args)
    {
        _revivedDuringShock = true;
        AddLog(ent, Loc.GetString("cmu-monitor-log-rosc"));
        UpdateMonitor(ent);
    }

    private void AddLog(Entity<CMUPatientMonitorComponent> ent, string text, bool critical = false)
    {
        ent.Comp.Log.Add(new CMUMonitorLogEntry(Clock(), text, critical));
        if (ent.Comp.Log.Count > MaxLogEntries)
            ent.Comp.Log.RemoveRange(0, ent.Comp.Log.Count - MaxLogEntries);
    }

    private string Clock()
    {
        var time = _gameTicker.RoundDuration();
        return $"{(int) time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUPatientMonitorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ChargedAt is { } chargedAt && now >= chargedAt)
            {
                FinishCharging((uid, comp));
                comp.NextUpdate = now;
            }
            else if (comp.ChargeExpiresAt is { } expiresAt && now >= expiresAt)
            {
                DisarmCharge((uid, comp), true);
                comp.NextUpdate = now;
            }

            if (comp.AnalyzeAt is { } analyzeAt && now >= analyzeAt && comp.AttachedTo is { } analyzed)
            {
                FinishAnalysis((uid, comp), analyzed);
                comp.NextUpdate = now;
            }

            // Alarms can repeat faster than the once-a-second refresh, so they're run every tick.
            RunAlarms((uid, comp), now);

            if (now < comp.NextUpdate)
                continue;

            comp.NextUpdate = now + UpdateInterval;
            var ent = (uid, comp);

            if (comp.AttachedTo is { } patient && !InRange(uid, patient, comp.Range))
            {
                Detach(ent, null);
                continue;
            }

            if (comp.AttachedTo == null && !_ui.IsUiOpen(uid, CMUPatientMonitorUi.Key))
                continue;

            UpdateMonitor(ent);
        }
    }

    private void UpdateMonitor(Entity<CMUPatientMonitorComponent> ent)
    {
        var state = new CMUPatientMonitorBuiState
        {
            Clock = Clock(),
            Joules = _charge.GetJoules(ent),
            Armed = _toggle.IsActivated(ent.Owner),
            AlarmSilenced = ent.Comp.AlarmsSilenced,
            Analyzing = ent.Comp.AnalyzeAt != null,
            Charging = ent.Comp.ChargedAt != null,
            Charged = ent.Comp.ChargeExpiresAt != null,
            PoweredOn = IsOn(ent),
            LeadsAttached = ent.Comp.AttachedTo != null,
            Log = new List<CMUMonitorLogEntry>(ent.Comp.Log),
        };

        if (_powerCell.TryGetBatteryFromSlot(ent.Owner, out var battery))
            state.Battery = (int) MathF.Round(_battery.GetChargeLevel(battery.Value.AsNullable()) * 100f);

        if (!state.PoweredOn)
        {
            GoDark(ent);
        }
        else if (ent.Comp.AttachedTo is { } patient && !TerminatingOrDeleted(patient))
        {
            FillVitals(ent, patient, state);

            if (state.Rhythm != ent.Comp.LastRhythm)
            {
                if (ent.Comp.LastRhythm != CMUMonitorRhythm.None || IsLethal(state.Rhythm))
                {
                    AddLog(ent, Loc.GetString("cmu-monitor-log-rhythm",
                        ("rhythm", Loc.GetString(CMUMonitorRhythms.LocId(state.Rhythm)))), IsLethal(state.Rhythm));
                    state.Log = new List<CMUMonitorLogEntry>(ent.Comp.Log);
                }

                ent.Comp.LastRhythm = state.Rhythm;
            }

            if (ent.Comp.DisplayHeartRate != state.HeartRate || ent.Comp.DisplayRhythm != state.Rhythm)
            {
                ent.Comp.DisplayHeartRate = state.HeartRate;
                ent.Comp.DisplayRhythm = state.Rhythm;
                Dirty(ent);
            }

            SoundAlarm(ent, state);
        }

        _ui.SetUiState(ent.Owner, CMUPatientMonitorUi.Key, state);
    }

    private void FillVitals(Entity<CMUPatientMonitorComponent> ent, EntityUid patient, CMUPatientMonitorBuiState state)
    {
        var now = _timing.CurTime;
        var elapsed = now - ent.Comp.AttachedAt;
        state.PatientName = Name(patient);
        state.Elapsed = $"{(int) elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";

        var dead = _mobState.IsDead(patient);
        var critical = _mobState.IsCritical(patient);

        var heartRate = 0;
        var stopped = true;
        if (_medicalIndex.TryGetOrgan<HeartComponent>(patient, out var organ) &&
            TryComp<HeartComponent>(organ, out var heart))
        {
            heartRate = heart.BeatsPerMinute;
            stopped = heart.Stopped;

            if (TryComp<OrganHealthComponent>(organ, out var heartHealth))
            {
                var current = heartHealth.Current;
                var max = heartHealth.Max;
                state.HeartStage = heartHealth.Stage;
                state.HeartCurrent = current.Int();
                state.HeartMax = max.Int();
            }
        }

        var pulseless = dead || stopped || heartRate <= 0;
        state.HeartRate = pulseless ? 0 : heartRate;
        state.Rhythm = GetRhythm(patient, dead, pulseless, heartRate);
        state.Status = dead
            ? CMUMonitorPatientStatus.Dead
            : pulseless
                ? CMUMonitorPatientStatus.Pulseless
                : critical
                    ? CMUMonitorPatientStatus.Unresponsive
                    : CMUMonitorPatientStatus.Conscious;

        var bloodLevel = TryComp<BloodstreamComponent>(patient, out var blood)
            ? Math.Clamp(_bloodstream.GetBloodLevel((patient, blood)), 0f, 1f)
            : 1f;

        var asphyxiation = 0f;
        if (TryComp<DamageableComponent>(patient, out var damageable))
        {
            asphyxiation = _damageable.GetAllDamage((patient, damageable)).DamageDict
                .GetValueOrDefault("Asphyxiation", FixedPoint2.Zero).Float();
        }

        if (pulseless)
        {
            state.SpO2 = null;
            state.RespiratoryRate = 0;
            state.EtCO2 = 0;
        }
        else
        {
            // Oxygen falls with suffocation and blood loss.
            var spo2 = 98f - asphyxiation * 0.35f - MathF.Max(0f, 0.9f - bloodLevel) * 60f + _random.NextFloat(-1f, 1f);
            state.SpO2 = (int) Math.Clamp(MathF.Round(spo2), 40f, 100f);

            var resp = critical ? _random.Next(6, 10) : _random.Next(14, 18);
            if (!critical && (asphyxiation > 20f || bloodLevel < 0.8f))
                resp += 8;
            state.RespiratoryRate = resp;

            // Poor perfusion shows up as a low end-tidal CO2.
            var etco2 = 38f * (0.4f + 0.6f * bloodLevel) + (critical ? -8f : 0f) + _random.NextFloat(-1f, 1f);
            state.EtCO2 = (int) Math.Clamp(MathF.Round(etco2), 5f, 60f);
        }

        if (TryComp<TemperatureComponent>(patient, out var temperature))
            state.TemperatureC = temperature.Temperature - 273.15f;

        if (now >= ent.Comp.NextNibp)
            TakeNibp(ent, patient);

        state.NibpSystolic = ent.Comp.NibpSystolic;
        state.NibpDiastolic = ent.Comp.NibpDiastolic;
        state.NibpTime = ent.Comp.NibpTime;
    }

    private CMUMonitorRhythm GetRhythm(EntityUid patient, bool dead, bool pulseless, int heartRate)
    {
        if (dead)
        {
            // A body that can still be shocked back is in a shockable rhythm; one that's too far gone flatlines.
            return _charge.IsShockable(patient)
                ? CMUMonitorRhythm.VentricularFibrillation
                : CMUMonitorRhythm.Asystole;
        }

        if (pulseless)
            return CMUMonitorRhythm.VentricularFibrillation;

        if (_status.HasEffectComp<ArrhythmiaComponent>(patient))
            return CMUMonitorRhythm.Irregular;

        if (heartRate > 100)
            return CMUMonitorRhythm.SinusTachycardia;

        if (heartRate < 60)
            return CMUMonitorRhythm.SinusBradycardia;

        return CMUMonitorRhythm.Sinus;
    }

    private void TakeNibp(Entity<CMUPatientMonitorComponent> ent, EntityUid patient)
    {
        ent.Comp.NextNibp = _timing.CurTime + ent.Comp.NibpInterval;
        ent.Comp.NibpTime = Clock();

        var heartRate = 0;
        var stopped = true;
        if (_medicalIndex.TryGetOrgan<HeartComponent>(patient, out var organ) &&
            TryComp<HeartComponent>(organ, out var heart))
        {
            heartRate = heart.BeatsPerMinute;
            stopped = heart.Stopped;
        }

        if (_mobState.IsDead(patient) || stopped || heartRate <= 0)
        {
            ent.Comp.NibpSystolic = null;
            ent.Comp.NibpDiastolic = null;
            AddLog(ent, Loc.GetString("cmu-monitor-log-nibp-failed"), true);
            return;
        }

        var bloodLevel = TryComp<BloodstreamComponent>(patient, out var blood)
            ? Math.Clamp(_bloodstream.GetBloodLevel((patient, blood)), 0f, 1f)
            : 1f;

        // Pressure drops with blood volume, and more steeply once a patient is critical.
        var systolic = 118f * (0.3f + 0.7f * bloodLevel) + (heartRate - 70) * 0.15f;
        if (_mobState.IsCritical(patient))
            systolic *= 0.8f;

        systolic += _random.NextFloat(-3f, 3f);
        var sys = (int) Math.Clamp(MathF.Round(systolic), 40f, 200f);
        var dia = (int) MathF.Round(sys * 0.64f + _random.NextFloat(-2f, 2f));

        ent.Comp.NibpSystolic = sys;
        ent.Comp.NibpDiastolic = dia;
        AddLog(ent, Loc.GetString("cmu-monitor-log-nibp", ("sys", sys), ("dia", dia)), sys < 90);
    }

    private void SoundAlarm(Entity<CMUPatientMonitorComponent> ent, CMUPatientMonitorBuiState state)
    {
        ent.Comp.WarningActive = state.SpO2 is < 90 || state.NibpSystolic is < 90;
    }

    /// <summary>
    /// Sounds at most one alarm at a time, most urgent first: VF, then the asystole flatline, then low oxygen or
    /// blood pressure. Everything holds off while the alarms are silenced or while the unit is speaking, shocking
    /// or chiming, so they never talk over it.
    /// </summary>
    private void RunAlarms(Entity<CMUPatientMonitorComponent> ent, TimeSpan now)
    {
        var comp = ent.Comp;
        var rhythm = comp.AttachedTo != null && IsOn(ent) ? comp.DisplayRhythm : CMUMonitorRhythm.None;
        var quiet = comp.AlarmsSilenced || comp.AlarmsQuietUntil > now;

        if (quiet)
        {
            SetFlatline(ent, false);
            StopAlarm(ent);
            return;
        }

        SetFlatline(ent, rhythm == CMUMonitorRhythm.Asystole);

        if (rhythm == CMUMonitorRhythm.VentricularFibrillation)
        {
            // VF cuts off a warning that's still going, rather than waiting for it.
            if (comp.AlarmStream != null && !comp.AlarmStreamLethal)
            {
                StopAlarm(ent);
                comp.NextAlarm = now;
            }

            if (now >= comp.NextAlarm)
                SoundAlarm(ent, comp.LethalAlarmSound, true, comp.LethalAlarmInterval, now);

            return;
        }

        // The flatline tone already covers asystole.
        if (rhythm == CMUMonitorRhythm.Asystole || rhythm == CMUMonitorRhythm.None || !comp.WarningActive)
            return;

        if (now >= comp.NextAlarm)
            SoundAlarm(ent, comp.WarningAlarmSound, false, comp.WarningAlarmInterval, now);
    }

    private void SoundAlarm(Entity<CMUPatientMonitorComponent> ent, SoundSpecifier? sound, bool lethal,
        TimeSpan interval, TimeSpan now)
    {
        StopAlarm(ent);
        ent.Comp.NextAlarm = now + interval;
        ent.Comp.AlarmStream = _audio.PlayPvs(sound, ent)?.Entity;
        ent.Comp.AlarmStreamLethal = lethal;
    }

    private void StopAlarm(Entity<CMUPatientMonitorComponent> ent)
    {
        if (ent.Comp.AlarmStream is not { } stream)
            return;

        if (!TerminatingOrDeleted(stream))
            _audio.Stop(stream);

        ent.Comp.AlarmStream = null;
    }

    /// <summary>
    /// Holds the alarms until these sounds have finished.
    /// </summary>
    private void QuietAlarms(Entity<CMUPatientMonitorComponent> ent, params SoundSpecifier?[] sounds)
    {
        var length = TimeSpan.Zero;
        foreach (var sound in sounds)
        {
            if (sound != null)
                length += _audio.GetAudioLength(_audio.ResolveSound(sound));
        }

        var until = _timing.CurTime + length;
        if (until > ent.Comp.AlarmsQuietUntil)
            ent.Comp.AlarmsQuietUntil = until;

        SetFlatline(ent, false);
        StopAlarm(ent);
    }

    private void OnPromptPlayed(Entity<CMUPatientMonitorComponent> ent, ref CMUDefibPromptPlayedEvent args)
    {
        QuietAlarms(ent, args.Sound);
    }

    private void OnZapDoAfter(Entity<CMUPatientMonitorComponent> ent, ref DefibrillatorZapDoAfterEvent args)
    {
        // A shock by hand: hold the alarms for the shock and the "check for pulse" or "check patient" after it.
        if (!args.Cancelled && TryComp<DefibrillatorComponent>(ent, out var defib))
            QuietForShock(ent, defib);
    }

    private void QuietForShock(Entity<CMUPatientMonitorComponent> ent, DefibrillatorComponent defib)
    {
        var success = defib.SuccessSound is { } s ? _audio.GetAudioLength(_audio.ResolveSound(s)) : TimeSpan.Zero;
        var failure = defib.FailureSound is { } f ? _audio.GetAudioLength(_audio.ResolveSound(f)) : TimeSpan.Zero;
        QuietAlarms(ent, defib.ZapSound, success > failure ? defib.SuccessSound : defib.FailureSound);
    }

    private void SetFlatline(Entity<CMUPatientMonitorComponent> ent, bool on)
    {
        if (on == (ent.Comp.FlatlineStream != null))
            return;

        if (!on)
        {
            _audio.Stop(ent.Comp.FlatlineStream);
            ent.Comp.FlatlineStream = null;
            return;
        }

        if (ent.Comp.FlatlineSound is not { } sound)
            return;

        ent.Comp.FlatlineStream = _audio.PlayPvs(sound, ent, sound.Params.WithLoop(true))?.Entity;
    }

    private static bool IsLethal(CMUMonitorRhythm rhythm)
    {
        return CMUMonitorRhythms.IsLethal(rhythm);
    }
}
