using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.JumpPack;

public sealed partial class CMUJumpPackSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedGasTankSystem _gasTank = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    private static readonly TimeSpan FuelSyncInterval = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUJumpPackComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUJumpPackComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUJumpPackComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<CMUJumpPackComponent, GotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<CMUJumpPackComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUJumpPackComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<CMUJumpPackComponent, CMUZHoverEndedEvent>(OnHoverEnded);
        SubscribeLocalEvent<CMUJumpPackComponent, InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<InventoryComponent, CMUZBeforeMovementEvent>(OnBeforeZMovement);

        SubscribeLocalEvent<CMUJumpPackComponent, CMUJumpPackBoostActionEvent>(OnBoostAction);
        SubscribeLocalEvent<CMUJumpPackComponent, CMUJumpPackDirectionActionEvent>(OnDirectionAction);
        SubscribeLocalEvent<CMUJumpPackComponent, CMUJumpPackJumpActionEvent>(OnJumpAction);
        SubscribeLocalEvent<CMUJumpPackComponent, CMUJumpPackHoverActionEvent>(OnHoverAction);
    }

    private void OnMapInit(Entity<CMUJumpPackComponent> ent, ref MapInitEvent args)
    {
        var comp = ent.Comp;
        _actionContainer.EnsureAction(ent, ref comp.BoostActionEntity, comp.BoostAction);
        _actionContainer.EnsureAction(ent, ref comp.DirectionActionEntity, comp.DirectionAction);
        _actionContainer.EnsureAction(ent, ref comp.JumpActionEntity, comp.JumpAction);
        _actionContainer.EnsureAction(ent, ref comp.HoverActionEntity, comp.HoverAction);
        Dirty(ent);
    }

    private void OnShutdown(Entity<CMUJumpPackComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.HoverUser is { } user && !TerminatingOrDeleted(user))
            _zLevels.CancelZHover(user, ent);

        if (ent.Comp.DescendingUser is { } descending && !TerminatingOrDeleted(descending))
            _zLevels.CancelZHover(descending, ent);
    }

    private void OnGetItemActions(Entity<CMUJumpPackComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.SlotFlags is not { } flags || (flags & SlotFlags.BACK) == 0)
            return;

        var comp = ent.Comp;
        args.AddAction(ref comp.BoostActionEntity, comp.BoostAction);
        args.AddAction(ref comp.DirectionActionEntity, comp.DirectionAction);
        args.AddAction(ref comp.JumpActionEntity, comp.JumpAction);
        args.AddAction(ref comp.HoverActionEntity, comp.HoverAction);
    }

    private void OnGotUnequipped(Entity<CMUJumpPackComponent> ent, ref GotUnequippedEvent args)
    {
        if (ent.Comp.HoverUser == args.EquipTarget)
            EndHover(ent, args.EquipTarget);

        if (ent.Comp.DescendingUser == args.EquipTarget)
            _zLevels.CancelZHover(args.EquipTarget, ent);
    }

    private void OnRefreshMovementSpeed(Entity<CMUJumpPackComponent> ent, ref InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        var wearer = args.Owner;
        if (ent.Comp.HoverUser != wearer ||
            !TryComp<MovementSpeedModifierComponent>(wearer, out var speed) ||
            speed.BaseSprintSpeed <= speed.BaseWalkSpeed)
        {
            return;
        }

        // Hovering caps the wearer at their walking pace, even while sprinting.
        args.Args.ModifySpeed(1f, speed.BaseWalkSpeed / speed.BaseSprintSpeed);
    }

    private void OnBeforeZMovement(Entity<InventoryComponent> ent, ref CMUZBeforeMovementEvent args)
    {
        if (_net.IsClient ||
            !_inventory.TryGetSlotEntity(ent, "back", out var back) ||
            !TryComp<CMUJumpPackComponent>(back, out var pack) ||
            !TryComp<CMUZPhysicsComponent>(ent, out var zPhysics))
        {
            return;
        }

        if (pack.HoverUser != null)
            return;

        var descending = pack.DescendingUser == ent.Owner;
        if (!descending &&
            (zPhysics.Velocity >= 0f ||
             _zLevels.IsZHovering(ent) ||
             _zLevels.DistanceToGround((ent.Owner, zPhysics), out _) <= 0f ||
             _mobState.IsDead(ent)))
        {
            return;
        }

        var drain = pack.LandingFuelPerSecond * args.FrameTime;
        var tank = CompOrNull<GasTankComponent>(back.Value);
        var outOfFuel = pack.RequiresFuel && (tank == null || tank.Air.TotalMoles < drain);
        if (outOfFuel || _mobState.IsDead(ent))
        {
            if (descending)
            {
                _zLevels.CancelZHover(ent, back.Value);
                if (!_mobState.IsDead(ent))
                    _popup.PopupEntity(Loc.GetString("cmu-jump-pack-no-fuel"), ent, ent, PopupType.MediumCaution);
            }
            return;
        }

        if (pack.RequiresFuel && tank != null)
        {
            tank.Air.Remove(drain);
            if (_timing.CurTime >= pack.NextFuelSync)
            {
                pack.NextFuelSync = _timing.CurTime + FuelSyncInterval;
                Dirty(back.Value, tank);
                _gasTank.UpdateUserInterface((back.Value, tank));
            }
        }

        if (descending)
            return;

        pack.DescendingUser = ent.Owner;
        Dirty(back.Value, pack);
        _zLevels.StartZDescent(ent, back.Value, pack.HoverDescendSpeed);
    }

    private void OnExamined(Entity<CMUJumpPackComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("cmu-jump-pack-examine",
            ("direction", DirectionName(ent.Comp.BoostDirection)),
            ("mode", ModeName(ent.Comp.EightDirections))));
    }

    private void OnGetAltVerbs(Entity<CMUJumpPackComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-jump-pack-verb-direction"),
            Priority = 10,
            Act = () => RotateDirection(ent, user),
        });
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-jump-pack-verb-toggle-mode",
                ("mode", ModeName(!ent.Comp.EightDirections))),
            Act = () => SetEightDirections(ent, !ent.Comp.EightDirections, user),
        });
    }

    private void OnHoverEnded(Entity<CMUJumpPackComponent> ent, ref CMUZHoverEndedEvent args)
    {
        var comp = ent.Comp;
        if (comp.HoverUser == args.Hoverer)
        {
            // The hover was cut short by something else (teleport, falling, admin), not by the pack.
            comp.HoverUser = null;
            ApplyHoverCooldown(ent);
            _movementSpeed.RefreshMovementSpeedModifiers(args.Hoverer);
        }

        if (comp.DescendingUser == args.Hoverer)
        {
            comp.DescendingUser = null;
            if (TryComp<GasTankComponent>(ent, out var tank))
            {
                Dirty(ent, tank);
                _gasTank.UpdateUserInterface((ent, tank));
            }
        }

        Dirty(ent);
    }

    private void OnBoostAction(Entity<CMUJumpPackComponent> ent, ref CMUJumpPackBoostActionEvent args)
    {
        if (args.Handled)
            return;

        var user = args.Performer;
        var comp = ent.Comp;
        if (!_actionBlocker.CanMove(user) || !TryComp(user, out PhysicsComponent? physics))
            return;

        if (!TryUseFuel(ent, user, comp.FuelPerBoost))
            return;

        args.Handled = true;

        _physics.ResetDynamics(user, physics);
        _throwing.TryThrow(user,
            comp.BoostDirection.ToVec() * comp.BoostDistance,
            comp.BoostSpeed,
            compensateFriction: false,
            recoil: false,
            animated: false,
            playSound: false,
            doSpin: false);

        _audio.PlayPredicted(comp.BoostSound, user, user);

        if (comp.HoverUser == null)
            StartBoostCooldown(ent);
        else
            _actions.SetUseDelay(comp.BoostActionEntity, null);
    }

    private void OnDirectionAction(Entity<CMUJumpPackComponent> ent, ref CMUJumpPackDirectionActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        RotateDirection(ent, args.Performer);
    }

    private void RotateDirection(Entity<CMUJumpPackComponent> ent, EntityUid user)
    {
        var comp = ent.Comp;
        comp.BoostDirection = NextDirection(comp.BoostDirection, comp.EightDirections);
        Dirty(ent);

        _popup.PopupClient(Loc.GetString("cmu-jump-pack-direction-set",
                ("direction", DirectionName(comp.BoostDirection))),
            user,
            user);
    }

    private void OnJumpAction(Entity<CMUJumpPackComponent> ent, ref CMUJumpPackJumpActionEvent args)
    {
        if (args.Handled)
            return;

        var user = args.Performer;
        var comp = ent.Comp;
        if (comp.HoverUser != null)
        {
            _popup.PopupClient(Loc.GetString("cmu-jump-pack-jump-hovering"), user, user, PopupType.SmallCaution);
            return;
        }

        if (!_actionBlocker.CanMove(user) ||
            !TryComp(user, out PhysicsComponent? physics) ||
            !TryComp(user, out CMUZPhysicsComponent? zPhysics))
        {
            return;
        }

        if (!HasOpenSkyAbove(user))
        {
            _popup.PopupClient(Loc.GetString("cmu-jump-pack-no-room"), user, user, PopupType.SmallCaution);
            return;
        }

        if (!TryUseFuel(ent, user, comp.FuelPerJump))
            return;

        if (comp.DescendingUser == user)
            _zLevels.CancelZHover(user, ent);

        args.Handled = true;

        _physics.ResetDynamics(user, physics);
        if (comp.JumpDashDistance > 0f)
        {
            _throwing.TryThrow(user,
                comp.BoostDirection.ToVec() * comp.JumpDashDistance,
                comp.JumpDashSpeed,
                compensateFriction: false,
                recoil: false,
                animated: false,
                playSound: false,
                doSpin: false);
        }

        _physics.SetBodyStatus(user, physics, BodyStatus.InAir);
        if (zPhysics.LocalPosition < comp.JumpTakeoffLocalPosition)
            _zLevels.SetZLocalPosition((user, zPhysics), comp.JumpTakeoffLocalPosition);

        // Launch fast enough that gravity stops the rise at JumpHeight.
        var rise = MathF.Max(0f, comp.JumpHeight - zPhysics.LocalPosition);
        _zLevels.SetZVelocity((user, zPhysics), MathF.Sqrt(2f * CMUSharedZLevelsSystem.ZGravityForce * rise));

        _audio.PlayPredicted(comp.BoostSound, user, user);
        StartBoostCooldown(ent);
    }

    private void OnHoverAction(Entity<CMUJumpPackComponent> ent, ref CMUJumpPackHoverActionEvent args)
    {
        if (args.Handled)
            return;

        var user = args.Performer;
        var comp = ent.Comp;
        if (comp.HoverUser != null)
        {
            args.Handled = true;
            EndHover(ent, comp.HoverUser.Value);
            return;
        }

        if (comp.DescendingUser != null)
            return;

        if (!_actionBlocker.CanMove(user))
            return;

        if (!_zLevels.CanStartZHover(user, out var targetMap))
        {
            _popup.PopupClient(Loc.GetString("cmu-jump-pack-no-room"), user, user, PopupType.SmallCaution);
            return;
        }

        if (!HasFuel(ent, comp.HoverFuelPerSecond))
        {
            _popup.PopupClient(Loc.GetString("cmu-jump-pack-no-fuel"), user, user, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        var now = _timing.CurTime;
        comp.HoverUser = user;
        comp.HoverStart = now;
        comp.HoverEnd = now + comp.HoverDuration;
        comp.NextFuelSync = now + FuelSyncInterval;
        Dirty(ent);

        _actions.SetToggled(comp.HoverActionEntity, true);
        _actions.SetUseDelay(comp.HoverActionEntity, null);
        _actions.ClearCooldown(comp.BoostActionEntity);
        _movementSpeed.RefreshMovementSpeedModifiers(user);

        _zLevels.StartZHover(user, targetMap.Value, ent, comp.HoverAscendSpeed, comp.HoverDescendSpeed);

        _audio.PlayPredicted(comp.HoverSound, user, user);
        _popup.PopupClient(Loc.GetString("cmu-jump-pack-hover-start"), user, user);
    }

    public override void Update(float frameTime)
    {
        // Z-physics and gas are server authoritative.
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUJumpPackComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.HoverUser is not { } user)
                continue;

            var ent = (uid, comp);
            if (TerminatingOrDeleted(user))
            {
                comp.HoverUser = null;
                ApplyHoverCooldown(ent);
                Dirty(uid, comp);
                continue;
            }

            var outOfFuel = false;
            if (comp.RequiresFuel)
            {
                var drain = comp.HoverFuelPerSecond * frameTime;
                var tank = CompOrNull<GasTankComponent>(uid);
                var removed = tank?.Air.Remove(drain).TotalMoles ?? 0f;
                outOfFuel = removed < drain * 0.99f;

                if (tank != null && (outOfFuel || now >= comp.NextFuelSync))
                {
                    comp.NextFuelSync = now + FuelSyncInterval;
                    Dirty(uid, tank);
                    _gasTank.UpdateUserInterface((uid, tank));
                }
            }

            if (outOfFuel)
            {
                _popup.PopupEntity(Loc.GetString("cmu-jump-pack-hover-out-of-fuel"), user, user, PopupType.MediumCaution);
                EndHover(ent, user);
            }
            else if (now >= comp.HoverEnd)
            {
                _popup.PopupEntity(Loc.GetString("cmu-jump-pack-hover-expired"), user, user, PopupType.SmallCaution);
                EndHover(ent, user);
            }
            else if (!_mobState.IsAlive(user))
            {
                EndHover(ent, user);
            }
        }
    }

    public void SetEightDirections(Entity<CMUJumpPackComponent> ent, bool eightDirections, EntityUid? user = null)
    {
        var comp = ent.Comp;
        comp.EightDirections = eightDirections;
        if (!eightDirections && IsDiagonal(comp.BoostDirection))
            comp.BoostDirection = (Direction) (((int) comp.BoostDirection + 7) % 8);

        Dirty(ent);

        if (user != null)
        {
            _popup.PopupClient(Loc.GetString("cmu-jump-pack-mode-set",
                    ("mode", ModeName(eightDirections)),
                    ("direction", DirectionName(comp.BoostDirection))),
                user.Value,
                user.Value);
        }
    }

    /// <summary>
    /// Ends an active hover and lets the wearer glide back to the floor. The hover cooldown runs from when it started.
    /// </summary>
    public void EndHover(Entity<CMUJumpPackComponent> ent, EntityUid user)
    {
        var comp = ent.Comp;
        if (comp.HoverUser != user)
            return;

        comp.HoverUser = null;
        comp.DescendingUser = user;
        ApplyHoverCooldown(ent);
        Dirty(ent);
        _movementSpeed.RefreshMovementSpeedModifiers(user);

        _zLevels.StopZHover(user);
    }

    /// <summary>
    /// Clockwise step through the cardinal directions, or all eight when <paramref name="eightDirections"/> is set.
    /// </summary>
    public static Direction NextDirection(Direction current, bool eightDirections)
    {
        var index = (int) current;
        if (eightDirections)
            return (Direction) ((index + 7) % 8);

        // Snap a stray diagonal to the next cardinal clockwise rather than skipping past it.
        if (IsDiagonal(current))
            return (Direction) ((index + 7) % 8);

        return (Direction) ((index + 6) % 8);
    }

    private static bool IsDiagonal(Direction direction) => (int) direction % 2 == 1;

    private void ApplyHoverCooldown(Entity<CMUJumpPackComponent> ent)
    {
        var comp = ent.Comp;
        _actions.SetToggled(comp.HoverActionEntity, false);

        var end = comp.HoverStart + comp.HoverCooldown;
        var now = _timing.CurTime;
        if (end <= now)
            return;

        // Ending through the action restarts its cooldown from UseDelay, so keep the remainder there too.
        _actions.SetUseDelay(comp.HoverActionEntity, end - now);
        _actions.SetCooldown(comp.HoverActionEntity, comp.HoverStart, end);
    }

    private void StartBoostCooldown(Entity<CMUJumpPackComponent> ent)
    {
        // PerformAction restarts the performed action's cooldown from its UseDelay, so set both.
        var comp = ent.Comp;
        _actions.SetUseDelay(comp.BoostActionEntity, comp.BoostCooldown);
        _actions.SetUseDelay(comp.JumpActionEntity, comp.BoostCooldown);
        _actions.SetCooldown(comp.BoostActionEntity, comp.BoostCooldown);
        _actions.SetCooldown(comp.JumpActionEntity, comp.BoostCooldown);
    }

    private bool HasOpenSkyAbove(EntityUid user)
    {
        var xform = Transform(user);
        return xform.MapUid is { } mapUid &&
               TryComp(mapUid, out CMUZLevelMapComponent? zMap) &&
               _zLevels.TryMapUp((mapUid, zMap), out _) &&
               !_zLevels.HasTileAbove(user);
    }

    private bool HasFuel(Entity<CMUJumpPackComponent> pack, float amount)
    {
        if (!pack.Comp.RequiresFuel)
            return true;

        return TryComp(pack, out GasTankComponent? tank) && tank.Air.TotalMoles >= amount;
    }

    private bool TryUseFuel(Entity<CMUJumpPackComponent> pack, EntityUid user, float amount)
    {
        if (!pack.Comp.RequiresFuel)
            return true;

        if (!TryComp(pack, out GasTankComponent? tank) || tank.Air.TotalMoles < amount)
        {
            _popup.PopupClient(Loc.GetString("cmu-jump-pack-no-fuel"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (_net.IsServer)
        {
            tank.Air.Remove(amount);
            Dirty(pack, tank);
            _gasTank.UpdateUserInterface((pack, tank));
        }

        return true;
    }

    private string DirectionName(Direction direction)
    {
        return Loc.GetString($"cmu-jump-pack-direction-{direction.ToString().ToLowerInvariant()}");
    }

    private string ModeName(bool eightDirections)
    {
        return Loc.GetString(eightDirections ? "cmu-jump-pack-mode-eight" : "cmu-jump-pack-mode-four");
    }
}
