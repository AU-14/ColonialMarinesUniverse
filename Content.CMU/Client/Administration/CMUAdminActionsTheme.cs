using System.Linq;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;
using static Robust.Client.UserInterface.StylesheetHelpers;

namespace Content.Client.CMU14.Administration;

internal static class CMUAdminActionsTheme
{
    private const string ControlClass = "CMUAdminControl";
    public const string Header = "CMUAdminHeader";
    public const string Dim = "CMUAdminDim";
    public const string Heading = "CMUAdminHeading";
    public const string Cyan = "CMUAdminCyan";
    public const string Violet = "CMUAdminViolet";
    public const string Amber = "CMUAdminAmber";

    private static readonly Color Surface = Color.FromHex("#171D26");
    private static readonly Color Inset = Color.FromHex("#121720");
    private static readonly Color Raised = Color.FromHex("#202936");
    private static readonly Color Border = Color.FromHex("#354354");
    private static readonly Color Text = Color.FromHex("#E2E8F0");
    private static readonly Color Muted = Color.FromHex("#A0ADBF");

    public static Stylesheet Create(Stylesheet basis, IResourceCache resources)
    {
        var body = resources.GetFont(new[]
        {
            "/Fonts/NotoSans/NotoSans-Regular.ttf",
            "/Fonts/NotoSans/NotoSansSymbols-Regular.ttf",
            "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf",
        }, 12);
        var bold = resources.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 12);
        var rules = new List<StyleRule>
        {
            Child().Parent(Element<CMUAdminActionsWindow>())
                .Child(Element<PanelContainer>().Class(DefaultWindow.StyleClassWindowPanel))
                .Prop(PanelContainer.StylePropertyPanel, Box(Surface, Border, new Thickness(1))),
            Element<PanelContainer>().Class(ControlClass).Class(Header)
                .Prop(PanelContainer.StylePropertyPanel, Box(Raised, Border, new Thickness(0, 0, 0, 1))),
            Element<Label>().Class(ControlClass)
                .Prop(Label.StylePropertyFont, body)
                .Prop(Label.StylePropertyFontColor, Text),
            Element<Label>().Class(ControlClass).Class(Dim)
                .Prop(Label.StylePropertyFontColor, Muted),
            Element<Label>().Class(ControlClass).Class(Heading)
                .Prop(Label.StylePropertyFont, bold),
            Element<RichTextLabel>().Class(ControlClass)
                .Prop("font", body)
                .Prop("font-color", Muted),
            Element<LineEdit>().Class(ControlClass)
                .Prop("font", body)
                .Prop("font-color", Text)
                .Prop(LineEdit.StylePropertyStyleBox, Box(Inset, Border, new Thickness(1))),
            Element<LineEdit>().Class(ControlClass).Pseudo(LineEdit.StylePseudoClassPlaceholder)
                .Prop("font-color", Muted),
            Element<TabContainer>().Class(ControlClass)
                .Prop("font", body)
                .Prop(TabContainer.StylePropertyPanelStyleBox, Box(Inset, Border, new Thickness(0, 1, 0, 0)))
                .Prop(TabContainer.StylePropertyTabStyleBoxInactive, Box(Surface, Border, new Thickness(0)))
                .Prop(TabContainer.StylePropertyTabFontColorInactive, Muted),
            Element<Button>().Class(ControlClass)
                .Prop(ContainerButton.StylePropertyStyleBox, Box(Raised, Border, new Thickness(1)))
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<Button>().Class(ControlClass).Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Prop(ContainerButton.StylePropertyStyleBox, Box(Surface, Border.WithAlpha(0.5f), new Thickness(1)))
                .Prop(Control.StylePropertyModulateSelf, Color.White.WithAlpha(0.5f)),
            Element<ScrollBar>().Class(ControlClass)
                .Prop(ScrollBar.StylePropertyGrabber, Box(Border, Border, new Thickness(0))),
            Element<ScrollBar>().Class(ControlClass).Pseudo(ScrollBar.StylePseudoClassHover)
                .Prop(ScrollBar.StylePropertyGrabber, Box(Muted, Muted, new Thickness(0))),
            Element<ScrollBar>().Class(ControlClass).Pseudo(ScrollBar.StylePseudoClassGrabbed)
                .Prop(ScrollBar.StylePropertyGrabber, Box(Muted, Muted, new Thickness(0))),
        };

        foreach (var (name, color) in new[]
                 {
                     (Cyan, Color.FromHex("#81CEDA")),
                     (Violet, Color.FromHex("#B9A4EA")),
                     (Amber, Color.FromHex("#DEBA7E")),
                 })
        {
            var tinted = Color.InterpolateBetween(Raised, color, 0.14f);
            rules.Add(Element<Label>().Class(ControlClass).Class(Heading).Class(name)
                .Prop(Label.StylePropertyFontColor, color));
            rules.Add(Element<Button>().Class(ControlClass).Class(name).Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(ContainerButton.StylePropertyStyleBox, Box(tinted, color.WithAlpha(0.6f), new Thickness(1))));
            rules.Add(Element<Button>().Class(ControlClass).Class(name).Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(ContainerButton.StylePropertyStyleBox, Box(tinted, color, new Thickness(0, 0, 0, 2))));
            rules.Add(Element<TabContainer>().Class(ControlClass).Class(name)
                .Prop(TabContainer.StylePropertyTabStyleBox, Box(tinted, color, new Thickness(0, 0, 0, 2)))
                .Prop(TabContainer.stylePropertyTabFontColor, color));
            rules.Add(Element<LineEdit>().Class(ControlClass).Class(name)
                .Prop(LineEdit.StylePropertyCursorColor, color)
                .Prop(LineEdit.StylePropertySelectionColor, color.WithAlpha(0.3f)));
        }

        return new Stylesheet(basis.Rules.Concat(rules).ToArray());
    }

    public static void Apply(Control root, string accent = Cyan)
    {
        root.AddStyleClass(ControlClass);
        SetAccent(root, accent);
        if (root is Button button)
        {
            button.Label.HorizontalExpand = true;
            button.Label.Align = Label.AlignMode.Center;
            // The palette supplies every state; Nano's button class would tint these boxes again.
            button.RemoveStyleClass(ContainerButton.StyleClassButton);
            // SpinBox's segment classes would otherwise override the common compact button boxes.
            button.RemoveStyleClass(SpinBox.LeftButtonStyle);
            button.RemoveStyleClass(SpinBox.MiddleButtonStyle);
            button.RemoveStyleClass(SpinBox.RightButtonStyle);
        }

        foreach (var child in root.Children)
            Apply(child, accent);
    }

    public static void SetAccent(Control control, string accent)
    {
        control.RemoveStyleClass(Cyan);
        control.RemoveStyleClass(Violet);
        control.RemoveStyleClass(Amber);
        control.AddStyleClass(accent);
    }

    private static StyleBoxFlat Box(Color fill, Color border, Thickness thickness) => new()
    {
        BackgroundColor = fill,
        BorderColor = border,
        BorderThickness = thickness,
        ContentMarginLeftOverride = 8,
        ContentMarginRightOverride = 8,
        ContentMarginTopOverride = 4,
        ContentMarginBottomOverride = 4,
    };
}
