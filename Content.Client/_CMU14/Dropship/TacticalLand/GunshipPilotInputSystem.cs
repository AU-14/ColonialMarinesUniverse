using System.Collections.Generic;
using Content.Client.CombatMode;
using Content.Shared._CMU14.Dropship.DirectFire;
using Content.Shared._CMU14.Dropship.TacticalLand;
using Content.Shared._CMU14.Input;
using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._CMU14.Dropship.TacticalLand;

public sealed partial class GunshipPilotInputSystem : EntitySystem
{
    private static readonly TimeSpan DirectFireAimUpdateInterval = TimeSpan.FromMilliseconds(100);

    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CombatModeSystem _combatMode = default!;

    private readonly HashSet<GunshipControlAction> _pressedActions = new();
    private TimeSpan _nextDirectFireAimUpdate;

    public override void Initialize()
    {
        base.Initialize();

        var binds = CommandBinds.Builder;
        binds.BindBefore(EngineKeyFunctions.MoveUp, new GunshipMovementBlocker(this), typeof(SharedMoverController));
        binds.BindBefore(EngineKeyFunctions.MoveDown, new GunshipMovementBlocker(this), typeof(SharedMoverController));
        binds.BindBefore(EngineKeyFunctions.MoveLeft, new GunshipMovementBlocker(this), typeof(SharedMoverController));
        binds.BindBefore(EngineKeyFunctions.MoveRight, new GunshipMovementBlocker(this), typeof(SharedMoverController));
        Bind(binds, CMUKeyFunctions.CMUGunshipForward, GunshipControlAction.Forward);
        Bind(binds, CMUKeyFunctions.CMUGunshipBack, GunshipControlAction.Back);
        Bind(binds, CMUKeyFunctions.CMUGunshipLeft, GunshipControlAction.Left);
        Bind(binds, CMUKeyFunctions.CMUGunshipRight, GunshipControlAction.Right);
        Bind(binds, CMUKeyFunctions.CMUGunshipRotateLeft, GunshipControlAction.RotateLeft);
        Bind(binds, CMUKeyFunctions.CMUGunshipRotateRight, GunshipControlAction.RotateRight);
        Bind(binds, CMUKeyFunctions.CMUGunshipAscend, GunshipControlAction.Ascend);
        Bind(binds, CMUKeyFunctions.CMUGunshipDescend, GunshipControlAction.Descend);
        BindThrust(binds, CMUKeyFunctions.CMUGunshipIncreaseThrust, 1);
        BindThrust(binds, CMUKeyFunctions.CMUGunshipDecreaseThrust, -1);
        BindPilotToggle(binds, CMUKeyFunctions.CMUGunshipCycleCamera, cycleCamera: true);
        BindPilotToggle(binds, CMUKeyFunctions.CMUGunshipTogglePanning, cycleCamera: false);
        binds.Register<GunshipPilotInputSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<GunshipPilotInputSystem>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextDirectFireAimUpdate ||
            !TryGetDirectFirePilot(out _))
        {
            return;
        }

        _nextDirectFireAimUpdate = _timing.CurTime + DirectFireAimUpdateInterval;
        var mouse = _eye.PixelToMap(_input.MouseScreenPosition);
        if (mouse.MapId == MapId.Nullspace)
            return;

