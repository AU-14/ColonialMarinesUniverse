using System.Numerics;
using System.Text;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     The working area of the set's display: a title bar and a list of lines under it, with a
///     cursor the keypad drives.
///
///     Drawn as a character grid rather than built out of labels. The glass is a fixed-pitch
///     display, so the pitch is taken from the font itself and every line is truncated to the
///     number of columns that actually fit - which means nothing here can be measured into
///     nothing, and nothing can be cut off in a way the panel did not decide on.
/// </summary>
public sealed class ANPRCScreenView : Control
{
    private const int TitleSize = 9;
    private const int RowSize = 10;

    /// <summary>Rows either side of the cursor kept on screen when it moves to the edge.</summary>
    private const int ScrollMargin = 1;

    public readonly List<ANPRCScreenRow> Rows = new();

    public string Title = string.Empty;
    public string Status = string.Empty;

    /// <summary>Printed instead of the rows when the screen cannot be worked at all.</summary>
    public string? Unavailable;

    public int Cursor;
    public bool Lit;
    public ANPRCBacklight Backlight = ANPRCBacklight.Night;

    /// <summary>A row was clicked. The panel decides whether that means select or activate.</summary>
    public event Action<int>? OnRowClicked;

    private int _scroll;

    /// <summary>How many rows last fitted, so the keypad can page by exactly one screenful.</summary>
    public int VisibleRows { get; private set; } = 1;

    public ANPRCScreenView()
    {
        MouseFilter = MouseFilterMode.Stop;
        VerticalExpand = true;
        HorizontalExpand = true;
    }

    /// <summary>
    ///     A floor in virtual pixels - enough for the title bar and a few lines. The glass gives
    ///     the screen whatever room is left over, so this only matters on a very short window.
    /// </summary>
    private const float MinimumHeight = 84f;

    protected override Vector2 MeasureOverride(Vector2 availableSize)
        => new(0f, MinimumHeight);

    private static float LineHeight(int size, float scale)
        => ANPRCPanelStyle.Mono(size).GetLineHeight(scale);

    /// <summary>The advance of one character, which for a monospace face is the column width.</summary>
    private static float Pitch(int size, float scale)
    {
        var metrics = ANPRCPanelStyle.Mono(size).GetCharMetrics(new System.Text.Rune('0'), scale);

        return metrics?.Advance ?? size * 0.6f * scale;
    }

    /// <summary>
    ///     Keep the cursor on screen. Called after the rows are rebuilt, because a row appearing or
    ///     going away above the cursor moves it.
    /// </summary>
    public void ScrollToCursor()
    {
        if (Cursor < _scroll + ScrollMargin)
            _scroll = Cursor - ScrollMargin;

        if (Cursor > _scroll + VisibleRows - 1 - ScrollMargin)
            _scroll = Cursor - VisibleRows + 1 + ScrollMargin;

        ClampScroll();
    }

    private void ClampScroll()
    {
        var maximum = Math.Max(0, Rows.Count - VisibleRows);
        _scroll = Math.Clamp(_scroll, 0, maximum);
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        if (Rows.Count <= VisibleRows)
            return;

        _scroll -= (int) args.Delta.Y;
        ClampScroll();
        args.Handle();
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick || Unavailable != null)
            return;

        var scale = UIScale;
        var top = LineHeight(TitleSize, scale) + 2f * scale;
        var pitch = LineHeight(RowSize, scale);

        if (pitch <= 0f)
            return;

        var index = _scroll + (int) ((args.RelativePixelPosition.Y - top) / pitch);

        if (index < 0 || index >= Rows.Count)
            return;

