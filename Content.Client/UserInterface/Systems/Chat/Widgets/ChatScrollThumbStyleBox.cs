using System;
using Robust.Client.Graphics;
using Robust.Shared.Maths;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

/// <summary>
///     The chat log's scrollbar thumb: a narrow centre bar with two end-caps wider than the bar
///     itself, bracketing the draggable region rather than filling it.
/// </summary>
/// <remarks>
///     Replaced a flat, single-colour fill (2026-08-27) that turned out to be genuinely invisible at
///     rest - not "appropriately quiet," actually unnoticed. A dim rectangle only ever answers "how
///     bright," and the fill had already been through several rounds of that; what it never had was a
///     *shape* that says "control" independent of colour. The caps borrow the same bracket idea as the
///     sidebar's corner ticks (<see cref="Content.Client._CMU14.Interface.CrtTerminalPalette"/>'s
///     consumers), so the scrollbar reads as the same instrument rather than a different control that
///     happens to share a palette.
/// </remarks>
public sealed class ChatScrollThumbStyleBox : StyleBox
{
    public Color BarColor { get; set; }
    public Color CapColor { get; set; }

    /// <summary>Width of the centre bar, in virtual pixels.</summary>
    public float BarWidth { get; set; } = 3f;

    /// <summary>Width of each end-cap, in virtual pixels - wider than the bar on purpose.</summary>
    public float CapWidth { get; set; } = 7f;

    /// <summary>Height of each end-cap, in virtual pixels.</summary>
    public float CapHeight { get; set; } = 2f;

    public ChatScrollThumbStyleBox()
    {
    }

    /// <summary>
    ///     Copies geometry and content margins, not colour - every caller immediately overrides
    ///     <see cref="BarColor"/> and <see cref="CapColor"/> for its own pseudo-class, same shape as
    ///     <c>StyleBoxFlat(StyleBoxFlat)</c>.
    /// </summary>
    public ChatScrollThumbStyleBox(ChatScrollThumbStyleBox other) : base(other)
    {
        BarColor = other.BarColor;
        CapColor = other.CapColor;
        BarWidth = other.BarWidth;
        CapWidth = other.CapWidth;
        CapHeight = other.CapHeight;
    }

    protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        var barWidth = MathF.Min(BarWidth * uiScale, box.Width);
        var capWidth = MathF.Min(CapWidth * uiScale, box.Width);
        // Capped at half the box height so two caps can never overlap on a thumb pinned to its
        // minimum draggable length - see ContentMarginTop/BottomOverride on the instances below,
        // which set that minimum in the first place.
        var capHeight = MathF.Min(CapHeight * uiScale, box.Height / 2f);

        var centerX = (box.Left + box.Right) / 2f;

        handle.DrawRect(
            new UIBox2(centerX - barWidth / 2f, box.Top, centerX + barWidth / 2f, box.Bottom),
            BarColor);

        // Caps sit flush to the thumb's own top and bottom, not inset - they mark where the
        // draggable region starts and ends, so the whole point is that they touch the ends.
        handle.DrawRect(
            new UIBox2(centerX - capWidth / 2f, box.Top, centerX + capWidth / 2f, box.Top + capHeight),
            CapColor);

        handle.DrawRect(
            new UIBox2(centerX - capWidth / 2f, box.Bottom - capHeight, centerX + capWidth / 2f, box.Bottom),
            CapColor);
    }

    /// <summary>
    ///     Never consulted in practice - every instance of this class sets all four content margin
    ///     overrides explicitly, since <see cref="Robust.Client.Graphics.StyleBox.MinimumSize"/> is
    ///     what actually sizes the scrollbar control (see <c>ScrollBar.MeasureOverride</c>): width
    ///     from left+right, and the *minimum draggable thumb length* from top+bottom.
    /// </summary>
    protected override float GetDefaultContentMargin(Margin margin)
    {
        return 0f;
    }
}
