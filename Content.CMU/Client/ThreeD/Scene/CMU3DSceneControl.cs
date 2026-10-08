using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>
/// Local-scene shader workbench. Its owner supplies authorized replicated content; this control
/// performs no world queries or networking. The first proof is opaque solid-part geometry.
/// </summary>
public sealed partial class CMU3DSceneControl : Control
{
    private static readonly ProtoId<ShaderPrototype> ShaderId = "CMU3DLocalScene";
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _gameTiming = default!;

    private CMU3DSceneEncoding _encoding;
    private CMU3DSceneEncoding? _stagingEncoding;
    private readonly List<CMU3DSceneBox> _snapshot = [];
    private OwnedTexture? _boxTexture;
    private OwnedTexture? _gridTexture;
    private OwnedTexture? _surfaceTexture;
    private OwnedTexture? _surfaceInfoTexture;
    private CMU3DSceneSurfaces? _surfaces;
    private IRenderTexture? _target;
    private ShaderInstance? _shader;
    private bool _released;
    private bool _hasScene;
    private bool _upload;
    private bool _redraw = true;
    private TimeSpan _nextDraw;
    private Vector2 _center;
    private float _yaw = -MathF.PI / 3;
    private float _pitch = 0.8f;
    private float _distance = 18;
    private int _selectedBox;
    private CMU3DCameraFrame? _cameraOverride;

    public bool FirstPerson { get; }
    public float FirstPersonPitch { get; private set; }
    public float RenderScale { get; set; } = 1;
    public int PixelBudget { get; set; } = CMU3DViewSettings.DefaultPixelBudget;
    public float SceneRadius { get; set; } = 8;
    public float VisibleRadius { get; set; } = 8;

    /// <summary>Asset reviewer camera; absent for the interactive local scene.</summary>
    public void SetCameraOverride(CMU3DCameraFrame frame)
    {
        _redraw |= _cameraOverride != frame;
        _cameraOverride = frame;
    }

    public event Action<EntityUid?>? EntitySelected;
    public int RenderedBoxes => _encoding.AcceptedBoxes;
    public int OmittedBoxes => _encoding.OmittedBoxes;

    public bool ContainsEntity(EntityUid uid)
    {
        foreach (var box in _encoding.Boxes)
        {
            if (box.Source == uid)
                return true;
        }
        return false;
    }

    public CMU3DSceneControl() : this(false) { }

    public CMU3DSceneControl(bool firstPerson)
    {
        FirstPerson = firstPerson;
        _encoding = new CMU3DSceneEncoding(firstPerson);
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
    }

    public void SetScene(IReadOnlyList<CMU3DSceneBox> boxes)
    {
        foreach (var step in StageScene(boxes)) { }
    }

    /// <summary>Retains the current drawing/picking snapshot until its replacement is fully packed.</summary>
    public IEnumerable<bool> StageScene(IReadOnlyList<CMU3DSceneBox> boxes, Func<bool>? shouldYield = null)
    {
        if (_released)
            yield break;
        var unchanged = _hasScene && boxes.Count == _snapshot.Count;
        for (var i = 0; unchanged && i < boxes.Count; i++)
            unchanged = boxes[i] == _snapshot[i];
        if (unchanged)
            yield break;
        _stagingEncoding ??= new CMU3DSceneEncoding(FirstPerson);
        foreach (var step in _stagingEncoding.BuildSteps(boxes, shouldYield))
            yield return step;
        if (_released)
            yield break;
        (_encoding, _stagingEncoding) = (_stagingEncoding, _encoding);
        _snapshot.Clear();
        if (boxes.Count <= CMU3DSceneEncoding.MaxSnapshotBoxes)
        {
            // The adapter reuses its list. Retain values, never the caller's mutable collection.
            foreach (var box in boxes)
                _snapshot.Add(box);
        }
        _hasScene = true;
        _upload = true;
        _redraw = true;
        // Packed IDs change with the current snapshot. Never highlight an unrelated replacement box.
        _selectedBox = 0;
    }

    public void ClearScene()
    {
        _hasScene = false;
        _upload = false;
        _snapshot.Clear();
        _selectedBox = 0;
        _encoding.Build(Array.Empty<CMU3DSceneBox>());
    }

    public void ResetCamera()
    {
        _center = Vector2.Zero;
        _yaw = -MathF.PI / 3;
        _pitch = 0.8f;
        _distance = 18;
        _redraw = true;
    }

    public void SetTopDown()
    {
        _pitch = MathF.PI / 2;
        _yaw = -MathF.PI / 2;
        _redraw = true;
    }