        var coordinates = _transform.ToCoordinates(mouse);
        RaiseNetworkEvent(new GunshipDirectFireAimEvent(GetNetCoordinates(coordinates)));
    }

    private bool TryGetDirectFirePilot(out EntityUid pilot)
    {
        pilot = default;
        if (_player.LocalEntity is not { } local ||
            !_combatMode.IsInCombatMode(local) ||
            !TryComp(local, out GunshipPilotHudComponent? hud) ||
            hud.Dropship == null ||
            !hud.FlightControlsAvailable ||
            !hud.HasDirectFireWeapon ||
            !TryComp(local, out BuckleComponent? buckle) ||
            buckle.BuckledTo is not { } seat ||
            !TryComp(seat, out GunshipPilotSeatComponent? pilotSeat) ||
            pilotSeat.ViewOffset != 0 ||
            pilotSeat.RearView)
        {
            return false;
        }

        pilot = local;
        return true;
    }

    private void Bind(CommandBinds.BindingsBuilder binds, BoundKeyFunction function, GunshipControlAction action)
    {
        binds.Bind(function,
            InputCmdHandler.FromDelegate(
                session => SendInput(session?.AttachedEntity, action, true),
                session => SendInput(session?.AttachedEntity, action, false),
                handle: false));
    }

    private void BindThrust(CommandBinds.BindingsBuilder binds, BoundKeyFunction function, int steps)
    {
        binds.Bind(function,
            InputCmdHandler.FromDelegate(
                session => SendThrustAdjustment(session?.AttachedEntity, steps),
                _ => { },
                handle: false));
    }

    private void BindPilotToggle(
        CommandBinds.BindingsBuilder binds,
        BoundKeyFunction function,
        bool cycleCamera)
    {
        binds.Bind(function, new PilotToggleHandler(this, cycleCamera));
    }

    private void SendPilotToggle(EntityUid? pilot, bool cycleCamera)
    {
        if (pilot is not { } user || !IsSeatedGunshipPilot(user))
            return;

        if (cycleCamera)
            RaiseNetworkEvent(new GunshipCycleCameraInputEvent());
        else
            RaiseNetworkEvent(new GunshipPilotPanningInputEvent());
    }

    /// <summary>
    /// Called by the main game viewport so the wheel remains a pilot control
    /// without consuming scroll input over unrelated UI windows.
    /// </summary>
    public bool TryAdjustThrustFromMouseWheel(float delta)
    {
        if (_player.LocalEntity is not { } pilot || !IsLinkedGunshipPilot(pilot))
            return false;

        var steps = Math.Sign(delta);
        if (steps == 0)
            return false;

        RaiseNetworkEvent(new GunshipThrustAdjustEvent(steps));
        return true;
    }

    private void SendThrustAdjustment(EntityUid? pilot, int steps)
    {
        if (pilot is not { } user || !IsLinkedGunshipPilot(user))
            return;

        RaiseNetworkEvent(new GunshipThrustAdjustEvent(steps));
    }

    private bool IsLinkedGunshipPilot(EntityUid pilot)
    {
        return TryComp(pilot, out GunshipPilotHudComponent? hud) &&
               hud.Dropship != null &&
               hud.FlightControlsAvailable &&
               TryComp(pilot, out BuckleComponent? buckle) &&
               buckle.BuckledTo is { } seat &&
               HasComp<GunshipPilotSeatComponent>(seat);
    }

    private bool IsSeatedGunshipPilot(EntityUid pilot)
    {
        return TryComp(pilot, out GunshipPilotHudComponent? hud) &&
               hud.Dropship != null &&
               TryComp(pilot, out BuckleComponent? buckle) &&
               buckle.BuckledTo is { } seat &&
               HasComp<GunshipPilotSeatComponent>(seat);
    }

    private void SendInput(EntityUid? pilot, GunshipControlAction action, bool pressed)
    {
        if (pressed)
        {
            if (!_pressedActions.Add(action))
                return;
        }
        else if (!_pressedActions.Remove(action))
        {
            return;
        }

        if (pilot is not { } user ||
            !TryComp(user, out GunshipPilotHudComponent? hud) ||
            hud.Dropship == null ||
            !hud.FlightControlsAvailable ||
            !TryComp(user, out BuckleComponent? buckle) ||
            buckle.BuckledTo is not { } seat ||
            !HasComp<GunshipPilotSeatComponent>(seat))
        {
            _pressedActions.Remove(action);
            return;
        }

        RaiseNetworkEvent(new GunshipControlInputEvent(action, pressed));
    }

    private bool ShouldBlockCharacterMovement(ICommonSession? session)
    {
        if (session?.AttachedEntity is not { } pilot ||
            HasComp<RelayInputMoverComponent>(pilot) ||
            !TryComp(pilot, out BuckleComponent? buckle) ||
            buckle.BuckledTo is not { } seat)
        {
            return false;
        }

        return HasComp<GunshipPilotSeatComponent>(seat);
    }

    private sealed class GunshipMovementBlocker(GunshipPilotInputSystem system) : InputCmdHandler
    {
        private bool _passedDown;

        public override bool HandleCmdMessage(
            IEntityManager entManager,
            ICommonSession? session,
            IFullInputCmdMessage message)
        {
            if (message.State == BoundKeyState.Down)
            {
                var block = system.ShouldBlockCharacterMovement(session);
                _passedDown = !block;
                return block;
            }

            if (message.State == BoundKeyState.Up && _passedDown)
            {
                _passedDown = false;
                return false;
            }

            return system.ShouldBlockCharacterMovement(session);
        }
    }

    private sealed class PilotToggleHandler(GunshipPilotInputSystem system, bool cycleCamera) : InputCmdHandler
    {
        private bool _pressed;

        public override bool HandleCmdMessage(
            IEntityManager entManager,
            ICommonSession? session,
            IFullInputCmdMessage message)
        {
            if (message.State == BoundKeyState.Down)
            {
                if (session?.AttachedEntity is not { } pilot || !system.IsSeatedGunshipPilot(pilot))
                    return false;

                if (!_pressed)
                {
                    _pressed = true;
                    system.SendPilotToggle(pilot, cycleCamera);
                }

                return true;
            }

            if (!_pressed)
                return false;

            _pressed = false;
            return true;
        }
    }
}
