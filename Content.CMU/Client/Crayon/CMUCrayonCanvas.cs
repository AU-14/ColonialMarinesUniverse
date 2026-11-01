using System.Numerics;
using Content.Shared.CMU14.Crayon;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client.CMU14.Crayon;

public enum CMUCrayonTool : byte
{
    Brush,
    Fill,
    Line,
    Circle,
    Gradient,
}

[Flags]
public enum CMUCrayonMirror : byte
{
    None = 0,
    X = 1,
    Y = 2,
    Both = X | Y,
}

public sealed class CMUCrayonCanvas : Control
{
    private static readonly Color Background = Color.FromHex("#2A2A2A");
    private static readonly Color GridLine = Color.FromHex("#3A3A3A");
    private static readonly Color MirrorLine = Color.Cyan.WithAlpha(0.6f);

    public const int MaxBrushSize = 4;

    public int Size { get; private set; } = CMUCrayonSymbol.Small;
    public Color? Brush = Color.White;
    public CMUCrayonTool Tool;
    public CMUCrayonMirror Mirror;
    public int BrushSize = 1;

    /// <summary>
    /// The colours a gradient steps through, from where it is dragged from to where it is dragged to.
    /// </summary>
    public List<Color> Gradient = new();

    private Color?[] _cells = new Color?[CMUCrayonSymbol.Small * CMUCrayonSymbol.Small];
    private bool _painting;
    private Vector2i? _start;
    private Vector2i _end;

    // Lines, circles and gradients show where they will go while they are dragged.
    private Dictionary<int, Color?> _preview = new();

    public event Action? OnChanged;

    public CMUCrayonCanvas()
    {
        MouseFilter = MouseFilterMode.Stop;
        MinSize = SetSize = new Vector2(384, 384);
    }

    public void Resize(int size)
    {
        var cells = new Color?[size * size];
        var keep = Math.Min(size, Size);
        for (var y = 0; y < keep; y++)
        {
            for (var x = 0; x < keep; x++)
            {
                cells[y * size + x] = _cells[y * Size + x];
            }
        }

        Size = size;
        _cells = cells;
        CancelShape();
        OnChanged?.Invoke();
    }

    public void Clear()
    {
        Array.Clear(_cells);
        OnChanged?.Invoke();
    }

    public void Load(CMUCrayonSymbol symbol)
    {
        Size = symbol.Size;
        _cells = new Color?[Size * Size];
        for (var i = 0; i < _cells.Length; i++)
        {
            var pixel = symbol.Pixels[i];
            _cells[i] = pixel == 0 ? null : symbol.Palette[pixel - 1];
        }

        CancelShape();
        OnChanged?.Invoke();
    }

    public HashSet<Color> Colors()
    {
        var colors = new HashSet<Color>();
        foreach (var cell in _cells)
        {
            if (cell is { } color)
                colors.Add(color);
        }

        return colors;
    }

    public CMUCrayonSymbol ToSymbol()
    {
        var symbol = new CMUCrayonSymbol { Size = Size, Pixels = new byte[_cells.Length] };
        for (var i = 0; i < _cells.Length; i++)
        {
            if (_cells[i] is not { } color)
                continue;

            var index = symbol.Palette.IndexOf(color);
            if (index == -1)
            {
                symbol.Palette.Add(color);
                index = symbol.Palette.Count - 1;
            }

            symbol.Pixels[i] = (byte) Math.Min(index + 1, byte.MaxValue);
        }

        return symbol;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var cell = PixelSize.X / (float) Size;
        handle.DrawRect(PixelSizeBox, Background);
        for (var i = 0; i < _cells.Length; i++)
        {
            var color = _preview.TryGetValue(i, out var preview) ? preview : _cells[i];
            if (color != null)
                handle.DrawRect(UIBox2.FromDimensions(new Vector2(i % Size, i / Size) * cell, new Vector2(cell, cell)), color.Value);
        }

        for (var i = 0; i <= Size; i += Size / 8)
        {
            handle.DrawLine(new Vector2(i * cell, 0), new Vector2(i * cell, PixelSize.Y), GridLine);
            handle.DrawLine(new Vector2(0, i * cell), new Vector2(PixelSize.X, i * cell), GridLine);
        }

        var middle = PixelSize / 2f;
        if ((Mirror & CMUCrayonMirror.X) != 0)
            handle.DrawLine(new Vector2(middle.X, 0), new Vector2(middle.X, PixelSize.Y), MirrorLine);

        if ((Mirror & CMUCrayonMirror.Y) != 0)
            handle.DrawLine(new Vector2(0, middle.Y), new Vector2(PixelSize.X, middle.Y), MirrorLine);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick || CellAt(args.RelativePixelPosition) is not { } cell)
            return;

