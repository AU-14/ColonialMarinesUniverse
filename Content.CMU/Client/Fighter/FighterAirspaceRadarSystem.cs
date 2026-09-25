using Content.Client.UserInterface.Controls;
using Content.Shared.CMU14.Fighter;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Fighter;

/// <summary>The wielded launcher supplies a radar display, not a second firing control.</summary>
public sealed partial class FighterAirspaceRadarSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private FighterManpadSystem _manpads = default!;
    [Dependency] private FighterBoilerAirDefenseSystem _boilers = default!;
    private FighterAirspaceRadarEvent? _state;
    private FighterAirspaceRadarEvent? _shownState;
    private FighterAirspaceRadarControl? _display;
    private NetEntity? _terrainMap;
    private byte[] _terrain = [];
    private TimeSpan _receivedAt;

    public override void Initialize() => SubscribeNetworkEvent<FighterAirspaceRadarEvent>(OnRadar);

    private void OnRadar(FighterAirspaceRadarEvent ev)
    {
        if (ev.TerrainMap != _terrainMap)
        {
            _terrainMap = ev.TerrainMap;
            _terrain = [];
        }
        if (ev.Terrain != null) _terrain = ev.Terrain;
        _state = ev;
        _receivedAt = _timing.RealTime;
    }

    public override void FrameUpdate(float frameTime)
    {
        if (_state is not { } state || _timing.RealTime - _receivedAt > TimeSpan.FromSeconds(2) ||
            _player.LocalEntity is not { } user || !CanView(user, state) ||
            _ui.ActiveScreen?.GetWidget<MainViewport>() is not { } viewport)
        {
            Hide();
            return;
        }
        var parent = _ui.WindowRoot;
        if (_display != null && _display.Parent != parent) Hide();
        if (_display == null)
        {
            _display = new FighterAirspaceRadarControl();
            parent.AddChild(_display);
        }
        var origin = viewport.GlobalPosition - parent.GlobalPosition;
        var width = Math.Min(290, viewport.Size.X * .35f);
        var height = Math.Min(350, viewport.Size.Y - 24);
        LayoutContainer.SetMarginLeft(_display, origin.X + viewport.Size.X - width - 12);
        LayoutContainer.SetMarginRight(_display, origin.X + viewport.Size.X - 12);
        LayoutContainer.SetMarginTop(_display, origin.Y + 12);
        LayoutContainer.SetMarginBottom(_display, origin.Y + 12 + height);
        if (_shownState == state) return;
        _shownState = state;
        _display.SetState(state, _terrain);
    }

    private bool CanView(EntityUid user, FighterAirspaceRadarEvent state)
    {
        if (state.Organic)
            return state.Launcher == GetNetEntity(user) &&
                TryComp(user, out FighterBoilerAirDefenseComponent? boiler) && boiler.Watching && _boilers.CanWatch(user);
        return _hands.GetActiveItem(user) is { } launcher && GetNetEntity(launcher) == state.Launcher &&
            TryComp(launcher, out FighterManpadComponent? manpad) && _manpads.CanAim((launcher, manpad), user);
    }

    private void Hide()
    {
        _display?.Orphan();
        _display?.Dispose();
        _display = null;
        _shownState = null;
    }

    public override void Shutdown()
    {
        Hide();
        base.Shutdown();
    }
}
