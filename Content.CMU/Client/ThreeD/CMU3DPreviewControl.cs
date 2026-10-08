using System.Numerics;
using Content.Shared.CMU14.ThreeD;
using Content.Client.CMU14.ThreeD.Scene;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.ThreeD;

public sealed class CMU3DPreviewControl : Control
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    private readonly CMU3DModelRenderer _renderer = new();
    private readonly CMU3DCamera _camera = new();
    private bool _dragging;
    private Vector2 _lastMouse;
    private bool _fit = true;
    private CMU3DSceneControl? _roundedView;
    private bool _usesRounded;
    private int _partLimit = 128;
    private Vector3 _previewMin;
    private Vector3 _previewMax;

    public bool ShowEdges { get; set; }
    public int PartCount => _renderer.PartCount;
    public bool WithinBudget => _usesRounded ? _roundedView is { OmittedBoxes: 0 } && PartCount <= _partLimit : _renderer.WithinBudget;
    public bool SupportsEdges => !_usesRounded;

    public CMU3DPreviewControl()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
    }

    public void SetModel(CMU3DModelPrototype? model)
    {
        _renderer.SetModel(model);
        _previewMin = _renderer.Min;
        _previewMax = _renderer.Max;
        _partLimit = model?.EquipmentOnly == true ? 512 : 128;
        _usesRounded = model?.EquipmentOnly == true || model?.Parts.Exists(part => part.Shape != CMU3DPartShape.Box || part.Surface != null || part.Yaw != 0 || part.Pitch != 0) == true;
        if (_usesRounded)
        {
            if (_roundedView == null)
            {
                _roundedView = new CMU3DSceneControl { MouseFilter = MouseFilterMode.Ignore, SetSize = Size };
                AddChild(_roundedView);
            }
            _roundedView.Visible = true;
            var extent = _renderer.Max - _renderer.Min;
            // A uniformly scaled review copy fits the shader's bounded grid. Authored bounds stay untouched.
            var longest = Math.Max(extent.X, Math.Max(extent.Y, extent.Z));
            // Enlarge tiny garment voxels for review so they do not crowd one spatial cell.
            var scale = 14 / Math.Max(model!.EquipmentOnly ? .01f : 14, longest);
            var origin = new Vector3((_renderer.Min.X + _renderer.Max.X) / 2,
                (_renderer.Min.Y + _renderer.Max.Y) / 2, _renderer.Min.Z);
            _previewMin = (_renderer.Min - origin) * scale;
            _previewMax = (_renderer.Max - origin) * scale;
            var solids = new List<CMU3DSceneBox>();
            foreach (var part in model!.Parts)
            {
                if (part.Valid)
                    solids.Add(new CMU3DSceneBox(((part.Min + part.Max) / 2 - origin) * scale,
                        (part.Max - part.Min) / 2 * scale, part.YawRadians, part.Color, Shape: part.Shape,
                        SurfaceIndex: part.Surface is { } surfaceId ? _prototypes.Index(surfaceId).AtlasIndex : (byte) 0, SurfaceAxis: part.SurfaceAxis, SurfaceFlipU: part.SurfaceFlipU) { Pitch = part.PitchRadians });
            }
            _roundedView.SetScene(solids);
        }
        else if (_roundedView != null)
        {
            _roundedView.Visible = false;
            _roundedView.ClearScene();
        }
        ResetCamera();
    }

    public void ResetCamera()
    {
        _camera.SetAngles(-MathF.PI / 2, MathF.PI / 6);
        _fit = true;
    }

    public void SetDirection(Direction direction)
    {
        _camera.SetAngles(direction switch
        {
            Direction.East => 0,
            Direction.North => MathF.PI / 2,
            Direction.West => MathF.PI,
            _ => -MathF.PI / 2,
        }, MathF.PI / 6);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        handle.DrawRect(PixelSizeBox, Color.FromHex("#101A23"));
        if (PixelWidth <= 0 || PixelHeight <= 0)
            return;
        if (_fit)
        {
            _camera.Fit(_previewMin, _previewMax, PixelWidth / (float) PixelHeight);
            _fit = false;
        }
        if (_usesRounded)
            _roundedView!.SetCameraOverride(_camera.Frame((Vector2) PixelSize));
        else
            _renderer.Draw(handle, _camera.Frame((Vector2) PixelSize), ShowEdges);
    }

    protected override void Resized()
    {
        base.Resized();
        _fit = true;
        if (_roundedView != null)
            _roundedView.SetSize = Size;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;
        _dragging = true;
        _lastMouse = args.RelativePosition;
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;
        _dragging = false;
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (!_dragging)
            return;
        _camera.Orbit(args.RelativePosition - _lastMouse);
        _lastMouse = args.RelativePosition;
        args.Handle();
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);
        _camera.Zoom(args.Delta.Y);
        args.Handle();
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _dragging = false;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _dragging = false;
    }
}
