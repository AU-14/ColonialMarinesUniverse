using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DSceneControl
{
    private bool _orbiting;
    private bool _panning;
    private Vector2 _lastMouse;
    private Vector2 _pressPosition;
    private bool _dragged;
    private bool _firstPersonLooking;
    public event Action? LookStarted;
    public event Action? LookEnded;

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (FirstPerson && !_released)
        {
            if (!args.Handled) _input.ViewportKeyEvent(this, args);
            return;
        }
        if (_released || args.Function != EngineKeyFunctions.UIClick && args.Function != EngineKeyFunctions.UIRightClick)
            return;
        _lastMouse = _pressPosition = args.RelativePosition;
        _orbiting = args.Function == EngineKeyFunctions.UIClick;
        _panning = !_orbiting;
        _dragged = false;
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (FirstPerson)
        {
            if (!args.Handled) _input.ViewportKeyEvent(this, args);
            return;
        }
        if (args.Function != EngineKeyFunctions.UIClick && args.Function != EngineKeyFunctions.UIRightClick)
            return;
        if (!_released && _hasScene && _orbiting && !_dragged && args.Function == EngineKeyFunctions.UIClick)
        {
            var camera = Camera();
            var ray = camera.RayDirection(args.RelativePosition * UIScale);
            var found = _encoding.TryPick(camera.Origin, ray, out var hit, _surfaces);
            _selectedBox = found ? hit.BoxIndex + 1 : 0;
            EntitySelected?.Invoke(found ? hit.Source : null);
        }
        _orbiting = _panning = false;
        _redraw = true;
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (FirstPerson && _firstPersonLooking && !_released)
        {
            var movement = args.Relative;
            _lastMouse = args.RelativePosition;
            LookDelta?.Invoke(movement.X * UIScale);
            FirstPersonPitch = Math.Clamp(FirstPersonPitch - movement.Y * UIScale * 0.004f,
                -CMU3DFirstPersonCamera.PitchLimit, CMU3DFirstPersonCamera.PitchLimit);
            _redraw = true;
            args.Handle();
            return;
        }
        if (_released || !_orbiting && !_panning)
            return;
        var delta = args.RelativePosition - _lastMouse;
        _lastMouse = args.RelativePosition;
        _dragged |= Vector2.DistanceSquared(args.RelativePosition, _pressPosition) > 9;
        if (_orbiting)
        {
            _yaw = (_yaw - delta.X * 0.008f) % (MathF.PI * 2);
            _pitch = Math.Clamp(_pitch + delta.Y * 0.006f, 0.12f, 1.55f);
        }
        else
        {
            var right = new Vector2(-MathF.Sin(_yaw), MathF.Cos(_yaw));
            var away = new Vector2(MathF.Cos(_yaw), MathF.Sin(_yaw));
            _center += (-right * delta.X + away * delta.Y) * (_distance * 0.83f / Math.Max(1, Height));
            _center = Vector2.Clamp(_center, new Vector2(-6), new Vector2(6));
        }
        _redraw = true;
        args.Handle();
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);
        if (_released)
            return;
        if (FirstPerson)
        {
            return;
        }
        _distance = Math.Clamp(_distance * MathF.Pow(0.82f, args.Delta.Y), 3, 60);
        _redraw = true;
        args.Handle();
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        if (!MouseCaptured) StopFirstPersonLook();
        _orbiting = _panning = false;
        _redraw = true;
    }

    private void StopFirstPersonLook()
    {
        if (!_firstPersonLooking)
            return;
        _firstPersonLooking = false;
        LookEnded?.Invoke();
    }
}
