using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.ConstructionRobot;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.ConstructionRobot;

public sealed partial class CMUConstructionRobotHudSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IResourceCache _resources = default!;

    private const string SpriteRoot = "/Textures/CMU14/Actions/Construcion_UGV/";
    private LayoutContainer? _parent;
    private ScrollContainer? _panel;
    private BoxContainer? _rows;
    private List<CMUConstructionRobotHudEntry> _shown = [];

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var screen = _ui.ActiveScreen;
        if (screen == null || _player.LocalEntity is not { } owner ||
            !TryComp<CMUConstructionRobotOwnerComponent>(owner, out var status) || status.Robots.Count == 0)
        {
            HidePanel();
            return;
        }

        var parent = screen.FindControl<LayoutContainer>("ViewportContainer");
        if (_parent != parent)
            HidePanel();

        if (_panel == null)
        {
            _parent = parent;
            var rows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
            _rows = rows;
            _panel = new ScrollContainer
            {
                HScrollEnabled = false,
                Children = { rows },
            };
            parent.AddChild(_panel);
            LayoutContainer.SetAnchorPreset(_panel, LayoutContainer.LayoutPreset.TopLeft);
            LayoutContainer.SetMarginLeft(_panel, 12);
            LayoutContainer.SetMarginTop(_panel, 100);
            LayoutContainer.SetMarginRight(_panel, 260);
        }

        if (_shown.SequenceEqual(status.Robots))
            return;

        _shown = new List<CMUConstructionRobotHudEntry>(status.Robots);
        LayoutContainer.SetMarginBottom(_panel, 100 + Math.Min(200, _shown.Count * 52));
        _rows!.RemoveAllChildren();
        foreach (var robot in _shown)
        {
            var (face, activity) = robot.Activity switch
            {
                CMUConstructionRobotActivity.Construction => ("CUGV_Happy", "cmu-construction-robot-hud-construction"),
                CMUConstructionRobotActivity.Repair => ("CUGV_Love", "cmu-construction-robot-hud-repair"),
                CMUConstructionRobotActivity.Hazard => ("CUGV_Sad", "cmu-construction-robot-hud-hazard"),
                _ => ("CUGV_Sleepy", "cmu-construction-robot-hud-idle"),
            };
            var heart = robot.IntegrityPercent switch
            {
                <= 0 => "CUGV_Heart_0",
                <= 25 => "CUGV_Heart_14",
                <= 50 => "CUGV_Heart_12",
                <= 75 => "CUGV_Heart_34",
                _ => "CUGV_Heart",
            };
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
            row.AddChild(Icon($"CUGV_Emotions/{face}.png"));
            row.AddChild(Icon($"CUGV_Heart_Quarters/{heart}.png"));
            row.AddChild(new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                Children =
                {
                    new Label { Text = robot.Name },
                    new Label { Text = Loc.GetString(activity) },
                    new Label { Text = Loc.GetString("cmu-construction-robot-hud-integrity", ("percent", robot.IntegrityPercent)) },
                },
            });
            _rows.AddChild(row);
        }
    }

    public override void Shutdown()
    {
        HidePanel();
        base.Shutdown();
    }

    private TextureRect Icon(string path)
    {
        return new TextureRect
        {
            Texture = _resources.GetResource<TextureResource>(new ResPath(SpriteRoot + path)).Texture,
            MinSize = new Vector2(32, 32),
            Stretch = TextureRect.StretchMode.KeepCentered,
        };
    }

    private void HidePanel()
    {
        _panel?.Orphan();
        _panel = null;
        _parent = null;
        _rows = null;
        _shown.Clear();
    }
}
