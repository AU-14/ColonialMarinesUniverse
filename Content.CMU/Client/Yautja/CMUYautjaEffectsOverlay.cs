using System.Numerics;
using JetBrains.Annotations;
using Content.Shared.CMU14.Yautja;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.Utility;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Yautja;

public sealed class CMUYautjaBackEffectsOverlay(IEntityManager entities) : CMUYautjaEffectsOverlay(entities, true);

/// <summary>Native sprite art and pixel-scale particles, clipped by the ordinary world FOV.</summary>
[Virtual]
public partial class CMUYautjaEffectsOverlay : Overlay
{
    public override OverlaySpace Space => _behind ? OverlaySpace.WorldSpaceBelowEntities : OverlaySpace.WorldSpaceBelowFOV;
    private readonly bool _behind;
    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprites;
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private readonly IPlayerManager _player = IoCManager.Resolve<IPlayerManager>();
    private readonly IPrototypeManager _prototypes = IoCManager.Resolve<IPrototypeManager>();
    private Texture? _experimentHunter;
    private readonly Texture _disc;
    private readonly Texture _combi;
    private readonly Texture[] _drone;
    private readonly Texture[] _folded;
    private readonly Texture _spine;
    private readonly Texture _gib;
    private readonly Texture _gib2;
    private readonly Texture _pool;
    private readonly IRsiStateLike _caster;
    private readonly ShaderInstance _tint;
    private readonly ShaderInstance _glowAttenuate;
    private readonly ShaderInstance _glowEmit;
    private IRenderTexture? _fragmentTarget;
    private static readonly Vector2[] SightDots = [new(0, 3), new(-3, -2), new(3, -2)];
    private static readonly ProtoId<ShaderPrototype> TissueShader = "CMUYautjaTissue";
    private static readonly ProtoId<ShaderPrototype> GlowAttenuateShader = "CMUYautjaGlowAttenuate";
    private static readonly ProtoId<ShaderPrototype> GlowEmitShader = "CMUYautjaGlowEmit";

    public CMUYautjaEffectsOverlay(IEntityManager entities, bool behind = false)
    {
        _behind = behind;
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _sprites = entities.System<SpriteSystem>();
        InitializeExperimentMaterials();
        _disc = Art("CMU14/Yautja/smart_disc.rsi", "active");
        _combi = Art("CMU14/Yautja/weapons.rsi", "combistick");
        _spine = Art("CMU14/Yautja/yautja_items.rsi", "spine");
        _gib = Art("CMU14/HunterShip/effects/blood.rsi", "gib1_flesh");
        _pool = Art("CMU14/Decals/blood_splatter.rsi", "splatter3");
        _gib2 = Art("CMU14/HunterShip/effects/blood.rsi", "gib2_flesh");
        _caster = _sprites.RsiStateLike(new SpriteSpecifier.Rsi(
            new ResPath("CMU14/Yautja/plasma_caster.rsi"), "equipped-SUITSTORAGE"));
        _tint = IoCManager.Resolve<IPrototypeManager>().Index(TissueShader).InstanceUnique();
        _glowAttenuate = IoCManager.Resolve<IPrototypeManager>().Index(GlowAttenuateShader).InstanceUnique();
        _glowEmit = IoCManager.Resolve<IPrototypeManager>().Index(GlowEmitShader).InstanceUnique();
        _drone = [Art("CMU14/HunterShip/obj/items/hunter/pred_gear.rsi", "falcon_drone_active"),
            Art("CMU14/HunterShip/obj/items/hunter/pred_gear.rsi", "falcon_drone_badblood_active")];
        _folded = [Art("CMU14/HunterShip/obj/items/hunter/pred_gear.rsi", "falcon_drone"),
            Art("CMU14/HunterShip/obj/items/hunter/pred_gear.rsi", "falcon_drone_badblood")];
    }

    private Texture Art(string path, string state) => _sprites.Frame0(new SpriteSpecifier.Rsi(new ResPath(path), state));

