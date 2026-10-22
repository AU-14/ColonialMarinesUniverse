using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Shared.CMU14.Vehicle.Open;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.Utility;
using Robust.Shared.ContentPack;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Input;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// A vehicle manual's picture: the vehicle drawn from its own art in one facing, its parts picked out
/// by the click maps the game uses for them. Hovering a part outlines it; clicking it selects it.
/// Where parts overlap, the smallest is the one under the cursor, so a lamp on a door is the lamp.
/// </summary>
public sealed class CMUVehicleManualDiagram : Control
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IResourceManager _resources = default!;

    // RSI direction order: the frames of each state are laid out south, north, east, west.
    public static readonly Direction[] Facings = [Direction.South, Direction.North, Direction.East, Direction.West];

    private readonly CMUVehicleManualPrototype _manual;
    private readonly Vector2i _size;
    private readonly Dictionary<string, Image<Rgba32>?> _images = new();
    private readonly Dictionary<(int Facing, bool Open), Texture> _art = new();
    private readonly Dictionary<(string Entry, int Facing, bool Open), bool[]?> _masks = new();

    private Texture? _marks;
    private bool _marksStale = true;
    private int _facing;
    private bool _open;
    private string? _hovered;
    private string? _selected;

    /// <summary>
    /// A part was clicked on the picture.
    /// </summary>
    public event Action<string>? PartClicked;

    public CMUVehicleManualDiagram(CMUVehicleManualPrototype manual)
    {
        IoCManager.InjectDependencies(this);
        _manual = manual;
        _size = _cache.GetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / manual.Sprite).RSI.Size;
        MouseFilter = MouseFilterMode.Stop;
        MinSize = new Vector2(_size.X, _size.Y) * 3;
    }

    public int Facing
    {
        get => _facing;
        set
        {
            _facing = value;
            _hovered = null;
            _marksStale = true;
        }
    }

    public bool Open
    {
        get => _open;
        set
        {
            _open = value;
            _hovered = null;
            _marksStale = true;
        }
    }

    public string? Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            _marksStale = true;
        }
    }

    /// <summary>
    /// Whether a part can be clicked on the picture in this facing, with the panels as shown.
    /// </summary>
    public bool Shows(CMUVehicleManualEntry entry)
        => Mask(entry, _facing, _open) is not null;

    protected override void Draw(DrawingHandleScreen handle)
    {
        var (origin, scale) = Fit();
        if (scale <= 0)
            return;

        var rect = UIBox2.FromDimensions(origin, new Vector2(_size.X, _size.Y) * scale);
        handle.DrawTextureRect(Art(_facing, _open), rect);
        if (_marksStale)
            RebuildMarks();

        if (_marks != null)
            handle.DrawTextureRect(_marks, rect);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        var hovered = PartAt(args.RelativePixelPosition);
        if (hovered == _hovered)
            return;

        _hovered = hovered;
        _marksStale = true;
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        if (_hovered == null)
            return;

        _hovered = null;
        _marksStale = true;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick || PartAt(args.RelativePixelPosition) is not { } part)
            return;

        args.Handle();
        PartClicked?.Invoke(part);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        foreach (var texture in _art.Values)
        {
            (texture as OwnedTexture)?.Dispose();
        }

        _art.Clear();
        (_marks as OwnedTexture)?.Dispose();
        _marks = null;
        _marksStale = true;
        foreach (var image in _images.Values)
        {
            image?.Dispose();
        }

        _images.Clear();
    }

    /// <summary>
    /// Where the art sits in the control and how many screen pixels each art pixel takes, whole
    /// pixels only so the pixel art stays crisp.
    /// </summary>
    private (Vector2 Origin, float Scale) Fit()
    {
        var scale = MathF.Floor(MathF.Min(PixelSize.X / (float) _size.X, PixelSize.Y / (float) _size.Y));
        var drawn = new Vector2(_size.X, _size.Y) * scale;
        return ((PixelSize - drawn) / 2f, scale);
    }

    /// <summary>
    /// The part under a point of the control, in pixels: of the parts whose click maps cover it, the
    /// one covering the least.
    /// </summary>
    private string? PartAt(Vector2 pixel)
    {
        var (origin, scale) = Fit();
        if (scale <= 0)
            return null;

        var art = (pixel - origin) / scale;
        var x = (int) MathF.Floor(art.X);
        var y = (int) MathF.Floor(art.Y);
        if (x < 0 || y < 0 || x >= _size.X || y >= _size.Y)
            return null;

        string? best = null;
        var bestArea = int.MaxValue;
        foreach (var entry in _manual.Entries)
        {
            if (Mask(entry, _facing, _open) is not { } mask || !mask[y * _size.X + x])
                continue;

            var area = Area(mask);
            if (area >= bestArea)
                continue;

            best = entry.Id;
            bestArea = area;
        }

        return best;
    }

    private static int Area(bool[] mask)
    {
        var area = 0;
        foreach (var set in mask)
        {
            if (set)
                area++;
        }

        return area;
    }

    private bool[]? Mask(CMUVehicleManualEntry entry, int facing, bool open)
    {
        if (_masks.TryGetValue((entry.Id, facing, open), out var cached))
            return cached;

        var states = open ? entry.OpenMasks ?? entry.Masks : entry.Masks;
        bool[]? mask = null;
        foreach (var state in states)
        {
            if (Frame(state, facing) is not { } frame)
                continue;

            for (var i = 0; i < frame.Length; i++)
            {
                if (frame[i].A == 0)
                    continue;

                mask ??= new bool[frame.Length];
                mask[i] = true;
            }
        }

        _masks[(entry.Id, facing, open)] = mask;
        return mask;
    }

    private Texture Art(int facing, bool open)
    {
        if (_art.TryGetValue((facing, open), out var texture))
            return texture;

        using var image = new Image<Rgba32>(_size.X, _size.Y);
        var pixels = image.GetPixelSpan();
        foreach (var layer in _manual.Layers)
        {
            Blend(pixels, State(layer, open), facing, 0);
        }

        var lift = _manual.OverlayLift.GetValueOrDefault(Facings[facing]);
        foreach (var layer in _manual.OverlayLayers)
        {
            Blend(pixels, State(layer, open), facing, lift);
        }

        texture = _clyde.LoadTextureFromImage(image);
        _art[(facing, open)] = texture;
        return texture;
    }

    private string State(string state, bool open)
        => open && _manual.OpenStates.TryGetValue(state, out var swapped) ? swapped : state;

    /// <summary>
    /// Draws a state's frame over the picture, lifted the given number of pixels.
    /// </summary>
    private void Blend(Span<Rgba32> into, string state, int facing, int lift)
    {
        if (Frame(state, facing) is not { } frame)
            return;

        for (var y = 0; y < _size.Y; y++)
        {
            var to = y - lift;
            if (to < 0 || to >= _size.Y)
                continue;

            for (var x = 0; x < _size.X; x++)
            {
                var src = frame[y * _size.X + x];
                if (src.A == 0)
                    continue;

                ref var dst = ref into[to * _size.X + x];
                var a = src.A / 255f;
                var keep = dst.A / 255f * (1f - a);
                var outA = a + keep;
                dst = new Rgba32(
                    (byte) ((src.R * a + dst.R * keep) / outA),
                    (byte) ((src.G * a + dst.G * keep) / outA),
                    (byte) ((src.B * a + dst.B * keep) / outA),
                    (byte) (outA * 255f));
            }
        }
    }

    /// <summary>
    /// A state's first frame in a facing, read from the RSI's sheet; one-direction states give their
    /// only frame for every facing.
    /// </summary>
    private Rgba32[]? Frame(string state, int facing)
    {
        var path = SpriteSpecifierSerializer.TextureRoot / _manual.Sprite;
        var rsi = _cache.GetResource<RSIResource>(path).RSI;
        if (!rsi.TryGetState(state, out var info) || Sheet(path / $"{state}.png") is not { } sheet)
            return null;

        var directional = info.RsiDirections != RsiDirectionType.Dir1;
        var index = (directional ? facing : 0) * info.DelayCount;
        var columns = sheet.Width / _size.X;
        var left = index % columns * _size.X;
        var top = index / columns * _size.Y;
        if (top + _size.Y > sheet.Height)
            return null;

        var pixels = sheet.GetPixelSpan();
        var frame = new Rgba32[_size.X * _size.Y];
        for (var y = 0; y < _size.Y; y++)
        {
            pixels.Slice((top + y) * sheet.Width + left, _size.X).CopyTo(frame.AsSpan(y * _size.X, _size.X));
        }

        return frame;
    }

    private Image<Rgba32>? Sheet(ResPath path)
    {
        if (_images.TryGetValue(path.ToString(), out var image))
            return image;

        image = null;
        if (_resources.TryContentFileRead(path, out var stream))
        {
            using (stream)
            {
                image = Image.Load<Rgba32>(stream);
            }
        }

        _images[path.ToString()] = image;
        return image;
    }

    /// <summary>
    /// The outlines over the art: highlighted parts always, the part under the cursor, and the
    /// selected one filled in.
    /// </summary>
    private void RebuildMarks()
    {
        _marksStale = false;
        (_marks as OwnedTexture)?.Dispose();
        _marks = null;

        using var image = new Image<Rgba32>(_size.X, _size.Y);
        var pixels = image.GetPixelSpan();
        var any = false;
        foreach (var entry in _manual.Entries)
        {
            var hovered = entry.Id == _hovered;
            var selected = entry.Id == _selected;
            if (!hovered && !selected && !entry.Highlight || Mask(entry, _facing, _open) is not { } mask)
                continue;

            var colour = entry.Highlight ? CrtTerminalPalette.Caution : CrtTerminalPalette.Accent;
            var edgeAlpha = selected || hovered ? 1f : 0.55f;
            var fillAlpha = selected ? 0.35f : hovered ? 0.18f : 0f;
            Mark(pixels, mask, colour, edgeAlpha, fillAlpha);
            any = true;
        }

        if (any)
            _marks = _clyde.LoadTextureFromImage(image);
    }

    /// <summary>
    /// Fills a part's area and rings it with the art pixels just outside it.
    /// </summary>
    private void Mark(Span<Rgba32> into, bool[] mask, Color colour, float edgeAlpha, float fillAlpha)
    {
        var w = _size.X;
        var h = _size.Y;
        var edge = new Rgba32((byte) (colour.R * 255), (byte) (colour.G * 255), (byte) (colour.B * 255), (byte) (edgeAlpha * 255));
        var fill = new Rgba32(edge.R, edge.G, edge.B, (byte) (fillAlpha * 255));
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var i = y * w + x;
                if (mask[i])
                {
                    if (fillAlpha > 0f && into[i].A < fill.A)
                        into[i] = fill;

                    continue;
                }

                if (x > 0 && mask[i - 1] || x < w - 1 && mask[i + 1] || y > 0 && mask[i - w] || y < h - 1 && mask[i + w])
                    into[i] = edge;
            }
        }
    }
}
