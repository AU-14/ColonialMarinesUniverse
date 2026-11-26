using Content.Shared.Chasm;
using Content.Shared.CMU14.Chasm;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Chasm;

public sealed partial class CMUChasmLedgeInputSystem : EntitySystem
{
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    private readonly HashSet<Keyboard.Key> _captured = new();

    public override void Initialize()
    {
        // The server decides between catching the ledge and starting the irreversible fall.
        SubscribeLocalEvent<CMUTerrainChasmComponent, EntityStartFallingAttemptEvent>(OnFallAttempt);
        _input.FirstChanceOnKeyEvent += OnKey;
        _overlays.AddOverlay(new CMUChasmLedgeOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        _input.FirstChanceOnKeyEvent -= OnKey;
        _overlays.RemoveOverlay<CMUChasmLedgeOverlay>();
    }

    private static void OnFallAttempt(Entity<CMUTerrainChasmComponent> ent, ref EntityStartFallingAttemptEvent args)
    {
        args.Cancelled = true;
    }

    public static Keyboard.Key GetKey(CMUChasmLedgeKey key) => key switch
    {
        CMUChasmLedgeKey.X => Keyboard.Key.X,
        CMUChasmLedgeKey.F => Keyboard.Key.F,
        CMUChasmLedgeKey.C => Keyboard.Key.C,
        _ => Keyboard.Key.Unknown,
    };

    private void OnKey(KeyEventArgs args, KeyEventType type)
    {
        if (type == KeyEventType.Up)
        {
            if (_captured.Remove(args.Key))
                args.Handle();
            return;
        }

        if (type == KeyEventType.Repeat && _captured.Contains(args.Key))
        {
            args.Handle();
            return;
        }

        if (type != KeyEventType.Down || args.Handled || _ui.KeyboardFocused != null ||
            !TryComp<CMUChasmLedgeGripComponent>(_player.LocalEntity, out var grip))
            return;

        CMUChasmLedgeKey? pressed = args.Key switch
        {
            Keyboard.Key.X => CMUChasmLedgeKey.X,
            Keyboard.Key.F => CMUChasmLedgeKey.F,
            Keyboard.Key.C => CMUChasmLedgeKey.C,
            _ => null,
        };
        if (pressed == null)
            return;

        args.Handle();
        if (_captured.Add(args.Key))
            RaiseNetworkEvent(new CMUChasmLedgePressEvent(grip.GripId, grip.Presses, pressed.Value));
    }
}