    private CMU3DCameraFrame Camera()
    {
        if (_cameraOverride is { } frame)
            return frame;
        var offset = new Vector3(MathF.Cos(_yaw) * MathF.Cos(_pitch), MathF.Sin(_yaw) * MathF.Cos(_pitch), MathF.Sin(_pitch));
        var forward = -offset;
        var right = new Vector3(-MathF.Sin(_yaw), MathF.Cos(_yaw), 0);
        return new CMU3DCameraFrame(new Vector3(_center, 0.6f) + offset * _distance, forward, right,
            Vector3.Cross(right, forward), Math.Max(1, PixelHeight) * 0.5f / MathF.Tan(CMU3DCamera.VerticalFieldOfView / 2), (Vector2) PixelSize * 0.5f);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        handle.DrawRect(PixelSizeBox, Color.FromHex("#101B22"));
        if (_released || !_hasScene || PixelWidth <= 0 || PixelHeight <= 0)
            return;
        PrepareTextures();
        var scale = FirstPerson ? CMU3DViewSettings.Clamp(RenderScale, .25f, 1, 1)
            : Math.Min(1f, (_orbiting || _panning ? 480f : 600f) / Math.Max(PixelWidth, PixelHeight));
        // Bound ray work on high-DPI/maximized windows; the HUD still draws at native resolution.
        // Zero is an explicit opt-in to tracing at the requested full window resolution.
        if (FirstPerson && PixelBudget != 0)
        {
            var budget = PixelBudget < 0 ? CMU3DViewSettings.DefaultPixelBudget : Math.Max(65536, PixelBudget);
            scale = Math.Min(scale, MathF.Sqrt(budget / ((float) PixelWidth * PixelHeight)));
        }
        var size = new Vector2i(Math.Max(1, (int) (PixelWidth * scale)), Math.Max(1, (int) (PixelHeight * scale)));
        if (_target == null || _target.Size != size)
        {
            _target?.Dispose();
            _target = _clyde.CreateRenderTarget(size, RenderTargetColorFormat.Rgba8Srgb,
                new TextureSampleParameters { Filter = true }, "cmu-local-3d-scene");
            _redraw = true;
            _nextDraw = TimeSpan.Zero;
        }
        _shader ??= _prototypes.Index(ShaderId).InstanceUnique();
        if (_redraw && (FirstPerson || _gameTiming.RealTime >= _nextDraw))
        {
            var camera = Camera();
            _shader.SetParameter("sceneBoxes", _boxTexture!);
            _shader.SetParameter("sceneGrid", _gridTexture!);
            _shader.SetParameter("sceneSurfaces", _surfaceTexture!);
            _shader.SetParameter("sceneSurfaceInfo", _surfaceInfoTexture!);
            _shader.SetParameter("surfaceAtlasSize", new Vector2(_surfaces!.Width, _surfaces.Height));
            _shader.SetParameter("sceneZ", new Vector2(_encoding.MinZ, _encoding.MaxZ));
            _shader.SetParameter("cameraOrigin", camera.Origin);
            _shader.SetParameter("cameraForward", camera.Forward);
            _shader.SetParameter("cameraRight", camera.Right);
            _shader.SetParameter("cameraUp", camera.Up);
            _shader.SetParameter("aspect", PixelWidth / (float) PixelHeight);
            _shader.SetParameter("tanHalfFov", Math.Max(1, PixelHeight) * .5f / camera.FocalPixels);
            _shader.SetParameter("gridLayout", new Vector4(_encoding.SpatialSize, _encoding.SpatialMin, _encoding.GridRows, _encoding.BoxRows));
            _shader.SetParameter("gridZ", new Vector3(CMU3DSceneEncoding.SpatialZMin, _encoding.SpatialZStep, _encoding.SpatialDepth));
            _shader.SetParameter("sceneRadius", SceneRadius);
            _shader.SetParameter("sceneView", FirstPerson
                ? new Vector3(camera.Origin.X, camera.Origin.Y, VisibleRadius)
                : new Vector3(0, 0, SceneRadius));
            _shader.SetParameter("selectedBox", (float) _selectedBox);
            _shader.SetParameter("equipmentBoxes", (Texture?) _equipmentTexture ?? Texture.White);
            _shader.SetParameter("equipmentRoots", (Texture?) _equipmentRoots ?? Texture.White);
            _shader.SetParameter("equipmentCount", (float) _equipment.Count);
            _shader.SetParameter("liveLight", _lightViewport?.LightRenderTarget.Texture ?? Texture.White);
            _shader.SetParameter("useLiveLight", FirstPerson ? 1f : 0f);
            _shader.SetParameter("brightness", Brightness);
            _shader.SetParameter("spriteAtlas", _spriteAtlas?.Texture ?? Texture.White);
            _shader.SetParameter("billboardData", (Texture?) _billboardTexture ?? Texture.White);
            _shader.SetParameter("billboardCount", (float) _billboards.Count);
            _shader.SetParameter("billboardLayout", new Vector2(BillboardColumns, BillboardLimit * BillboardTexels));
            var previous = handle.GetTransform();
            try
            {
                handle.RenderInRenderTarget(_target, () =>
                {
                    handle.SetTransform(Matrix3x2.Identity);
                    handle.UseShader(_shader);
                    handle.DrawTextureRect(Texture.White, UIBox2.FromDimensions(Vector2.Zero, size));
                    handle.UseShader(null);
                }, Color.Black);
            }
            finally
            {
                handle.UseShader(null);
                handle.SetTransform(previous);
            }
            _redraw = false;
            _nextDraw = _gameTiming.RealTime + TimeSpan.FromSeconds(1.0 / 30);
        }
        handle.DrawTextureRect(_target.Texture, PixelSizeBox);
        if (MouseCaptured)
        {
            var center = (Vector2) PixelSize / 2;
            handle.DrawLine(center - new Vector2(4, 0), center + new Vector2(4, 0), Color.White);
            handle.DrawLine(center - new Vector2(0, 4), center + new Vector2(0, 4), Color.White);
        }
    }

