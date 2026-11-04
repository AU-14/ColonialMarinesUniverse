using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Fighter;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Fighter;

/// <summary>Localized exhaust refraction; inactive viewports never copy the screen.</summary>
public sealed class FighterVtolHeatOverlay(IEntityManager entities) : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    public override bool RequestScreenTexture => true;
    private const int MaximumJets = 4;
    private readonly SharedTransformSystem _transform = entities.System<SharedTransformSystem>();
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private readonly IConfigurationManager _config = IoCManager.Resolve<IConfigurationManager>();
    private readonly IPrototypeManager _prototypes = IoCManager.Resolve<IPrototypeManager>();
    private static readonly ProtoId<ShaderPrototype> HeatShader = "CMUFighterVtolHeat";
    private readonly ShaderInstance _shader = IoCManager.Resolve<IPrototypeManager>().Index(HeatShader).InstanceUnique();
    private readonly Vector2[] _positions = new Vector2[MaximumJets];
    private readonly Vector2[] _axisX = new Vector2[MaximumJets];
    private readonly Vector2[] _axisY = new Vector2[MaximumJets];
    private readonly float[] _strength = new float[MaximumJets];
    private int _count;

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        _count = 0;
        if (args.Viewport.Eye == null || _config.GetCVar(CCVars.DisableHeatDistortion)) return false;
        var query = entities.EntityQueryEnumerator<FighterVtolVisualComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var visual, out var xform))
        {
            if (xform.MapID != args.MapId) continue;
            var definition = _prototypes.Index(visual.Airframe);
            var power = FighterVtolPresentation.Power(visual, definition, _timing.CurTime);
            if (power < .01f) continue;
            var matrix = _transform.GetWorldMatrix(uid);
            var center = Vector2.Zero;
            foreach (var socket in definition.EngineOffsets) center += FighterVtolPresentation.Socket(socket);
            center /= Math.Max(1, definition.EngineOffsets.Length);
            var lift = CMUFighterTransition.Lift(FighterVtol.Progress(_timing.CurTime, visual.StartedAt, visual.EndsAt), visual.Landing, definition);
            center = center * (1 + lift * .28f) + new Vector2(0, lift * 2.5f - definition.ExhaustLength * .5f);
            center += FighterVtolPresentation.Roll(visual, definition, _timing.CurTime);
            var origin = Vector2.Transform(center, matrix);
            if (!args.WorldAABB.Enlarged(4).Contains(origin)) continue;
            var screen = args.Viewport.WorldToLocal(origin);
            var x = args.Viewport.WorldToLocal(origin + Vector2.TransformNormal(Vector2.UnitX * FighterGroundComponent.SizeMultiplier, matrix)) - screen;
            var y = args.Viewport.WorldToLocal(origin + Vector2.TransformNormal(Vector2.UnitY * FighterGroundComponent.SizeMultiplier, matrix)) - screen;
            var size = (Vector2) args.Viewport.Size;
            _positions[_count] = new Vector2(screen.X / size.X, 1 - screen.Y / size.Y);
            _axisX[_count] = new Vector2(x.X / size.X, -x.Y / size.Y);
            _axisY[_count] = new Vector2(y.X / size.X, -y.Y / size.Y);
            _strength[_count] = power * (1 - FighterVtol.Smooth((lift - .55f) / .45f));
            if (++_count == MaximumJets) break;
        }
        return _count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null) return;
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("positions", _positions);
        _shader.SetParameter("axisX", _axisX);
        _shader.SetParameter("axisY", _axisY);
        _shader.SetParameter("strength", _strength);
        _shader.SetParameter("count", _count);
        _shader.SetParameter("clock", (float) _timing.CurTime.TotalSeconds);
        args.WorldHandle.SetTransform(Matrix3x2.Identity);
        args.WorldHandle.UseShader(_shader);
        args.WorldHandle.DrawRect(args.WorldBounds, Color.White);
        args.WorldHandle.UseShader(null);
    }

    protected override void DisposeBehavior()
    {
        _shader.Dispose();
        base.DisposeBehavior();
    }
}
