using System.Numerics;
using Content.Client.Graphics;
using Content.Client.UserInterface.Systems.Actions;
using Content.Shared.Actions.Components;
using Content.Shared._RMC14.Armor.ThermalCloak;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Yautja;

public sealed partial class CMUYautjaVisualsSystem : EntitySystem
{
    private const string CloakShader = "cmu-yautja-optical-cloak";
    private const string ButcherShader = "cmu-yautja-butcher";
    [Dependency] private SpriteSystem _sprites = default!;
    [Dependency] private PointLightSystem _lights = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    private readonly Dictionary<EntityUid, (Vector2 Base, Vector2 Applied)> _offsets = new();
    private readonly Dictionary<EntityUid, (Angle BaseRotation, Angle AppliedRotation, Vector2 BaseScale, Vector2 AppliedScale)> _poses = new();
    private readonly HashSet<EntityUid> _seen = new();
    private readonly List<EntityUid> _finished = new();
    private TimeSpan _nextAim;
    private EntityUid? _crouching;
    private TimeSpan _crouchStarted;
    private EntityUid? _aimCaster;
    public EntityUid? LocalAimCaster => _aimCaster;
    public EntityUid? LocalAimUser { get; private set; }
    public MapCoordinates LocalAimPosition { get; private set; }
    public TimeSpan LocalAimStartedAt { get; private set; }

