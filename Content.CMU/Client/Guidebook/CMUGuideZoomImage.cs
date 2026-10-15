using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client.Guidebook.Richtext;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Guidebook;

/// <summary>
///     Guidebook tag for a large image that can be inspected in detail,
///     e.g. &lt;CMUGuideZoomImage Path="/ServerInfo/Guidebook/x.png"/&gt;.
///     Fits the page width; scroll to zoom around the cursor, drag to pan, right-click to reset.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUGuideZoomImage : Control, IDocumentTag
{
    private const float ZoomStep = 1.25f;
    private const float MaxPixelZoom = 2f; // never magnify past 2 screen pixels per image pixel

    [Dependency] private IResourceCache _resources = default!;

    private Texture? _texture;
    private float _zoom = 1f;
    private Vector2 _offset;
    private bool _dragging;
    private Vector2 _lastMouse;

    public CMUGuideZoomImage()
    {
        IoCManager.InjectDependencies(this);
        HorizontalExpand = true;
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
        Margin = new Thickness(0, 4);
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!args.TryGetValue("Path", out var path))
            return false;

        if (!_resources.TryGetResource<TextureResource>(new ResPath(path), out var texture))
            return false;

        _texture = texture;
        control = this;
        return true;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (_texture == null)
            return Vector2.Zero;

        var size = (Vector2) _texture.Size;
        var width = float.IsFinite(availableSize.X) ? availableSize.X : size.X;
        return new Vector2(width, width * size.Y / size.X);
    }

    /// <summary>Screen pixels per image pixel when fully zoomed out.</summary>
    private float BaseScale => _texture == null ? 1f : PixelSize.X / (float) _texture.Size.X;

    private float MaxZoom => MathF.Max(1f, MaxPixelZoom / MathF.Max(BaseScale, 0.0001f));

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_texture == null || PixelSize.X <= 0)
            return;

        ClampOffset();
        var scale = BaseScale * _zoom;
        var shown = new UIBox2(-_offset / scale, (PixelSize - _offset) / scale);
        handle.DrawTextureRectRegion(_texture, PixelSizeBox, shown);
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);
        if (_texture == null || args.Delta.Y == 0)
            return;

        var newZoom = Math.Clamp(_zoom * (args.Delta.Y > 0 ? ZoomStep : 1f / ZoomStep), 1f, MaxZoom);
        var cursor = args.RelativePixelPosition;
        var imagePoint = (cursor - _offset) / (BaseScale * _zoom);
        _zoom = newZoom;
        _offset = cursor - imagePoint * BaseScale * _zoom;
        ClampOffset();
        args.Handle();
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function == EngineKeyFunctions.UIClick)
        {
            _dragging = true;
            _lastMouse = args.RelativePixelPosition;
            args.Handle();
        }
        else if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            _zoom = 1f;
            _offset = Vector2.Zero;
            args.Handle();
        }
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function == EngineKeyFunctions.UIClick)
            _dragging = false;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (!_dragging)
            return;

        _offset += args.RelativePixelPosition - _lastMouse;
        _lastMouse = args.RelativePixelPosition;
        ClampOffset();
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _dragging = false;
    }

    /// <summary>Keeps the image covering the whole control so no empty space shows at the edges.</summary>
    private void ClampOffset()
    {
        if (_texture == null)
            return;

        _zoom = Math.Clamp(_zoom, 1f, MaxZoom);
        var shown = (Vector2) _texture.Size * BaseScale * _zoom;
        _offset = Vector2.Clamp(_offset, PixelSize - shown, Vector2.Zero);
    }
}