    protected override void Draw(in OverlayDrawArgs args)
    {
        var h = args.WorldHandle;
        var now = _timing.CurTime;
        var effects = _entities.EntityQueryEnumerator<CMUYautjaEffectComponent, TransformComponent>();
        while (effects.MoveNext(out var uid, out var effect, out var xform))
        {
            if (xform.MapID != args.MapId || now >= effect.EndsAt)
                continue;
            var origin = _transform.GetWorldPosition(uid);
            if (!args.WorldAABB.Enlarged(22).Contains(origin))
                continue;
            var target = effect.Target is { } actor && _entities.TryGetComponent<TransformComponent>(actor, out var tx)
                && tx.MapID == args.MapId ? _transform.GetWorldPosition(actor) : origin;
            var p = CMUYautjaPresentation.Progress(now, effect.StartedAt, effect.EndsAt);
            switch (effect.Kind)
            {
                case CMUYautjaEffect.CasterDeploy when !_behind:
                    var deploy = CMUYautjaPresentation.Segment(p, .04f / .18f, 1);
                    var direction = (_transform.GetWorldRotation(effect.Target ?? uid) +
                                     (args.Viewport.Eye?.Rotation ?? Angle.Zero)).GetCardinalDir();
                    DrawArt(h, _caster.GetFrame(direction.Convert(_caster.RsiDirections), 0),
                        target + new Vector2(0, -4 * (1 - deploy)) / 32, 1, 0, Color.White.WithAlpha(deploy));
                    break;
                case CMUYautjaEffect.DiscReturn or CMUYautjaEffect.CombiReturn when !_behind:
                    var stick = effect.Kind == CMUYautjaEffect.CombiReturn;
                    var to = target + new Vector2(10, -3) / 32;
                    var position = CMUYautjaPresentation.Recall(effect.Destination, to, p);
                    var rotation = CMUYautjaPresentation.RecallRotation(p, stick);
                    var travel = CMUYautjaPresentation.Segment(p, .30f, .74f);
                    if (travel > .03f && travel < .94f)
                    {
                        for (var i = 2; i > 0; i--)
                            DrawArt(h, stick ? _combi : _disc, position + new Vector2(i * 3, i) / 32,
                                .8f, rotation - i * .08f, Color.White.WithAlpha(.08f * (1 - i / 3f)));
                    }
                    DrawArt(h, stick ? _combi : _disc, position, .8f, rotation, Color.White);
                    break;
                case CMUYautjaEffect.FalconRecall when CanSeeDrone():
                    var fold = 1 - CMUYautjaPresentation.Segment(p, .20f, .43f);
                    var rise = 1 - CMUYautjaPresentation.Segment(p, .46f, .78f);
                    var bob = rise > .99f ? -MathF.Sin(p * 18) * .35f : 0;
                    var back = target + new Vector2(-4 * (1 - rise), 7 + rise * 30 + bob) / 32;
                    var arrival = CMUYautjaPresentation.Segment(p, 0, .18f);
                    var droneAt = Vector2.Lerp(effect.Destination + new Vector2(0, CMUYautjaPresentation.FalconHeight), back, arrival);
                    if (_behind == (rise < .42f))
                        Drone(h, droneAt, fold, 1 - CMUYautjaPresentation.Segment(p, .79f, .91f), effect.BadBloodDrone);
                    break;
                case CMUYautjaEffect.PlasmaMuzzle when !_behind:
                    DrawPlasmaMuzzle(h, target + new Vector2(9, 10) / 32, effect.Destination, p, effect.PlasmaMode);
                    break;
                case CMUYautjaEffect.PlasmaImpact when !_behind:
                    DrawPlasmaImpact(h, origin, p, effect.PlasmaMode);
                    break;
                case CMUYautjaEffect.Experiment when !_behind && effect.Experiment is { } experiment:
                    DrawExperiment(h, origin, p, _prototypes.Index(experiment));
                    break;
                case CMUYautjaEffect.Landing when !_behind:
                    Smoke(h, origin + new Vector2(0, -14) / 32, p, 37, Color.FromHex("#9F8F6F"));
                    Particles(h, origin + new Vector2(0, -13) / 32, p,
                        Color.FromHex("#B3A17D"), 28, 37, 15);
                    break;
                case CMUYautjaEffect.Roar when !_behind:
                    if (p > .23f && p < .7f)
                    {
                        Smoke(h, target + new Vector2(-8, -14) / 32,
                            (p - .23f) / .47f, 16, Color.FromHex("#9C9275"));
                        Smoke(h, target + new Vector2(8, -14) / 32,
                            (p - .23f) / .47f, 16, Color.FromHex("#9C9275"));
                    }
                    break;
                case CMUYautjaEffect.ButcherCut when !_behind:
                    BloodJet(h, origin, p * 1.4f, effect.Blood, effect.Stage + 7,
                        14 + Math.Max(0, effect.Stage - 1) * 4, .42f);
                    break;
            }
        }

        var drones = _entities.EntityQueryEnumerator<CMUYautjaMotionComponent, TransformComponent>();
        while (drones.MoveNext(out var uid, out var motion, out var xform))
        {
            if (xform.MapID != args.MapId || motion.Kind != CMUYautjaEffect.FalconDeploy ||
                now >= motion.EndsAt || !CanSeeDrone())
                continue;
            var p = CMUYautjaPresentation.Progress(now, motion.StartedAt, motion.EndsAt);
            var rise = CMUYautjaPresentation.Segment(p, .26f, .55f);
            if (_behind != (rise < .42f))
                continue;
            var at = _transform.GetWorldPosition(uid);
            if (motion.Controller is { } controller && _entities.TryGetComponent<TransformComponent>(controller, out var cx)
                && cx.MapID == args.MapId)
                at = Vector2.Lerp(_transform.GetWorldPosition(controller), at, CMUYautjaPresentation.Segment(p, .76f, 1));
            var bad = _entities.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "CMUYautjaFalconDroneBadBloodDeployed";
            Drone(h, at + new Vector2(-4 * (1 - rise),
                    7 + rise * 30 - (rise > .99f ? MathF.Sin(p * 18) * .35f : 0)) / 32,
                CMUYautjaPresentation.FalconUnfold(p), CMUYautjaPresentation.Segment(p, .13f, .27f), bad);
        }

        DrawButchers(args);
        if (!_behind)
        {
            DrawCloakTransitions(args);
            DrawSights(args);
            DrawLeapTrails(args);
            DrawPlasmaBolts(args);
        }
        h.SetTransform(Matrix3x2.Identity);
    }

