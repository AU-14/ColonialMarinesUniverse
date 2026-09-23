using System.Numerics;
using Content.Shared.Maps;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.CMU14.TacticalMap.Reconstruction;

/// <summary>A real 3D raycast, drawn and cached using content-accessible 2D render targets.</summary>
public sealed partial class CMUReconstructionControl : Control
{
    private static readonly ProtoId<ShaderPrototype> ShaderId = "CMUTacticalReconstruction";
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IComponentFactory _components = default!;

    private ShaderInstance? _shader;
    private OwnedTexture? _terrain;
    private OwnedTexture? _furnitureModels;
    private OwnedTexture? _appearance;
    private OwnedTexture? _occupancy;
    private Rgba32[] _chunkPixels = [];
    private IRenderTexture? _surfaceAtlas;
    private readonly Dictionary<ushort, (Texture[] Textures, UIBox2? Region, Color Tint)> _surfaceTextures = new();
    private readonly List<ushort> _newSurfaces = new();
    private readonly Queue<CMUReconSurface> _pendingSurfaces = new();
    private readonly Queue<(int X, int Y, int Level)> _pendingUploads = new();
    private readonly HashSet<(int X, int Y, int Level)> _queuedUploads = new();
    private bool _clearSurfaces;
    private IRenderTexture? _target;
    private readonly Font _font;
    private bool _redraw = true;
    private TimeSpan _nextSceneDraw;
    private bool _rotating;
    private bool _panning;
    private Vector2 _center;
    private Vector2 _lastMouse;
    private float _yaw = -MathF.PI / 2;
    private float _pitch = 0.80f;
    private float _distance = 72;
    private float _wallScale = 0.55f;
    private bool _isolate;
    private bool _fit = true;
    private int _selectedLevel;
    private bool _overhead;

    public CMUReconSnapshotMessage? Scene { get; private set; }
    public CMUReconOrder[] Orders { get; set; } = [];
    public CMUReconDraft Draft { get; set; } = new();

    public int SelectedLevel => _selectedLevel;
    public bool ShowLabels = true;