        args.Handle();
        OnRowClicked?.Invoke(index);
    }

    /// <summary>
    ///     Scroll the view without moving the cursor. The preset rocker falls back to this once the
    ///     cursor has run out of lines it can sit on, which is what makes a screen of unselectable
    ///     text - the net log, the key card - readable from the keypad at all.
    /// </summary>
    public bool Scroll(int delta)
    {
        var before = _scroll;

        _scroll += delta;
        ClampScroll();

        return _scroll != before;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var box = PixelSizeBox;

        if (box.Width <= 0 || box.Height <= 0)
            return;

        var palette = ANPRCPanelStyle.Palette(Backlight);
        var scale = UIScale;

        // dead glass shows nothing but the reason it is dead, faintly, the way an unlit panel
        // does. Everything else on the screen needs the backlight behind it to be worth drawing
        if (!Lit)
        {
            if (Unavailable is { } dark)
            {
                var darkFont = ANPRCPanelStyle.Mono(RowSize, true);
                var darkPitch = Pitch(RowSize, scale);
                var darkColumns = Math.Max(1, (int) ((box.Width - 6f * scale) / darkPitch));

                handle.DrawString(
                    darkFont,
                    new Vector2(box.Left + 3f * scale, box.Top + 4f * scale),
                    Truncate(dark, darkColumns),
                    scale,
                    palette.Unlit);
            }

            return;
        }

        var titleFont = ANPRCPanelStyle.Mono(TitleSize, true);
        var rowFont = ANPRCPanelStyle.Mono(RowSize);
        var rowFontBold = ANPRCPanelStyle.Mono(RowSize, true);

        var titleHeight = titleFont.GetLineHeight(scale);
        var rowHeight = rowFont.GetLineHeight(scale);
        var rowPitch = Pitch(RowSize, scale);

        if (rowHeight <= 0f || rowPitch <= 0f)
            return;

        // ----- title bar -------------------------------------------------------------------------

        handle.DrawRect(new UIBox2(box.Left, box.Top, box.Right, box.Top + titleHeight), palette.Grid);
        handle.DrawString(titleFont, new Vector2(box.Left + 2f * scale, box.Top), Title, scale, palette.Bright);

        if (Status.Length > 0)
        {
            var width = Pitch(TitleSize, scale) * Status.Length;

            handle.DrawString(
                titleFont,
                new Vector2(box.Right - 2f * scale - width, box.Top),
                Status,
                scale,
                palette.Mid);
        }

        var top = box.Top + titleHeight + 2f * scale;
        var columns = Math.Max(1, (int) ((box.Width - 6f * scale) / rowPitch));

        VisibleRows = Math.Max(1, (int) ((box.Bottom - top) / rowHeight));

        // ----- a screen that cannot be worked ----------------------------------------------------

        if (Unavailable is { } unavailable)
        {
            handle.DrawString(
                rowFontBold,
                new Vector2(box.Left + 3f * scale, top + rowHeight),
                Truncate(unavailable, columns),
                scale,
                palette.Warn);

            return;
        }

        // ----- rows ------------------------------------------------------------------------------

        ClampScroll();

        var last = Math.Min(Rows.Count, _scroll + VisibleRows);

        for (var i = _scroll; i < last; i++)
        {
            var row = Rows[i];
            var y = top + rowHeight * (i - _scroll);
            var selected = i == Cursor && row.Selectable;

            var colour = row.Armed ? palette.Bad
                : row.Lit ? palette.Bright
                : row.Style switch
                {
                    ANPRCRowStyle.Heading => palette.Mid,
                    ANPRCRowStyle.Note => palette.Dim,
                    ANPRCRowStyle.Good => palette.Bright,
                    ANPRCRowStyle.Warn => palette.Warn,
                    ANPRCRowStyle.Bad => palette.Bad,
                    _ => row.Selectable ? palette.Mid : palette.Dim,
                };

            // the selected line is reversed out, the way a real set marks the field it is on
            if (selected)
            {
                handle.DrawRect(
                    new UIBox2(box.Left + 1f * scale, y, box.Right - 1f * scale, y + rowHeight),
                    palette.Grid);
            }

            var font = row.Style == ANPRCRowStyle.Heading || selected || row.Armed ? rowFontBold : rowFont;
            var line = Compose(row, selected, columns);

            handle.DrawString(font, new Vector2(box.Left + 3f * scale, y), line, scale, colour);
        }

        // ----- more above or below ---------------------------------------------------------------

        if (_scroll > 0)
        {
            handle.DrawString(
                rowFontBold,
                new Vector2(box.Right - 10f * scale, top),
                "^",
                scale,
                palette.Mid);
        }

        if (last < Rows.Count)
        {
            handle.DrawString(
                rowFontBold,
                new Vector2(box.Right - 10f * scale, top + rowHeight * (VisibleRows - 1)),
                "v",
                scale,
                palette.Mid);
        }
    }

    /// <summary>
    ///     One line of the grid: the cursor mark, the label, and the value pushed out to the right
    ///     margin with dots so the eye can follow a setting across to its reading.
    /// </summary>
    private static string Compose(ANPRCScreenRow row, bool selected, int columns)
    {
        var marker = row.Selectable ? selected ? ">" : " " : " ";
        var indent = row.Style == ANPRCRowStyle.Heading ? string.Empty : " ";
        var label = marker + indent + row.Label;

        if (row.Value.Length == 0)
            return Truncate(label, columns);

        // the value always gets its room; the label gives way first
        var room = columns - row.Value.Length - 1;

        if (room < 1)
            return Truncate(row.Value, columns);

        if (label.Length > room)
            label = label[..room];

        var builder = new StringBuilder(columns);
        builder.Append(label);

        while (builder.Length < room)
        {
            // a leader only where there is a gap worth crossing, otherwise it reads as noise
            builder.Append(room - builder.Length > 2 && builder.Length % 2 == 0 ? '.' : ' ');
        }

        builder.Append(' ');
        builder.Append(row.Value);

        return builder.ToString();
    }

    private static string Truncate(string text, int columns)
        => text.Length <= columns ? text : text[..columns];
}