    private bool CanSeeDrone() => _player.LocalEntity is { } viewer &&
        _entities.System<YautjaFalconVisibilitySystem>().CanSeeFalcon(viewer);

    private void DrawSights(in OverlayDrawArgs args)
    {
        var local = _entities.System<CMUYautjaVisualsSystem>();
        if (local.LocalAimUser is { } user && local.LocalAimCaster is { } caster && local.LocalAimPosition.MapId == args.MapId)
            DrawSight(args.WorldHandle, user, caster, local.LocalAimPosition.Position, local.LocalAimStartedAt);
        var query = _entities.EntityQueryEnumerator<CMUYautjaCasterAimComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var aim, out var xform))
        {
            if (uid == _player.LocalEntity || xform.MapID != args.MapId || _timing.CurTime >= aim.ExpiresAt)
                continue;
            DrawSight(args.WorldHandle, uid, aim.Caster, aim.Position, aim.StartedAt);
        }
    }

    private void DrawSight(DrawingHandleWorld h, EntityUid user, EntityUid caster, Vector2 position, TimeSpan startedAt)
    {
        if (!_entities.TryGetComponent<YautjaCasterComponent>(caster, out var weapon))
            return;
        var profile = CMUYautjaPlasmaAppearance.For((CMUYautjaPlasmaMode) Math.Clamp(weapon.CurrentMode, 0, 3));
        var origin = _transform.GetWorldPosition(user) + new Vector2(7, 9) / 32;
        var age = (float) (_timing.CurTime - startedAt).TotalSeconds;
        var acquire = CMUYautjaPresentation.Segment(age, 0, .54f);
        var locked = CMUYautjaPresentation.Segment(age, .108f, 1.026f);
        var at = position + new Vector2(MathF.Sin(age / 5.4f * 20) * (1 - locked) * 9, 0) / 32;
        foreach (var offset in SightDots)
        {
            var dot = at + offset / 32;
            PixelLine(h, origin, Vector2.Lerp(origin, dot, acquire),
                profile.Glow.WithAlpha(.28f + .12f * locked));
            Glow(h, dot, 4, profile.Glow, acquire * .45f);
            Pixel(h, dot, new Vector2(1f / 32),
                profile.Shell.WithAlpha(acquire));
        }
        Pixel(h, origin, new Vector2(1f / 32), profile.Glow);
    }

    private void DrawLeapTrails(in OverlayDrawArgs args)
    {
        var query = _entities.EntityQueryEnumerator<CMUYautjaMotionComponent, SpriteComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var motion, out var sprite, out var physics))
        {
            if (motion.Kind != CMUYautjaEffect.Leap || _timing.CurTime >= motion.EndsAt || !sprite.Visible ||
                _transform.GetMapCoordinates(uid).MapId != args.MapId)
                continue;
            var p = CMUYautjaPresentation.Progress(_timing.CurTime, motion.StartedAt, motion.EndsAt);
            if (p < .12f || p > .9f)
                continue;
            var position = _transform.GetWorldPosition(uid);
            var color = sprite.Color;
            try
            {
                for (var i = 3; i > 0; i--)
                {
                    _sprites.SetColor((uid, sprite), color.WithAlpha(color.A * (4 - i) / 16));
                    var backwards = physics.LinearVelocity.LengthSquared() > .001f
                        ? Vector2.Normalize(physics.LinearVelocity) : Vector2.UnitX;
                    _sprites.RenderSprite((uid, sprite), args.WorldHandle, args.Viewport.Eye?.Rotation ?? Angle.Zero,
                        _transform.GetWorldRotation(uid), position - backwards * (i * 4f / 32) - new Vector2(0, i / 32f),
                        _sprites.GetPostShaders(sprite));
                }
            }
            finally
            {
                _sprites.SetColor((uid, sprite), color);
                args.WorldHandle.SetTransform(Matrix3x2.Identity);
                args.WorldHandle.UseShader(null);
            }
        }
    }

    private void DrawButchers(in OverlayDrawArgs args)
    {
        var query = _entities.EntityQueryEnumerator<CMUYautjaButcherVisualComponent, TransformComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var body, out var xform, out var sprite))
        {
            if (xform.MapID != args.MapId)
                continue;
            var origin = _transform.GetWorldPosition(uid);
            if (!args.WorldAABB.Enlarged(2).Contains(origin))
                continue;
            var active = body.Hunter != null && _timing.CurTime < body.EndsAt;
            var p = active ? CMUYautjaPresentation.Progress(_timing.CurTime, body.StartedAt, body.EndsAt) : 0;
            var full = body.Procedure == YautjaButcherProcedure.Skin;
            var phase = full ? CMUYautjaPresentation.ButcherPhase(body.CompletedStage, active ? body.ActiveStage : 0, p) : p;
            if (_behind)
            {
                if (active)
                {
                    var amount = Math.Min(.85f, CMUYautjaPresentation.Segment(phase, .10f, .95f));
                    for (var i = 0; i < (int) MathF.Ceiling(amount * 6); i++)
                    {
                        var at = origin + new Vector2((Random(i + 51) - .5f) * 22,
                            -3 - (Random(i + 71) - .5f) * 12) / 32;
                        DrawTissue(args.WorldHandle, _pool, at, .65f + Random(i + 33) * .35f,
                            Random(i + 21) * 6.28f, Math.Min(.8f, amount) * .8f, true, body.Blood);
                    }
                }
                continue;
            }
            if (!active && body.CompletedStage == 0)
                continue;

            var rotation = (sprite.NoRotation ? -(args.Viewport.Eye?.Rotation ?? Angle.Zero) : _transform.GetWorldRotation(uid))
                           + sprite.Rotation;
            var offset = (sprite.NoRotation ? -(args.Viewport.Eye?.Rotation ?? Angle.Zero)
                : _transform.GetWorldRotation(uid)).RotateVec(sprite.Offset);
            origin += offset;
            if (body.Xeno)
            {
                DrawXenoButcher(args, uid, body, sprite, origin, rotation, phase);
                continue;
            }
            Vector2 BodyPoint(float x, float y) =>
                origin + rotation.RotateVec(new Vector2(x - 16, 16 - y) / 32 * sprite.Scale);
            if (!full)
            {
                var point = body.Procedure switch
                {
                    YautjaButcherProcedure.Head => new Vector2(16, 9),
                    YautjaButcherProcedure.RightArm => new(9, 10),
                    YautjaButcherProcedure.LeftArm => new(24, 10),
                    YautjaButcherProcedure.RightHand => new(9, 19),
                    YautjaButcherProcedure.LeftHand => new(24, 19),
                    YautjaButcherProcedure.RightLeg => new(13, 20),
                    YautjaButcherProcedure.LeftLeg => new(19, 20),
                    YautjaButcherProcedure.RightFoot => new(13, 28),
                    YautjaButcherProcedure.LeftFoot => new(19, 28),
                    _ => new(16, 11),
                };
                for (var i = 0; i < 3; i++)
                    BloodJet(args.WorldHandle, BodyPoint(point.X, point.Y),
                        (p - (i == 0 ? .18f : i == 1 ? .42f : .70f)) * 7.2f,
                        body.Blood, i + 8, 14 + i * 4, .42f);
                continue;
            }
            for (var i = 0; i < 4; i++)
            {
                var point = i switch { 0 => new Vector2(16, 12), 1 => new(9, 16),
                    2 => new(16, 18), _ => new(16, 7) };
                BloodJet(args.WorldHandle, BodyPoint(point.X, point.Y), (phase - (.12f + i * .25f)) * 16,
                    body.Blood, i + 8, 14 + i * 4, i == 3 ? .85f : .42f);
            }
            if (phase > .34f)
            {
                var pull = CMUYautjaPresentation.Segment(phase, .34f, .44f);
                var wound = BodyPoint(9, 16);
                var piece = wound + new Vector2(-7, -11) / 32 * pull;
                DrawVictimFragment(args, uid, sprite, piece, rotation - pull * .4f);
                PixelLine(args.WorldHandle, wound, piece, Color.FromHex("#781627").WithAlpha(1 - pull));
                DrawTissue(args.WorldHandle, _gib, wound + new Vector2(-6, -13) / 32,
                    .38f, 1.2f, .95f, false, body.Blood);
            }
            if (phase > .61f)
            {
                var pull = CMUYautjaPresentation.Segment(phase, .61f, .73f);
                DrawTissue(args.WorldHandle, _gib2, origin + new Vector2(pull * 12,
                        -pull * 17 + MathF.Sin(pull * MathF.PI) * 8) / 32,
                    .5f, pull, .9f, false, body.Blood);
            }
            if (phase > .86f && !body.Xeno)
            {
                var pull = CMUYautjaPresentation.Segment(phase, .86f, .97f);
                var wound = BodyPoint(16, 6);
                var trophy = wound + new Vector2(-3, 28) / 32 * pull;
                for (var i = 0; i < 2 && pull < .9f; i++)
                    PixelLine(args.WorldHandle, wound + new Vector2(i, 0) / 32,
                        trophy + new Vector2(i, -8) / 32, Color.FromHex("#711023").WithAlpha(1 - pull));
                DrawArt(args.WorldHandle, _spine, trophy, .8f, -.1f * pull,
                    Color.White.WithAlpha(Math.Min(1, pull * 6)));
            }
        }
    }

    private void DrawVictimFragment(in OverlayDrawArgs args, EntityUid uid, SpriteComponent sprite,
        Vector2 at, Angle rotation, Box2? bounds = null, UIBox2? region = null)
    {
        var savedRotation = sprite.Rotation;
        var savedOffset = sprite.Offset;
        var savedScale = sprite.Scale;
        var bodyBounds = bounds ?? Box2.CenteredAround(Vector2.Zero, Vector2.Abs(savedScale));
        var nativeSize = bodyBounds.Size / Vector2.Abs(savedScale) * 32;
        var nativeCenter = bodyBounds.Center / Vector2.Abs(savedScale) * 32;
        var required = new Vector2i(Math.Max(64, (int) MathF.Ceiling(nativeSize.X + MathF.Abs(nativeCenter.X) * 2 + 4)),
            Math.Max(64, (int) MathF.Ceiling(nativeSize.Y + MathF.Abs(nativeCenter.Y) * 2 + 4)));
        if (_fragmentTarget == null || _fragmentTarget.Texture.Width < required.X || _fragmentTarget.Texture.Height < required.Y)
        {
            if (_fragmentTarget != null)
            {
                required = Vector2i.ComponentMax(required, _fragmentTarget.Texture.Size);
                _fragmentTarget.Dispose();
            }
            _fragmentTarget = IoCManager.Resolve<IClyde>().CreateRenderTarget(required,
                RenderTargetColorFormat.Rgba8Srgb, new TextureSampleParameters { Filter = false }, "cmu-yautja-tissue");
        }
        var center = (Vector2) _fragmentTarget.Texture.Size / 2;
        var piece = region ?? new UIBox2(8, 12, 12, 22);
        var topLeft = center + new Vector2(nativeCenter.X, -nativeCenter.Y) - nativeSize / 2 +
            piece.TopLeft / 32 * nativeSize;
        var size = piece.Size / 32 * nativeSize;
        var screen = args.ScreenHandle;
        var yaw = _transform.GetWorldRotation(uid);
        var eye = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        try
        {
            _sprites.SetRotation((uid, sprite), Angle.Zero);
            _sprites.SetOffset((uid, sprite), Vector2.Zero);
            _sprites.SetScale((uid, sprite), Vector2.One);
            screen.RenderInRenderTarget(_fragmentTarget, () =>
                screen.DrawEntity(uid, center, Vector2.One, yaw + eye,
                    sprite: sprite), Color.Transparent);
        }
        finally
        {
            _sprites.SetRotation((uid, sprite), savedRotation);
            _sprites.SetOffset((uid, sprite), savedOffset);
            _sprites.SetScale((uid, sprite), savedScale);
        }
        args.WorldHandle.SetTransform(Matrix3x2.CreateRotation((float) rotation.Theta) *
                                      Matrix3x2.CreateTranslation(at));
        var half = size / 64 * savedScale;
        args.WorldHandle.DrawTextureRectRegion(_fragmentTarget.Texture, new Box2(-half, half),
            subRegion: new UIBox2(topLeft, topLeft + size));
        args.WorldHandle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawTissue(DrawingHandleWorld h, Texture texture, Vector2 at, float scale,
        float angle, float alpha, bool pool, Color blood)
    {
        _tint.SetParameter("pool", pool ? 1f : 0f);
        _tint.SetParameter("blood", blood.G > blood.R ? blood : Color.FromHex("#780D1D"));
        h.UseShader(_tint);
        DrawArt(h, texture, at, scale, angle, Color.White.WithAlpha(alpha));
        h.UseShader(null);
    }

    protected override void DisposeBehavior()
    {
        DisposeExperimentMaterials();
        _fragmentTarget?.Dispose();
        _tint.Dispose();
        _glowAttenuate.Dispose();
        _glowEmit.Dispose();
        base.DisposeBehavior();
    }

    private void Drone(DrawingHandleWorld h, Vector2 at, float wings, float alpha, bool bad)
    {
        var variant = bad ? 1 : 0;
        var texture = wings < .03f ? _folded[variant] : _drone[variant];
        var scale = wings < .03f ? new Vector2(.65f) : new Vector2(.28f + wings * .52f, .8f);
        DrawArt(h, texture, at, scale, 0, Color.White.WithAlpha(alpha));
    }

    private static void DrawArt(DrawingHandleWorld h, Texture texture, Vector2 at, float scale, float angle, Color color) =>
        DrawArt(h, texture, at, new Vector2(scale), angle, color);

    private static void DrawArt(DrawingHandleWorld h, Texture texture, Vector2 at, Vector2 scale, float angle, Color color)
    {
        at = new Vector2(MathF.Round(at.X * 32), MathF.Round(at.Y * 32)) / 32;
        h.SetTransform(Matrix3x2.CreateRotation(-angle) * Matrix3x2.CreateTranslation(at));
        var half = (Vector2) texture.Size / 64 * scale;
        h.DrawTextureRect(texture, new Box2(-half, half), color);
        h.SetTransform(Matrix3x2.Identity);
    }

    private static void Pixel(DrawingHandleWorld h, Vector2 at, Vector2 size, Color color)
    {
        at = new Vector2(MathF.Round(at.X * 32), MathF.Round(at.Y * 32)) / 32;
        h.DrawRect(new Box2(at + new Vector2(0, -size.Y), at + new Vector2(size.X, 0)), color);
    }

    private static float Random(int seed) => CMUYautjaPresentation.Random(seed);

    private static void PixelLine(DrawingHandleWorld h, Vector2 from, Vector2 to, Color color)
    {
        var delta = Vector2.Abs(to - from) * 32;
        var steps = Math.Max(1, (int) MathF.Ceiling(Math.Max(delta.X, delta.Y)));
        for (var i = 0; i <= steps; i++)
            Pixel(h, Vector2.Lerp(from, to, i / (float) steps), new Vector2(1f / 32), color);
    }

    private static void Particles(DrawingHandleWorld h, Vector2 at, float p, Color color,
        int count, float reach, float gravity = 25)
    {
        if (p < 0 || p > 1)
            return;
        for (var i = 0; i < count; i++)
        {
            var angle = Random(i + 20) * MathF.Tau;
            var velocity = new Vector2(MathF.Cos(angle), -MathF.Sin(angle));
            var position = at + (velocity * (reach * (.3f + Random(i + 80)) * p) -
                                 new Vector2(0, gravity * p * p)) / 32;
            PixelLine(h, position, position - velocity * (2f / 32), color.WithAlpha(1 - p));
        }
    }

    private static void Smoke(DrawingHandleWorld h, Vector2 at, float p, float reach, Color color)
    {
        if (p < 0 || p > 1)
            return;
        for (var i = 0; i < 16; i++)
        {
            var angle = Random(i + 22) * 6.28f;
            var distance = p * reach * Random(i + 45);
            Pixel(h, at + new Vector2(MathF.Cos(angle) * distance,
                    -MathF.Sin(angle) * distance * .5f + p * 15) / 32,
                new Vector2(MathF.Round(2 + p * 7) / 32), color.WithAlpha((1 - p) * .18f));
        }
    }

    private void Glow(DrawingHandleWorld h, Vector2 at, float radius, Color color, float alpha)
    {
        if (alpha <= 0)
            return;
        var bounds = Box2.CenteredAround(at, new Vector2(radius * 2 / 32));
        _glowAttenuate.SetParameter("glow", color);
        _glowAttenuate.SetParameter("intensity", alpha);
        _glowEmit.SetParameter("glow", color);
        _glowEmit.SetParameter("intensity", alpha);
        try
        {
            h.UseShader(_glowAttenuate);
            h.DrawRect(bounds, Color.White);
            h.UseShader(_glowEmit);
            h.DrawRect(bounds, Color.White);
        }
        finally
        {
            h.UseShader(null);
        }
    }

    private static void BloodJet(DrawingHandleWorld h, Vector2 at, float age, Color blood,
        int seed, float reach, float strength)
    {
        if (age < 0)
            return;
        for (var i = 0; i < (int) (34 * strength); i++)
        {
            var u = Math.Clamp(age / (.55f + Random(i + seed) * .4f), 0, 1);
            var angle = -MathF.PI * .95f + Random(i + seed * 23) * MathF.PI * 1.4f;
            var speed = reach * (.35f + Random(i + seed * 7));
            var delta = new Vector2(MathF.Cos(angle) * speed * u,
                -MathF.Sin(angle) * speed * u - u * u * reach * .65f) / 32;
            var color = blood.G > blood.R ? blood.WithAlpha(.9f)
                : Color.FromHex(i % 4 == 0 ? "#B02A36" : i % 3 == 0 ? "#721321" : "#460C17");
            if (u < 1)
                PixelLine(h, at + delta, at + delta * new Vector2(.93f, .95f), color);
            else
                Pixel(h, at + delta, new Vector2((i % 6 == 0 ? 2f : 1f) / 32), color.WithAlpha(.9f));
        }
    }

}
