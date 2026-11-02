using System.Numerics;
using Content.Client.CMU14.Yautja.ClanHall;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Yautja;

// the small bracer and mask tools (translator, thrall line, relay beacon, audio panel) in the same predator hud as
// the bracer window: black field, title in light, a status reading beside it, close. each tool fills Body
[Virtual]
public class YautjaHudWindow : BaseWindow
{
    protected readonly Font TitleFont;
    protected readonly Font LabelFont;
    protected readonly Font SmallFont;
    protected readonly Font BodyFont;
    protected readonly Font BodyBoldFont;

    protected readonly BoxContainer Header;
    protected readonly BoxContainer Body;

    public YautjaHudWindow(string title, Vector2 size, Vector2 minSize)
    {
        TitleFont = Hud.Font(Hud.Osd, 13);
        LabelFont = Hud.Font(Hud.Osd, 10);
        SmallFont = Hud.Font(Hud.Osd, 8);
        BodyFont = Hud.Font(Hud.Sans, 12);
        BodyBoldFont = Hud.Font(Hud.SansBold, 12);

        Resizable = true;
        MouseFilter = MouseFilterMode.Stop;
        SetSize = size;
        MinSize = minSize;

        var background = new HudBackground { HorizontalExpand = true, VerticalExpand = true };
        AddChild(background);
        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(16, 12),
        };
        background.AddChild(root);

        Header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        var label = Hud.Write(title.ToUpperInvariant(), TitleFont, Hud.Bright, clip: true);
        label.HorizontalExpand = true;
        label.VerticalAlignment = VAlignment.Center;
        Header.AddChild(label);
        root.AddChild(Header);

        Body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        root.AddChild(Body);
    }

    // call after anything else goes in the header, so close always sits at the far right
    protected void AddClose()
    {
        var close = new HudButton("X", LabelFont);
        close.Activated += Close;
        Header.AddChild(close);
    }

    protected Label Reading(Color color)
    {
        var label = Hud.Write(string.Empty, SmallFont, color);
        label.VerticalAlignment = VAlignment.Center;
        return label;
    }

    // a hud text box: the hairline frame of the hall's fields around a multi-line edit
    protected static PanelContainer Field(Control content)
    {
        var panel = new PanelContainer
        {
            PanelOverride = Hud.Field(),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        panel.AddChild(content);
        return panel;
    }

    protected const float ResizeGrip = 14;

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        if (relativeMousePos.X >= Size.X - ResizeGrip && relativeMousePos.Y >= Size.Y - ResizeGrip)
            return DragMode.Bottom | DragMode.Right;

        return DragMode.Move;
    }

    protected static string Collapse(TextEdit edit)
    {
        return Rope.Collapse(edit.TextRope);
    }
}
