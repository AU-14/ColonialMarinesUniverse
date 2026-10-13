using System.Numerics;
using Content.Shared.CMU14.Crayon;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client.CMU14.Crayon;

public sealed class CMUCrayonCanvas : Control
{
    private static readonly Color Background = Color.FromHex("#2A2A2A");
    private static readonly Color GridLine = Color.FromHex("#3A3A3A");

    public int Size { get; private set; } = CMUCrayonSymbol.Small;
    public Color? Brush = Color.White;
    public bool Fill;

    private Color?[] _cells = new Color?[CMUCrayonSymbol.Small * CMUCrayonSymbol.Small];
    private bool _painting;

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
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                if (_cells[y * Size + x] is { } color)
                    handle.DrawRect(UIBox2.FromDimensions(new Vector2(x, y) * cell, new Vector2(cell, cell)), color);
            }
        }

        for (var i = 0; i <= Size; i += Size / 8)
        {
            handle.DrawLine(new Vector2(i * cell, 0), new Vector2(i * cell, PixelSize.Y), GridLine);
            handle.DrawLine(new Vector2(0, i * cell), new Vector2(PixelSize.X, i * cell), GridLine);
        }
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (Fill)
        {
            FloodFill(CellAt(args.RelativePixelPosition));
            return;
        }

        _painting = true;
        Paint(args.RelativePixelPosition);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function == EngineKeyFunctions.UIClick)
            _painting = false;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (_painting)
            Paint(args.RelativePixelPosition);
    }

    private Vector2i? CellAt(Vector2 pixel)
    {
        var cell = (pixel / (PixelSize.X / (float) Size)).Floored();
        return cell.X < 0 || cell.Y < 0 || cell.X >= Size || cell.Y >= Size ? null : cell;
    }

    private void Paint(Vector2 pixel)
    {
        if (CellAt(pixel) is not { } cell || _cells[cell.Y * Size + cell.X] == Brush)
            return;

        _cells[cell.Y * Size + cell.X] = Brush;
        OnChanged?.Invoke();
    }

    private void FloodFill(Vector2i? start)
    {
        if (start is not { } origin)
            return;

        var target = _cells[origin.Y * Size + origin.X];
        if (target == Brush)
            return;

        var open = new Stack<Vector2i>();
        open.Push(origin);
        while (open.TryPop(out var at))
        {
            if (at.X < 0 || at.Y < 0 || at.X >= Size || at.Y >= Size || _cells[at.Y * Size + at.X] != target)
                continue;

            _cells[at.Y * Size + at.X] = Brush;
            open.Push(at + Vector2i.Right);
            open.Push(at + Vector2i.Left);
            open.Push(at + Vector2i.Up);
            open.Push(at + Vector2i.Down);
        }

        OnChanged?.Invoke();
    }
}
