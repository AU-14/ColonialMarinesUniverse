using System.Numerics;
using Content.Client.Graphics;
using Content.Shared._RMC14.Water;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Standing;
using Content.Shared.Tag;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.Water;

/// <summary>
/// Ports cmss13#12918's depth, water tint and wakes without modifying the mob's transform or hitbox.
/// Movement queues surface checks; only mobs already near water are refreshed while stationary.
/// </summary>
public sealed partial class RMCWaterVisualsSystem : EntitySystem
{
    [Dependency] private RMCWaterSystem _water = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<ShaderPrototype> WaterShader = "RMCWaterSubmersion";
    private static readonly ProtoId<TagPrototype> LarvaTag = "RMCXenoLarva";
    private static readonly ResPath ShallowWater = new("/Textures/_RMC14/Tiles/planet/water.rsi");
    private readonly HashSet<EntityUid> _pending = new();
    private readonly HashSet<EntityUid> _newWater = new();
    private readonly HashSet<Entity<MobStateComponent>> _nearby = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<MobStateComponent, ComponentStartup>(OnMobStartup);
        SubscribeLocalEvent<MobStateComponent, ComponentShutdown>(OnMobShutdown);
        SubscribeLocalEvent<MobStateComponent, MoveEvent>(OnMobMove);
        SubscribeLocalEvent<RMCWaterComponent, ComponentStartup>(OnWaterStartup);
        SubscribeLocalEvent<RMCWaterComponent, StartCollideEvent>(OnWaterContact);
        SubscribeLocalEvent<RMCWaterVisualsComponent, ComponentShutdown>(OnVisualsShutdown);
        SubscribeLocalEvent<RMCWaterVisualsComponent, BeforePostShaderRenderEvent>(OnBeforeShader);
    }

    public override void Shutdown()
    {
        _pending.Clear();
        _newWater.Clear();
        _nearby.Clear();
        base.Shutdown();
    }

    private void OnMobStartup(Entity<MobStateComponent> ent, ref ComponentStartup args) => _pending.Add(ent);

    private void OnMobShutdown(Entity<MobStateComponent> ent, ref ComponentShutdown args)
    {
        _pending.Remove(ent);
        RemCompDeferred<RMCWaterVisualsComponent>(ent);
    }

    private void OnMobMove(Entity<MobStateComponent> ent, ref MoveEvent args)
    {
        _pending.Add(ent);
        if (args.OldPosition != args.NewPosition && TryComp<RMCWaterVisualsComponent>(ent, out var visual))
            visual.MovingUntil = _timing.CurTime + TimeSpan.FromSeconds(0.2);
    }

    private void OnWaterStartup(Entity<RMCWaterComponent> ent, ref ComponentStartup args) => _newWater.Add(ent);

    private void OnWaterContact(Entity<RMCWaterComponent> ent, ref StartCollideEvent args)
    {
        if (HasComp<MobStateComponent>(args.OtherEntity))
            _pending.Add(args.OtherEntity);
    }

    private bool UpdateSurface(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid) || !HasComp<MobStateComponent>(uid) || !TryComp<SpriteComponent>(uid, out var sprite))
            return false;

        var found = _water.TryGetWaterSurface(uid, out var water, out var depth, out var covered);
        if (!TryComp<RMCWaterVisualsComponent>(uid, out var visual))
        {
            if (!found)
                return false;

            visual = AddComp<RMCWaterVisualsComponent>(uid);
            visual.BaseOffset = sprite.Offset;
            visual.LastOffset = sprite.Offset;
        }

        visual.Water = water;
        visual.TargetDepth = found && !covered && _water.CanSubmerge(uid) ? Math.Clamp(depth, 0, 32) : 0;
        visual.NextSurfaceCheck = _timing.CurTime + TimeSpan.FromSeconds(0.1);
        return found;
    }

    public override void FrameUpdate(float frameTime)
    {
        // Defer startup until transforms and physics fixtures are initialized (including PVS arrivals).
        foreach (var uid in _newWater)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            _nearby.Clear();
            _lookup.GetEntitiesInRange(Transform(uid).Coordinates, 1f, _nearby);
            foreach (var mob in _nearby)
                _pending.Add(mob);
        }
        _newWater.Clear();

        foreach (var uid in _pending)
            UpdateSurface(uid);
        _pending.Clear();

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<RMCWaterVisualsComponent, SpriteComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var visual, out var sprite, out var mob))
        {
            if (time >= visual.NextSurfaceCheck && !UpdateSurface(uid) && visual.Depth <= 0.01f)
            {
                RemCompDeferred<RMCWaterVisualsComponent>(uid);
                continue;
            }

            // Other systems (buckling, carrying, transformations) can replace the offset while immersed.
            if (sprite.Offset != visual.LastOffset)
                visual.BaseOffset = sprite.Offset;

            visual.Depth = MathHelper.Lerp(visual.Depth, visual.TargetDepth, Math.Min(1, frameTime * 12));
            if (MathF.Abs(visual.Depth - visual.TargetDepth) < 0.01f)
                visual.Depth = visual.TargetDepth;

            visual.LastOffset = visual.BaseOffset - new Vector2(0, visual.Depth * sprite.Scale.Y / EyeManager.PixelsPerMeter);
            _sprite.SetOffset((uid, sprite), visual.LastOffset);

            var down = _standing.IsDown(uid) || HasComp<XenoRestingComponent>(uid);
            var larva = _tags.HasTag(uid, LarvaTag);
            var parasite = HasComp<XenoParasiteComponent>(uid);
            visual.Immersed = visual.TargetDepth > 0 &&
                              (down && (visual.TargetDepth >= 18 || larva) || parasite && visual.TargetDepth >= 8);
            var tint = visual.Depth > 0 && (!down || !HasComp<XenoComponent>(uid) || larva || visual.Immersed);
            if (tint && !_sprite.HasPostShader((uid, sprite), ContentPostShaderIds.WaterSubmersion))
            {
                _sprite.SetPostShader((uid, sprite), new SpriteComponent.PostShaderArgs(
                    ContentPostShaderIds.WaterSubmersion, ProtoMan.Index(WaterShader).InstanceUnique())
                {
                    RaiseShaderEvent = true,
                    Before = [ContentPostShaderIds.Stealth, "RMCInvisible", .. ContentPostShaderIds.BeforeOutlines],
                });
            }
            else if (!tint)
            {
                _sprite.RemovePostShader((uid, sprite), ContentPostShaderIds.WaterSubmersion);
            }

            UpdateSplash((uid, visual), sprite, mob.CurrentState != MobState.Dead,
                visual.TargetDepth > 0 && (visual.Immersed || !larva && !parasite && (!down || !HasComp<XenoComponent>(uid))));
        }
    }

    private void UpdateSplash(Entity<RMCWaterVisualsComponent> ent, SpriteComponent sprite, bool alive, bool visible)
    {
        visible &= !ent.Comp.Immersed || alive;
        if (!visible)
        {
            if (ent.Comp.Splash != null)
                QueueDel(ent.Comp.Splash);
            ent.Comp.Splash = null;
            ent.Comp.SplashState = null;
            return;
        }

        if (ent.Comp.Splash is not { } splash || !Exists(splash))
        {
            splash = Spawn("RMCWaterWake", Transform(ent).Coordinates);
            ent.Comp.Splash = splash;
        }

        // This is a separate local entity: camouflage must not hide disturbed water.
        _transform.SetCoordinates(splash, Transform(ent).Coordinates);
        _transform.SetLocalRotation(splash, Transform(ent).LocalRotation);
        var splashSprite = Comp<SpriteComponent>(splash);
        var width = sprite.BaseRSI?.Size.X ?? 32;
        var size = width <= 32 ? 32 : width <= 48 ? 48 : width <= 64 ? 64 : 88;
        var depth = ent.Comp.TargetDepth;
        var state = ent.Comp.Immersed ? "bubbles" :
            depth <= 2 ? "coast_shallow" : depth <= 4 ? "coast_deep" : depth <= 8 ? "shallow" : depth <= 12 ? "intermediate" : "deep";
        if (!ent.Comp.Immersed && size == 32 && _standing.IsDown(ent))
            state = $"human_resting_{(depth <= 4 ? "coast" : "deep")}_{(sprite.Rotation.Theta > 0 ? "w" : "e")}";
        var rsi = _resources.GetResource<RSIResource>(new ResPath($"/Textures/_RMC14/Effects/Water/splash{size}.rsi")).RSI;
        if (_timing.CurTime < ent.Comp.MovingUntil && rsi.TryGetState(state + "_moving", out _))
            state += "_moving";

        if (ent.Comp.SplashState != state || ent.Comp.SplashSize != size)
        {
            _sprite.LayerSetRsi((splash, splashSprite), 0, rsi);
            _sprite.LayerSetRsiState((splash, splashSprite), 0, state);
            ent.Comp.SplashState = state;
            ent.Comp.SplashSize = size;
        }

        _sprite.SetOffset((splash, splashSprite), ent.Comp.Immersed ? ent.Comp.BaseOffset : ent.Comp.LastOffset);
        _sprite.SetScale((splash, splashSprite), sprite.Scale);
        _sprite.SetDrawDepth((splash, splashSprite), sprite.DrawDepth + 1);
        _sprite.SetVisible((splash, splashSprite), sprite.Visible);
    }

    private void OnVisualsShutdown(Entity<RMCWaterVisualsComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Splash != null)
            QueueDel(ent.Comp.Splash);
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.RemovePostShader((ent.Owner, sprite), ContentPostShaderIds.WaterSubmersion);
        if (sprite.Offset == ent.Comp.LastOffset)
            _sprite.SetOffset((ent.Owner, sprite), ent.Comp.BaseOffset);
    }

    private void OnBeforeShader(Entity<RMCWaterVisualsComponent> ent, ref BeforePostShaderRenderEvent args)
    {
        if (args.Id != ContentPostShaderIds.WaterSubmersion)
            return;

        var viewport = args.Viewport;
        var eyeRotation = viewport.Eye?.Rotation ?? Angle.Zero;
        var position = _transform.GetWorldPosition(ent);
        // CM's wake artwork puts the feet four pixels above the bottom of each size's canvas.
        var height = args.Sprite.BaseRSI?.Size.Y ?? 32;
        var baseline = (4 - height / 2f) * args.Sprite.Scale.Y / EyeManager.PixelsPerMeter;
        var feet = viewport.WorldToLocal(position + (-eyeRotation).RotateVec(ent.Comp.BaseOffset + new Vector2(0, baseline)));
        var origin = viewport.WorldToLocal(position);
        var unit = viewport.WorldToLocal(position + (-eyeRotation).RotateVec(Vector2.UnitX));
        var pixelsPerMeter = Math.Max(1, Vector2.Distance(origin, unit));
        args.Shader.SetParameter("water_line", viewport.Size.Y - feet.Y);
        args.Shader.SetParameter("surface_origin", new Vector2(origin.X, viewport.Size.Y - origin.Y));
        args.Shader.SetParameter("pixels_per_meter", pixelsPerMeter);
        args.Shader.SetParameter("immersed", ent.Comp.Immersed);
        args.Shader.SetParameter("water_light", viewport.LightRenderTarget.Texture);

        // Use the actual animated frame and tint, including purification and custom map colors.
        Texture? texture = null;
        var color = Color.White;
        if (TryComp<SpriteComponent>(ent.Comp.Water, out var waterSprite) &&
            _sprite.TryGetLayer((ent.Comp.Water!.Value, waterSprite), 0, out var layer, false))
        {
            color = waterSprite.Color * layer.Color;
            if (layer.ActualRsi is { } rsi && rsi.TryGetState(_sprite.LayerGetRsiState((ent.Comp.Water.Value, waterSprite), 0), out var state))
                texture = state.GetFrame(layer.EffectiveDirection(_transform.GetWorldRotation(ent.Comp.Water.Value)), layer.AnimationFrame);
            else
                texture = layer.Texture;
        }

        if (texture == null)
        {
            var state = _resources.GetResource<RSIResource>(ShallowWater).RSI["seashallow"];
            texture = state.GetFrame(RsiDirection.South, (int)(_timing.CurTime.TotalSeconds / 0.3) % state.DelayCount);
        }

        var region = new Vector4(0, 0, 1, 1);
        if (texture is AtlasTexture atlas)
        {
            texture = atlas.SourceTexture;
            region = new Vector4(atlas.SubRegion.Left / texture.Width, 1 - atlas.SubRegion.Bottom / texture.Height,
                atlas.SubRegion.Right / texture.Width, 1 - atlas.SubRegion.Top / texture.Height);
        }

        args.Shader.SetParameter("water_texture", texture);
        args.Shader.SetParameter("water_uv", region);
        args.Shader.SetParameter("water_color", color);
    }
}
