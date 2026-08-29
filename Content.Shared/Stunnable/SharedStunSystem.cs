using System.Globalization;
using Content.Shared.ActionBlocker;
using Content.Shared._CMU14.Chemistry.Effects;
using Content.Shared._RMC14.StatusEffect;
using Content.Shared.Administration.Logs;
using Content.Shared.Alert;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Hands;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Shared.StatusEffectNew;
using Content.Shared.Throwing;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using NewStatusEffectsSystem = Content.Shared.StatusEffectNew.StatusEffectsSystem;

namespace Content.Shared.Stunnable;

public abstract partial class SharedStunSystem : EntitySystem
{
    public static readonly EntProtoId StunId = "StatusEffectStunned";
    public static readonly EntProtoId ParalyzeId = "StatusEffectParalyzed";
    public static readonly EntProtoId RMCUnconsciousId = "StatusEffectRMCUnconscious";

    [Dependency] protected IGameTiming GameTiming = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] protected ActionBlockerSystem Blocker = default!;
    [Dependency] protected AlertsSystem Alerts = default!;
    [Dependency] private EntityWhitelistSystem _entityWhitelist = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] protected SharedAppearanceSystem Appearance = default!;
    [Dependency] protected SharedDoAfterSystem DoAfter = default!;
    [Dependency] protected SharedStaminaSystem Stamina = default!;
    [Dependency] private NewStatusEffectsSystem _status = default!;

    private readonly HashSet<EntityUid> _explicitParalyzeClears = new();
    private readonly HashSet<EntityUid> _silentParalyzeApplications = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<StunnedComponent, ComponentStartup>(UpdateCanMove);
        SubscribeLocalEvent<StunnedComponent, ComponentShutdown>(OnStunShutdown);

        SubscribeLocalEvent<StunOnContactComponent, StartCollideEvent>(OnStunOnContactCollide);

        // Attempt event subscriptions.
        SubscribeLocalEvent<StunnedComponent, ChangeDirectionAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StunnedComponent, UpdateCanMoveEvent>(OnMoveAttempt);
        SubscribeLocalEvent<StunnedComponent, InteractionAttemptEvent>(OnAttemptInteract);
        SubscribeLocalEvent<StunnedComponent, UseAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StunnedComponent, ThrowAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StunnedComponent, DropAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StunnedComponent, AttackAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StunnedComponent, PickupAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StunnedComponent, IsEquippingAttemptEvent>(OnEquipAttempt);
        SubscribeLocalEvent<StunnedComponent, IsUnequippingAttemptEvent>(OnUnequipAttempt);
        SubscribeLocalEvent<MobStateComponent, MobStateChangedEvent>(OnMobStateChanged);

        // New Status Effect subscriptions
        SubscribeLocalEvent<StunnedStatusEffectComponent, StatusEffectAppliedEvent>(OnStunStatusApplied);
        SubscribeLocalEvent<StunnedStatusEffectComponent, StatusEffectRemovedEvent>(OnStunStatusRemoved);
        SubscribeLocalEvent<StunnedStatusEffectComponent, StatusEffectRelayedEvent<StunEndAttemptEvent>>(OnStunEndAttempt);

        SubscribeLocalEvent<KnockdownStatusEffectComponent, StatusEffectAppliedEvent>(OnKnockdownStatusApplied);
        SubscribeLocalEvent<KnockdownStatusEffectComponent, StatusEffectRemovedEvent>(OnKnockdownStatusRemoved);
        SubscribeLocalEvent<KnockdownStatusEffectComponent, StatusEffectRelayedEvent<StandUpAttemptEvent>>(OnStandUpAttempt);

        // Stun Appearance Data
        InitializeKnockdown();
        InitializeAppearance();
    }

    private void OnAttemptInteract(Entity<StunnedComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnMobStateChanged(EntityUid uid, MobStateComponent component, MobStateChangedEvent args)
    {
        switch (args.NewMobState)
        {
            case MobState.Alive:
                {
                    break;
                }
            case MobState.Critical:
                {
                    TryClearStunAndKnockdown(uid);
                    RemComp<KnockedDownComponent>(uid);
                    _standingState.Down(uid, playSound: false, dropHeldItems: false, force: true);
                    break;
                }
            case MobState.Dead:
                {
                    TryClearStunAndKnockdown(uid);
                    RemComp<KnockedDownComponent>(uid);
                    _standingState.Down(uid, playSound: false, dropHeldItems: false, force: true);
                    break;
                }
            case MobState.Invalid:
            default:
                return;
        }

    }

    private void OnStunShutdown(Entity<StunnedComponent> ent, ref ComponentShutdown args)
    {
        // This exists so the client can end their funny animation if they're playing one.
        UpdateCanMove(ent, ent.Comp, args);
        Appearance.RemoveData(ent, StunVisuals.SeeingStars);
        if (!TerminatingOrDeleted(ent))
            RaiseLocalEvent(ent, new StatusEffectEndedEvent(ent, "Stun"));
    }

    private void UpdateCanMove(EntityUid uid, StunnedComponent component, EntityEventArgs args)
    {
        Blocker.UpdateCanMove(uid);
    }

    private void OnStunOnContactCollide(Entity<StunOnContactComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != ent.Comp.FixtureId)
            return;

        if (_entityWhitelist.IsWhitelistPass(ent.Comp.Blacklist, args.OtherEntity))
            return;

        TryUpdateStunDuration(args.OtherEntity, ent.Comp.Duration);
        TryKnockdown(args.OtherEntity, ent.Comp.Duration, force: true);
    }

    // TODO STUN: Make events for different things. (Getting modifiers, attempt events, informative events...)
    public bool TryAddStunDuration(EntityUid uid, TimeSpan duration, bool visualized = false, bool force = false)
    {
        duration = ApplyStunDurationModifiers(uid, duration);
        if (duration <= TimeSpan.Zero || !CanApplyStun(uid, force))
            return false;

        return TryAddStunDurationRaw(uid, duration, visualized, force);
    }

    public bool TryUpdateStunDuration(EntityUid uid, TimeSpan? duration, bool visualized = false, bool force = false)
    {
        if (duration is { } value)
        {
            duration = ApplyStunDurationModifiers(uid, value);
            if (duration <= TimeSpan.Zero)
                return false;
        }

        if (!CanApplyStun(uid, force))
            return false;

        return TryUpdateStunDurationRaw(uid, duration, visualized, force);
    }

    private void OnStunnedSuccessfully(EntityUid uid, TimeSpan? duration, bool visualized)
    {
        var ev = new StunnedEvent(); // todo: rename event or change how it is raised - this event is raised each time duration of stun was externally changed
        RaiseLocalEvent(uid, ref ev);

        var evDropHands = new DropHandItemsEvent();
        RaiseLocalEvent(uid, ref evDropHands);

        if (visualized)
            TrySeeingStars(uid);

        var timeForLogs = duration.HasValue
            ? duration.Value.TotalSeconds.ToString(CultureInfo.CurrentCulture)
            : "Infinite";
        _adminLogger.Add(LogType.Stamina, LogImpact.Medium, $"{ToPrettyString(uid):user} stunned for {timeForLogs} seconds");
    }

    /// <summary>
    /// Applies a stun using the legacy refresh-or-stack contract.
    /// </summary>
    public bool TryStun(EntityUid uid, TimeSpan time, bool refresh, bool force = false)
    {
        time = ApplyStunDurationModifiers(uid, time);
        if (time <= TimeSpan.Zero || !CanApplyStun(uid, force))
            return false;

        return refresh
            ? TryUpdateStunDurationRaw(uid, time, visualized: false, force)
            : TryAddStunDurationRaw(uid, time, visualized: false, force);
    }

    /// <summary>
    ///     Tries to knock an entity to the ground, but will fail if they aren't able to crawl.
    ///     Useful if you don't want to paralyze an entity that can't crawl, but still want to knockdown
    ///     entities that can.
    /// </summary>
    /// <param name="entity">Entity we're trying to knockdown.</param>
    /// <param name="time">Time of the knockdown.</param>
    /// <param name="refresh">Do we refresh their timer, or add to it if one exists?</param>
    /// <param name="autoStand">Whether we should automatically stand when knockdown ends.</param>
    /// <param name="drop">Should we drop what we're holding?</param>
    /// <param name="force">Should we force crawling? Even if something tried to block it?</param>
    /// <returns>Returns true if the entity is able to crawl, and was able to be knocked down.</returns>
    public bool TryCrawling(Entity<CrawlerComponent?> entity,
        TimeSpan? time,
        bool refresh = true,
        bool autoStand = true,
        bool drop = true,
        bool force = false)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return false;

        return TryKnockdown(entity, time, refresh, autoStand, drop, force);
    }

    /// <inheritdoc cref="TryCrawling(Entity{CrawlerComponent?},TimeSpan?,bool,bool,bool,bool)"/>
    /// <summary>An overload of TryCrawling which uses the default crawling time from the CrawlerComponent as its timespan.</summary>
    public bool TryCrawling(Entity<CrawlerComponent?> entity,
        bool refresh = true,
        bool autoStand = true,
        bool drop = true,
        bool force = false)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return false;

        return TryKnockdown(entity, entity.Comp.DefaultKnockedDuration, refresh, autoStand, drop, force);
    }

    /// <summary>
    ///     Checks if we can knock down an entity to the ground...
    /// </summary>
    /// <param name="entity">The entity we're trying to knock down</param>
    /// <param name="time">The time of the knockdown</param>
    /// <param name="autoStand">Whether we want to automatically stand when knockdown ends.</param>
    /// <param name="drop">Whether we should drop items.</param>
    /// <param name="force">Should we force the status effect?</param>
    public bool CanKnockdown(Entity<StandingStateComponent?> entity, ref TimeSpan? time, ref bool autoStand, ref bool drop, bool force = false)
    {
        if (time <= TimeSpan.Zero)
            return false;

        // Can't fall down if you can't actually be downed.
        if (!Resolve(entity, ref entity.Comp, false))
            return false;

        var evAttempt = new KnockDownAttemptEvent(autoStand, drop, time);
        RaiseLocalEvent(entity, ref evAttempt);

        autoStand = evAttempt.AutoStand;
        drop = evAttempt.Drop;

        return force || !evAttempt.Cancelled;
    }

    /// <summary>
    ///     Knocks down the entity, making it fall to the ground.
    /// </summary>
    /// <param name="entity">The entity we're trying to knock down</param>
    /// <param name="time">The time of the knockdown</param>
    /// <param name="refresh">Whether we should refresh a running timer or add to it, if one exists.</param>
    /// <param name="autoStand">Whether we want to automatically stand when knockdown ends.</param>
    /// <param name="drop">Whether we should drop items.</param>
    /// <param name="force">Should we force the status effect?</param>
    public bool TryKnockdown(Entity<CrawlerComponent?> entity, TimeSpan? time, bool refresh = true, bool autoStand = true, bool drop = true, bool force = false)
    {
        if (time is { } value)
            time = ApplyKnockdownDurationModifiers(entity, value);

        if (!CanKnockdown(entity.Owner, ref time, ref autoStand, ref drop, force))
            return false;

        // If the entity can't crawl they also need to be stunned, and therefore we should be using paralysis status effect.
        // Also time shouldn't be null if we're and trying to add time but, we check just in case anyways.
        if (!Resolve(entity, ref entity.Comp, false))
        {
            if (!CanApplyParalyzeStatus(entity, force))
                return false;

            return refresh || time == null
                ? TryUpdateParalyzeDurationRaw(entity, time, visualized: false, force: force)
                : TryAddParalyzeDurationRaw(entity, time.Value, visualized: false, force: force);
        }

        Knockdown(entity, time, refresh, autoStand, drop);
        return true;
    }

    private void Crawl(Entity<CrawlerComponent?> entity, TimeSpan? time, bool refresh, bool autoStand, bool drop)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return;

        Knockdown(entity, time, refresh, autoStand, drop);
    }

    private void Knockdown(EntityUid uid, TimeSpan? time, bool refresh, bool autoStand, bool drop)
    {
        // Initialize our component with the relevant data we need if we don't have it
        if (EnsureComp<KnockedDownComponent>(uid, out var component))
        {
            RefreshKnockedMovement((uid, component));
            CancelKnockdownDoAfter((uid, component));
        }
        else
        {
            // Only drop items the first time we want to fall...
            if (drop)
            {
                var ev = new DropHandItemsEvent();
                RaiseLocalEvent(uid, ref ev);
            }

            // Only update Autostand value if it's our first time being knocked down...
            SetAutoStand((uid, component), autoStand);
        }

        var knockedEv = new KnockedDownEvent();
        RaiseLocalEvent(uid, ref knockedEv);

        if (time != null)
        {
            UpdateKnockdownTime((uid, component), time.Value, refresh);
            _adminLogger.Add(LogType.Stamina, LogImpact.Medium, $"{ToPrettyString(uid):user} was knocked down for {time.Value.TotalSeconds} seconds");
        }
        else
        {
            Alerts.ShowAlert(uid, KnockdownAlert);
            _adminLogger.Add(LogType.Stamina, LogImpact.Medium, $"{ToPrettyString(uid):user} was knocked down");
        }
    }

    public bool TryAddParalyzeDuration(EntityUid uid, TimeSpan? duration, bool visualized = false, bool force = false)
    {
        if (duration == null)
            return TryUpdateParalyzeDuration(uid, duration, visualized, force);

        duration = ApplyParalyzeDurationModifiers(uid, duration.Value);
        if (duration <= TimeSpan.Zero || !CanApplyParalyze(uid, duration, force))
            return false;

        return TryAddParalyzeDurationRaw(uid, duration.Value, visualized, force);
    }

    /// <summary>
    /// Applies paralysis using the legacy refresh-or-stack contract.
    /// </summary>
    public bool TryParalyze(EntityUid uid, TimeSpan time, bool refresh, bool force = false)
    {
        time = ApplyParalyzeDurationModifiers(uid, time);
        if (time <= TimeSpan.Zero || !CanApplyParalyze(uid, time, force))
            return false;

        return refresh
            ? TryUpdateParalyzeDurationRaw(uid, time, visualized: false, force)
            : TryAddParalyzeDurationRaw(uid, time, visualized: false, force);
    }

    public bool TryUpdateParalyzeDuration(EntityUid uid, TimeSpan? duration, bool visualized = false, bool force = false)
    {
        if (duration is { } value)
        {
            duration = ApplyParalyzeDurationModifiers(uid, value);
            if (duration <= TimeSpan.Zero)
                return false;
        }

        if (!CanApplyParalyze(uid, duration, force))
            return false;

        return TryUpdateParalyzeDurationRaw(uid, duration, visualized, force);
    }

    /// <summary>
    /// Removes the authoritative stun effect and knockdown state together.
    /// </summary>
    public bool TryClearStunAndKnockdown(EntityUid uid)
    {
        _explicitParalyzeClears.Add(uid);
        var removed = _status.TryRemoveStatusEffect(uid, StunId);
        removed |= _status.TryRemoveStatusEffect(uid, ParalyzeId);
        removed |= TryCompleteExplicitParalyzeClear(uid, explicitClear: true);
        return removed;
    }

    /// <summary>
    /// Reduces paralysis once, including a separately timed crawler knockdown when present.
    /// </summary>
    public bool TryRemoveStunAndKnockdownTime(EntityUid uid, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            return false;

        var removed = _status.TryRemoveTime(uid, StunId, duration);
        removed |= _status.TryRemoveTime(uid, ParalyzeId, duration);

        if (!TryComp(uid, out KnockedDownComponent? knockedDown) ||
            knockedDown.LifeStage > ComponentLifeStage.Running ||
            knockedDown.NextUpdate <= GameTiming.CurTime)
        {
            return removed;
        }

        SetKnockdownTime((uid, knockedDown), knockedDown.NextUpdate - GameTiming.CurTime - duration);
        return true;
    }

    /// <summary>
    /// Removes only the direct paralysis status, retaining pure stun and independently timed knockdown owners.
    /// </summary>
    public bool TryClearParalyze(EntityUid uid)
    {
        return _status.TryRemoveStatusEffect(uid, ParalyzeId);
    }

    /// <summary>
    /// Reduces only the direct paralysis status.
    /// </summary>
    public bool TryRemoveParalyzeTime(EntityUid uid, TimeSpan duration)
    {
        return duration > TimeSpan.Zero && _status.TryRemoveTime(uid, ParalyzeId, duration);
    }

    /// <summary>
    /// Sets the direct paralysis duration.
    /// </summary>
    public bool TrySetParalyzeDuration(EntityUid uid, TimeSpan? duration, bool force = false)
    {
        if (duration <= TimeSpan.Zero || !CanApplyParalyzeStatus(uid, force))
            return false;

        _silentParalyzeApplications.Add(uid);
        try
        {
            if (!_status.TrySetStatusEffectDuration(uid, ParalyzeId, duration, force: true))
                return false;

            EnsureComp<StunnedComponent>(uid);
            EnsureComp<KnockedDownComponent>(uid);
            return true;
        }
        finally
        {
            _silentParalyzeApplications.Remove(uid);
        }
    }

    private bool TryCompleteExplicitParalyzeClear(EntityUid uid, bool explicitClear)
    {
        if (explicitClear &&
            (_status.HasStatusEffect(uid, StunId) || _status.HasStatusEffect(uid, ParalyzeId)))
        {
            return false;
        }

        if (_status.HasEffectComp<KnockdownStatusEffectComponent>(uid) ||
            !TryComp(uid, out KnockedDownComponent? knockedDown) ||
            knockedDown.LifeStage > ComponentLifeStage.Running ||
            !knockedDown.AutoStand ||
            !explicitClear && knockedDown.NextUpdate > GameTiming.CurTime)
        {
            if (explicitClear)
                _explicitParalyzeClears.Remove(uid);
            return false;
        }

        _explicitParalyzeClears.Add(uid);
        try
        {
            SetKnockdownTime((uid, knockedDown), TimeSpan.Zero);
            CancelKnockdownDoAfter((uid, knockedDown));

            if (!CanCompleteStatusKnockdown((uid, knockedDown)) || !_standingState.Stand(uid))
                return false;

            return RemComp<KnockedDownComponent>(uid);
        }
        finally
        {
            _explicitParalyzeClears.Remove(uid);
        }
    }

    private bool CanCompleteStatusKnockdown(Entity<KnockedDownComponent> entity)
    {
        var ev = new StandUpAttemptEvent(entity.Comp.AutoStand);
        RaiseLocalEvent(entity, ref ev);

        if (ev.Autostand != entity.Comp.AutoStand)
            SetAutoStand(entity.Owner, ev.Autostand);

        if (ev.Message != null)
            _popup.PopupEntity(ev.Message.Value.Item1, entity, entity, ev.Message.Value.Item2);

        return !ev.Cancelled;
    }

    public bool TryUnstun(Entity<StunnedComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, logMissing: false))
            return true;

        var ev = new StunEndAttemptEvent();
        RaiseLocalEvent(entity, ref ev);

        return !ev.Cancelled && RemComp<StunnedComponent>(entity);
    }

    private TimeSpan ApplyChemicalDurationModifier(EntityUid uid, TimeSpan time)
    {
        var ev = new GetChemicalStunTimeMultiplierEvent();
        RaiseLocalEvent(uid, ref ev);
        return time * MathF.Max(0f, ev.Multiplier);
    }

    private TimeSpan ApplyStatusDurationModifier(EntityUid uid, TimeSpan time, string key)
    {
        var ev = new RMCStatusEffectTimeEvent(key, time);
        RaiseLocalEvent(uid, ref ev);
        return ev.Duration;
    }

    private TimeSpan ApplyStunDurationModifiers(EntityUid uid, TimeSpan time)
    {
        return ApplyStatusDurationModifier(uid, ApplyChemicalDurationModifier(uid, time), "Stun");
    }

    private TimeSpan ApplyKnockdownDurationModifiers(EntityUid uid, TimeSpan time)
    {
        return ApplyStatusDurationModifier(uid, ApplyChemicalDurationModifier(uid, time), "KnockedDown");
    }

    private TimeSpan ApplyParalyzeDurationModifiers(EntityUid uid, TimeSpan time)
    {
        var chemicallyAdjusted = ApplyChemicalDurationModifier(uid, time);
        var stun = ApplyStatusDurationModifier(uid, chemicallyAdjusted, "Stun");
        var knockdown = ApplyStatusDurationModifier(uid, chemicallyAdjusted, "KnockedDown");
        return stun >= knockdown ? stun : knockdown;
    }

    private bool CanApplyStun(EntityUid uid, bool force)
    {
        // New effects perform this preflight while spawning. Existing effects must explicitly
        // repeat it so state-dependent immunities can reject refreshes and stacks too.
        return _status.CanAddStatusEffect(uid, StunId, force);
    }

    private bool CanApplyParalyzeStatus(EntityUid uid, bool force)
    {
        return _status.CanAddStatusEffect(uid, ParalyzeId, force);
    }

    private bool CanApplyParalyze(EntityUid uid, TimeSpan? duration, bool force)
    {
        var autoStand = true;
        var drop = true;
        return CanKnockdown(uid, ref duration, ref autoStand, ref drop, force) &&
               CanApplyParalyzeStatus(uid, force);
    }

    private bool TryAddStunDurationRaw(EntityUid uid, TimeSpan duration, bool visualized, bool force)
    {
        if (!_status.TryAddStatusEffectDuration(uid, StunId, duration, force: true))
            return false;

        OnStunnedSuccessfully(uid, duration, visualized);
        return true;
    }

    private bool TryUpdateStunDurationRaw(EntityUid uid, TimeSpan? duration, bool visualized, bool force)
    {
        if (!_status.TryUpdateStatusEffectDuration(uid, StunId, duration, force: true))
            return false;

        OnStunnedSuccessfully(uid, duration, visualized);
        return true;
    }

    private bool TryAddParalyzeDurationRaw(EntityUid uid, TimeSpan duration, bool visualized, bool force)
    {
        var existed = _status.TryGetStatusEffect(uid, ParalyzeId, out _);
        if (!_status.TryAddStatusEffectDuration(uid, ParalyzeId, duration, force: true))
            return false;

        if (existed)
            Knockdown(uid, null, false, true, true);
        OnStunnedSuccessfully(uid, duration, visualized);
        return true;
    }

    private bool TryUpdateParalyzeDurationRaw(EntityUid uid, TimeSpan? duration, bool visualized = false, bool force = false)
    {
        var existed = _status.TryGetStatusEffect(uid, ParalyzeId, out _);
        if (!_status.TryUpdateStatusEffectDuration(uid, ParalyzeId, duration, force: true))
            return false;

        if (existed)
            Knockdown(uid, null, false, true, true);
        OnStunnedSuccessfully(uid, duration, visualized);
        return true;
    }

    private void OnStunStatusApplied(Entity<StunnedStatusEffectComponent> entity, ref StatusEffectAppliedEvent args)
    {
        if (GameTiming.ApplyingState)
            return;

        EnsureComp<StunnedComponent>(args.Target);
    }

    private void OnStunStatusRemoved(Entity<StunnedStatusEffectComponent> entity, ref StatusEffectRemovedEvent args)
    {
        TryUnstun(args.Target);

        if (_explicitParalyzeClears.Contains(args.Target))
            TryCompleteExplicitParalyzeClear(args.Target, explicitClear: true);
    }

    private void OnStunEndAttempt(Entity<StunnedStatusEffectComponent> entity, ref StatusEffectRelayedEvent<StunEndAttemptEvent> args)
    {
        if (args.Args.Cancelled)
            return;

        var ev = args.Args;
        ev.Cancelled = true;
        args.Args = ev;
    }

    private void OnKnockdownStatusApplied(Entity<KnockdownStatusEffectComponent> entity, ref StatusEffectAppliedEvent args)
    {
        if (GameTiming.ApplyingState)
            return;

        var silentParalyze = _silentParalyzeApplications.Contains(args.Target) &&
                              MetaData(entity.Owner).EntityPrototype?.ID == ParalyzeId;
        if (entity.Comp.Silent || silentParalyze)
        {
            EnsureComp<KnockedDownComponent>(args.Target);
            return;
        }

        // If you make something that shouldn't crawl, crawl, that's your own fault.
        if (entity.Comp.Crawl)
            Crawl(args.Target, null, true, true, drop: entity.Comp.Drop);
        else
            Knockdown(args.Target, null, true, true, drop: entity.Comp.Drop);
    }

    private void OnKnockdownStatusRemoved(Entity<KnockdownStatusEffectComponent> entity, ref StatusEffectRemovedEvent args)
    {
        TryCompleteExplicitParalyzeClear(args.Target, _explicitParalyzeClears.Contains(args.Target));
    }

    private void OnStandUpAttempt(Entity<KnockdownStatusEffectComponent> entity, ref StatusEffectRelayedEvent<StandUpAttemptEvent> args)
    {
        if (args.Args.Cancelled)
            return;

        var ev = args.Args;
        ev.Cancelled = true;
        args.Args = ev;
    }

    #region Attempt Event Handling

    private void OnMoveAttempt(EntityUid uid, StunnedComponent stunned, UpdateCanMoveEvent args)
    {
        if (stunned.LifeStage > ComponentLifeStage.Running)
            return;

        args.Cancel();
    }

    private void OnAttempt(EntityUid uid, StunnedComponent stunned, CancellableEntityEventArgs args)
    {
        args.Cancel();
    }

    private void OnEquipAttempt(EntityUid uid, StunnedComponent stunned, IsEquippingAttemptEvent args)
    {
        // is this a self-equip, or are they being stripped?
        if (args.User == uid)
            args.Cancel();
    }

    private void OnUnequipAttempt(EntityUid uid, StunnedComponent stunned, IsUnequippingAttemptEvent args)
    {
        // is this a self-equip, or are they being stripped?
        if (args.User == uid)
            args.Cancel();
    }

    #endregion
}
