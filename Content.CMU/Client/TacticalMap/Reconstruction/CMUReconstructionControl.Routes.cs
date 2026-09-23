using System.Numerics;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client.CMU14.TacticalMap.Reconstruction;

public sealed partial class CMUReconstructionControl
{
    [Dependency] private IInputManager _input = default!;
    private readonly List<Vector2> _stroke = [];
    private readonly List<Vector2> _strokeVertices = [];
    private readonly List<Vector2> _projectedStroke = [];
    private bool _drawing;
    private Vector2 _lastSample;
    public bool DrawingEnabled { get; set; }
    public bool TextEnabled { get; set; }
    public float StrokeWidth { get; set; } = 3;
    public CMUReconInk Ink { get; set; } = CMUReconInk.Yellow;
    public Action<Vector2[], int, CMUReconInk>? OnStroke;
    public Action<Vector2, int>? OnTextPoint;

    public static Color InkColor(CMUReconInk ink) => Color.FromHex(ink switch
    {
        CMUReconInk.Red => "#FF7775",
        CMUReconInk.Blue => "#76BFFF",
        CMUReconInk.Green => "#8FE5A2",
        CMUReconInk.White => "#F2EEDF",
        CMUReconInk.Purple => "#CD9DF5",
        _ => "#F5D47D",
    });

    public static string OrderText(CMUReconOrder order) => Loc.GetString(order.Kind switch
    {
        CMUReconOrderKind.Move => "cmu-recon-move-marker",
        CMUReconOrderKind.Route => "cmu-recon-drawing-marker",
        _ => "cmu-recon-rally-marker",
    }, ("id", order.Id));

    private void DrawStroke(DrawingHandleScreen handle, IReadOnlyList<Vector2> stroke, Color color, float width)
    {
        if (Scene is not { } scene || stroke.Count == 0) return;
        Vector2 Point(Vector2 world) => Project(new Vector3(world - (Vector2) scene.Origin,
            _selectedLevel * CMUReconGeometry.LevelHeight + 0.3f));
        if (stroke.Count == 1)
        {
            handle.DrawCircle(Point(stroke[0]), width * 0.5f * UIScale, color);
            return;
        }
        // Several translucent graphite-like strands follow the actual mouse path. No nodes or arrows.
        _projectedStroke.Clear();
        foreach (var point in stroke) _projectedStroke.Add(Point(point));
        var strands = Math.Max(1, (int) MathF.Ceiling(width * UIScale));
        for (var strand = 0; strand < strands; strand++)
        {
            _strokeVertices.Clear();
            for (var i = 0; i < stroke.Count; i++)
            {
                var point = _projectedStroke[i];
                var tangent = _projectedStroke[Math.Min(i + 1, stroke.Count - 1)] - _projectedStroke[Math.Max(0, i - 1)];
                var normal = tangent.LengthSquared() > 0.001f ? Vector2.Normalize(new Vector2(-tangent.Y, tangent.X)) : Vector2.UnitY;
                var grain = (strand - (strands - 1) * 0.5f) * (0.85f + 0.15f * MathF.Sin(i * 2.4f));
                _strokeVertices.Add(point + normal * grain);
            }
            handle.DrawPrimitives(DrawPrimitiveTopology.LineStrip, _strokeVertices,
                new Color(color.R, color.G, color.B, strand == 0 || strand == strands - 1 ? 0.55f : 0.95f));
        }
    }

    private void DrawTextMarker(DrawingHandleScreen handle, Vector2 world, Color color, string text)
    {
        if (Scene is not { } scene) return;
        var point = Project(new Vector3(world - (Vector2) scene.Origin, _selectedLevel * CMUReconGeometry.LevelHeight + 0.3f));
        if (!PixelSizeBox.Contains(new Vector2i((int) point.X, (int) point.Y))) return;
        var lines = new List<string>();
        for (var start = 0; start < text.Length; start += 30)
            lines.Add(text.Substring(start, Math.Min(30, text.Length - start)));
        var width = 0f;
        foreach (var line in lines) width = Math.Max(width, handle.GetDimensions(_font, line, 1).X);
        var size = new Vector2(width + 16, lines.Count * 18 + 12);
        var position = Vector2.Clamp(point + new Vector2(12, -size.Y - 12), Vector2.Zero, Vector2.Max(Vector2.Zero, (Vector2) PixelSize - size));
        handle.DrawCircle(point, 4 * UIScale, color);
        handle.DrawLine(point, position + new Vector2(4, size.Y), color);
        handle.DrawRect(UIBox2.FromDimensions(position, size), Color.FromHex("#0B1826EE"));
        handle.DrawRect(UIBox2.FromDimensions(position, size), color, false);
        for (var i = 0; i < lines.Count; i++) handle.DrawString(_font, position + new Vector2(8, 6 + i * 18), lines[i], color);
    }

