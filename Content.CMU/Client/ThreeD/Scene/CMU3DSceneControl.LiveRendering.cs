using System.Numerics;
using Content.Client.Clickable;
using Content.Client.Effects;
using Content.Shared._RMC14.Effect;
using Content.Shared.Mobs.Components;
using Content.Shared.Item;
using Content.Shared.Projectiles;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DSceneControl
{
    private const int BillboardLimit = 256;
    private const int BillboardColumns = 16;
    private const int SpriteCell = 128;
    private const int BillboardTexels = 4;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _players = default!;
    private readonly List<EntityUid> _billboardEntities = [];
    private readonly HashSet<EntityUid> _liveSpriteEntities = [];
    private readonly List<BillboardCandidate> _billboards = [];
    private readonly record struct BillboardCandidate(EntityUid Uid, SpriteComponent Sprite, TransformComponent Transform,
        Box2 Bounds, Angle Yaw, float PlaneYaw, Vector2 Scale, Vector2 Size, Vector3 Center, Vector3 Offset,
        bool FaceCamera, bool Combat, bool AlongTrajectory, bool Ground, float Tilt, Direction? Direction)
    {
        public float WorldScale => Combat || Ground ? 1 : 1.6f;
        public Angle EyeRotation => FaceCamera || AlongTrajectory ? Angle.Zero : new Angle(-PlaneYaw);
    }
    private readonly List<BillboardCandidate> _billboardCandidates = [];
    private readonly Rgba32[] _billboardPixels = new Rgba32[BillboardLimit * BillboardTexels];
    private IRenderTexture? _spriteAtlas;
    private OwnedTexture? _billboardTexture;
    private IClydeViewport? _lightViewport;
    private TimeSpan _nextLiveRender;
    private TimeSpan _nextLightingRender;
    private (Vector2 Origin, MapId Map, float Radius) _lightingRegion;
    public float Brightness { get; set; } = 1;

    public void SetBillboards(IReadOnlyList<EntityUid> entities)
    {
        _billboardEntities.Clear();
        _billboardEntities.AddRange(entities);
        _redraw = true;
    }

    protected override void Draw(IRenderHandle render)
    {
        if (FirstPerson && !_released && _hasScene && _gameTiming.RealTime >= _nextLiveRender)
        {
            PrepareLiveRendering(render);
            _redraw = true;
            _nextLiveRender = _gameTiming.RealTime + TimeSpan.FromSeconds(1.0 / 30);
        }
        if (FirstPerson && !_released && _hasScene)
        {
            UpdateBillboardPositions();
        }
        Draw(render.DrawingHandleScreen);
    }

    private void PrepareLiveRendering(IRenderHandle render)
    {
        var sprites = _entities.System<SpriteSystem>();
        var transforms = _entities.System<SharedTransformSystem>();
        var parameters = TextureLoadParameters.Default;
        parameters.Srgb = false;
        parameters.SampleParameters = new TextureSampleParameters { Filter = false };
        _spriteAtlas ??= _clyde.CreateRenderTarget(new Vector2i(SpriteCell * BillboardColumns, SpriteCell * BillboardColumns), RenderTargetColorFormat.Rgba8Srgb,
            new TextureSampleParameters { Filter = false }, "cmu-3d-live-sprites");
        _billboardTexture ??= _clyde.CreateBlankTexture<Rgba32>(new Vector2i(BillboardLimit * BillboardTexels, 1), "cmu-3d-sprite-data", parameters);
        CollectBillboards(sprites, transforms);
        _billboards.Clear();
        var handle = render.DrawingHandleScreen;
        var previous = handle.GetTransform();
        try
        {
            handle.RenderInRenderTarget(_spriteAtlas, () =>
            {
                handle.SetTransform(Matrix3x2.Identity);
                foreach (var candidate in _billboardCandidates)
                {
                    if (_billboards.Count == BillboardLimit) break;
                    var uid = candidate.Uid;
                    var sprite = candidate.Sprite;
                    var index = _billboards.Count;
                    var cell = new Vector2(index % BillboardColumns, index / BillboardColumns) * SpriteCell;
                    var position = cell + new Vector2(SpriteCell / 2f) -
                        candidate.Bounds.Center * new Vector2(1, -1) * (candidate.Scale * EyeManager.PixelsPerMeter);
                    render.DrawEntity(uid, position, candidate.Scale, candidate.Yaw, candidate.EyeRotation,
                        overrideDirection: candidate.Direction, sprite: sprite, xform: candidate.Transform, xformSystem: transforms);
                    _billboards.Add(candidate);
                    PackBillboard(index, candidate);
                }
            }, Color.Transparent);
        }
        finally { handle.SetTransform(previous); }
        _billboardTexture.SetSubImage(Vector2i.Zero, new Vector2i(BillboardLimit * BillboardTexels, 1), _billboardPixels);

        var region = (SceneOrigin, SceneMap, SceneRadius);
        if (_gameTiming.RealTime < _nextLightingRender && _lightingRegion == region)
            return;
        _lightingRegion = region;
        _nextLightingRender = _gameTiming.RealTime + TimeSpan.FromSeconds(.1);
        // Sample the normal game's map lighting, including power changes, roof shadows and occluders.
        _lightViewport ??= _clyde.CreateViewport(new Vector2i(384, 384), new TextureSampleParameters { Filter = true });
        _lightViewport.Eye = new Robust.Shared.Graphics.Eye
        {
            Position = new MapCoordinates(SceneOrigin, SceneMap),
            Zoom = new Vector2(SceneRadius * 2 * EyeManager.PixelsPerMeter / 384f),
            DrawFov = false,
            DrawLight = true,
        };
        _lightViewport.Render();
    }

    private void CollectBillboards(SpriteSystem sprites, SharedTransformSystem transforms)
    {
        _billboardCandidates.Clear();
        // Short-lived client effects and predicted projectiles can complete their entire
        // lifetime between static scene publications. Read their current replicated state.
        _liveSpriteEntities.Clear();
        _liveSpriteEntities.UnionWith(_billboardEntities);
        AddLiveSprites<MobStateComponent>();
        AddLiveSprites<ProjectileComponent>();
        AddLiveSprites<EffectVisualsComponent>();
        AddLiveSprites<RMCEffectComponent>();
        AddLiveSprites<CMU3DCombatVisualComponent>();
        var camera = Camera();
        var elevation = _entities.System<CMU3DElevationSystem>();
        foreach (var uid in _liveSpriteEntities)
        {
            if (uid == _players.LocalEntity ||
                !_entities.TryGetComponent(uid, out SpriteComponent? sprite) || !sprite.Visible || sprite.Color.A <= 0 || sprite.ContainerOccluded ||
                !_entities.TryGetComponent(uid, out TransformComponent? xform) || !SceneMaps.Contains(xform.MapID) ||
                !_entities.TryGetComponent(uid, out MetaDataComponent? meta) ||
                (meta.Flags & (MetaDataFlags.Detached | MetaDataFlags.InContainer)) != 0)
                continue;
            sprites.ForceUpdate(uid);
            var yaw = transforms.GetWorldRotation(xform);
            var combat = _entities.HasComponent<ProjectileComponent>(uid) ||
                         _entities.HasComponent<CMU3DCombatVisualComponent>(uid);
            var along = _entities.TryGetComponent(uid, out CMU3DCombatVisualComponent? effect) && effect.AlongTrajectory;
            var faceCamera = !along && (combat || _entities.HasComponent<MobStateComponent>(uid) ||
                                       _entities.HasComponent<EffectVisualsComponent>(uid) || _entities.HasComponent<RMCEffectComponent>(uid));
            var ground = !faceCamera && !along && _entities.HasComponent<ItemComponent>(uid);
            var planeYaw = faceCamera ? BillboardFacing(transforms.GetWorldPosition(xform), camera)
                : ground || sprite.NoRotation && !along ? 0f : (float) yaw.Reduced().Theta;
            // Rasterize portraits and beams in their own axes. Applying the 3D plane angle
            // again through DrawEntity's 2D eye transform spins the artwork inside the plane.
            var artYaw = faceCamera || along ? Angle.Zero : yaw;
            var eyeRotation = faceCamera || along ? Angle.Zero : new Angle(-planeYaw);
            Direction? direction = sprite.EnableDirectionOverride ? sprite.DirectionOverride
                : faceCamera ? (yaw - new Angle(planeYaw)).GetDir() : null;
            var bounds = Matrix3Helpers.CreateRotation(eyeRotation)
                .TransformBox(sprite.CalculateRotatedBoundingBox(default, artYaw, eyeRotation));
            if (bounds.Width <= 0 || bounds.Height <= 0) continue;
            var scale = along
                ? new Vector2((SpriteCell - 4) / (bounds.Width * EyeManager.PixelsPerMeter), (SpriteCell - 4) / (bounds.Height * EyeManager.PixelsPerMeter))
                : new Vector2((SpriteCell - 4) / (Math.Max(bounds.Width, bounds.Height) * EyeManager.PixelsPerMeter));
            var size = new Vector2(SpriteCell) / (scale * EyeManager.PixelsPerMeter) * (combat || ground ? 1 : 1.6f);
            var candidate = new BillboardCandidate(uid, sprite, xform, bounds, artYaw, planeYaw, scale, size,
                default, default, faceCamera, combat, along, ground, 0, direction);
            candidate = PositionBillboard(candidate, transforms, elevation, camera);
            var center = candidate.Center;
            var radius = size.Length() / 2;
            // Include a margin for movement between atlas updates. Off-screen floors and
            // objects behind the camera must not consume the visible sprite budget.
            if (MathF.Abs(center.X - camera.Origin.X) > VisibleRadius + radius ||
                MathF.Abs(center.Y - camera.Origin.Y) > VisibleRadius + radius ||
                !camera.IntersectsSphere(center, radius + 1)) continue;
            _billboardCandidates.Add(candidate);
        }
        _billboardCandidates.Sort((a, b) =>
        {
            var priority = (b.FaceCamera || b.Combat).CompareTo(a.FaceCamera || a.Combat);
            if (priority != 0) return priority;
            var order = Vector3.DistanceSquared(a.Center, camera.Origin).CompareTo(Vector3.DistanceSquared(b.Center, camera.Origin));
            return order != 0 ? order : a.Uid.CompareTo(b.Uid);
        });
    }

    private void AddLiveSprites<T>() where T : IComponent
    {
        var query = _entities.EntityQueryEnumerator<T, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out _))
            _liveSpriteEntities.Add(uid);
    }

    private BillboardCandidate PositionBillboard(BillboardCandidate billboard, SharedTransformSystem transforms,
        CMU3DElevationSystem elevation, CMU3DCameraFrame camera)
    {
        var position = transforms.GetWorldPosition(billboard.Transform);
        var planeYaw = billboard.FaceCamera ? BillboardFacing(position, camera) : billboard.PlaneYaw;
        var right = new Vector3(MathF.Cos(planeYaw), MathF.Sin(planeYaw), 0);
        var origin = new Vector3(position - SceneOrigin,
            elevation.PhysicalHeight(billboard.Uid, SceneDepth));
        if (billboard.Combat) origin.Z += CMU3DCombatVisualComponent.WeaponHeight;
        var up = Vector3.UnitZ;
        var tilt = 0f;
        if (billboard.Ground)
        {
            // Item artwork is a top-down footprint, including transparent padding.
            // Keep it on its physical floor instead of raising it into an upright portrait.
            origin.Z += .01f;
            up = Vector3.UnitY;
            tilt = -MathF.PI / 2;
        }
        else if (billboard.AlongTrajectory)
        {
            up = Vector3.Cross(camera.Origin - origin, right);
            up = up.LengthSquared() < .000001f ? Vector3.UnitZ : Vector3.Normalize(up);
            if (up.Z < 0) up = -up;
            tilt = MathF.Atan2(Vector3.Dot(up, new Vector3(right.Y, -right.X, 0)), up.Z);
        }
        var offset = right * (billboard.Bounds.Center.X * billboard.WorldScale) + up *
            (billboard.Combat || billboard.Ground ? billboard.Bounds.Center.Y : billboard.Bounds.Height / 2) * billboard.WorldScale;
        return billboard with { Center = origin + offset, Offset = offset, PlaneYaw = planeYaw, Tilt = tilt };
    }

    private float BillboardFacing(Vector2 position, CMU3DCameraFrame camera)
    {
        // Face the viewer's position, independently of where they are looking.
        var delta = new Vector2(camera.Origin.X, camera.Origin.Y) - (position - SceneOrigin);
        return MathF.Atan2(delta.X, -delta.Y);
    }

    private void PackBillboard(int index, BillboardCandidate billboard)
    {
        var start = index * BillboardTexels;
        _billboardPixels[start] = PackPair(billboard.Center.X, billboard.Center.Y);
        _billboardPixels[start + 1] = PackPair(billboard.Center.Z, billboard.Size.X);
        _billboardPixels[start + 2] = PackPair(billboard.Size.Y, billboard.PlaneYaw);
        _billboardPixels[start + 3] = PackPair(billboard.Tilt, billboard.Combat ? 1 : 0);
    }

    private void UpdateBillboardPositions()
    {
        // Reuse the last artwork while applying this frame's interpolated transform and stair height.
        if (_billboardTexture == null)
            return;
        var transforms = _entities.System<SharedTransformSystem>();
        var elevation = _entities.System<CMU3DElevationSystem>();
        var camera = Camera();
        var changed = false;
        for (var i = 0; i < _billboards.Count; i++)
        {
            var billboard = _billboards[i];
            var next = billboard;
            if (!_entities.TryGetComponent(billboard.Uid, out TransformComponent? xform) ||
                !SceneMaps.Contains(xform.MapID) ||
                !_entities.TryGetComponent(billboard.Uid, out MetaDataComponent? meta) ||
                (meta.Flags & (MetaDataFlags.Detached | MetaDataFlags.InContainer)) != 0 ||
                !_entities.TryGetComponent(billboard.Uid, out SpriteComponent? sprite) || !sprite.Visible)
                next = billboard with { Size = Vector2.Zero };
            else
                next = PositionBillboard(billboard, transforms, elevation, camera);
            if (next == billboard)
                continue;
            changed = true;
            _billboards[i] = next;
            PackBillboard(i, next);
        }
        if (!changed)
            return;
        _billboardTexture.SetSubImage(Vector2i.Zero, new Vector2i(BillboardLimit * BillboardTexels, 1), _billboardPixels);
        _redraw = true;
    }

    private static Rgba32 PackPair(float a, float b)
    {
        var x = (ushort) Math.Clamp((int) MathF.Round(a * 1024 + 32768), 0, 65535);
        var y = (ushort) Math.Clamp((int) MathF.Round(b * 1024 + 32768), 0, 65535);
        return new Rgba32((byte) x, (byte) (x >> 8), (byte) y, (byte) (y >> 8));
    }

    private void PickBillboards(Vector3 origin, Vector3 ray, ref float distance, ref EntityUid? target)
    {
        var clickables = _entities.System<ClickableSystem>();
        var transforms = _entities.System<SharedTransformSystem>();
        foreach (var billboard in _billboards)
        {
            var right = new Vector3(MathF.Cos(billboard.PlaneYaw), MathF.Sin(billboard.PlaneYaw), 0);
            var up = billboard.Ground ? Vector3.UnitY : Vector3.UnitZ;
            var eye = new Robust.Shared.Graphics.Eye { Rotation = billboard.EyeRotation };
            if (billboard.Combat || billboard.Size == Vector2.Zero ||
                !CMU3DTargeting.IntersectBillboard(origin, ray, billboard.Center, billboard.Size, right, distance, out var t, out var local, up) ||
                !_entities.TryGetComponent(billboard.Uid, out SpriteComponent? sprite) ||
                !_entities.TryGetComponent(billboard.Uid, out TransformComponent? xform)) continue;
            var sourcePoint = transforms.GetWorldPosition(xform) + (-eye.Rotation).RotateVec(billboard.Bounds.Center + local / billboard.WorldScale);
            if (!clickables.CheckClick((billboard.Uid, null, sprite, xform, null), sourcePoint, eye, true, out _, out _, out _,
                    visualRotation: billboard.Yaw, visualDirection: billboard.Direction)) continue;
            distance = t;
            target = billboard.Uid;
        }
    }

    private void ReleaseLiveRendering()
    {
        _lightViewport?.Dispose();
        _spriteAtlas?.Dispose();
        _billboardTexture?.Dispose();
        _lightViewport = null;
        _spriteAtlas = null;
        _billboardTexture = null;
        _billboardEntities.Clear();
        _liveSpriteEntities.Clear();
        _billboardCandidates.Clear();
        _billboards.Clear();
    }
}