        switch (Tool)
        {
            case CMUCrayonTool.Fill:
                Commit(Fill(cell));
                break;
            case CMUCrayonTool.Brush:
                _painting = true;
                Commit(Dab(cell, new Dictionary<int, Color?>()));
                break;
            default:
                _start = cell;
                _end = cell;
                _preview = Shape(cell, cell);
                break;
        }
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _painting = false;
        if (_start is not { } start)
            return;

        if (CellAt(args.RelativePixelPosition) is { } end)
            _end = end;

        var shape = Shape(start, _end);
        CancelShape();
        Commit(shape);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (CellAt(args.RelativePixelPosition) is not { } cell)
            return;

        if (_painting)
        {
            Commit(Dab(cell, new Dictionary<int, Color?>()));
        }
        else if (_start is { } start && cell != _end)
        {
            _end = cell;
            _preview = Shape(start, cell);
        }
    }

    private Vector2i? CellAt(Vector2 pixel)
    {
        var cell = (pixel / (PixelSize.X / (float) Size)).Floored();
        return cell.X < 0 || cell.Y < 0 || cell.X >= Size || cell.Y >= Size ? null : cell;
    }

    private void CancelShape()
    {
        _start = null;
        _painting = false;
        _preview = new Dictionary<int, Color?>();
    }

    private void Commit(Dictionary<int, Color?> cells)
    {
        var changed = false;
        foreach (var (index, color) in cells)
        {
            if (_cells[index] == color)
                continue;

            _cells[index] = color;
            changed = true;
        }

        if (changed)
            OnChanged?.Invoke();
    }

    private Dictionary<int, Color?> Shape(Vector2i start, Vector2i end)
    {
        var cells = new Dictionary<int, Color?>();
        switch (Tool)
        {
            case CMUCrayonTool.Line:
                Line(start, end, cells);
                break;
            case CMUCrayonTool.Circle:
                Circle(start, end, cells);
                break;
            case CMUCrayonTool.Gradient:
                GradientFill(start, end, cells);
                break;
        }

        return cells;
    }

    /// <summary>
    /// One cell, and the cells the mirror reflects it to.
    /// </summary>
    private void Put(Dictionary<int, Color?> cells, int x, int y, Color? color)
    {
        if (x < 0 || y < 0 || x >= Size || y >= Size)
            return;

        var far = Size - 1;
        cells[y * Size + x] = color;
        if ((Mirror & CMUCrayonMirror.X) != 0)
            cells[y * Size + far - x] = color;

        if ((Mirror & CMUCrayonMirror.Y) != 0)
            cells[(far - y) * Size + x] = color;

        if (Mirror == CMUCrayonMirror.Both)
            cells[(far - y) * Size + far - x] = color;
    }

    /// <summary>
    /// The brush's square, its size across, put down on a cell.
    /// </summary>
    private Dictionary<int, Color?> Dab(Vector2i at, Dictionary<int, Color?> cells)
    {
        var offset = (BrushSize - 1) / 2;
        for (var y = 0; y < BrushSize; y++)
        {
            for (var x = 0; x < BrushSize; x++)
            {
                Put(cells, at.X + x - offset, at.Y + y - offset, Brush);
            }
        }

        return cells;
    }

    private void Line(Vector2i from, Vector2i to, Dictionary<int, Color?> cells)
    {
        var dx = Math.Abs(to.X - from.X);
        var dy = -Math.Abs(to.Y - from.Y);
        var sx = from.X < to.X ? 1 : -1;
        var sy = from.Y < to.Y ? 1 : -1;
        var error = dx + dy;
        var x = from.X;
        var y = from.Y;
        while (true)
        {
            Dab(new Vector2i(x, y), cells);
            if (x == to.X && y == to.Y)
                return;

            var twice = 2 * error;
            if (twice >= dy)
            {
                error += dy;
                x += sx;
            }

            if (twice <= dx)
            {
                error += dx;
                y += sy;
            }
        }
    }

    /// <summary>
    /// A ring round the first cell, through the second.
    /// </summary>
    private void Circle(Vector2i centre, Vector2i edge, Dictionary<int, Color?> cells)
    {
        var radius = (int) MathF.Round(((Vector2) (edge - centre)).Length());
        var x = radius;
        var y = 0;
        var error = 1 - radius;
        while (x >= y)
        {
            Dab(new Vector2i(centre.X + x, centre.Y + y), cells);
            Dab(new Vector2i(centre.X + y, centre.Y + x), cells);
            Dab(new Vector2i(centre.X - y, centre.Y + x), cells);
            Dab(new Vector2i(centre.X - x, centre.Y + y), cells);
            Dab(new Vector2i(centre.X - x, centre.Y - y), cells);
            Dab(new Vector2i(centre.X - y, centre.Y - x), cells);
            Dab(new Vector2i(centre.X + y, centre.Y - x), cells);
            Dab(new Vector2i(centre.X + x, centre.Y - y), cells);

            y++;
            if (error < 0)
            {
                error += 2 * y + 1;
            }
            else
            {
                x--;
                error += 2 * (y - x) + 1;
            }
        }
    }

    /// <summary>
    /// The area of one colour under the first cell, shaded through the gradient's colours along the
    /// drag from the first cell to the second.
    /// </summary>
    private void GradientFill(Vector2i start, Vector2i end, Dictionary<int, Color?> cells)
    {
        if (Gradient.Count == 0)
            return;

        var axis = (Vector2) (end - start);
        var length = axis.LengthSquared();
        foreach (var at in Region(start))
        {
            var t = length < 0.0001f ? 0f : Math.Clamp(Vector2.Dot((Vector2) (at - start), axis) / length, 0f, 1f);
            Put(cells, at.X, at.Y, Gradient[(int) MathF.Round(t * (Gradient.Count - 1))]);
        }
    }

    private Dictionary<int, Color?> Fill(Vector2i origin)
    {
        var cells = new Dictionary<int, Color?>();
        foreach (var at in Region(origin))
        {
            Put(cells, at.X, at.Y, Brush);
        }

        return cells;
    }

    /// <summary>
    /// The cells joined to this one that share its colour.
    /// </summary>
    private List<Vector2i> Region(Vector2i origin)
    {
        var region = new List<Vector2i>();
        var target = _cells[origin.Y * Size + origin.X];
        var seen = new HashSet<Vector2i>();
        var open = new Stack<Vector2i>();
        open.Push(origin);
        while (open.TryPop(out var at))
        {
            if (at.X < 0 || at.Y < 0 || at.X >= Size || at.Y >= Size ||
                !seen.Add(at) ||
                _cells[at.Y * Size + at.X] != target)
            {
                continue;
            }

            region.Add(at);
            open.Push(at + Vector2i.Right);
            open.Push(at + Vector2i.Left);
            open.Push(at + Vector2i.Up);
            open.Push(at + Vector2i.Down);
        }

        return region;
    }
}