    public CMUReconstructionControl()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Stop;
        _font = new VectorFont(_resources.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 11);
    }

    public void SetScene(CMUReconSnapshotMessage scene)
    {
        var hasCells = scene.Cells.Length != 0;
        if (!CMUReconSceneData.Initialize(scene))
            return;
        Scene = scene;
        _center = new Vector2(scene.Width, scene.Height) / 2;
        Orders = scene.Orders;
        TrackedContacts = [];
        CancelStroke();
        _selectedLevel = Math.Clamp(-scene.MinDepth, 0, scene.Levels - 1);
        _terrain?.Dispose();
        _appearance?.Dispose();
        _occupancy?.Dispose();
        _surfaceTextures.Clear();
        _newSurfaces.Clear();
        _pendingSurfaces.Clear();
        _pendingUploads.Clear();
        _queuedUploads.Clear();
        _clearSurfaces = true;
        var load = TextureLoadParameters.Default;
        load.Srgb = false; // These are byte-valued cells, not colors. sRGB decoding corrupts material IDs.
        load.SampleParameters = new TextureSampleParameters { Filter = false };
        var atlasSize = new Vector2i(scene.Width * Math.Min(4, scene.Levels), scene.Height * ((scene.Levels + 3) / 4));
        _terrain = _clyde.CreateBlankTexture<Rgba32>(atlasSize,
            name: "cmu-reconstruction-cells", loadParams: load);
        _appearance = _clyde.CreateBlankTexture<Rgba32>(atlasSize, name: "cmu-reconstruction-appearance", loadParams: load);
        // The zeroed occupancy mask hides uninitialized terrain, including shader neighbour reads.
        // Upload each occupied chunk before publishing its occupancy bit; no map-sized clear upload.
        var chunkSize = atlasSize / CMUReconGeometry.ChunkSize;
        _occupancy = _clyde.CreateBlankTexture<Rgba32>(chunkSize, name: "cmu-reconstruction-chunks", loadParams: load);
        _chunkPixels = new Rgba32[chunkSize.X * chunkSize.Y];
        _occupancy.SetSubImage(Vector2i.Zero, chunkSize, _chunkPixels.AsSpan());
        for (var level = 0; hasCells && level < scene.Levels; level++)
        for (var y = 0; y < scene.Height / CMUReconGeometry.ChunkSize; y++)
        for (var x = 0; x < scene.Width / CMUReconGeometry.ChunkSize; x++)
            if (HasGeometry(scene, x, y, level)) QueueUpload(x, y, level);
        _occupancy.SetSubImage(Vector2i.Zero, chunkSize, _chunkPixels.AsSpan());
        AddSurfaces(scene.Surfaces);
        _redraw = true;
        _fit = true;
    }

    public void Apply(CMUReconPatchMessage patch)
    {
        if (Scene is not { } scene || patch.Generation != scene.Generation || _terrain == null)
            return;
        CMUReconSceneData.Apply(scene, patch);
        Orders = scene.Orders;
        AddSurfaces(patch.Surfaces);
        foreach (var chunk in patch.Chunks)
        {
            if (!CMUReconSceneData.ValidChunk(scene, chunk))
                continue;
            var across = _occupancy!.Width;
            var at = (chunk.Level / 4 * scene.Height / CMUReconGeometry.ChunkSize + chunk.Y) * across +
                     chunk.Level % 4 * scene.Width / CMUReconGeometry.ChunkSize + chunk.X;
            if (chunk.Empty && _chunkPixels[at].R == 0) continue;
            QueueUpload(chunk.X, chunk.Y, chunk.Level);
        }
    }

    public bool TryResume(CMUReconSnapshotMessage scene)
    {
        if (!scene.ReuseGeometry || scene.AtlasId == 0 || Scene is not { } previous || previous.AtlasId != scene.AtlasId ||
            previous.Width != scene.Width || previous.Height != scene.Height || previous.Origin != scene.Origin ||
            previous.Levels != scene.Levels || previous.MinDepth != scene.MinDepth || previous.MapChoice != scene.MapChoice)
            return false;
        scene.Cells = previous.Cells;
        scene.Appearance = previous.Appearance;
        scene.Directions = previous.Directions;
        scene.Surfaces = previous.Surfaces;
        scene.Revisions = previous.Revisions;
        Scene = scene;
        Orders = scene.Orders;
        return true;
    }

    private static bool HasGeometry(CMUReconSnapshotMessage scene, int x, int y, int level)
    {
        const int size = CMUReconGeometry.ChunkSize;
        for (var row = 0; row < size; row++)
        {
            var start = CMUReconGeometry.Index(x * size, y * size + row, level, scene.Width, scene.Height);
            for (var dx = 0; dx < size; dx++)
                if (scene.Cells[start + dx] != 0 || scene.Appearance[start + dx] != 0) return true;
        }
        return false;
    }

    private void QueueUpload(int x, int y, int level)
    {
        if (_queuedUploads.Add((x, y, level))) _pendingUploads.Enqueue((x, y, level));
    }

    private void UploadPending()
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var uploaded = 0;
        while (uploaded < 24 && timer.Elapsed.TotalMilliseconds < 2 && _pendingUploads.TryDequeue(out var chunk))
        {
            _queuedUploads.Remove(chunk);
            UploadChunk(chunk.X, chunk.Y, chunk.Level);
            uploaded++;
        }
        if (uploaded == 0 || _occupancy == null) return;
        _occupancy.SetSubImage(Vector2i.Zero, _occupancy.Size, _chunkPixels.AsSpan());
        _redraw = true;
    }

    public void SelectLevel(int level)
    {
        if (Scene == null)
            return;
        _selectedLevel = Math.Clamp(level, 0, Scene.Levels - 1);
        CancelStroke();
        _redraw = true;
    }

    public void Rotate(float amount) { _yaw += amount; _redraw = true; }
    public void SetTopDown() { FinishStroke(); _overhead = true; _redraw = true; }

    public void Pan(Vector2 offset)
    {
        if (Scene is not { } scene) return;
        _center = Vector2.Clamp(_center + offset, Vector2.Zero, new Vector2(scene.Width, scene.Height));
        _fit = false;
        _redraw = true;
    }
    public CMUReconCamera CaptureCamera() => new(_center + (Vector2) (Scene?.Origin ?? Vector2i.Zero), _yaw, _pitch,
        _distance, (Scene?.MinDepth ?? 0) + _selectedLevel, _overhead, _wallScale < 1, _isolate, ShowLabels, _fit);

    public void RestoreCamera(CMUReconCamera camera)
    {
        if (Scene is not { } scene) return;
        _center = Vector2.Clamp(camera.Center - (Vector2) scene.Origin, Vector2.Zero, new Vector2(scene.Width, scene.Height));
        _yaw = camera.Yaw;
        _pitch = camera.Pitch;
        _distance = camera.Distance;
        _overhead = camera.Overhead;
        _wallScale = camera.LowWalls ? 0.55f : 1f;
        _isolate = camera.Isolated;

        ShowLabels = camera.Labels;
        _selectedLevel = Math.Clamp(camera.Depth - scene.MinDepth, 0, scene.Levels - 1);
        _fit = camera.Fit;
        _redraw = true;
    }

    public void ClearScene()
    {
        Scene = null;
        Orders = [];
        CancelStroke();
    }

    public bool CenterOnPlayer()
    {
        if (Scene is not { OperatorPosition: { } position } scene ||
            scene.OperatorDepth < scene.MinDepth || scene.OperatorDepth >= scene.MinDepth + scene.Levels)
            return false;
        FinishStroke();
        _center = Vector2.Clamp(position - (Vector2) scene.Origin, Vector2.Zero, new Vector2(scene.Width, scene.Height));
        _selectedLevel = scene.OperatorDepth - scene.MinDepth;
        _distance = 64;
        _fit = false;
        _redraw = true;
        return true;
    }
    public void SetLowWalls(bool enabled) { _wallScale = enabled ? 0.55f : 1f; _redraw = true; }
    public void SetIsolated(bool enabled) { _isolate = enabled; _redraw = true; }
    public void ResetCamera()
    {
        FinishStroke();
        _yaw = -MathF.PI / 2;
        _pitch = 0.80f;
        _overhead = false;
        if (Scene is { } scene) _center = new Vector2(scene.Width, scene.Height) / 2;
        _fit = true;
        _redraw = true;
    }

    private Vector3 CameraTarget => new(_center.X, _center.Y,
        _selectedLevel * CMUReconGeometry.LevelHeight + 0.5f);

    private void Camera(out Vector3 origin, out Vector3 forward, out Vector3 right, out Vector3 up)
    {
        var offset = _overhead ? Vector3.UnitZ : new Vector3(MathF.Cos(_yaw) * MathF.Cos(_pitch), MathF.Sin(_yaw) * MathF.Cos(_pitch), MathF.Sin(_pitch));
        origin = CameraTarget + offset * _distance;
        forward = -offset;
        right = _overhead ? Vector3.UnitX : Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitZ));
        up = Vector3.Cross(right, forward);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        handle.DrawRect(PixelSizeBox, Color.FromHex("#10191F"));
        if (Scene is not { } scene || _terrain == null || _appearance == null || _occupancy == null || PixelWidth <= 0 || PixelHeight <= 0)
            return;
        UploadPending();
        var surfaceTimer = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 4 && surfaceTimer.Elapsed.TotalMilliseconds < 2 && _pendingSurfaces.TryDequeue(out var surface); i++) LoadSurfaces([surface]);
        RenderSurfaces(handle);
        _shader ??= _prototypes.Index(ShaderId).InstanceUnique();

        // Cap fragment workload; labels remain at full UI resolution. No scene pass when unchanged.
        var moving = _rotating || _panning;
        var scale = Math.Min(1f, (moving ? 880f : 1440f) / Math.Max(PixelWidth, PixelHeight));
        var size = new Vector2i(Math.Max(1, (int) (PixelWidth * scale)), Math.Max(1, (int) (PixelHeight * scale)));
        if (_target == null || _target.Size != size)
        {
            _target?.Dispose();
            _target = _clyde.CreateRenderTarget(size, RenderTargetColorFormat.Rgba8Srgb, new TextureSampleParameters { Filter = true }, "cmu-reconstruction-view");
            _redraw = true;
            _nextSceneDraw = TimeSpan.Zero;
        }
        if (_fit)
        {
            // Fit all eight corners in perspective, including the near edge and lower floors.
            // An orthographic span estimate crops the foreground when the camera is tilted.
            var aspect = PixelWidth / (float) PixelHeight;
            Camera(out _, out var fitForward, out var fitRight, out var fitUp);
            _distance = 12;
            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -1 : scene.Width + 1,
                    (i & 2) == 0 ? -1 : scene.Height + 1,
                    (i & 4) == 0 ? (_isolate ? _selectedLevel * CMUReconGeometry.LevelHeight : 0) - 0.3f
                        : (_selectedLevel + 1) * CMUReconGeometry.LevelHeight);
                var relative = corner - CameraTarget;
                var span = Math.Max(MathF.Abs(Vector3.Dot(relative, fitRight)) / aspect,
                    MathF.Abs(Vector3.Dot(relative, fitUp))) / (0.45f * 0.9f);
                _distance = Math.Max(_distance, span - (_overhead ? 0 : Vector3.Dot(relative, fitForward)));
            }
            _fit = false;
        }
        Camera(out var origin, out var forward, out var right, out var up);
        if (_redraw && (_timing.RealTime >= _nextSceneDraw || moving ||
            scene.LoadedChunks == scene.TotalChunks && _pendingUploads.Count == 0 && _pendingSurfaces.Count == 0))
        {
            _shader.SetParameter("terrain", _terrain);
            _shader.SetParameter("furnitureModels", _furnitureModels!);
            _shader.SetParameter("appearance", _appearance);
            _shader.SetParameter("occupancy", _occupancy);
            _shader.SetParameter("surfaceAtlas", _surfaceAtlas!.Texture);
            _shader.SetParameter("mapSize", new Vector2(scene.Width, scene.Height));
            _shader.SetParameter("atlasSize", (Vector2) _terrain.Size);
            _shader.SetParameter("pixelFootprint", _distance * 0.9f / PixelHeight);
            _shader.SetParameter("levels", (float) scene.Levels);
            _shader.SetParameter("visibleLevels", (float) _selectedLevel + 1);
            _shader.SetParameter("cameraOrigin", origin);
            _shader.SetParameter("cameraForward", forward);
            _shader.SetParameter("cameraRight", right);
            _shader.SetParameter("cameraUp", up);
            _shader.SetParameter("aspect", PixelWidth / (float) PixelHeight);
            _shader.SetParameter("orthographic", _overhead ? 1f : 0f);
            _shader.SetParameter("orthoScale", _distance * 0.45f);
            _shader.SetParameter("wallScale", _wallScale);
            _shader.SetParameter("isolateLevel", _isolate ? 1f : 0f);
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
            _nextSceneDraw = _timing.RealTime + TimeSpan.FromSeconds(0.1);
        }
        handle.DrawTextureRect(_target.Texture, PixelSizeBox);
        DrawFrame(handle, scene);
        if (ShowLabels) DrawLabels(handle, scene);
        foreach (var order in Orders)
        {
            if (Draft.Removals.Contains(order.Id) || order.Depth != scene.MinDepth + _selectedLevel)
                continue;
            if (order.Text is { } text && order.Waypoints is { Length: > 0 } point)
                DrawTextMarker(handle, point[0], (order.Color ?? InkColor(order.Ink)), text);
            else if (order.Waypoints is { Length: > 0 } stroke)
                DrawStroke(handle, stroke, (order.Color ?? InkColor(order.Ink)), order.Width);
            else
                DrawMarker(handle, order.Tile - scene.Origin, (order.Color ?? InkColor(order.Ink)), OrderText(order));
        }
        foreach (var draft in Draft.Additions)
        {
            if (draft.Depth != scene.MinDepth + _selectedLevel) continue;
            if (draft.Text is { } text) DrawTextMarker(handle, draft.Points[0], InkColor(draft.Ink), text);
            else DrawStroke(handle, draft.Points, InkColor(draft.Ink), draft.Width);
        }
        if (_stroke.Count > 0) DrawStroke(handle, _stroke, InkColor(Ink), StrokeWidth);
        DrawContacts(handle);
    }
    private void DrawMarker(DrawingHandleScreen handle, Vector2i tile, Color color, string text)
    {
        if (Scene is not { } scene || tile.X < 0 || tile.Y < 0 || tile.X >= scene.Width || tile.Y >= scene.Height)
            return;
        var world = new Vector3(tile.X + 0.5f, tile.Y + 0.5f, _selectedLevel * CMUReconGeometry.LevelHeight + CMUReconGeometry.FloorHeight + 0.05f);
        var point = Project(world);
        if (!PixelSizeBox.Contains(new Vector2i((int) point.X, (int) point.Y)))
            return;
        // Mark the actual footprint in world space, keeping the tile address legible while orbiting.
        var a = Project(world + new Vector3(-0.46f, -0.46f, 0));
        var b = Project(world + new Vector3(0.46f, -0.46f, 0));
        var c = Project(world + new Vector3(0.46f, 0.46f, 0));
        var d = Project(world + new Vector3(-0.46f, 0.46f, 0));
        handle.DrawLine(a, b, color); handle.DrawLine(b, c, color);
        handle.DrawLine(c, d, color); handle.DrawLine(d, a, color);
        handle.DrawCircle(point, 4, color);
        handle.DrawLine(point, point - new Vector2(0, 28), color);
        var label = point + new Vector2(9, -44);
        handle.DrawRect(UIBox2.FromDimensions(label - new Vector2(5, 0), handle.GetDimensions(_font, text, 1) + new Vector2(12, 8)), Color.FromHex("#10222CEF"));
        handle.DrawString(_font, label + new Vector2(0, 4), text, color);
    }

    private Vector2 Project(Vector3 world)
    {
        Camera(out var origin, out var forward, out var right, out var up);
        var relative = world - origin;
        var depth = Vector3.Dot(relative, forward);
        if (depth <= 0)
            return new Vector2(-10000);
        var divisor = _overhead ? _distance * 0.45f : depth * 0.45f;
        var aspect = PixelWidth / (float) PixelHeight;
        return new Vector2((Vector3.Dot(relative, right) / (divisor * aspect) + 1) * PixelWidth / 2,
            (1 - Vector3.Dot(relative, up) / divisor) * PixelHeight / 2);
    }

    private void DrawFrame(DrawingHandleScreen handle, CMUReconSnapshotMessage scene)
    {
        var ink = Color.FromHex("#75A7B4");
        handle.DrawRect(PixelSizeBox, Color.FromHex("#34515E"), false);
        handle.DrawString(_font, new Vector2(14, 24), Loc.GetString("cmu-recon-view-floor", ("z", scene.MinDepth + _selectedLevel)), ink);
        var center = CameraTarget;
        var north = Project(center + new Vector3(0, 6, 0)) - Project(center);
        if (north.LengthSquared() > 0.01f)
        {
            north = Vector2.Normalize(north) * 21;
            var compass = new Vector2(PixelWidth - 36, 43);
            handle.DrawCircle(compass, 25, Color.FromHex("#34515E"), false);
            handle.DrawLine(compass, compass + north, ink);
            handle.DrawString(_font, compass + north - new Vector2(4, 3), Loc.GetString("cmu-recon-north"), ink);
        }
        handle.DrawString(_font, new Vector2(14, PixelHeight - 24), Loc.GetString("cmu-recon-scale"), ink);
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);
        FinishStroke();
        var before = _distance;
        _distance = Math.Clamp(_distance * MathF.Pow(0.82f, args.Delta.Y), 8, CMUReconGeometry.MaxSize * 4);
        _fit = false;
        // Zoom toward the mouse on the chosen floor, so a room stays under the cursor.
        var pixel = args.RelativePosition * UIScale;
        var nx = (pixel.X / Math.Max(1, PixelWidth) * 2 - 1) * PixelWidth / Math.Max(1f, PixelHeight);
        var ny = 1 - pixel.Y / Math.Max(1, PixelHeight) * 2;
        Camera(out _, out var forward, out var right, out var up);
        var offset = (right * nx + up * ny) * (before - _distance) * 0.45f;
        if (!_overhead && Math.Abs(forward.Z) > 0.01f)
            offset -= forward * (offset.Z / forward.Z);
        Pan(new Vector2(offset.X, offset.Y));
        _redraw = true;
        args.Handle();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _target?.Dispose();
        _terrain?.Dispose();
        _appearance?.Dispose();
        _occupancy?.Dispose();
        _surfaceAtlas?.Dispose();
        _furnitureModels?.Dispose();
        _shader?.Dispose();
        _target = null;
        _terrain = null;
        _furnitureModels = null;
        _appearance = null;
        _occupancy = null;
        _surfaceAtlas = null;
        _shader = null;
        _input.FirstChanceOnKeyEvent -= OnMiddleMouse;
        _rotating = false;
        _panning = false;
        CancelStroke();
    }
}
