using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._CMU14.Interface;

/// <summary>
///     One bordered choice: a button that is a segment of the card, and a line or two saying what
///     the choice actually does.
/// </summary>
/// <remarks>
///     <para>
///     Extracted from the join-round window because the same shape keeps coming up - a short list of
///     terminal choices where the label alone does not explain the option. "Join Govfor" and "Admin
///     Help" have the same problem: the button names the action and nothing tells a new player what
///     it means. Anywhere that is true, this control fits.
///     </para>
///     <para>
///     The button is deliberately part of the box rather than something placed on it. It fills the
///     card's height, runs flush into the border, and its only edge is the single rule facing the
///     text - bordering all four sides would put a box inside a box, which this theme has refused
///     everywhere else.
///     </para>
///     <para>
///     Colours are passed in rather than taken from <see cref="CrtTerminalPalette"/>. The palette is
///     one hue by design, and the whole reason a caller reaches for this control is that its choices
///     need telling apart - a faction, a severity, an audience. <see cref="Terminal"/> is there for
///     callers that genuinely want the house style.
///     </para>
/// </remarks>
public sealed class CmuChoiceCard : PanelContainer
{
    /// <summary>The card's edge, its fill, the button's fill, and the text colour.</summary>
    public readonly record struct Palette(Color Edge, Color Fill, Color Button, Color Text);

    /// <summary>The house style, for choices that have no identity of their own to carry.</summary>
    public static readonly Palette Terminal = new(
        CrtTerminalPalette.Surface4,
        CrtTerminalPalette.Surface0,
        CrtTerminalPalette.Surface2,
        CrtTerminalPalette.Text);

    public Button Button { get; }

    private readonly RichTextLabel _description;
    private readonly bool _buttonOnLeft;
    private Palette _palette;

    public CmuChoiceCard(
        string buttonText,
        string description,
        Palette palette,
        bool buttonOnLeft = true,
        float buttonWidth = CmuPanelMetrics.ButtonWide,
        float descriptionWidth = CmuPanelMetrics.DescriptionWidth)
    {
        _palette = palette;
        _buttonOnLeft = buttonOnLeft;
        HorizontalExpand = true;

        Button = new Button
        {
            Text = buttonText,
            MinSize = new Vector2(buttonWidth, CmuPanelMetrics.RowTall),
            VerticalExpand = true,
        };

        // Centring a label takes both of these. AlignMode centres the text inside the Label's own
        // box; HorizontalExpand is what makes that box span the button. The second is a plain
        // property and cannot come from a stylesheet rule, so it has to be set here.
        Button.Label.HorizontalExpand = true;
        Button.Label.Align = Label.AlignMode.Center;

        _description = new RichTextLabel
        {
            HorizontalExpand = true,
            MaxWidth = descriptionWidth,
            Margin = CmuPanelMetrics.ContentPadding,
            VerticalAlignment = VAlignment.Center,
        };

        if (StyleNano.CrtUiEnabled)
            _description.AddStyleClass(StyleNano.StyleClassCrtRichText);

        // No separation and no margin: the button has to meet the border, not float inside padding.
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 0,
            HorizontalExpand = true,
        };

        if (buttonOnLeft)
        {
            row.AddChild(Button);
            row.AddChild(_description);
        }
        else
        {
            row.AddChild(_description);
            row.AddChild(Button);
        }

        AddChild(row);
        ApplyPalette();
        SetDescription(description);
    }

    /// <summary>
    ///     Re-apply the card's own look. Call after anything that re-walks the tree - the CRT theme
    ///     pass hands every button the same shared box, so a card themed before it runs comes out
    ///     looking like every other control.
    /// </summary>
    public void ApplyPalette()
    {
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = _palette.Fill,
            BorderColor = _palette.Edge,
            BorderThickness = new Thickness(1),
        };

        Button.StyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = _palette.Button,
            BorderColor = _palette.Edge,
            BorderThickness = _buttonOnLeft
                ? new Thickness(0, 0, 1, 0)
                : new Thickness(1, 0, 0, 0),
            ContentMarginLeftOverride = 12,
            ContentMarginRightOverride = 12,
            ContentMarginTopOverride = 6,
            ContentMarginBottomOverride = 4,
        };

        Button.Label.FontColorOverride = _palette.Text;
    }

    /// <summary>
    ///     Swap the card's colours - for a choice that needs to signal something at runtime, such as
    ///     an unread message waiting behind it.
    /// </summary>
    public void SetPalette(Palette palette)
    {
        _palette = palette;
        ApplyPalette();
        SetDescription(_description.Text ?? string.Empty);
    }

    /// <summary>
    ///     Dimmed against the button's own text, so the button stays what the eye lands on first.
    /// </summary>
    public void SetDescription(string description)
    {
        _description.SetMessage(description, defaultColor: _palette.Text.WithAlpha(0.72f));
    }
}
