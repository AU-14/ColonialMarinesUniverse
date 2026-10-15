using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Ghost;

public sealed class CMUGhostColorWindow : DefaultWindow
{
    private readonly ColorSelectorSliders _sliders;

    public CMUGhostColorWindow(CMUGhostColorSystem system)
    {
        Title = Loc.GetString("cmu-ghost-color-window-title");
        MinSize = new(320, 240);

        _sliders = new ColorSelectorSliders { HorizontalExpand = true };

        var apply = new Button { Text = Loc.GetString("cmu-ghost-color-apply"), HorizontalExpand = true };
        apply.OnPressed += _ => system.SetColor(_sliders.Color);

        var reset = new Button { Text = Loc.GetString("cmu-ghost-color-reset"), HorizontalExpand = true };
        reset.OnPressed += _ =>
        {
            _sliders.Color = Color.White;
            system.SetColor(null);
        };

        var buttons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
        };
        buttons.AddChild(apply);
        buttons.AddChild(reset);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
        };
        root.AddChild(_sliders);
        root.AddChild(buttons);
        Contents.AddChild(root);
    }

    public void SetColor(Color color)
    {
        _sliders.Color = color;
    }
}
