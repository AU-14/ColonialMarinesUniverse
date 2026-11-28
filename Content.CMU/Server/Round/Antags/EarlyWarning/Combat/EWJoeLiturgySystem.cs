using Content.Server.Administration.Logs;
using Content.Server.CMU14.Combat.CQC;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Alert;
using Content.Shared.CMU14.Combat.CQC;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Combat;
using Content.Shared.Database;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Combat;

/// <summary>
/// ARCH-05 / FLOW-06 / ARCH-04 (node N12): the Liturgy meter. Turns the Joe's close-quarters inputs into
/// stacks, empties them when the window lapses or the Joe is stunned, makes the Joe Exalted at max stacks and
/// trims the close-quarters cooldowns on each hit. The inputs, the sequence buffer (ARCH-06) and the per-target
/// hard-CC immunity windows belong to the generic <see cref="CMUCqcSystem"/>; this system listens to its events.
/// The passive per-stack buffs (ARCH-12) live in the shared <see cref="EWJoeLiturgyBuffsSystem"/>.
/// </summary>
/// <remarks>
/// CQC listeners (one directed subscription per pair; all pairs were free):
/// <list type="bullet">
/// <item>(EWJoeLiturgyComponent, CMUCqcInputEvent): stacks and the cooldown cut, raised after matching, so the
/// sequence an input completes is matched before that input's gain ("match first, gain after").</item>
/// <item>(EWJoeLiturgyComponent, CMUCqcSequenceFiredEvent): an empowered sequence spends Exalted.</item>
/// <item>(EWJoeComponent, CMUCqcSequenceAttemptEvent): the level gate (a sequence's tier above the Joe's level is
/// refused) and the empowered flag from Exalted.</item>
/// <item>(EWJoeComponent, CMUCqcHardCcDurationEvent): the xenofauna stun scaling (ECS-11).</item>
/// </list>
/// <see cref="Register"/>, <see cref="AbilityLanded"/>, <see cref="ApplyHardCc(EntityUid, EntityUid, TimeSpan, EWJoeHardCcKind, out TimeSpan)"/>,
/// <see cref="IsCcImmune"/>, <see cref="RecordHardCc"/> and <see cref="TakeHardCc"/> forward to <see cref="CMUCqcSystem"/>
/// so the Early Warning call sites stay as they are. Server only. The client reads the networked stacks for the eye
/// tint; the alert is set here.
/// </remarks>
public sealed class EWJoeLiturgySystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    // ARCH-06: the generic CQC system owns the inputs, the buffer and the CC windows (RB-13)
    [Dependency] private CMUCqcSystem _cqc = default!;

    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(0.2);

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeLiturgyComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<EWJoeLiturgyComponent, EWJoeFlashStunEvent>(OnFlashStun);
        SubscribeLocalEvent<EWJoeLiturgyComponent, EWJoeFlashStunLandedEvent>(OnFlashStunLanded);
        SubscribeLocalEvent<EWJoeLiturgyComponent, CMUCqcInputEvent>(OnCqcInput);
        SubscribeLocalEvent<EWJoeLiturgyComponent, CMUCqcSequenceFiredEvent>(OnSequenceFired);
        SubscribeLocalEvent<EWJoeComponent, CMUCqcSequenceAttemptEvent>(OnSequenceAttempt);
        SubscribeLocalEvent<EWJoeComponent, CMUCqcHardCcDurationEvent>(OnHardCcDuration);
    }

    #region Hooks

    /// <summary>
    /// A melee swing by <paramref name="joe"/>, from its own fists or forwarded from the machete. Forwards to
    /// <see cref="CMUCqcSystem.HandleMeleeHit"/>, which records it; the stacks and the cooldown cut come back through
    /// <see cref="CMUCqcInputEvent"/>.
    /// </summary>
    public void HandleMeleeHit(EntityUid joe, MeleeHitEvent args)
    {
        _cqc.HandleMeleeHit(joe, args);
    }

    /// <summary>ARCH-12: a blade is a weapon other than the Joe's own body that is the machete or deals Slash damage.</summary>
    public bool IsBlade(EntityUid joe, EntityUid weapon)
    {
        return _cqc.IsBlade(joe, weapon);
    }

    private void OnShutdown(Entity<EWJoeLiturgyComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
        if (!TerminatingOrDeleted(ent))
            RemCompDeferred<EWJoeLiturgyGlowComponent>(ent);
    }

    /// <summary>
    /// Every input the CQC system recorded, after its sequence matching: one stack per hit, disarm and aggressive grab,
    /// two per landed ability, one per body (up to the cap) for a wide swing; a melee hit also trims the cooldowns.
    /// </summary>
    private void OnCqcInput(Entity<EWJoeLiturgyComponent> ent, ref CMUCqcInputEvent args)
    {
        int amount;
        if (args.Gain is { } gain)
            amount = gain;
        else if (args.MeleeHit && args.Input == CMUCqcInput.Wide)
            amount = Math.Min(args.Bodies, ent.Comp.WideGainCap) * ent.Comp.WideGain;
        else
            amount = GainFor(ent.Comp, args.Input);

        Gain(ent, amount);

        if (args.MeleeHit)
            CutCooldowns(ent);
    }

    /// <summary>ARCH-12: an empowered sequence spends the Exalted state once its effect has landed.</summary>
    private void OnSequenceFired(Entity<EWJoeLiturgyComponent> ent, ref CMUCqcSequenceFiredEvent args)
    {
        // eligibility (the CC-immunity rule) was settled before the effect; the effect's own hard CC has just
        // opened the target's window (RB-13), so checking it again here would refuse the spend
        if (args.Empowered)
            TryConsumeEmpowered(ent, args.Target, ccBonus: false, args.Sequence.Id);
    }

    /// <summary>
    /// The level gate: a sequence whose tier is above the Joe's level is refused. ARCH-12: an Exalted Joe's sequence is
    /// empowered, under the same CC-immunity rule as the hotbar (FLOW-06). The flag is read before the effect and spent
    /// only once it lands, so a miss keeps it.
    /// </summary>
    private void OnSequenceAttempt(Entity<EWJoeComponent> ent, ref CMUCqcSequenceAttemptEvent args)
    {
        if (!_proto.TryIndex(args.Sequence, out var sequence) || sequence.Tier > ent.Comp.Level)
        {
            args.Cancelled = true;
            return;
        }

        args.Empowered = CanConsumeEmpowered(ent, args.Target, sequence.HardCc);
    }

    /// <summary>ECS-11: every Early Warning Joe hard CC on xenofauna is scaled before the CC window applies.</summary>
    private void OnHardCcDuration(Entity<EWJoeComponent> ent, ref CMUCqcHardCcDurationEvent args)
    {
        args.Duration = EWJoeTargetRules.ScaleStun(EntityManager, args.Target, args.Duration, ent.Comp.XenofaunaStunMultiplier);
    }

    #endregion

    #region Meter

    /// <summary>
    /// Records one close-quarters input: the CQC system pushes it and fires the sequence it completes, then the stacks
    /// (<paramref name="gain"/>, or the component's gain for <paramref name="input"/>) are added and the window restarts.
    /// <paramref name="target"/> is null for an untargeted ability. <paramref name="fromBlade"/> marks a swing made
    /// with a blade (ARCH-12). A Joe without the CQC component only gains; one without the meter only records.
    /// </summary>
    public void Register(EntityUid joe, CMUCqcInput input, EntityUid? target, int? gain = null, bool fromBlade = false)
    {
        if (_cqc.Register(joe, input, target, fromBlade, gain))
            return;

        if (TryComp(joe, out EWJoeLiturgyComponent? liturgy))
            Gain((joe, liturgy), gain ?? GainFor(liturgy, input));
    }

    /// <summary>A wasted swing: the sequence buffer starts over, the stacks stay.</summary>
    public void RegisterMiss(EntityUid joe)
    {
        _cqc.ClearBuffer(joe);
    }

    public static int GainFor(EWJoeLiturgyComponent liturgy, CMUCqcInput input)
    {
        return input switch
        {
            CMUCqcInput.Hit => liturgy.HitGain,
            CMUCqcInput.Wide => liturgy.WideGain,
            CMUCqcInput.Disarm => liturgy.DisarmGain,
            CMUCqcInput.Grab => liturgy.GrabGain,
            CMUCqcInput.AggressiveGrab => liturgy.AggressiveGrabGain,
            CMUCqcInput.Ability => liturgy.AbilityGain,
            _ => 0,
        };
    }

    private void Gain(Entity<EWJoeLiturgyComponent> ent, int amount)
    {
        if (amount <= 0)
            return;

        AddStacks(ent, amount);
        var window = ent.Comp.Window;
        if (HasComp<EWJoeOverclockComponent>(ent))
            window *= ent.Comp.OverclockWindowMultiplier;
        ent.Comp.ExpiresAt = _timing.CurTime + window;
    }

    private void AddStacks(Entity<EWJoeLiturgyComponent> ent, int amount)
    {
        var before = ent.Comp.Stacks;
        ent.Comp.Stacks = Math.Clamp(before + amount, 0, ent.Comp.MaxStacks);
        if (ent.Comp.Stacks >= ent.Comp.MaxStacks && !ent.Comp.Exalted)
        {
            ent.Comp.Exalted = true;
            // readability for the victim (CqcSequences §6.4): everyone near sees the Joe reach it
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-liturgy-exalted-others", ("joe", ent.Owner)), ent, PopupType.MediumCaution);
        }

        if (ent.Comp.Stacks != before)
            MeterChanged(ent);
    }

    /// <summary>Sets or clears Exalted directly (admin and test use; the meter sets it at max stacks).</summary>
    public void SetExalted(EntityUid joe, bool exalted)
    {
        if (!TryComp(joe, out EWJoeLiturgyComponent? liturgy) || liturgy.Exalted == exalted)
            return;

        liturgy.Exalted = exalted;
        MeterChanged((joe, liturgy));
    }

    /// <summary>
    /// Whether <see cref="TryConsumeEmpowered"/> would succeed now, without spending anything: the Joe is Exalted
    /// and a crowd-control bonus (<paramref name="ccBonus"/>) is not aimed at a target inside its CC-immunity
    /// window (FLOW-06).
    /// </summary>
    public bool CanConsumeEmpowered(EntityUid joe, EntityUid? target, bool ccBonus)
    {
        if (!TryComp(joe, out EWJoeLiturgyComponent? liturgy) || !liturgy.Exalted)
            return false;

        return !ccBonus || target is not { } t || !_cqc.IsCcImmune(joe, t);
    }

    /// <summary>
    /// Asked by a hotbar ability or a CQC sequence with an empowered branch. When the Joe is Exalted the flag is
    /// spent, the stacks drop to <see cref="EWJoeLiturgyComponent.StacksAfterEmpower"/> (ARCH-12: some attack
    /// speed survives) and the move plays its empowered version. A crowd-control bonus
    /// (<paramref name="ccBonus"/>) on a target inside its CC-immunity window is refused (FLOW-06) and keeps
    /// the Exalted state for the next move.
    /// </summary>
    public bool TryConsumeEmpowered(EntityUid joe, EntityUid? target, bool ccBonus, string ability)
    {
        if (!CanConsumeEmpowered(joe, target, ccBonus) || !TryComp(joe, out EWJoeLiturgyComponent? liturgy))
            return false;

        var before = liturgy.Stacks;
        liturgy.Exalted = false;
        liturgy.Stacks = Math.Clamp(Math.Min(liturgy.Stacks, liturgy.StacksAfterEmpower), 0, liturgy.MaxStacks);
        _cqc.ClearBuffer(joe);
        MeterChanged((joe, liturgy));

        var against = target is { } victim ? $" against {ToPrettyString(victim)}" : string.Empty;
        _adminLogger.Add(LogType.Action, LogImpact.Low,
            $"Early Warning Joe {ToPrettyString(joe):joe} spent Exalted ({before} -> {liturgy.Stacks} stacks) on an empowered {ability}{against}");
        return true;
    }

    /// <summary>
    /// A close-quarters hotbar ability landed: +<see cref="EWJoeLiturgyComponent.AbilityGain"/> stacks and an
    /// Ability step on <paramref name="target"/> (null for a cone). A hard CC (<paramref name="hardCc"/>) also
    /// opens the target's CC-immunity window. Forwards to <see cref="CMUCqcSystem.RegisterAbility"/>.
    /// </summary>
    public void AbilityLanded(EntityUid joe, EntityUid? target, bool hardCc = false)
    {
        _cqc.RegisterAbility(joe, target, hardCc);
    }

    /// <summary>Empties the meter and the buffer: the Joe was stunned or knocked down.</summary>
    public void Break(Entity<EWJoeLiturgyComponent> ent)
    {
        _cqc.ClearBuffer(ent);
        if (ent.Comp.Stacks == 0 && !ent.Comp.Exalted)
            return;

        ent.Comp.Stacks = 0;
        ent.Comp.Exalted = false;
        MeterChanged(ent);
    }

    private void MeterChanged(Entity<EWJoeLiturgyComponent> ent)
    {
        Dirty(ent);
        if (ent.Comp.Stacks > 0 || ent.Comp.Exalted)
        {
            _alerts.ShowAlert(ent.Owner, ent.Comp.Alert, (short) Math.Clamp(ent.Comp.Exalted ? ent.Comp.MaxStacks : ent.Comp.Stacks, 0, ent.Comp.MaxStacks));

            // the visible half exists only while the meter is up, so an idle Joe is not singled out
            var glow = EnsureComp<EWJoeLiturgyGlowComponent>(ent);
            if (glow.Stacks != ent.Comp.Stacks || glow.Exalted != ent.Comp.Exalted)
            {
                glow.Stacks = ent.Comp.Stacks;
                glow.Exalted = ent.Comp.Exalted;
                Dirty(ent, glow);
            }
        }
        else
        {
            _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
            RemComp<EWJoeLiturgyGlowComponent>(ent);
        }
    }

    /// <summary>
    /// Each landed hit takes <see cref="EWJoeLiturgyComponent.CooldownCutPerHit"/> off the remaining cooldown
    /// of the Joe's close-quarters actions, so the rotation keeps moving while the Joe stays in melee.
    /// </summary>
    public void CutCooldowns(Entity<EWJoeLiturgyComponent> ent)
    {
        if (ent.Comp.CooldownCutPerHit <= TimeSpan.Zero)
            return;

        var now = _timing.CurTime;
        foreach (var action in _actions.GetActions(ent))
        {
            if (action.Comp.Cooldown is not { } cooldown || cooldown.End <= now)
                continue;

            if (MetaData(action).EntityPrototype?.ID is not { } id || !ent.Comp.CqcActions.Contains(id))
                continue;

            var end = cooldown.End - ent.Comp.CooldownCutPerHit;
            if (end < now)
                end = now;

            _actions.SetCooldown(new Entity<ActionComponent?>(action.Owner, action.Comp), cooldown.Start, end);
        }
    }

    #endregion

    #region CC immunity (FLOW-06), forwarded to CMUCqcSystem

    public bool IsCcImmune(Entity<EWJoeLiturgyComponent?> joe, EntityUid target)
    {
        return _cqc.IsCcImmune(joe.Owner, target);
    }

    /// <summary>Opens <paramref name="target"/>'s hard-CC immunity window against this Joe.</summary>
    public void RecordHardCc(Entity<EWJoeLiturgyComponent?> joe, EntityUid target)
    {
        _cqc.RecordHardCc(joe.Owner, target);
    }

    /// <summary>RB-13: a Test of Faith or Judgement flash stun obeys the CC window like every other hard CC.</summary>
    private void OnFlashStun(Entity<EWJoeLiturgyComponent> ent, ref EWJoeFlashStunEvent args)
    {
        args.Duration = HardCcDuration(ent, args.Target, args.Duration);
        args.Handled = true;
    }

    private void OnFlashStunLanded(Entity<EWJoeLiturgyComponent> ent, ref EWJoeFlashStunLandedEvent args)
    {
        RecordHardCc((ent.Owner, ent.Comp), args.Target);
    }

    /// <summary>
    /// RB-13: how long a hard CC (knockdown or paralyze) of <paramref name="duration"/> from <paramref name="joe"/>
    /// lasts on <paramref name="target"/>, without applying it or opening the window. The xenofauna scaling
    /// (<see cref="EWJoeTargetRules.ScaleStun"/>, the Joe's <see cref="EWJoeComponent.XenofaunaStunMultiplier"/>) comes
    /// first, then, inside the target's window, <see cref="CMUCqcComponent.CcWindowMultiplier"/>. A source
    /// without <see cref="EWJoeComponent"/> is not scaled for xenofauna; one without the CQC component has no window.
    /// </summary>
    public TimeSpan HardCcDuration(EntityUid joe, EntityUid target, TimeSpan duration)
    {
        return _cqc.HardCcDuration(joe, target, duration);
    }

    /// <summary>
    /// RB-13: <see cref="HardCcDuration"/>, then opens (or refreshes) the target's window. For a hard CC another system
    /// applies later, such as the RMC obstacle-slam paralyze armed for a flight: the window opens when it is armed.
    /// </summary>
    public TimeSpan TakeHardCc(EntityUid joe, EntityUid target, TimeSpan duration)
    {
        return _cqc.TakeHardCc(joe, target, duration);
    }

    /// <summary>
    /// RB-13: the one way an Early Warning Joe applies a hard CC (knockdown or paralyze) to a target, from any source:
    /// the hotbar, Consecrate, Saturate, THE LIGHT and every CQC sequence. Scales <paramref name="duration"/> by
    /// <see cref="HardCcDuration"/> (xenofauna first, then half inside the window), applies it with refresh, and when it
    /// lands opens or refreshes the target's window. <paramref name="applied"/> is the duration used.
    /// </summary>
    public bool ApplyHardCc(EntityUid joe, EntityUid target, TimeSpan duration, EWJoeHardCcKind kind, out TimeSpan applied)
    {
        return _cqc.ApplyHardCc(joe, target, duration, ToCqcKind(kind), out applied);
    }

    /// <inheritdoc cref="ApplyHardCc(EntityUid, EntityUid, TimeSpan, EWJoeHardCcKind, out TimeSpan)"/>
    public bool ApplyHardCc(EntityUid joe, EntityUid target, TimeSpan duration, EWJoeHardCcKind kind)
    {
        return ApplyHardCc(joe, target, duration, kind, out _);
    }

    private static CMUHardCcKind ToCqcKind(EWJoeHardCcKind kind)
    {
        return kind switch
        {
            EWJoeHardCcKind.Knockdown => CMUHardCcKind.Knockdown,
            _ => CMUHardCcKind.Paralyze,
        };
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckEvery;

        var query = EntityQueryEnumerator<EWJoeLiturgyComponent>();
        while (query.MoveNext(out var uid, out var liturgy))
        {
            var buffered = TryComp(uid, out CMUCqcComponent? cqc) && cqc.Buffer.Count > 0;
            var active = liturgy.Stacks > 0 || liturgy.Exalted || buffered;
            if (active && (HasComp<StunnedComponent>(uid) || HasComp<KnockedDownComponent>(uid)))
            {
                Break((uid, liturgy));
            }
            else if ((liturgy.Stacks > 0 || liturgy.Exalted) && now >= liturgy.ExpiresAt)
            {
                // no banking between fights
                liturgy.Stacks = 0;
                liturgy.Exalted = false;
                MeterChanged((uid, liturgy));
            }
        }
    }
}

/// <summary>RB-13: the two hard CCs the window governs. Flashes, dazes, slows and roots are not hard CC.</summary>
public enum EWJoeHardCcKind : byte
{
    Knockdown,
    Paralyze,
}
