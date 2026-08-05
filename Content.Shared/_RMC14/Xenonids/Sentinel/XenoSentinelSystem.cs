using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Projectile;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Rejuvenate;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Sentinel;

public sealed partial class XenoSentinelSystem : EntitySystem
{
    private static readonly ProtoId<AlertPrototype> IntoxicatedAlert = "XenoIntoxicated";

    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private CMArmorSystem _armor = default!;
    [Dependency] private SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedRMCFlammableSystem _rmcFlammable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private XenoProjectileSystem _xenoProjectile = default!;
    [Dependency] private XenoSystem _xeno = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;

    private EntityQuery<ProjectileComponent> _projectileQuery;

    public override void Initialize()
    {
        _projectileQuery = GetEntityQuery<ProjectileComponent>();

        SubscribeLocalEvent<XenoToxicSpitComponent, XenoToxicSpitActionEvent>(OnToxicSpitAction);
        SubscribeLocalEvent<XenoToxicSpitProjectileComponent, ProjectileHitEvent>(OnToxicSpitHit);

        SubscribeLocalEvent<XenoToxicSlashComponent, XenoToxicSlashActionEvent>(OnToxicSlashAction);
        SubscribeLocalEvent<XenoActiveToxicSlashComponent, MeleeHitEvent>(OnToxicSlashHit);
        SubscribeLocalEvent<XenoActiveToxicSlashComponent, ComponentShutdown>(OnToxicSlashRemoved);
        SubscribeLocalEvent<XenoToxicSlashSpeedComponent, ComponentStartup>(OnToxicSlashSpeedStartup);
        SubscribeLocalEvent<XenoToxicSlashSpeedComponent, ComponentShutdown>(OnToxicSlashSpeedShutdown);
        SubscribeLocalEvent<XenoToxicSlashSpeedComponent, RefreshMovementSpeedModifiersEvent>(OnToxicSlashRefreshSpeed);

        SubscribeLocalEvent<XenoDrainStingComponent, XenoDrainStingActionEvent>(OnDrainStingAction);

        SubscribeLocalEvent<XenoIntoxicatedComponent, ComponentStartup>(OnIntoxicatedStartup);
        SubscribeLocalEvent<XenoIntoxicatedComponent, ComponentRemove>(OnIntoxicatedRemove);
        SubscribeLocalEvent<XenoIntoxicatedComponent, AfterAutoHandleStateEvent>(OnIntoxicatedAfterState);
        SubscribeLocalEvent<XenoIntoxicatedComponent, MobStateChangedEvent>(OnIntoxicatedMobStateChanged);
        SubscribeLocalEvent<XenoIntoxicatedComponent, RejuvenateEvent>(OnIntoxicatedRejuvenate);
        SubscribeLocalEvent<XenoIntoxicatedComponent, RefreshMovementSpeedModifiersEvent>(OnIntoxicatedRefreshSpeed);
        SubscribeLocalEvent<XenoIntoxicatedComponent, XenoIntoxicatedResistAlertEvent>(OnIntoxicatedResistAlert);
        SubscribeLocalEvent<XenoIntoxicatedResistingComponent, XenoIntoxicatedResistDoAfterEvent>(OnIntoxicatedResistDoAfter);
        SubscribeLocalEvent<XenoIntoxicatedResistingComponent, AttackAttemptEvent>(OnIntoxicatedResistingAttempt);
        SubscribeLocalEvent<XenoIntoxicatedResistingComponent, DropAttemptEvent>(OnIntoxicatedResistingAttempt);
        SubscribeLocalEvent<XenoIntoxicatedResistingComponent, PickupAttemptEvent>(OnIntoxicatedResistingAttempt);
        SubscribeLocalEvent<XenoIntoxicatedResistingComponent, ThrowAttemptEvent>(OnIntoxicatedResistingAttempt);
        SubscribeLocalEvent<XenoIntoxicatedResistingComponent, UseAttemptEvent>(OnIntoxicatedResistingAttempt);
        SubscribeLocalEvent<XenoIntoxicatedResistingComponent, InteractionAttemptEvent>(OnIntoxicatedResistingInteractionAttempt);

        SubscribeLocalEvent<XenoDrainSurgeComponent, ComponentStartup>(OnDrainSurgeStartup);
        SubscribeLocalEvent<XenoDrainSurgeComponent, ComponentRemove>(OnDrainSurgeRemove);
        SubscribeLocalEvent<XenoDrainSurgeComponent, CMGetArmorEvent>(OnDrainSurgeGetArmor);
    }

