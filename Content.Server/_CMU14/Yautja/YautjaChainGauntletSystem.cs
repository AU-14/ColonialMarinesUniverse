using System.Numerics;
using Content.Server.Beam;
using Content.Server.Stunnable;
using Content.Server.Chat.Systems;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Movement;
using Content.Shared._RMC14.Tackle;
using Content.Shared._RMC14.Xenonids.Construction.ResinWhisper;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Popups;
using Content.Shared._RMC14.Projectiles;
using Content.Shared._RMC14.Stun;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaChainGauntletSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedCombatModeSystem _combatMode = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private TemporarySpeedModifiersSystem _temporarySpeed = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private RMCSizeStunSystem _sizeStun = default!;
    [Dependency] private StunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private BeamSystem _beam = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private RMCProjectileSystem _rmcProjectile = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedDoorSystem _doors = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaChainGauntletComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<YautjaChainGauntletComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<YautjaChainGauntletComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<YautjaChainGauntletComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<YautjaChainGauntletComponent, YautjaChainGauntletExecuteDoAfterEvent>(OnExecutionDoAfter);
        SubscribeLocalEvent<YautjaChainGauntletComponent, YautjaGuardChainGauntletActionEvent>(OnGuardAction);
        SubscribeLocalEvent<DoorComponent, YautjaChainGauntletForceDoorDoAfterEvent>(OnForceDoorDoAfter);
        SubscribeLocalEvent<YautjaComponent, PullStartedMessage>(OnPullStarted);
        SubscribeLocalEvent<TransformComponent, CMDisarmEvent>(OnDisarmFinisher, before: [typeof(TackleSystem)]);
        SubscribeLocalEvent<TransformComponent, InteractUsingEvent>(OnHelpFinisher);
    }

    public override void Update(float frameTime)
    {
        ProcessChainPulls();

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<YautjaChainGauntletComponent>();

        while (query.MoveNext(out var uid, out var gauntlet))
        {
            if (gauntlet.Executing &&
                gauntlet.ExecutionUnlockAt != TimeSpan.Zero &&
                gauntlet.ExecutionUnlockAt <= time)
            {
                gauntlet.Executing = false;
                gauntlet.ExecutionUnlockAt = TimeSpan.Zero;
                Dirty(uid, gauntlet);
            }

            if (!gauntlet.GuardActive || gauntlet.GuardExpiresAt > time)
                continue;

            gauntlet.GuardActive = false;
            gauntlet.GuardExpiresAt = TimeSpan.Zero;
            gauntlet.PunchKnockback = gauntlet.GuardExpiredPunchKnockback;
            Dirty(uid, gauntlet);
        }
    }

    private void OnGetItemActions(Entity<YautjaChainGauntletComponent> ent, ref GetItemActionsEvent args)
    {
        if (!args.InHands || !HasComp<YautjaComponent>(args.User))
            return;

        args.AddAction(ref ent.Comp.GuardAction, ent.Comp.GuardActionId);
    }

    private void OnExamined(Entity<YautjaChainGauntletComponent> ent, ref ExaminedEvent args)
    {
        if (!HasComp<YautjaComponent>(args.Examiner))
            return;

        args.PushMarkup("Stack up your combo meter by using [color=red]HARM[/color] intent, you can then use these combo stacks on different intents to do different finishers.");
        args.PushMarkup("Finish your combo on [color=green]HELP[/color] intent to slam the target to the ground, incapacitating them for a few seconds, if the target is a humanoid you do extra damage as well.");
        args.PushMarkup("Finish your combo on [color=blue]SHOVE[/color] intent to throw the target away from you, if you have some chains wrapped around the gauntlet, you'll pull them back towards you. If you are using the special ability, the throw range will be further.");
        args.PushMarkup("Finish your combo on [color=orange]GRAB[/color] intent to do an execution that instantly kills your target, they must already be unconscious or in critical state.");
    }

    private void OnInteractUsing(Entity<YautjaChainGauntletComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<YautjaChainWrapperComponent>(args.Used))
            return;

        args.Handled = true;

        if (!HasComp<YautjaComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-tech-denied"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        if (ent.Comp.HasChain)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-chain-gauntlet-chain-existing"), ent.Owner, args.User, PopupType.SmallCaution);
            return;
        }

        ent.Comp.HasChain = true;
        Dirty(ent);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-chain-gauntlet-chain-wrapped", ("chain", args.Used), ("item", ent.Owner)), ent.Owner, args.User);
        QueueDel(args.Used);
    }

    private void OnMeleeHit(Entity<YautjaChainGauntletComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit ||
            args.HitEntities.Count == 0 ||
            !HasComp<YautjaComponent>(args.User))
        {
            return;
        }

        var hasValidTarget = false;
        foreach (var hit in args.HitEntities)
        {
            if (hit == args.User)
                continue;

            hasValidTarget = true;
            break;
        }

        if (!hasValidTarget)
            return;

        if (ent.Comp.ComboExpiresAt <= _timing.CurTime)
            ent.Comp.ComboCounter = 0;

        ent.Comp.ComboExpiresAt = _timing.CurTime + ent.Comp.ComboDuration;
        ent.Comp.ComboCounter++;
        Dirty(ent);
    }

    private void OnDisarmFinisher(Entity<TransformComponent> target, ref CMDisarmEvent args)
    {
        if (args.Handled || target.Owner == args.User || !HasComp<YautjaComponent>(args.User))
            return;

        var gauntletUid = _hands.GetActiveItem(args.User);
        if (gauntletUid == null ||
            !TryComp<YautjaChainGauntletComponent>(gauntletUid, out var gauntlet))
        {
            return;
        }

        if (gauntlet.ComboExpiresAt != TimeSpan.Zero && gauntlet.ComboExpiresAt <= _timing.CurTime)
        {
            ResetCombo(gauntletUid.Value, gauntlet);
            return;
        }

        if (gauntlet.ComboCounter < gauntlet.DisarmFinisherComboRequired)
            return;

        var origin = _transform.GetMapCoordinates(args.User);
        var destination = _transform.GetMapCoordinates(target.Owner);
        if (origin.MapId != destination.MapId)
            return;

        var direction = destination.Position - origin.Position;
        if (direction == Vector2.Zero)
            direction = Vector2.UnitX;

        direction = Vector2.Normalize(direction) * gauntlet.PunchKnockback;
        _throwing.TryThrow(
            target.Owner,
            direction,
            gauntlet.DisarmFinisherThrowSpeed,
            args.User,
            compensateFriction: true,
            doSpin: false);

        if (gauntlet.HasChain)
        {
            TryCreateChainHookProjectile(args.User, target.Owner, gauntletUid.Value, gauntlet);
            TryCreateChainHookVisual(args.User, target.Owner, gauntlet);
            ScheduleChainPull(target.Owner, args.User, gauntlet);
            TrySendChainMessage(args.User, gauntlet);
        }

        ResetCombo(gauntletUid.Value, gauntlet);
        args.Handled = true;
    }

    private void OnHelpFinisher(Entity<TransformComponent> target, ref InteractUsingEvent args)
    {
        OnForceDoorWithChainGauntlet(target, ref args);

        if (args.Handled ||
            target.Owner != args.Target ||
            target.Owner == args.User ||
            !HasComp<YautjaComponent>(args.User) ||
            _hands.GetActiveItem(args.User) != args.Used ||
            !TryComp<YautjaChainGauntletComponent>(args.Used, out var gauntlet))
        {
            return;
        }

        if (gauntlet.ComboExpiresAt != TimeSpan.Zero && gauntlet.ComboExpiresAt <= _timing.CurTime)
        {
            ResetCombo(args.Used, gauntlet);
            return;
        }

        if (gauntlet.ComboCounter < gauntlet.HelpFinisherComboRequired)
            return;

        _stun.TryKnockdown(target.Owner, gauntlet.HelpFinisherKnockdown, true);

        if (HasComp<HumanoidAppearanceComponent>(target.Owner))
        {
            _damage.TryChangeDamage(
                target.Owner,
                new DamageSpecifier(gauntlet.HelpFinisherDamage),
                origin: args.User,
                tool: args.Used,
                armorPiercing: gauntlet.HelpFinisherArmorPiercing);
        }

        _popup.PopupEntity(
            Loc.GetString(gauntlet.HelpFinisherMessage, ("user", args.User), ("target", target.Owner)),
            target.Owner,
            PopupType.LargeCaution);
        TrySpawnSlamOverlay(target.Owner, gauntlet);
        _audio.PlayPvs(gauntlet.HelpFinisherSound, target.Owner);
        ResetCombo(args.Used, gauntlet);
        args.Handled = true;
    }

    private void OnForceDoorWithChainGauntlet(Entity<TransformComponent> target, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            target.Owner != args.Target ||
            !TryComp(target.Owner, out DoorComponent? door) ||
            !CanUseChainGauntletOnDoor(args.User, args.Used, out var gauntlet))
        {
            return;
        }

        var close = false;
        var damageAirlock = false;
        TimeSpan delay;

        if (HasComp<ResinDoorComponent>(target))
        {
            if (_combatMode.IsInCombatMode(args.User))
                return;

            switch (door.State)
            {
                case DoorState.Closed:
                    delay = gauntlet.ForceResinOpenDoAfter;
                    break;
                case DoorState.Open:
                    close = true;
                    delay = gauntlet.ForceResinCloseDoAfter;
                    break;
                default:
                    return;
            }
        }
        else
        {
            if (!HasComp<AirlockComponent>(target) ||
                door.State != DoorState.Closed ||
                _doors.IsBolted(target))
            {
                return;
            }

            delay = gauntlet.ForceAirlockDoAfter;
            damageAirlock = true;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            delay,
            new YautjaChainGauntletForceDoorDoAfterEvent(close, damageAirlock),
            target.Owner,
            target.Owner,
            args.Used)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 2f,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        args.Handled = true;
    }

    private void OnForceDoorDoAfter(Entity<DoorComponent> door, ref YautjaChainGauntletForceDoorDoAfterEvent args)
    {
        if (args.Cancelled ||
            args.Target != door.Owner ||
            args.Used is not { } used ||
            !CanUseChainGauntletOnDoor(args.User, used, out var gauntlet))
        {
            return;
        }

        if (args.Close)
        {
            if (door.Comp.State == DoorState.Open)
            {
                _doors.StartClosing(door.Owner, door.Comp, args.User);
                args.Handled = true;
            }

            return;
        }

        if (door.Comp.State == DoorState.Closed)
        {
            _doors.StartOpening(door.Owner, door.Comp, args.User);

            if (args.DamageAirlock)
            {
                // CMSS13 applies door.ex_act(100) after forcing the airlock open; this is not a melee hit,
                // so do not pass the gauntlet as a damage tool or Yautja tech multipliers inflate it.
                _damage.TryChangeDamage(
                    door.Owner,
                    new DamageSpecifier(gauntlet.ForceAirlockDamage),
                    origin: args.User);
                _audio.PlayPvs(gauntlet.ForceAirlockCrashSound, door.Owner);
            }

            args.Handled = true;
        }
    }

    private bool CanUseChainGauntletOnDoor(EntityUid user, EntityUid used, out YautjaChainGauntletComponent gauntlet)
    {
        gauntlet = default!;

        if (!HasComp<YautjaComponent>(user) ||
            _hands.GetActiveItem(user) != used ||
            !TryComp<YautjaChainGauntletComponent>(used, out var found) ||
            !_actionBlocker.CanConsciouslyPerformAction(user) ||
            !_actionBlocker.CanUseHeldEntity(user, used))
        {
            return false;
        }

        gauntlet = found;
        return true;
    }

    private void OnPullStarted(Entity<YautjaComponent> ent, ref PullStartedMessage args)
    {
        if (ent.Owner != args.PullerUid || ent.Owner == args.PulledUid)
            return;

        var gauntletUid = _hands.GetActiveItem(ent.Owner);
        if (gauntletUid == null ||
            !TryComp<YautjaChainGauntletComponent>(gauntletUid, out var gauntlet) ||
            gauntlet.Executing ||
            !CanExecute(args.PulledUid))
        {
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager,
            ent.Owner,
            gauntlet.ExecutionDoAfter,
            new YautjaChainGauntletExecuteDoAfterEvent(),
            gauntletUid,
            args.PulledUid,
            gauntletUid)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 2f,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        gauntlet.Executing = true;
        gauntlet.ExecutionUnlockAt = TimeSpan.Zero;
        Dirty(gauntletUid.Value, gauntlet);
    }

    private void OnExecutionDoAfter(Entity<YautjaChainGauntletComponent> ent, ref YautjaChainGauntletExecuteDoAfterEvent args)
    {
        if (args.Cancelled ||
            args.Target is not { } target ||
            Deleted(args.User) ||
            Deleted(target) ||
            _hands.GetActiveItem(args.User) != ent.Owner ||
            !CanExecute(target))
        {
            ent.Comp.Executing = false;
            ent.Comp.ExecutionUnlockAt = TimeSpan.Zero;
            Dirty(ent);
            return;
        }

        _damage.TryChangeDamage(
            target,
            new DamageSpecifier(ent.Comp.ExecutionDamage),
            origin: args.User,
            tool: ent.Owner,
            armorPiercing: ent.Comp.ExecutionArmorPiercing);
        _mobState.ChangeMobState(target, MobState.Dead);
        _popup.PopupEntity(
            Loc.GetString(ent.Comp.ExecutionMessage, ("user", args.User), ("target", target)),
            target,
            PopupType.LargeCaution);
        PlayExecutionAnimation(target, ent.Comp);
        _audio.PlayPvs(ent.Comp.ExecutionTargetSound, target);
        _audio.PlayPvs(ent.Comp.ExecutionUserSound, args.User);
        ScheduleExecutionSlam(target, ent.Comp);
        ent.Comp.Executing = true;
        ent.Comp.ExecutionUnlockAt = _timing.CurTime + ent.Comp.ExecutionRecovery;
        Dirty(ent);
        args.Handled = true;
    }

    private void OnGuardAction(Entity<YautjaChainGauntletComponent> ent, ref YautjaGuardChainGauntletActionEvent args)
    {
        if (args.Handled || !_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        var user = args.Performer;

        if (!HasComp<YautjaComponent>(user) || _hands.GetActiveItem(user) != ent.Owner)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-chain-gauntlet-active-hand", ("item", ent.Owner)), ent.Owner, user, PopupType.SmallCaution);
            return;
        }

        if (ent.Comp.GuardActive && ent.Comp.GuardExpiresAt > _timing.CurTime)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-chain-gauntlet-already"), ent.Owner, user, PopupType.SmallCaution);
            return;
        }

        ent.Comp.GuardActive = true;
        ent.Comp.GuardExpiresAt = _timing.CurTime + ent.Comp.GuardDuration;
        ent.Comp.PunchKnockback = ent.Comp.GuardPunchKnockback;
        Dirty(ent);

        _temporarySpeed.ModifySpeed(user, new List<TemporarySpeedModifierSet>
        {
            new(ent.Comp.GuardDuration, ent.Comp.GuardSpeedMultiplier, ent.Comp.GuardSpeedMultiplier),
        });

        _popup.PopupEntity(Loc.GetString("cmu-yautja-chain-gauntlet-start", ("item", ent.Owner)), ent.Owner, user);
    }

    private void ResetCombo(EntityUid uid, YautjaChainGauntletComponent gauntlet)
    {
        gauntlet.ComboCounter = 0;
        gauntlet.ComboExpiresAt = TimeSpan.Zero;
        Dirty(uid, gauntlet);
    }

    private bool CanExecute(EntityUid target)
    {
        return !_mobState.IsDead(target) &&
               (_mobState.IsCritical(target) || _sizeStun.IsKnockedOut(target));
    }

    private void TrySpawnSlamOverlay(EntityUid target, YautjaChainGauntletComponent gauntlet)
    {
        if (string.IsNullOrWhiteSpace(gauntlet.SlamOverlayPrototype))
            return;

        Spawn(gauntlet.SlamOverlayPrototype, Transform(target).Coordinates);
    }

    private void PlayExecutionAnimation(EntityUid target, YautjaChainGauntletComponent gauntlet)
    {
        var ev = new YautjaChainGauntletExecutionAnimationEvent(
            GetNetEntity(target),
            gauntlet.ExecutionLiftHeight,
            gauntlet.ExecutionLiftDuration,
            gauntlet.ExecutionDropDuration);

        RaiseNetworkEvent(ev, Filter.Pvs(target));
    }

    private void ScheduleExecutionSlam(EntityUid target, YautjaChainGauntletComponent gauntlet)
    {
        Timer.Spawn(gauntlet.ExecutionLiftDuration, () =>
        {
            if (Deleted(target))
                return;

            _audio.PlayPvs(gauntlet.ExecutionSlamSound, target);
            TrySpawnSlamOverlay(target, gauntlet);
        });
    }

    private void ScheduleChainPull(EntityUid target, EntityUid puller, YautjaChainGauntletComponent gauntlet)
    {
        var pull = EnsureComp<YautjaChainGauntletPullComponent>(target);
        pull.Puller = puller;
        pull.PullAt = _timing.CurTime + gauntlet.ChainPullDelay;
        pull.Distance = gauntlet.ChainPullDistance;
        pull.Speed = gauntlet.ChainPullSpeed;
    }

    private void TryCreateChainHookVisual(EntityUid user, EntityUid target, YautjaChainGauntletComponent gauntlet)
    {
        if (string.IsNullOrWhiteSpace(gauntlet.ChainHookBeamPrototype))
            return;

        _beam.TryCreateBeam(user, target, gauntlet.ChainHookBeamPrototype, gauntlet.ChainHookBeamState);
    }

    private void TryCreateChainHookProjectile(EntityUid user, EntityUid target, EntityUid gauntletUid, YautjaChainGauntletComponent gauntlet)
    {
        if (string.IsNullOrWhiteSpace(gauntlet.ChainHookProjectilePrototype) ||
            gauntlet.ChainHookProjectileSpeed <= 0f ||
            gauntlet.ChainHookProjectileMaxRange <= 0f)
        {
            return;
        }

        var origin = _transform.GetMapCoordinates(user);
        var destination = _transform.GetMapCoordinates(target);
        if (origin.MapId != destination.MapId)
            return;

        var direction = destination.Position - origin.Position;
        if (direction == Vector2.Zero)
            direction = Vector2.UnitX;

        var projectile = Spawn(gauntlet.ChainHookProjectilePrototype, Transform(user).Coordinates);
        _gun.ShootProjectile(projectile, direction, Vector2.Zero, gauntletUid, user, gauntlet.ChainHookProjectileSpeed);

        var maxRange = EnsureComp<ProjectileMaxRangeComponent>(projectile);
        _rmcProjectile.SetMaxRange(projectile, gauntlet.ChainHookProjectileMaxRange);
    }

    private void TrySendChainMessage(EntityUid user, YautjaChainGauntletComponent gauntlet)
    {
        if (string.IsNullOrWhiteSpace(gauntlet.ChainMessage) ||
            gauntlet.ChainMessageChance <= 0f ||
            !_random.Prob(gauntlet.ChainMessageChance))
        {
            return;
        }

        TryComp<RMCSpeechBubbleSpecificStyleComponent>(user, out var style);
        var hadStyle = style != null;
        var oldStyleClass = style?.SpeechStyleClass;

        if (!string.IsNullOrWhiteSpace(gauntlet.ChainMessageSpeechStyleClass))
        {
            style ??= EnsureComp<RMCSpeechBubbleSpecificStyleComponent>(user);
            style.SpeechStyleClass = gauntlet.ChainMessageSpeechStyleClass;
            Dirty(user, style);
        }

        try
        {
            _chat.TrySendInGameICMessage(
                user,
                gauntlet.ChainMessage,
                InGameICChatType.Speak,
                ChatTransmitRange.Normal,
                hideLog: true,
                checkRadioPrefix: false,
                ignoreActionBlocker: true);
        }
        finally
        {
            if (style != null && hadStyle)
            {
                style.SpeechStyleClass = oldStyleClass ?? style.SpeechStyleClass;
                Dirty(user, style);
            }
            else if (style != null)
            {
                RemComp<RMCSpeechBubbleSpecificStyleComponent>(user);
            }
        }
    }

    private void ProcessChainPulls()
    {
        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<YautjaChainGauntletPullComponent>();

        while (query.MoveNext(out var uid, out var pull))
        {
            if (pull.PullAt > time)
                continue;

            if (Deleted(pull.Puller))
            {
                RemCompDeferred<YautjaChainGauntletPullComponent>(uid);
                continue;
            }

            var origin = _transform.GetMapCoordinates(uid);
            var destination = _transform.GetMapCoordinates(pull.Puller);
            if (origin.MapId != destination.MapId)
            {
                RemCompDeferred<YautjaChainGauntletPullComponent>(uid);
                continue;
            }

            var direction = destination.Position - origin.Position;
            if (direction == Vector2.Zero)
                direction = Vector2.UnitX;

            direction = Vector2.Normalize(direction) * pull.Distance;
            _throwing.TryThrow(
                uid,
                direction,
                pull.Speed,
                pull.Puller,
                compensateFriction: true,
                doSpin: false);

            RemCompDeferred<YautjaChainGauntletPullComponent>(uid);
        }
    }
}
