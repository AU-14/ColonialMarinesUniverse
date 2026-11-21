using System.Numerics;
using Content.Shared.CMU14.Dropship.DirectFire;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared.Movement.Systems;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Dropship.DirectFire;

public sealed class MohawkGunnerInputSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    private readonly HashSet<BoundKeyFunction> _held = new();
    private TimeSpan _nextInput;
    private Button? _exit;

    public override void Initialize()
    {
        _overlays.AddOverlay(new MohawkGunnerOverlay(EntityManager, _player));
        var binds = CommandBinds.Builder;
        foreach (var key in new[] { EngineKeyFunctions.MoveUp, EngineKeyFunctions.MoveDown,
                     EngineKeyFunctions.MoveLeft, EngineKeyFunctions.MoveRight })
            binds.BindBefore(key, new MovementHandler(this, key), typeof(SharedMoverController));
        binds.Register<MohawkGunnerInputSystem>();
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<MohawkGunnerOverlay>();
        CommandBinds.Unregister<MohawkGunnerInputSystem>();
        _exit?.Dispose();
        base.Shutdown();
    }

    private bool TryGetGunner(out Entity<DropshipTerminalWeaponsComponent> console)
    {
        var query = EntityQueryEnumerator<DropshipTerminalWeaponsComponent>();
        while (query.MoveNext(out var uid, out var terminal))
        {
            if (terminal.Gunnery && terminal.ChinGunOperator != null &&
                terminal.ChinGunOperator == _player.LocalEntity && terminal.ChinGunEye != null)
            {
                console = (uid, terminal);
                return true;
            }
        }
        console = default;
        return false;
    }

    public override void FrameUpdate(float frameTime)
    {
        if (!TryGetGunner(out var console))
        {
            _held.Clear();
            _exit?.Dispose();
            _exit = null;
            return;
        }
        if (console.Comp.ChinGunAr && _exit == null && _ui.ActiveScreen != null)
        {
            _exit = new Button
            {
                Text = Loc.GetString("cmu-mohawk-gunnery-ar-exit"),
                Mode = BaseButton.ActionMode.Press,
                StyleClasses = { "CrtButton" },
            };
            _exit.OnPressed += _ =>
            {
                if (TryGetGunner(out var active))
                    RaiseNetworkEvent(new MohawkChinGunInputEvent(GetNetEntity(active), Vector2.Zero, null, false, true));
            };
            var parent = _ui.ActiveScreen.FindControl<LayoutContainer>("ViewportContainer");
            parent.AddChild(_exit);
            LayoutContainer.SetAnchorPreset(_exit, LayoutContainer.LayoutPreset.CenterTop);
            LayoutContainer.SetMarginLeft(_exit, -160);
            LayoutContainer.SetMarginRight(_exit, 160);
            LayoutContainer.SetMarginTop(_exit, 8);
            LayoutContainer.SetMarginBottom(_exit, 44);
        }
        else if (!console.Comp.ChinGunAr && _exit != null)
        {
            _exit.Dispose();
            _exit = null;
        }
        if (_timing.CurTime < _nextInput)
            return;
        if (!_input.Enabled || _ui.KeyboardFocused is LineEdit)
            _held.Clear();
        _nextInput = _timing.CurTime + TimeSpan.FromMilliseconds(100);
        var movement = new Vector2(
            (_held.Contains(EngineKeyFunctions.MoveRight) ? 1 : 0) - (_held.Contains(EngineKeyFunctions.MoveLeft) ? 1 : 0),
            (_held.Contains(EngineKeyFunctions.MoveUp) ? 1 : 0) - (_held.Contains(EngineKeyFunctions.MoveDown) ? 1 : 0));
        var world = _eye.PixelToMap(_input.MouseScreenPosition);
        var aiming = console.Comp.ChinGunAr && _input.Enabled && _ui.KeyboardFocused is not LineEdit &&
                     _ui.CurrentlyHovered is IViewportControl;
        RaiseNetworkEvent(new MohawkChinGunInputEvent(GetNetEntity(console), movement,
            aiming ? GetNetCoordinates(_transform.ToCoordinates(world)) : null,
            aiming && _input.IsKeyDown(Keyboard.Key.MouseLeft)));
    }

    private sealed class MovementHandler(MohawkGunnerInputSystem system, BoundKeyFunction key) : InputCmdHandler
    {
        private bool _passedDown;

        public override bool HandleCmdMessage(IEntityManager entManager, ICommonSession? session, IFullInputCmdMessage message)
        {
            if (message.State == BoundKeyState.Down)
            {
                if (!system.TryGetGunner(out _))
                {
                    _passedDown = true;
                    return false;
                }
                system._held.Add(key);
                return true;
            }
            system._held.Remove(key);
            if (_passedDown)
            {
                _passedDown = false;
                return false;
            }
            return system.TryGetGunner(out _);
        }
    }
}