    private void OnToxicSpitAction(Entity<XenoToxicSpitComponent> xeno, ref XenoToxicSpitActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = _xenoProjectile.TryShoot(
            xeno,
            args.Target,
            xeno.Comp.PlasmaCost,
            xeno.Comp.ProjectileId,
            xeno.Comp.Sound,
            1,
            Angle.Zero,
            xeno.Comp.Speed,
            target: args.Entity
        );
    }

    private void OnToxicSpitHit(Entity<XenoToxicSpitProjectileComponent> spit, ref ProjectileHitEvent args)
    {
        if (_net.IsClient ||
            !_projectileQuery.TryComp(spit, out var projectile) ||
            projectile.Shooter is not { Valid: true } shooter)
        {
            return;
        }

        if (TryAddIntoxicated(shooter, args.Target, spit.Comp.Stacks))
        {
            _audio.PlayPvs(spit.Comp.HitSound, args.Target);
            _popup.PopupEntity(Loc.GetString("rmc-xeno-sentinel-toxic-spit-hit"), args.Target, shooter);
        }
    }

    private void OnToxicSlashAction(Entity<XenoToxicSlashComponent> xeno, ref XenoToxicSlashActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        var active = EnsureComp<XenoActiveToxicSlashComponent>(xeno);
        active.ExpiresAt = _timing.CurTime + xeno.Comp.ActiveDuration;
        active.HitsRemaining = xeno.Comp.MaxHits;
        active.StacksPerHit = xeno.Comp.StacksPerHit;
        Dirty(xeno, active);
        ApplyToxicSlashSpeed(xeno, xeno.Comp.SpeedModifier, xeno.Comp.ActiveDuration);

        _audio.PlayPredicted(xeno.Comp.ActivateSound, xeno, xeno);
        _popup.PopupClient(Loc.GetString("rmc-xeno-sentinel-toxic-slash-start"), xeno, xeno);
        foreach (var action in _rmcActions.GetActionsWithEvent<XenoToxicSlashActionEvent>(xeno))
        {
            _actions.SetToggled(action.AsNullable(), true);
        }
    }

    private void OnToxicSlashRemoved(Entity<XenoActiveToxicSlashComponent> xeno, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(xeno) &&
            TryComp(xeno, out XenoToxicSlashComponent? slash))
        {
            _audio.PlayPredicted(slash.ExpireSound, xeno, xeno);
        }