    private void PrepareTextures()
    {
        var parameters = TextureLoadParameters.Default;
        parameters.Srgb = false;
        parameters.SampleParameters = new TextureSampleParameters { Filter = false };
        _boxTexture ??= _clyde.CreateBlankTexture<Rgba32>(new Vector2i(CMU3DSceneEncoding.TextureWidth, _encoding.BoxRows),
            "cmu-local-3d-boxes", parameters);
        if (_gridTexture != null && _gridTexture.Size.Y != _encoding.GridRows)
        {
            _gridTexture.Dispose();
            _gridTexture = null;
            _upload = true;
        }
        _gridTexture ??= _clyde.CreateBlankTexture<Rgba32>(new Vector2i(CMU3DSceneEncoding.GridTextureWidth, _encoding.GridRows),
            "cmu-local-3d-grid", parameters);
        if (_surfaces == null)
        {
            _surfaces = CMU3DSceneSurfaces.Load(_prototypes, _resources);
            var size = new Vector2i(_surfaces.Width, _surfaces.Height);
            _surfaceTexture = _clyde.CreateBlankTexture<Rgba32>(size, "cmu-3d-surfaces", parameters);
            _surfaceTexture.SetSubImage(Vector2i.Zero, size, _surfaces.AtlasPixels());
            var informationSize = new Vector2i(CMU3DSceneSurfaces.Maximum + 1, 1);
            _surfaceInfoTexture = _clyde.CreateBlankTexture<Rgba32>(informationSize, "cmu-3d-surface-info", parameters);
            _surfaceInfoTexture.SetSubImage(Vector2i.Zero, informationSize, _surfaces.Information);
        }
        if (!_upload)
            return;
        _boxTexture.SetSubImage(Vector2i.Zero, _boxTexture.Size, _encoding.BoxPixels.AsSpan());
        _gridTexture.SetSubImage(Vector2i.Zero, _gridTexture.Size, _encoding.GridPixels.AsSpan());
        _upload = false;
    }

    /// <summary>Refresh source artwork after prototype/resource edits without retaining old atlas slots.</summary>
    public void InvalidateSurfaces()
    {
        _surfaceTexture?.Dispose();
        _surfaceInfoTexture?.Dispose();
        _surfaceTexture = _surfaceInfoTexture = null;
        _surfaces = null;
        _redraw = true;
    }

    /// <summary>Permanent and idempotent; closing/revoking access cannot leave a scene drawable.</summary>
    public void ReleaseResources()
    {
        if (_released)
            return;
        StopFirstPersonLook();
        ReleaseLiveRendering();
        LookDelta = null;
        MapProjection = null;
        LookStarted = LookEnded = null;
        _released = true;
        ClearScene();
        _target?.Dispose();
        _boxTexture?.Dispose();
        _gridTexture?.Dispose();
        InvalidateSurfaces();
        _shader?.Dispose();
        _target = null;
        _boxTexture = _gridTexture = null;
        _shader = null;
        EntitySelected = null;
    }

    protected override void Resized()
    {
        base.Resized();
        _redraw = true;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        ReleaseResources();
    }
}