    /// <summary>Intersect the chosen floor's drawing plane, including walls, props and empty terrain.</summary>
    public bool TryDrawingPoint(Vector2 relativePosition, out Vector2 point)
    {
        point = default;
        if (Scene is not { } scene || PixelWidth <= 0 || PixelHeight <= 0) return false;
        var pixel = relativePosition * UIScale;
        var nx = (pixel.X / PixelWidth * 2 - 1) * PixelWidth / PixelHeight;
        var ny = 1 - pixel.Y / PixelHeight * 2;
        Camera(out var origin, out var forward, out var right, out var up);
        var direction = forward;
        if (_overhead) origin += (right * nx + up * ny) * (_distance * 0.45f);
        else direction = Vector3.Normalize(forward + (right * nx + up * ny) * 0.45f);
        if (MathF.Abs(direction.Z) < 0.0001f) return false;
        var distance = (_selectedLevel * CMUReconGeometry.LevelHeight + 0.3f - origin.Z) / direction.Z;
        if (distance < 0) return false;
        var hit = origin + direction * distance;
        point = new Vector2(hit.X, hit.Y) + (Vector2) scene.Origin;
        return hit.X >= 0 && hit.Y >= 0 && hit.X <= scene.Width && hit.Y <= scene.Height;
    }

    private void SampleStroke(Vector2 position, bool force = false)
    {
        if (!_drawing || !DrawingEnabled || !force && Vector2.DistanceSquared(position, _lastSample) < 4) return;
        if (!TryDrawingPoint(position, out var point))
        {
            FinishStroke();
            return;
        }
        _lastSample = position;
        CMUReconRoutes.AddPoint(_stroke, point);
    }

    public void FinishStroke()
    {
        if (!_drawing) return;
        _drawing = false;
        if (DrawingEnabled && Scene is { } scene && _stroke.Count > 0)
            OnStroke?.Invoke(_stroke.ToArray(), scene.MinDepth + _selectedLevel, Ink);
        _stroke.Clear();
    }

    public void CancelStroke() { _drawing = false; _stroke.Clear(); }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _input.FirstChanceOnKeyEvent += OnMiddleMouse;
    }

    // UI has no middle-click binding. Consume the raw button only over this map; never rebind gameplay.
    private void OnMiddleMouse(KeyEventArgs args, KeyEventType type)
    {
        if (args.Key != Keyboard.Key.MouseMiddle) return;
        if (type == KeyEventType.Up && _rotating)
        {
            _rotating = false;
            args.Handle();
        }
        else if (type == KeyEventType.Down && !args.Handled && UserInterfaceManager.CurrentlyHovered == this)
        {
            FinishStroke();
            _panning = false;
            _rotating = true;
            _lastMouse = UserInterfaceManager.MousePositionScaled.Position - GlobalPosition;
            args.Handle();
        }
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick && args.Function != EngineKeyFunctions.UIRightClick) return;
        _lastMouse = args.RelativePosition;
        if (!_rotating && args.Function == EngineKeyFunctions.UIClick && TextEnabled && Scene is { } scene)
        {
            if (TryDrawingPoint(args.RelativePosition, out var point)) OnTextPoint?.Invoke(point, scene.MinDepth + _selectedLevel);
        }
        else if (!_rotating && args.Function == EngineKeyFunctions.UIClick && DrawingEnabled)
        {
            _drawing = true;
            _stroke.Clear();
            SampleStroke(args.RelativePosition, true);
        }
        else if (!_rotating)
        {
            FinishStroke();
            _panning = true;
        }
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick && args.Function != EngineKeyFunctions.UIRightClick) return;
        if (args.Function == EngineKeyFunctions.UIClick)
        {
            SampleStroke(args.RelativePosition, true);
            FinishStroke();
        }
        _panning = false;
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        var delta = args.RelativePosition - _lastMouse;
        _lastMouse = args.RelativePosition;
        if (_drawing) SampleStroke(args.RelativePosition);
        else if (_rotating)
        {
            _overhead = false;
            _yaw -= delta.X * 0.009f;
            _pitch = Math.Clamp(_pitch + delta.Y * 0.006f, 0.2f, 1.45f);
            _fit = false;
            _redraw = true;
        }
        else if (_panning)
        {
            Camera(out _, out var forward, out var right, out var up);
            var movement = (-right * delta.X + up * delta.Y) * (_distance * 0.9f / Math.Max(1, Height));
            if (!_overhead && MathF.Abs(forward.Z) > 0.01f) movement -= forward * (movement.Z / forward.Z);
            Pan(new Vector2(movement.X, movement.Y));
        }
        else return;
        args.Handle();
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        FinishStroke();
        _panning = false;
        _rotating = false;
    }
}
