using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Client.Eye;
using Content.Client.Lobby.UI;
using Content.Client.Stylesheets;
using Content.Client.Viewport;
using Content.Shared.CMU14.Overwatch;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.CMU14.Overwatch;

/// <summary>
/// Up to four overwatch camera feeds in a 2x2 grid, beside the console. Any feed can be focused to fill the window on
/// its own, at a higher resolution, and unfocused to bring the grid back.
/// </summary>
public sealed class CMUOverwatchFeedsWindow : DefaultWindow
{
    public event Action<NetEntity>? OnCloseFeed;

    private readonly Tile[] _tiles = new Tile[CMUOverwatchFeedsComponent.MaxFeeds];
    private readonly GridContainer _grid;
    private int? _focused;

    public CMUOverwatchFeedsWindow()
    {
        Title = Loc.GetString("cmu-overwatch-feeds-title");
        MinSize = new Vector2(520, 460);
        SetSize = new Vector2(820, 760);

        _grid = new GridContainer
        {
            Columns = 2,
            HorizontalExpand = true,
            VerticalExpand = true,
            HSeparationOverride = 6,
            VSeparationOverride = 6,
        };

        for (var i = 0; i < _tiles.Length; i++)
        {
            var tile = new Tile(i + 1);
            tile.Close.OnPressed += _ =>
            {
                if (tile.Target is { } target)
                    OnCloseFeed?.Invoke(target);
            };

            var index = i;
            tile.Focus.OnToggled += args => SetFocus(args.Pressed ? index : null);

            _tiles[i] = tile;
            _grid.AddChild(tile);
        }

        Contents.AddChild(_grid);
        CrtLobbyTheme.ApplyWindow(this, useCrtTypography: true);
    }

    public void SetFeeds(IReadOnlyList<CMUOverwatchFeed> feeds)
    {
        for (var i = 0; i < _tiles.Length; i++)
        {
            _tiles[i].SetFeed(i < feeds.Count ? feeds[i] : null);
        }

        // A focused feed that closed hands the window back to the grid.
        if (_focused is { } focused && _tiles[focused].Target == null)
            SetFocus(null);
    }

    /// <summary>Shows one feed on its own, filling the window, or all of them again for null.</summary>
    private void SetFocus(int? index)
    {
        _focused = index;
        _grid.Columns = index == null ? 2 : 1;
        for (var i = 0; i < _tiles.Length; i++)
        {
            var focused = i == index;
            _tiles[i].Visible = index == null || focused;
            _tiles[i].SetFocused(focused);
        }
    }

    public void ClearEyes()
    {
        foreach (var tile in _tiles)
        {
            tile.SetFeed(null);
        }
    }

    private sealed class Tile : PanelContainer
    {
        [Dependency] private IEntityManager _entities = default!;

        public readonly Button Close;
        public readonly Button Focus;
        private readonly Button _nightVision;

        /// <summary>
        ///     The feed's own eye with lighting turned off, for night vision. It copies the camera's eye every frame
        ///     rather than changing it, so other screens watching the same camera aren't affected.
        /// </summary>
        /// <summary>
        ///     Feeds are drawn closer in than the wearer sees, so the picture is easier to make out in a small tile.
        ///     Below 1 zooms in: 0.7 shows about 10 tiles across instead of 15.
        /// </summary>
        private const float FeedZoom = 0.7f;

        private readonly Robust.Shared.Graphics.Eye _feedEye = new();
        private readonly Robust.Shared.Graphics.Eye _nightVisionEye = new() { DrawLight = false };
        public NetEntity? Target { get; private set; }

        private readonly EyeLerpingSystem _eyeLerping;
        private readonly FixedEye _blank = new();
        private readonly Label _name;
        private readonly Label _noSignal;
        private readonly ScalingViewport _viewport;
        private readonly CMUVhsScreenControl _vhs;
        private readonly CMUVhsHud _hud;
        private NetEntity? _eyeNet;
        private EntityUid? _eye;

