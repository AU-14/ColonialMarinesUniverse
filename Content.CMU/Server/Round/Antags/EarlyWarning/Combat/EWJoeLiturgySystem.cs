using Content.Shared._RMC14.Fireman;
using Content.Server.Administration.Logs;
using Content.Shared._RMC14.Tackle;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Alert;
using Content.Shared.CMU14.Fireman;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Combat;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Database;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Combat;

/// <summary>
/// ARCH-05 / FLOW-06 / ARCH-04 (node N12): the Liturgy meter. Turns the Joe's close-quarters inputs into
/// stacks, empties them when the window lapses or the Joe is stunned, makes the Joe Exalted at max stacks,
/// trims the close-quarters cooldowns on each hit and keeps the per-target hard-CC immunity windows. Every
/// input is also passed to <see cref="EWJoeCqcSequenceSystem"/> (ARCH-06). The passive per-stack buffs (ARCH-12)
/// live in the shared <see cref="EWJoeLiturgyBuffsSystem"/>.
/// </summary>
/// <remarks>
/// Input hooks (one directed subscription per pair; all pairs below were free):
/// <list type="bullet">
/// <item>(EWJoeLiturgyComponent, MeleeHitEvent): unarmed hits, raised on the Joe as its own weapon. Machete hits
/// are raised on the machete and forwarded by <c>EWJoeArsenalSystem.OnMacheteHit</c>.</item>
/// <item>(MobStateComponent, CMDisarmEvent): a disarm attempt, raised on the target. Never sets Handled.</item>
/// <item>(MobStateComponent, PullStartedMessage) and (EWJoeLiturgyComponent, CMUAggressiveGrabbedEvent): grabs. The
/// pull is taken on the pulled end, the last of its two raises.</item>
/// <item>Each close-quarters action handler calls <see cref="AbilityLanded"/> when it lands.</item>
/// </list>
/// Server only: melee is predicted, but nothing here is, so nothing mispredicts. The client reads the
/// networked stacks for the eye tint; the alert is set here.
/// </remarks>
public sealed class EWJoeLiturgySystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private EWJoeCqcSequenceSystem _sequences = default!;
    // RB-13: every hard CC from a Joe goes through ApplyHardCc
    [Dependency] private SharedStunSystem _stun = default!;

    private static readonly ProtoId<DamageTypePrototype> Slash = "Slash";

    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(0.2);

    private TimeSpan _nextCheck;
    private readonly List<EntityUid> _hitTargets = new();
    private readonly List<EntityUid> _expiredImmunity = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeLiturgyComponent, MeleeHitEvent>(OnMeleeHit);
        // PullStartedMessage is raised on the puller, then on the pulled. FiremanCarrySystem resets the carrier's
        // grab state on both ends (OnCarrierPullStarted, OnCarriablePullStarted), which undid an instant Seize grip
        // made from the puller's raise. Taking the Grab step on the pulled end, after FiremanCarrySystem, runs it
        // after both resets.
        SubscribeLocalEvent<MobStateComponent, PullStartedMessage>(OnPullStarted, after: [typeof(FiremanCarrySystem)]);
        SubscribeLocalEvent<EWJoeLiturgyComponent, CMUAggressiveGrabbedEvent>(OnAggressiveGrabbed);
        SubscribeLocalEvent<EWJoeLiturgyComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<EWJoeLiturgyComponent, EWJoeFlashStunEvent>(OnFlashStun);
        SubscribeLocalEvent<EWJoeLiturgyComponent, EWJoeFlashStunLandedEvent>(OnFlashStunLanded);
        // ARCH-04: the pair is free; TransformComponent belongs to the Yautja gauntlet and the tackle
        // components to RMC. Handled is left alone so both still resolve the disarm.
        SubscribeLocalEvent<MobStateComponent, CMDisarmEvent>(OnDisarm);
    }

    #region Hooks

    private void OnMeleeHit(Entity<EWJoeLiturgyComponent> ent, ref MeleeHitEvent args)
    {
        // raised on the weapon: only a bare-handed Joe is its own weapon
        if (args.Weapon != ent.Owner)
            return;

        HandleMeleeHit(ent.Owner, args);
    }

    /// <summary>
    /// A melee swing by <paramref name="joe"/>, from its own fists or forwarded from the machete. An empty
    /// swing is a miss: it breaks the sequence but keeps the stacks. A hit on living bodies is a Hit (or a
    /// Wide for a wide swing, one stack per body up to the cap) and trims the close-quarters cooldowns. The step
    /// remembers whether it came from a blade (ARCH-12), which a <see cref="EWJoeCqcSequencePrototype.RequiresBlade"/>
    /// sequence's Wide steps need.
    /// </summary>
    public void HandleMeleeHit(EntityUid joe, MeleeHitEvent args)
    {
        if (args.Handled || !args.IsHit || !TryComp(joe, out EWJoeLiturgyComponent? liturgy))
            return;

        if (args.HitEntities.Count == 0)
        {
            ClearBuffer(liturgy);
            return;
        }

        _hitTargets.Clear();
        foreach (var hit in args.HitEntities)
        {
            if (hit != joe && TryComp(hit, out MobStateComponent? mob) && _mobState.IsAlive(hit, mob))
                _hitTargets.Add(hit);
        }

        // furniture and corpses build nothing and leave the sequence as it is
        if (_hitTargets.Count == 0)
            return;

        // the body the buffer is already on keeps the sequence going through a crowd
        var target = _hitTargets[0];
        if (CurrentTarget(liturgy) is { } current && _hitTargets.Contains(current))
            target = current;

        var blade = IsBlade(joe, args.Weapon);
        if (args.Direction == null)
        {
            Register(joe, EWJoeCqcInput.Hit, target, fromBlade: blade);
        }
        else
        {
            var bodies = Math.Min(_hitTargets.Count, liturgy.WideGainCap);
            Register(joe, EWJoeCqcInput.Wide, target, bodies * liturgy.WideGain, blade);
        }

        CutCooldowns((joe, liturgy));
    }

    /// <summary>
    /// ARCH-12: a blade is a weapon other than the Joe's own body that is the machete or deals Slash damage.
    /// </summary>
    public bool IsBlade(EntityUid joe, EntityUid weapon)
    {
        if (weapon == joe)
            return false;

        if (HasComp<EWJoeMacheteComponent>(weapon))
            return true;

        return _melee.GetDamage(weapon, joe).DamageDict.TryGetValue(Slash, out var slash) && slash > 0;
    }

    private void OnDisarm(Entity<MobStateComponent> ent, ref CMDisarmEvent args)
    {
        if (args.User == ent.Owner || !HasComp<EWJoeLiturgyComponent>(args.User))
            return;

        Register(args.User, EWJoeCqcInput.Disarm, ent.Owner);
    }

    private void OnPullStarted(Entity<MobStateComponent> ent, ref PullStartedMessage args)
    {
        // raised on both ends of the pull; only a Joe's grab of this mob counts
        if (args.PulledUid != ent.Owner || !HasComp<EWJoeLiturgyComponent>(args.PullerUid))
            return;

        Register(args.PullerUid, EWJoeCqcInput.Grab, ent.Owner);
    }

    private void OnAggressiveGrabbed(Entity<EWJoeLiturgyComponent> ent, ref CMUAggressiveGrabbedEvent args)
    {
        // review fix: a corpse (or anything not alive) must not feed the meter
        if (args.Carrier != ent.Owner || !_mobState.IsAlive(args.Target))
            return;

        Register(ent.Owner, EWJoeCqcInput.AggressiveGrab, args.Target);
    }

    private void OnShutdown(Entity<EWJoeLiturgyComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
        if (!TerminatingOrDeleted(ent))
            RemCompDeferred<EWJoeLiturgyGlowComponent>(ent);
    }

    #endregion

    #region Meter

    /// <summary>
    /// Records one close-quarters input: adds its stacks (<paramref name="gain"/>, or the component's gain for
    /// <paramref name="input"/>), restarts the window, then feeds the sequence buffer. <paramref name="target"/>
    /// is null for an untargeted ability. <paramref name="fromBlade"/> marks a swing made with a blade (ARCH-12).
    /// Does nothing for an entity without the meter.
    /// </summary>
    public void Register(EntityUid joe, EWJoeCqcInput input, EntityUid? target, int? gain = null, bool fromBlade = false)
    {
        if (!TryComp(joe, out EWJoeLiturgyComponent? liturgy))
            return;

        var now = _timing.CurTime;

        // match first, gain after: max stacks empower the NEXT move, so the input that fills the meter must not
        // also find its own sequence already Exalted
        if (TryComp(joe, out EWJoeComponent? ewJoe))
            _sequences.Push((joe, liturgy), (joe, ewJoe), input, target, now, fromBlade);

        var amount = gain ?? GainFor(liturgy, input);
        if (amount > 0)
        {
            AddStacks((joe, liturgy), amount);
            var window = liturgy.Window;
            if (HasComp<EWJoeOverclockComponent>(joe))
                window *= liturgy.OverclockWindowMultiplier;
            liturgy.ExpiresAt = now + window;
        }
    }

    /// <summary>A wasted swing: the sequence buffer starts over, the stacks stay.</summary>
    public void RegisterMiss(EntityUid joe)
    {
        if (TryComp(joe, out EWJoeLiturgyComponent? liturgy))
            ClearBuffer(liturgy);
    }

    public static int GainFor(EWJoeLiturgyComponent liturgy, EWJoeCqcInput input)
    {
        return input switch
        {
            EWJoeCqcInput.Hit => liturgy.HitGain,
            EWJoeCqcInput.Wide => liturgy.WideGain,
            EWJoeCqcInput.Disarm => liturgy.DisarmGain,
            EWJoeCqcInput.Grab => liturgy.GrabGain,
            EWJoeCqcInput.AggressiveGrab => liturgy.AggressiveGrabGain,
            EWJoeCqcInput.Ability => liturgy.AbilityGain,
            _ => 0,
        };
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

        return !ccBonus || target is not { } t || !IsCcImmune((joe, liturgy), t);
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
        ClearBuffer(liturgy);
        MeterChanged((joe, liturgy));

        var against = target is { } victim ? $" against {ToPrettyString(victim)}" : string.Empty;
        _adminLogger.Add(LogType.Action, LogImpact.Low,
            $"Early Warning Joe {ToPrettyString(joe):joe} spent Exalted ({before} -> {liturgy.Stacks} stacks) on an empowered {ability}{against}");
        return true;
    }

    /// <summary>
    /// A close-quarters hotbar ability landed: +<see cref="EWJoeLiturgyComponent.AbilityGain"/> stacks and an
    /// Ability step on <paramref name="target"/> (null for a cone). A hard CC (<paramref name="hardCc"/>) also
    /// opens the target's CC-immunity window.
    /// </summary>
    public void AbilityLanded(EntityUid joe, EntityUid? target, bool hardCc = false)
    {
        if (!HasComp<EWJoeLiturgyComponent>(joe))
            return;

        if (hardCc && target is { } victim)
            RecordHardCc(joe, victim);

        Register(joe, EWJoeCqcInput.Ability, target);
    }

    /// <summary>Empties the meter and the buffer: the Joe was stunned or knocked down.</summary>
    public void Break(Entity<EWJoeLiturgyComponent> ent)
    {
        ClearBuffer(ent.Comp);
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

    #region CC immunity (FLOW-06)

    public bool IsCcImmune(Entity<EWJoeLiturgyComponent?> joe, EntityUid target)
    {
        return Resolve(joe, ref joe.Comp, false)
            && joe.Comp.CcImmuneUntil.TryGetValue(target, out var until)
            && _timing.CurTime < until;
    }

    /// <summary>Opens <paramref name="target"/>'s hard-CC immunity window against this Joe.</summary>
    public void RecordHardCc(Entity<EWJoeLiturgyComponent?> joe, EntityUid target)
    {
        if (!Resolve(joe, ref joe.Comp, false))
            return;

        joe.Comp.CcImmuneUntil[target] = _timing.CurTime + joe.Comp.CcImmunity;
    }

    /// <summary>
    /// RB-13: how long a hard CC (knockdown or paralyze) of <paramref name="duration"/> from <paramref name="joe"/>
    /// lasts on <paramref name="target"/>, without applying it or opening the window. The xenofauna scaling
    /// (<see cref="EWJoeTargetRules.ScaleStun"/>, the Joe's <see cref="EWJoeComponent.XenofaunaStunMultiplier"/>) comes
    /// first, then, inside the target's window, <see cref="EWJoeLiturgyComponent.CcWindowMultiplier"/>. A source
    /// without <see cref="EWJoeComponent"/> is not scaled for xenofauna; one without the meter has no window.
    /// </summary>
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

    public TimeSpan HardCcDuration(EntityUid joe, EntityUid target, TimeSpan duration)
    {
        if (TryComp(joe, out EWJoeComponent? ewJoe))
            duration = EWJoeTargetRules.ScaleStun(EntityManager, target, duration, ewJoe.XenofaunaStunMultiplier);

        if (TryComp(joe, out EWJoeLiturgyComponent? liturgy) && IsCcImmune((joe, liturgy), target))
            duration *= Math.Clamp(liturgy.CcWindowMultiplier, 0f, 1f);

        return duration;
    }

    /// <summary>
    /// RB-13: <see cref="HardCcDuration"/>, then opens (or refreshes) the target's window. For a hard CC another system
    /// applies later, such as the RMC obstacle-slam paralyze armed for a flight: the window opens when it is armed.
    /// </summary>
    public TimeSpan TakeHardCc(EntityUid joe, EntityUid target, TimeSpan duration)
    {
        duration = HardCcDuration(joe, target, duration);
        RecordHardCc(joe, target);
        return duration;
    }

    /// <summary>
    /// RB-13: the one way an Early Warning Joe applies a hard CC (knockdown or paralyze) to a target, from any source:
    /// the hotbar, Consecrate, Saturate, THE LIGHT and every CQC sequence. Scales <paramref name="duration"/> by
    /// <see cref="HardCcDuration"/> (xenofauna first, then half inside the window), applies it with refresh, and when it
    /// lands opens or refreshes the target's window. <paramref name="applied"/> is the duration used.
    /// </summary>
    public bool ApplyHardCc(EntityUid joe, EntityUid target, TimeSpan duration, EWJoeHardCcKind kind, out TimeSpan applied)
    {
        applied = HardCcDuration(joe, target, duration);
        var landed = kind switch
        {
            EWJoeHardCcKind.Knockdown => _stun.TryKnockdown(target, applied, refresh: true, autoStand: true, drop: true),
            _ => _stun.TryParalyze(target, applied, true),
        };

        if (landed)
            RecordHardCc(joe, target);

        return landed;
    }

    /// <inheritdoc cref="ApplyHardCc(EntityUid, EntityUid, TimeSpan, EWJoeHardCcKind, out TimeSpan)"/>
    public bool ApplyHardCc(EntityUid joe, EntityUid target, TimeSpan duration, EWJoeHardCcKind kind)
    {
        return ApplyHardCc(joe, target, duration, kind, out _);
    }

    #endregion

    #region Buffer

    private static void ClearBuffer(EWJoeLiturgyComponent liturgy)
    {
        liturgy.Buffer.Clear();
    }

    /// <summary>The body the buffer's steps are on: the last recorded target.</summary>
    public static EntityUid? CurrentTarget(EWJoeLiturgyComponent liturgy)
    {
        for (var i = liturgy.Buffer.Count - 1; i >= 0; i--)
        {
            if (liturgy.Buffer[i].Target is { } target)
                return target;
        }

        return null;
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
            var active = liturgy.Stacks > 0 || liturgy.Exalted || liturgy.Buffer.Count > 0;
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

            if (liturgy.Buffer.Count > 0 && now - liturgy.Buffer[^1].Time > liturgy.MaxGap)
                ClearBuffer(liturgy);

            if (liturgy.CcImmuneUntil.Count == 0)
                continue;

            _expiredImmunity.Clear();
            foreach (var (target, until) in liturgy.CcImmuneUntil)
            {
                if (now >= until || TerminatingOrDeleted(target))
                    _expiredImmunity.Add(target);
            }

            foreach (var target in _expiredImmunity)
            {
                liturgy.CcImmuneUntil.Remove(target);
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
