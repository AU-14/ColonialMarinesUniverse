using Robust.Shared.Maths;

namespace Content.Client._CMU14.Interface;

/// <summary>
///     The measurements the CRT panels share: how tall a control is, how far things sit from a
///     border, and how much air goes between them.
/// </summary>
/// <remarks>
///     <para>
///     Written because the vote popup, the lobby action column, the join-round window and the staff
///     help window each arrived at their own answer. Between them they used six control heights
///     (26, 34, 36, 40, 46, 48) and three paddings, and almost none of the differences meant
///     anything - 34 and 36 are the same intent typed twice, a fortnight apart. Panels that share a
///     screen should be built out of the same parts.
///     </para>
///     <para>
///     This is a scale, not a single value. The differences that *are* deliberate stay: a primary
///     action is meant to be taller than a secondary one, and flattening the lobby column to one
///     height would throw away the ranking that column exists to express. What changes is that a
///     panel now picks a step by name instead of inventing a number, so the next panel has
///     somewhere to look.
///     </para>
///     <para>
///     Sizes are <c>float</c> and separations <c>int</c> to match the properties they feed
///     (<see cref="Robust.Client.UserInterface.Control.MinHeight"/> and
///     <c>SeparationOverride</c>), which lets XAML bind them directly with <c>{x:Static}</c> rather
///     than restating the number.
///     </para>
/// </remarks>
public static class CmuPanelMetrics
{
    /// <summary>
    ///     A secondary action - real, but not the one being offered. Observe in the lobby column.
    /// </summary>
    public const float RowCompact = 26;

    /// <summary>
    ///     The ordinary control height. Vote options, and the lobby's primary action.
    /// </summary>
    public const float Row = 36;

    /// <summary>
    ///     A control that has to hold its own against a block of text beside it: the button on a
    ///     <see cref="CmuChoiceCard"/>, or a vote option whose label has wrapped to two lines.
    /// </summary>
    public const float RowTall = 46;

    /// <summary>A window's header strip.</summary>
    public const float Header = 40;

    /// <summary>Chrome that only has to fit a word - MINIMIZE, RESTORE.</summary>
    public const float ButtonNarrow = 80;

    /// <summary>
    ///     A button that names a choice rather than an action, and shares its row with prose. Wide
    ///     enough that the longest faction name does not wrap, and identical across cards so the
    ///     descriptions start on the same line however the buttons are ordered.
    /// </summary>
    public const float ButtonWide = 164;

    /// <summary>
    ///     Width a description is allowed before it wraps. Without a cap a
    ///     <see cref="Robust.Client.UserInterface.Controls.RichTextLabel"/> reports its whole
    ///     unwrapped line as its desired width, and a window sized to that grows until every
    ///     description fits on one line - which is how the join-round window first came out over
    ///     1300px wide.
    /// </summary>
    public const float DescriptionWidth = 430;

    /// <summary>
    ///     Minimum width for a window that is a stack of <see cref="CmuChoiceCard"/>s: the button,
    ///     the widest a description is allowed to run, and the panel's inset on both sides. It is a
    ///     floor rather than the width - such a window sizes itself to its cards - but stating it
    ///     keeps the join-round and staff-help windows from floating apart at 640 and 620, which is
    ///     where they were.
    /// </summary>
    public const float ChoiceWindowWidth = ButtonWide + DescriptionWidth + PanelPaddingHorizontal * 2;

    /// <summary>
    ///     Width of the lobby's action column. Two controls have to agree on it: the panel itself,
    ///     and the spacer that holds the round clock clear of it so the clock centres in the gap
    ///     between that panel and the server-info screen rather than on top of it.
    /// </summary>
    public const float LobbyActionColumnWidth = 380;

    /// <summary>
    ///     How far below the top of the window the round clock sits by default. Near the top rather
    ///     than centred: the middle of the screen is where the art is, and a panel parked there
    ///     reads as being in the way even when nothing is behind it.
    /// </summary>
    public const float LobbyClockTopMargin = 24;

    /// <summary>Between things that are separate objects: cards, popups, stacked panels.</summary>
    public const int RowSeparation = 8;

    /// <summary>Within one group of related controls, where the gap only has to stop them touching.</summary>
    public const int GroupSeparation = 4;

    /// <summary>
    ///     Horizontal component of <see cref="PanelPadding"/>, for the rare caller that needs the
    ///     number rather than the thickness - working out how much width the content actually got.
    /// </summary>
    public const int PanelPaddingHorizontal = 10;

    /// <summary>Horizontal component of <see cref="ContentPadding"/>.</summary>
    public const int ContentPaddingHorizontal = 14;

    /// <summary>
    ///     The inset between a panel's ground and what sits on it. The default: enough to read as
    ///     deliberate, not enough to waste a small window.
    /// </summary>
    public static readonly Thickness PanelPadding = new(PanelPaddingHorizontal, 8);

    /// <summary>
    ///     For content that would otherwise crowd a border - a paragraph, or a row of controls that
    ///     runs the full width of its panel. The vote options and the choice-card descriptions are
    ///     both the widest thing in their panel, and at <see cref="PanelPadding"/> they sat all but
    ///     touching the edge.
    /// </summary>
    public static readonly Thickness ContentPadding = new(ContentPaddingHorizontal, 10);
}