    public override void Initialize()
    {
        InitializeItems();
        SubscribeLocalEvent<CMUYautjaOpticalCloakComponent, BeforePostShaderRenderEvent>(OnCloakRender);
        SubscribeLocalEvent<CMUYautjaOpticalCloakComponent, ComponentShutdown>(OnCloakShutdown);
        SubscribeLocalEvent<CMUYautjaButcherVisualComponent, BeforePostShaderRenderEvent>(OnButcherRender);
        SubscribeLocalEvent<CMUYautjaButcherVisualComponent, ComponentShutdown>(OnButcherShutdown);
        _overlays.AddOverlay(new CMUYautjaEffectsOverlay(EntityManager));
        _overlays.AddOverlay(new CMUYautjaBackEffectsOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        RestoreItems();
        _overlays.RemoveOverlay<CMUYautjaEffectsOverlay>();
        _overlays.RemoveOverlay<CMUYautjaBackEffectsOverlay>();
        foreach (var uid in _offsets.Keys)
            RestoreOffset(uid);
        _offsets.Clear();
        RestorePoses();
        _heldVisuals.Clear();
    }

    private void OnCloakShutdown(Entity<CMUYautjaOpticalCloakComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprites.RemovePostShader(sprite, CloakShader);
    }

    private void OnButcherShutdown(Entity<CMUYautjaButcherVisualComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprites.RemovePostShader(sprite, ButcherShader);
    }

    private void EnsureShader(SpriteComponent sprite, string key, string prototype, bool screen = false)
    {
        if (_sprites.TryGetPostShader(sprite, key, out _))
            return;
        _sprites.SetPostShader(sprite, new SpriteComponent.PostShaderArgs(key,
            _prototypes.Index<ShaderPrototype>(prototype).InstanceUnique())
        {
            GetScreenTexture = screen,
            RaiseShaderEvent = true,
            Before = screen ? [] : ContentPostShaderIds.BeforeOutlines,
            // Outlining the opaque refracted background exposes the cloaked silhouette.
            After = screen ? ContentPostShaderIds.BeforeOutlines : [],
        });
    }

    public override void FrameUpdate(float frameTime)
    {
        UpdateExperimentLights();
        RestorePoses();
        UpdateReturningItems();
        var cloaks = EntityQueryEnumerator<CMUYautjaOpticalCloakComponent, SpriteComponent>();
        while (cloaks.MoveNext(out _, out var cloak, out var sprite))
        {
            if (cloak.Enabled || (_timing.CurTime - cloak.StartedAt).TotalSeconds <
                    CMUYautjaPresentation.CloakDuration)
                EnsureShader(sprite, CloakShader, "CMUYautjaOpticalCloak", true);
            else
                _sprites.RemovePostShader(sprite, CloakShader);
        }
        var bodies = EntityQueryEnumerator<CMUYautjaButcherVisualComponent, SpriteComponent>();
        while (bodies.MoveNext(out _, out _, out var sprite))
            EnsureShader(sprite, ButcherShader, "CMUYautjaButcher");
        var bolts = EntityQueryEnumerator<CMUYautjaPlasmaVisualComponent, SpriteComponent>();
        while (bolts.MoveNext(out var bolt, out var visual, out var sprite))
        {
            _sprites.SetVisible((bolt, sprite), false);
            // Only caster shots carry this component; other weapons share the underlying bullet prototypes.
            if (TryComp<PointLightComponent>(bolt, out var light))
                _lights.SetColor(bolt, CMUYautjaPlasmaAppearance.For(visual.Mode).Glow, light);
        }

        _seen.Clear();
        var motions = EntityQueryEnumerator<CMUYautjaMotionComponent, SpriteComponent>();
        while (motions.MoveNext(out var uid, out var motion, out var sprite))
        {
            if (motion.Kind != CMUYautjaEffect.FalconDeploy && _timing.CurTime >= motion.EndsAt)
                continue;
            var p = CMUYautjaPresentation.Progress(_timing.CurTime, motion.StartedAt, motion.EndsAt);
            var offset = Vector2.Zero;
            var angle = 0f;
            var scale = Vector2.One;
            switch (motion.Kind)
            {
                case CMUYautjaEffect.Leap:
                    offset.Y = MathF.Sin(p * MathF.PI) * 43f / 32f;
                    angle = .2f * MathF.Sin(p * MathF.PI);
                    break;
                case CMUYautjaEffect.Landing:
                    offset.Y = -MathF.Sin(p * MathF.PI) * 3f / 32;
                    break;
                case CMUYautjaEffect.Roar:
                    var drive = CMUYautjaPresentation.Segment(p, .15f, .28f) *
                                (1 - CMUYautjaPresentation.Segment(p, .62f, .82f));
                    offset.Y = MathF.Round(drive) / 32;
                    angle = .025f * MathF.Sin(p * 45) * drive;
                    scale.X = 1 + drive * .045f;
                    break;
                case CMUYautjaEffect.Flinch:
                    var flinch = Math.Clamp((p - .38f) / .28f, 0, 1);
                    offset = new Vector2(MathF.Sin(flinch * 25) * 2 * MathF.Sin(flinch * MathF.PI),
                        -MathF.Round(MathF.Sin(flinch * MathF.PI) * 2)) / 32;
                    break;
                case CMUYautjaEffect.FalconDeploy:
                    offset.Y = CMUYautjaPresentation.FalconHeight + MathF.Sin((float) _timing.CurTime.TotalSeconds * 3) / 96;
                    break;
            }
            ApplyOffset(uid, sprite, offset);
            if (angle != 0 || scale != Vector2.One)
                ApplyPose(uid, sprite, angle, scale);
            _seen.Add(uid);
        }
        UpdateLeapAnticipation();
        _finished.Clear();
        foreach (var uid in _offsets.Keys)
        {
            if (_seen.Contains(uid))
                continue;
            RestoreOffset(uid);
            _finished.Add(uid);
        }
        foreach (var uid in _finished)
            _offsets.Remove(uid);

        UpdateAim();
    }

    private void UpdateAim()
    {
        if (_player.LocalEntity is not { } player || _hands.GetActiveItem(player) is not { } caster ||
            !HasComp<YautjaCasterComponent>(caster))
        {
            LocalAimUser = null;
            _aimCaster = null;
            return;
        }

        if (LocalAimUser != player || _aimCaster != caster)
            LocalAimStartedAt = _timing.CurTime;
        LocalAimUser = player;
        _aimCaster = caster;
        var mouse = _eye.PixelToMap(_input.MouseScreenPosition);
        var origin = _transform.GetMapCoordinates(player);
        var delta = mouse.Position - origin.Position;
        LocalAimPosition = new MapCoordinates(origin.Position + delta / MathF.Max(1, delta.Length() / 20), mouse.MapId);
        // The owner's sight follows every rendered frame; only remote observers use the throttled update.
        if (_timing.CurTime >= _nextAim)
        {
            _nextAim = _timing.CurTime + TimeSpan.FromSeconds(.1);
            RaiseNetworkEvent(new CMUYautjaCasterAimEvent(GetNetEntity(caster), LocalAimPosition));
        }
    }

    private void UpdateLeapAnticipation()
    {
        if (_player.LocalEntity is not { } hunter || _seen.Contains(hunter) ||
            _ui.GetUIController<ActionUIController>().SelectingTargetFor is not { } selected ||
            !TryComp<EntityTargetActionComponent>(selected, out var action) ||
            action.Event is not YautjaHuntingLeapActionEvent || !TryComp<SpriteComponent>(hunter, out var sprite))
        {
            _crouching = null;
            return;
        }
        // Loading the jump while aiming gives it anticipation without delaying the authoritative attack.
        if (_crouching != hunter)
        {
            _crouching = hunter;
            _crouchStarted = _timing.CurTime;
        }
        var crouch = CMUYautjaPresentation.Segment((float) (_timing.CurTime - _crouchStarted).TotalSeconds, 0, .864f);
        var offset = new Vector2(0, -MathF.Round(crouch * 3) / 32);
        ApplyOffset(hunter, sprite, offset);
        ApplyPose(hunter, sprite, crouch * .1f, Vector2.One);
        _seen.Add(hunter);
    }

    private void OnCloakRender(Entity<CMUYautjaOpticalCloakComponent> ent, ref BeforePostShaderRenderEvent args)
    {
        if (args.Id != CloakShader)
            return;
        var enabled = ent.Comp.Enabled;
        // The gameplay invisibility component is set fully opaque during predicted decloaking,
        // before the server's reverse sweep arrives. Use the field's opacity, which never contains
        // that instantaneous reset, so the composed body, mask and armour stay behind the sweep.
        var field = CompOrNull<ThermalCloakUserComponent>(ent);
        var visibility = field?.CurrentOpacity ?? 0;
        SetBodyCoordinates(ent, args, bounds: _sprites.GetLocalBounds((ent.Owner, args.Sprite)));
        var origin = _transform.GetWorldPosition(ent);
        var pixel = (args.Viewport.WorldToLocal(origin + new Vector2(1f / 32, 0)) -
                     args.Viewport.WorldToLocal(origin)).Length();
        args.Shader.SetParameter("native_pixel", pixel);
        args.Shader.SetParameter("moving", TryComp<PhysicsComponent>(ent, out var physics) &&
            physics.LinearVelocity.LengthSquared() > .01f ? 1f : 0f);
        args.Shader.SetParameter("phase", (float) _timing.CurTime.TotalSeconds);
        args.Shader.SetParameter("retro", ent.Comp.Preset == YautjaInvisibilitySound.Retro ? 1f : 0f);
        args.Shader.SetParameter("visibility", enabled ? visibility : 0f);
        args.Shader.SetParameter("transition", CMUYautjaPresentation.CloakCoverage(ent.Comp, _timing.CurTime));
        args.Shader.SetParameter("rain", enabled && field is { Malfunction: true } ? 1f : 0f);
    }

    private void OnButcherRender(Entity<CMUYautjaButcherVisualComponent> ent, ref BeforePostShaderRenderEvent args)
    {
        if (args.Id != ButcherShader)
            return;
        var body = ent.Comp;
        var p = CMUYautjaPresentation.Progress(_timing.CurTime, body.StartedAt, body.EndsAt);
        var active = body.Hunter != null && _timing.CurTime < body.EndsAt;
        var cut = active ? CMUYautjaPresentation.Segment(p, .13f, .56f) : 0;
        var phase = CMUYautjaPresentation.ButcherPhase(body.CompletedStage,
            active && body.Procedure == YautjaButcherProcedure.Skin ? body.ActiveStage : 0, p);
        args.Shader.SetParameter("incision", CMUYautjaPresentation.Segment(phase, .08f, .21f));
        args.Shader.SetParameter("strip", CMUYautjaPresentation.Segment(phase, .30f, .45f));
        args.Shader.SetParameter("torso", CMUYautjaPresentation.Segment(phase, .55f, .71f));
        args.Shader.SetParameter("extracted", phase > .90f ? 1f : 0f);
        args.Shader.SetParameter("cut", body.Procedure != YautjaButcherProcedure.Skin ? cut : 0);
        args.Shader.SetParameter("part", Part(body.Procedure));
        args.Shader.SetParameter("blood", body.Blood);
        args.Shader.SetParameter("xeno", body.Xeno ? 1f : 0f);
        SetBodyCoordinates(ent, args, body.Xeno);
    }

    public Box2 GetButcherBounds(EntityUid uid, SpriteComponent sprite, bool xeno)
    {
        // Caste sprites have different frame sizes and layer offsets. Ignore held items and shields.
        if (xeno && _sprites.LayerMapTryGet((uid, sprite), XenoVisualLayers.Base, out var index, false) &&
            _sprites.TryGetLayer((uid, sprite), index, out var layer, false))
            return _sprites.GetLocalBounds(layer).Scale(Vector2.Abs(sprite.Scale));
        return Box2.CenteredAround(Vector2.Zero, Vector2.Abs(sprite.Scale));
    }

    private void SetBodyCoordinates(EntityUid ent, BeforePostShaderRenderEvent args, bool xeno = false, Box2? bounds = null)
    {
        // Match RenderSprite's local matrix, including prone rotation, scale and camera rotation.
        var sprite = args.Sprite;
        var rotation = sprite.NoRotation ? -(args.Viewport.Eye?.Rotation ?? Angle.Zero) : _transform.GetWorldRotation(ent);
        var origin = _transform.GetWorldPosition(ent) + rotation.RotateVec(sprite.Offset);
        rotation += sprite.Rotation;
        var bodyBounds = bounds ?? GetButcherBounds(ent, sprite, xeno);
        origin += rotation.RotateVec(bodyBounds.Center);
        var center = ScreenUv(args.Viewport, origin);
        var x = ScreenUv(args.Viewport, origin + rotation.RotateVec(new Vector2(bodyBounds.Width / 32, 0))) - center;
        var y = ScreenUv(args.Viewport, origin + rotation.RotateVec(new Vector2(0, -bodyBounds.Height / 32))) - center;
        var determinant = x.X * y.Y - y.X * x.Y;
        if (MathF.Abs(determinant) < 1e-12f)
            return;
        args.Shader.SetParameter("body_origin", center);
        args.Shader.SetParameter("body_x", new Vector2(y.Y, -y.X) / determinant);
        args.Shader.SetParameter("body_y", new Vector2(-x.Y, x.X) / determinant);
    }

    private void ApplyOffset(EntityUid uid, SpriteComponent sprite, Vector2 offset)
    {
        // AnimationPlayer can replace the offset between frames (notably the landing melee lunge).
        // Only undo our previous contribution if it is still the value on the sprite.
        var basis = _offsets.TryGetValue(uid, out var previous) && sprite.Offset == previous.Applied
            ? previous.Base : sprite.Offset;
        var applied = basis + offset;
        _offsets[uid] = (basis, applied);
        _sprites.SetOffset((uid, sprite), applied);
    }

    private void RestoreOffset(EntityUid uid)
    {
        if (_offsets.TryGetValue(uid, out var previous) && TryComp<SpriteComponent>(uid, out var sprite) &&
            sprite.Offset == previous.Applied)
            _sprites.SetOffset((uid, sprite), previous.Base);
    }

    private void ApplyPose(EntityUid uid, SpriteComponent sprite, Angle angle, Vector2 scale)
    {
        var rotation = sprite.Rotation + angle;
        var scaled = sprite.Scale * scale;
        _poses[uid] = (sprite.Rotation, rotation, sprite.Scale, scaled);
        _sprites.SetRotation((uid, sprite), rotation);
        _sprites.SetScale((uid, sprite), scaled);
    }

    private void RestorePoses()
    {
        foreach (var (uid, pose) in _poses)
        {
            if (!TryComp<SpriteComponent>(uid, out var sprite))
                continue;
            if (sprite.Rotation == pose.AppliedRotation)
                _sprites.SetRotation((uid, sprite), pose.BaseRotation);
            if (sprite.Scale == pose.AppliedScale)
                _sprites.SetScale((uid, sprite), pose.BaseScale);
        }
        _poses.Clear();
    }

    private static Vector2 ScreenUv(IClydeViewport viewport, Vector2 world)
    {
        var p = viewport.WorldToLocal(world) / viewport.Size;
        return new Vector2(p.X, 1 - p.Y);
    }

    private static Vector4 Part(YautjaButcherProcedure part) => part switch
    {
        YautjaButcherProcedure.Head => new(8, 9, 16, 10),
        YautjaButcherProcedure.RightArm => new(5, 10, 7, 12),
        YautjaButcherProcedure.LeftArm => new(21, 10, 7, 12),
        YautjaButcherProcedure.RightHand => new(5, 19, 7, 5),
        YautjaButcherProcedure.LeftHand => new(21, 19, 7, 5),
        YautjaButcherProcedure.RightLeg => new(10, 20, 6, 12),
        YautjaButcherProcedure.LeftLeg => new(16, 20, 6, 12),
        YautjaButcherProcedure.RightFoot => new(9, 28, 7, 4),
        YautjaButcherProcedure.LeftFoot => new(16, 28, 7, 4),
        _ => Vector4.Zero,
    };
}