        public Tile(int channel)
        {
            IoCManager.InjectDependencies(this);
            _eyeLerping = _entities.System<EyeLerpingSystem>();

            StyleClasses.Add(StyleNano.StyleClassCrtInsetPanel);
            HorizontalExpand = true;
            VerticalExpand = true;

            _name = new Label { HorizontalExpand = true, ClipText = true, StyleClasses = { StyleNano.StyleClassCrtHeading } };
            Close = new Button { Text = "X", ToolTip = Loc.GetString("cmu-overwatch-feeds-close") };
            Focus = new Button
            {
                Text = Loc.GetString("cmu-overwatch-feeds-focus"),
                ToolTip = Loc.GetString("cmu-overwatch-feeds-focus-tooltip"),
                ToggleMode = true,
            };
            _nightVision = new Button
            {
                Text = Loc.GetString("cmu-overwatch-feeds-night-vision"),
                ToolTip = Loc.GetString("cmu-overwatch-feeds-night-vision-tooltip"),
                ToggleMode = true,
            };

            _viewport = new ScalingViewport
            {
                HorizontalExpand = true,
                VerticalExpand = true,
                MouseFilter = MouseFilterMode.Ignore,
                ViewportSize = new Vector2i(480, 480),
                RenderZLevels = true,
                MinimumRenderInterval = TimeSpan.FromSeconds(1.0 / 20),
                Eye = _blank,
            };

            _noSignal = new Label
            {
                Text = Loc.GetString("cmu-overwatch-feeds-no-signal"),
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
                FontColorOverride = CrtTerminalPalette.TextDim,
            };

            // The VHS pass sits after the viewport it shades, and the on-screen display above both.
            _vhs = new CMUVhsScreenControl { Source = _viewport };
            _hud = new CMUVhsHud(channel);

            var screen = new Control
            {
                HorizontalExpand = true,
                VerticalExpand = true,
                MinSize = new Vector2(200, 160),
                RectClipContent = true,
            };
            screen.AddChild(_viewport);
            screen.AddChild(_vhs);
            screen.AddChild(_hud);
            screen.AddChild(_noSignal);

            AddChild(new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                SeparationOverride = 4,
                Children =
                {
                    new BoxContainer
                    {
                        Orientation = LayoutOrientation.Horizontal,
                        Children = { _name, Focus, _nightVision, Close },
                    },
                    screen,
                },
            });

            SetFeed(null);
        }

        public void SetFeed(CMUOverwatchFeed? feed)
        {
            Target = feed?.Target;
            _name.Text = feed?.Name ?? Loc.GetString("cmu-overwatch-feeds-empty");
            Close.Visible = feed != null;
            Focus.Visible = feed != null;
            _nightVision.Visible = feed != null;

            if (_eyeNet == feed?.Eye)
                return;

            ReleaseEye();
            _eyeNet = feed?.Eye;
        }

        /// <summary>
        ///     A focused feed fills the window, so it renders at twice the resolution to stay sharp.
        /// </summary>
        public void SetFocused(bool focused)
        {
            Focus.Pressed = focused;
            _viewport.ViewportSize = focused ? new Vector2i(960, 960) : new Vector2i(480, 480);
        }

        private void ReleaseEye()
        {
            if (_eye is { } eye && _entities.EntityExists(eye))
                _eyeLerping.RemoveEye(eye);

            _eye = null;
            _viewport.Eye = _blank;
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            // The eye entity can arrive a tick after the feed list, so keep looking for it.
            if (_eye == null && _eyeNet is { } eyeNet && _entities.TryGetEntity(eyeNet, out var eye) &&
                _entities.HasComponent<EyeComponent>(eye.Value))
            {
                _eye = eye.Value;
                _eyeLerping.AddEye(eye.Value);
            }

            if (_eye is { } current && _entities.TryGetComponent(current, out EyeComponent? eyeComp) && eyeComp.Eye != null)
            {
                _vhs.NightVision = _nightVision.Pressed;
                // A copy of the wearer's eye, zoomed in, and drawn without lighting for night vision.
                var source = eyeComp.Eye;
                var view = _nightVision.Pressed ? _nightVisionEye : _feedEye;
                view.Position = source.Position;
                view.Rotation = source.Rotation;
                view.Zoom = source.Zoom * FeedZoom;
                view.Offset = source.Offset;
                view.DrawFov = source.DrawFov;
                _viewport.Eye = view;

                SetSignal(true);
            }
            else
            {
                SetSignal(false);
            }
        }

        private void SetSignal(bool signal)
        {
            _viewport.Visible = signal;
            _vhs.Visible = signal;
            _hud.Visible = signal;
            _noSignal.Visible = !signal;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                ReleaseEye();
        }
    }
}
