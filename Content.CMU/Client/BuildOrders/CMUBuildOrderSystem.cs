using Content.Shared._RMC14.Marines;
using Content.Shared.Ghost.Components;
using Content.Shared.CMU14.BuildOrders;
using Content.Shared.CMU14.ForceOnForce;
using Robust.Shared.Timing;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.BuildOrders;

/// <summary>
///     Opens the commander's build menu, and draws each blueprint as a see-through picture of the thing to build, for
///     everyone on the commander's side.
/// </summary>
public sealed class CMUBuildOrderSystem : EntitySystem
{
    private static readonly Color BlueprintColor = new(0.45f, 0.8f, 1f, 0.55f);

    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ForceOnForceSystem _factions = default!;

    private CMUBuildMenuWindow? _window;
    private CMUCommandMarkOverlay? _overlay;

    public override void Initialize()
    {
        SubscribeNetworkEvent<CMUBuildMenuOpenEvent>(OnOpen);

        _overlay = new CMUCommandMarkOverlay(EntityManager, _player, _timing, _factions);
        _overlays.AddOverlay(_overlay);
        SubscribeLocalEvent<CMUBuildOrderMarkerComponent, AfterAutoHandleStateEvent>(OnMarkerState);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_overlay != null)
            _overlays.RemoveOverlay(_overlay);
    }

    private void OnOpen(CMUBuildMenuOpenEvent msg)
    {
        Log.Info($"Build menu open event received with {msg.Options.Count} options");
        _window?.Close();

        try
        {
            _window = new CMUBuildMenuWindow(msg.Options, msg.Selected);
            _window.OnSelect += proto => RaiseNetworkEvent(new CMUBuildSelectEvent(proto));
            _window.OnClear += () => RaiseNetworkEvent(new CMUBuildClearEvent());
            _window.OpenCentered();
        }
        catch (Exception e)
        {
            Log.Error($"Build menu window failed to open: {e}");
        }
    }

    private void OnMarkerState(Entity<CMUBuildOrderMarkerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.Order is not { } order || !TryComp(ent, out SpriteComponent? sprite))
            return;

        _sprite.LayerSetTexture((ent, sprite), 0, _sprite.GetPrototypeIcon(order.Id).Default);
        _sprite.SetColor((ent, sprite), BlueprintColor);
    }

    // Only the commander's own side sees the blueprints (and observers, who see everything).
    public override void FrameUpdate(float frameTime)
    {
        var player = _player.LocalEntity;
        var faction = player != null && TryComp(player, out MarineComponent? marine) ? marine.Faction : null;
        var observer = player == null || HasComp<GhostComponent>(player);

        var markers = EntityQueryEnumerator<CMUBuildOrderMarkerComponent, SpriteComponent>();
        while (markers.MoveNext(out var uid, out var marker, out var sprite))
        {
            var visible = observer || string.Equals(marker.Faction, faction, StringComparison.OrdinalIgnoreCase);
            _sprite.SetVisible((uid, sprite), visible);
        }
    }
}