        foreach (var action in _rmcActions.GetActionsWithEvent<XenoToxicSlashActionEvent>(xeno))
        {
            _actions.SetToggled(action.AsNullable(), false);
        }
    }

    private void OnToxicSlashSpeedStartup(Entity<XenoToxicSlashSpeedComponent> xeno, ref ComponentStartup args)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(xeno);
    }

    private void OnToxicSlashSpeedShutdown(Entity<XenoToxicSlashSpeedComponent> xeno, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(xeno))
            _movementSpeed.RefreshMovementSpeedModifiers(xeno);
    }

    private void OnToxicSlashRefreshSpeed(Entity<XenoToxicSlashSpeedComponent> xeno, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(xeno.Comp.SpeedModifier, xeno.Comp.SpeedModifier);
    }

    private void OnToxicSlashHit(Entity<XenoActiveToxicSlashComponent> xeno, ref MeleeHitEvent args)
    {
        if (_net.IsClient ||
            !args.IsHit ||
            args.HitEntities.Count == 0)
        {
            return;
        }

        foreach (var target in args.HitEntities)
        {
            if (!TryAddIntoxicated(xeno, target, xeno.Comp.StacksPerHit))
                continue;

            if (TryComp(xeno, out XenoToxicSlashComponent? slash))
                _audio.PlayPvs(slash.HitSound, target);

            xeno.Comp.HitsRemaining--;
            Dirty(xeno);

            if (xeno.Comp.HitsRemaining <= 0)
                RemCompDeferred<XenoActiveToxicSlashComponent>(xeno);

            break;
        }
    }

    private void OnDrainStingAction(Entity<XenoDrainStingComponent> xeno, ref XenoDrainStingActionEvent args)
    {
        if (args.Handled)
            return;

        if (!CanIntoxicateTarget(xeno, args.Target) ||
            !TryComp(args.Target, out XenoIntoxicatedComponent? intoxicated) ||
            intoxicated.Stacks <= 0)
        {
            _popup.PopupClient(Loc.GetString("rmc-xeno-sentinel-drain-sting-not-intoxicated"), xeno, xeno, PopupType.SmallCaution);
            return;
        }

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;

        var stacks = intoxicated.Stacks;
        _audio.PlayPredicted(xeno.Comp.Sound, args.Target, xeno);

        if (stacks >= xeno.Comp.SurgeThreshold)
            PlayDrainSurgeSounds(xeno, args.Target);

        if (_net.IsClient)
            return;

        var potency = stacks * xeno.Comp.PotencyPerStack;
        var damageAmount = FixedPoint2.New(potency / xeno.Comp.DamageDivisor);
        var damage = new DamageSpecifier
        {
            DamageDict = { ["Heat"] = damageAmount },
        };
        _damage.TryChangeDamage(args.Target, damage, origin: xeno, tool: xeno);

        var knockdown = TimeSpan.FromSeconds(Math.Max(0.1f, (stacks - 10) / 10f));
        _stun.TryKnockdown(args.Target, knockdown, true);

        _xeno.HealDamage((xeno.Owner, null), FixedPoint2.New(potency));
        _xenoPlasma.RegenPlasma((xeno.Owner, null), FixedPoint2.New(potency * xeno.Comp.PlasmaMultiplier));

        var consumed = Math.Max(1, (int) MathF.Round(stacks * xeno.Comp.ConsumeFraction, MidpointRounding.AwayFromZero));
        intoxicated.Stacks = Math.Max(0, intoxicated.Stacks - consumed);
        Dirty(args.Target, intoxicated);
        UpdateIntoxicatedMovement(args.Target, intoxicated);

        if (intoxicated.Stacks <= 0)
            RemCompDeferred<XenoIntoxicatedComponent>(args.Target);

        _popup.PopupEntity(Loc.GetString("rmc-xeno-sentinel-drain-sting-drain"), args.Target, xeno);

        if (stacks >= xeno.Comp.SurgeThreshold)
        {
            ApplyDrainSurge(xeno, xeno.Comp.SurgeArmor, xeno.Comp.SurgeDuration);
        }
    }

    private void OnIntoxicatedStartup(Entity<XenoIntoxicatedComponent> ent, ref ComponentStartup args)
    {
        if (_net.IsServer)
            _alerts.ShowAlert(ent, IntoxicatedAlert);

        UpdateIntoxicatedMovement(ent, ent.Comp);
    }

    private void OnIntoxicatedRemove(Entity<XenoIntoxicatedComponent> ent, ref ComponentRemove args)
    {
        if (_net.IsServer)
            _alerts.ClearAlert(ent, IntoxicatedAlert);

        StopIntoxicatedResist(ent);
        RemCompDeferred<XenoSlowVisualsComponent>(ent);

        if (!TerminatingOrDeleted(ent))
            _movementSpeed.RefreshMovementSpeedModifiers(ent);
    }

    private void OnIntoxicatedAfterState(Entity<XenoIntoxicatedComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(ent);
    }

    private void OnIntoxicatedMobStateChanged(Entity<XenoIntoxicatedComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        RemCompDeferred<XenoIntoxicatedComponent>(ent);
    }

    private void OnIntoxicatedRejuvenate(Entity<XenoIntoxicatedComponent> ent, ref RejuvenateEvent args)
    {
        RemCompDeferred<XenoIntoxicatedComponent>(ent);
    }

    private void OnIntoxicatedRefreshSpeed(Entity<XenoIntoxicatedComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var modifier = GetIntoxicatedSpeedModifier(ent.Comp);
        if (modifier >= 1)
            return;

        args.ModifySpeed(modifier, modifier);
    }

    private void OnIntoxicatedResistAlert(Entity<XenoIntoxicatedComponent> ent, ref XenoIntoxicatedResistAlertEvent args)
    {
        if (_net.IsClient ||
            args.Handled ||
            HasComp<XenoIntoxicatedResistingComponent>(ent))
        {
            return;
        }

        if (!TryStartIntoxicatedResist(ent, ent.Comp))
            return;

        args.Handled = true;
    }

    private void OnIntoxicatedResistDoAfter(Entity<XenoIntoxicatedResistingComponent> ent, ref XenoIntoxicatedResistDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        StopIntoxicatedResist(ent);

        if (args.Cancelled ||
            !TryComp(ent, out XenoIntoxicatedComponent? intoxicated))
        {
            return;
        }

        intoxicated.Stacks = Math.Max(0, intoxicated.Stacks - intoxicated.ResistReduction);
        Dirty(ent.Owner, intoxicated);
        UpdateIntoxicatedMovement(ent, intoxicated);

        if (intoxicated.Stacks <= 0)
        {
            _popup.PopupEntity(Loc.GetString("rmc-xeno-sentinel-intoxicated-resist-finish"), ent, ent);
            RemCompDeferred<XenoIntoxicatedComponent>(ent);
            return;
        }

        _popup.PopupEntity(Loc.GetString("rmc-xeno-sentinel-intoxicated-resist-reduce"), ent, ent, PopupType.SmallCaution);
    }

    private bool TryStartIntoxicatedResist(EntityUid uid, XenoIntoxicatedComponent intoxicated)
    {
        if (_net.IsClient ||
            !_actionBlocker.CanInteract(uid, null))
        {
            return false;
        }

        var doAfter = new DoAfterArgs(EntityManager, uid, intoxicated.ResistDuration, new XenoIntoxicatedResistDoAfterEvent(), uid, uid)
        {
            BreakOnMove = true,
            BreakOnDamage = false,
            NeedHand = false,
            RequireCanInteract = false,
            MovementThreshold = 0.05f,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        var wasDown = _standing.IsDown(uid);

        _rmcFlammable.DoStopDropRollAnimation(uid);

        var resisting = EnsureComp<XenoIntoxicatedResistingComponent>(uid);
        resisting.Resisting = true;
        resisting.WasDown = wasDown;
        resisting.ForcedDown = !wasDown && _standing.Down(uid, dropHeldItems: false, force: true, changeCollision: true);
        return true;
    }

    private void StopIntoxicatedResist(EntityUid uid)
    {
        if (TryComp(uid, out XenoIntoxicatedResistingComponent? resisting) &&
            resisting.ForcedDown &&
            !resisting.WasDown &&
            !HasComp<KnockedDownComponent>(uid) &&
            !_mobState.IsDead(uid) &&
            !TerminatingOrDeleted(uid))
        {
            _standing.Stand(uid, force: true);
        }

        RemCompDeferred<XenoIntoxicatedResistingComponent>(uid);
    }

    private void OnIntoxicatedResistingAttempt(Entity<XenoIntoxicatedResistingComponent> ent, ref AttackAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnIntoxicatedResistingAttempt(Entity<XenoIntoxicatedResistingComponent> ent, ref DropAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnIntoxicatedResistingAttempt(Entity<XenoIntoxicatedResistingComponent> ent, ref PickupAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnIntoxicatedResistingAttempt(Entity<XenoIntoxicatedResistingComponent> ent, ref ThrowAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnIntoxicatedResistingAttempt(Entity<XenoIntoxicatedResistingComponent> ent, ref UseAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnIntoxicatedResistingInteractionAttempt(Entity<XenoIntoxicatedResistingComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnDrainSurgeStartup(Entity<XenoDrainSurgeComponent> ent, ref ComponentStartup args)
    {
        _armor.UpdateArmorValue((ent, null));
    }

    private void OnDrainSurgeRemove(Entity<XenoDrainSurgeComponent> ent, ref ComponentRemove args)
    {
        if (!TerminatingOrDeleted(ent))
            _armor.UpdateArmorValue((ent, null));
    }

    private void OnDrainSurgeGetArmor(Entity<XenoDrainSurgeComponent> ent, ref CMGetArmorEvent args)
    {
        args.XenoArmor += ent.Comp.Armor;
    }

    public bool TryAddIntoxicated(EntityUid source, EntityUid target, int stacks)
    {
        if (!CanIntoxicateTarget(source, target))
            return false;

        var intoxicated = EnsureComp<XenoIntoxicatedComponent>(target);
        intoxicated.Stacks = Math.Clamp(intoxicated.Stacks + stacks, 0, intoxicated.MaxStacks);
        intoxicated.LastSource = source;
        if (intoxicated.NextTick == TimeSpan.Zero)
            intoxicated.NextTick = _timing.CurTime + intoxicated.TickEvery;

        Dirty(target, intoxicated);
        UpdateIntoxicatedMovement(target, intoxicated);
        var filter = Filter.Pvs(target, entityManager: EntityManager);
        _colorFlash.RaiseEffect(Color.FromHex("#7DCC00"), new List<EntityUid> { target }, filter);
        _popup.PopupEntity(Loc.GetString("rmc-xeno-sentinel-intoxicated-gained"), target, target, PopupType.SmallCaution);
        return true;
    }

    private bool CanIntoxicateTarget(EntityUid source, EntityUid target)
    {
        if (_mobState.IsDead(target) ||
            HasComp<SynthComponent>(target) ||
            !_xeno.CanAbilityAttackTarget(source, target))
        {
            return false;
        }

        if (!HasComp<XenoComponent>(target))
            return true;

        return _hive.GetHive(source) is not { } hive ||
               !_hive.IsAllyOfHive(target, hive.Owner);
    }

    private void ApplyDrainSurge(EntityUid uid, int armor, TimeSpan duration)
    {
        var surge = EnsureComp<XenoDrainSurgeComponent>(uid);
        surge.Armor = armor;
        surge.ExpiresAt = _timing.CurTime + duration;
        Dirty(uid, surge);
        _armor.UpdateArmorValue((uid, null));
        _popup.PopupEntity(Loc.GetString("rmc-xeno-sentinel-drain-surge"), uid, uid);
    }

    private void ApplyToxicSlashSpeed(EntityUid uid, float speedModifier, TimeSpan duration)
    {
        var speed = EnsureComp<XenoToxicSlashSpeedComponent>(uid);
        speed.SpeedModifier = speedModifier;
        speed.ExpiresAt = _timing.CurTime + duration;
        Dirty(uid, speed);
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    private void UpdateIntoxicatedMovement(EntityUid uid, XenoIntoxicatedComponent intoxicated)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(uid);

        if (_net.IsClient)
            return;

        if (GetIntoxicatedSpeedModifier(intoxicated) < 1 && !HasComp<XenoComponent>(uid))
            EnsureComp<XenoSlowVisualsComponent>(uid);
        else
            RemCompDeferred<XenoSlowVisualsComponent>(uid);
    }

    private static float GetIntoxicatedSpeedModifier(XenoIntoxicatedComponent intoxicated)
    {
        if (intoxicated.Stacks < intoxicated.HighStackThreshold)
            return 1f;

        var stackRange = Math.Max(1, intoxicated.MaxStacks - intoxicated.HighStackThreshold);
        var progress = Math.Clamp((intoxicated.Stacks - intoxicated.HighStackThreshold) / (float) stackRange, 0f, 1f);
        var modifier = intoxicated.HighStackSlowAtThreshold +
                       (intoxicated.HighStackSlowAtMax - intoxicated.HighStackSlowAtThreshold) * progress;

        return Math.Clamp(modifier, 0f, 1f);
    }

    private void PlayDrainSurgeSounds(Entity<XenoDrainStingComponent> xeno, EntityUid target)
    {
        if (_net.IsClient && !_timing.IsFirstTimePredicted)
            return;

        _audio.PlayPredicted(xeno.Comp.SurgePlasmaTransferSound, target, xeno);

        var sound = xeno.Comp.SurgeHeadbiteSound;
        Timer.Spawn(xeno.Comp.SurgeHeadbiteSoundDelay, () =>
        {
            if (!TerminatingOrDeleted(target))
                _audio.PlayPredicted(sound, target, xeno);
        });
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;

        var slashes = EntityQueryEnumerator<XenoActiveToxicSlashComponent>();
        while (slashes.MoveNext(out var uid, out var slash))
        {
            if (slash.ExpiresAt > time)
                continue;

            RemCompDeferred<XenoActiveToxicSlashComponent>(uid);
            _popup.PopupEntity(Loc.GetString("rmc-xeno-sentinel-toxic-slash-expire"), uid, uid, PopupType.SmallCaution);
        }

        var intoxicatedQuery = EntityQueryEnumerator<XenoIntoxicatedComponent>();
        while (intoxicatedQuery.MoveNext(out var uid, out var intoxicated))
        {
            if (time < intoxicated.NextTick)
                continue;

            intoxicated.NextTick = time + intoxicated.TickEvery;
            var stackDivisor = Math.Max(1f, intoxicated.TickDamageStackDivisor);
            var tickDamage = intoxicated.TickBaseDamage + FixedPoint2.New((int) MathF.Round(intoxicated.Stacks / stackDivisor, MidpointRounding.AwayFromZero));
            var damage = new DamageSpecifier
            {
                DamageDict = { ["Heat"] = tickDamage },
            };
            var origin = intoxicated.LastSource is { Valid: true } source && !TerminatingOrDeleted(source)
                ? source
                : EntityUid.Invalid;
            if (origin.Valid)
                _damage.TryChangeDamage(uid, damage, origin: origin);
            else
                _damage.TryChangeDamage(uid, damage);

            intoxicated.Stacks = Math.Max(0, intoxicated.Stacks - intoxicated.TickDecay);
            Dirty(uid, intoxicated);
            UpdateIntoxicatedMovement(uid, intoxicated);

            if (intoxicated.Stacks <= 0)
                RemCompDeferred<XenoIntoxicatedComponent>(uid);
        }

        var surges = EntityQueryEnumerator<XenoDrainSurgeComponent>();
        while (surges.MoveNext(out var uid, out var surge))
        {
            if (surge.ExpiresAt > time)
                continue;

            RemCompDeferred<XenoDrainSurgeComponent>(uid);
        }

        var toxicSlashSpeeds = EntityQueryEnumerator<XenoToxicSlashSpeedComponent>();
        while (toxicSlashSpeeds.MoveNext(out var uid, out var speed))
        {
            if (speed.ExpiresAt > time)
                continue;

            RemCompDeferred<XenoToxicSlashSpeedComponent>(uid);
        }
    }
}
