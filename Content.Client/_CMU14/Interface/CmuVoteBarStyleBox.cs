using Robust.Client.Graphics;
using Robust.Shared.Maths;

namespace Content.Client._CMU14.Interface;

/// <summary>
///     A vote option's row: a track, a fill showing that option's share of the votes so far, and an
///     accent edge when it is the one you picked.
/// </summary>
/// <remarks>
///     <para>
///     Replaces reading the count out of the label. "Raijin Hydroelectric (3)" puts the number inside
///     the name, where it has to be found and compared against three other numbers to mean anything;
///     a fill turns four counts into a shape you read at a glance without counting. It is also honest
///     to the theme - a bar of phosphor is exactly what a terminal would draw.
///     </para>
///     <para>
///     Built from <see cref="DrawingHandleScreen.DrawRect"/> rather than DrawPrimitives, matching
///     every other custom stylebox here: the UI render path batches quads, and mixing in primitive
///     draws breaks the batch.
///     </para>
/// </remarks>
public sealed class CmuVoteBarStyleBox : StyleBox
{
    public Color TrackColor { get; set; }
    public Color FillColor { get; set; }

    /// <summary>Drawn down the leading edge when <see cref="IsOurVote"/>.</summary>
    public Color AccentColor { get; set; }

    /// <summary>Share of the total vote, 0 to 1. Zero draws no fill at all.</summary>
    public float Fraction { get; set; }

    public bool IsOurVote { get; set; }

    /// <summary>Width of the leading edge marking your own vote.</summary>
    public float AccentWidth { get; set; } = 2f;

    protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        handle.DrawRect(box, TrackColor);

        // An option with no votes gets nothing rather than a hairline. A 1px sliver at zero reads as
        // "one vote" at a glance, which is the one thing this must not do.
        var fraction = MathHelper.Clamp(Fraction, 0f, 1f);
        if (fraction > 0f)
        {
            var width = box.Width * fraction;
            if (width >= 1f)
                handle.DrawRect(new UIBox2(box.Left, box.Top, box.Left + width, box.Bottom), FillColor);
        }

        if (!IsOurVote)
            return;

        var accent = AccentWidth * uiScale;
        handle.DrawRect(new UIBox2(box.Left, box.Top, box.Left + accent, box.Bottom), AccentColor);
    }

    protected override float GetDefaultContentMargin(Margin margin)
    {
        return margin switch
        {
            Margin.Left => 10,
            Margin.Right => 10,
            Margin.Top => 5,
            Margin.Bottom => 4,
            _ => 0,
        };
    }
}
