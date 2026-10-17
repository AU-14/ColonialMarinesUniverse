using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Fighter;

/// <summary>Grounded silhouettes and light pools give the hull a readable height above the pad.</summary>
public sealed class FighterVtolFloorOverlay(IEntityManager entities) : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;
    private readonly SharedTransformSystem _transform = entities.System<SharedTransformSystem>();
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private readonly Texture _silhouette = entities.System<SpriteSystem>().Frame0(
        new SpriteSpecifier.Rsi(new ResPath("/Textures/CMU14/Vehicles/Fighter/jetfighter.rsi"), "vtolmode"));
    private readonly Texture _foldedSilhouette = entities.System<SpriteSystem>().Frame0(
        new SpriteSpecifier.Rsi(new ResPath("/Textures/CMU14/Vehicles/Fighter/jetfighter.rsi"), "folded"));
    private readonly FighterParticleBatch _floor = new();

    protected override void Draw(in OverlayDrawArgs args)
    {
        var now = _timing.CurTime;
        var h = args.WorldHandle;
        var hulls = entities.EntityQueryEnumerator<FighterGroundComponent, TransformComponent>();
        while (hulls.MoveNext(out var uid, out var ground, out var xform))
        {
            if (xform.MapID != args.MapId || !args.WorldAABB.Enlarged(6).Contains(_transform.GetWorldPosition(uid))) continue;
            var active = ground.State is FighterGroundState.TakingOff or FighterGroundState.Landing;
            var lift = active ? FighterVtol.Lift(FighterVtol.Progress(now, ground.StartedAt, ground.EndsAt), ground.State == FighterGroundState.Landing) : 0;
            // Keep the silhouette on the surface while the sprite rises; soften using several faint copies.
            h.SetTransform(_transform.GetWorldMatrix(uid));
            var half = new Vector2(266, 335) / 128 * FighterGroundComponent.SizeMultiplier * (1 - lift * .15f);
            var offset = new Vector2(.12f + lift * .28f, .15f + lift * .2f);
            for (var i = 0; i < 5; i++)
            {
                var drift = new Vector2(MathF.Cos(i * MathF.Tau / 5), MathF.Sin(i * MathF.Tau / 5)) * (.02f + lift * .1f);
                h.DrawTextureRect(ground.State == FighterGroundState.Grounded ? _foldedSilhouette : _silhouette,
                    new Box2(offset + drift - half, offset + drift + half),
                    Color.Black.WithAlpha((1 - lift * .8f) * .14f));
            }
        }
        _floor.Clear();
        var effects = entities.EntityQueryEnumerator<FighterVtolVisualComponent, TransformComponent>();
        while (effects.MoveNext(out var uid, out var visual, out var xform))
        {
            if (xform.MapID != args.MapId || !args.WorldAABB.Enlarged(6).Contains(_transform.GetWorldPosition(uid))) continue;
            var power = FighterVtolPresentation.Power(visual, now);
            var origin = _transform.GetWorldPosition(uid);
            var rotation = _transform.GetWorldRotation(uid);
            for (var side = -1; side <= 1; side += 2)
            {
                var nozzle = origin + rotation.RotateVec(FighterVtolPresentation.Nozzle(side));
                _floor.Mote(nozzle, new Vector2(1.05f, .8f), Color.FromHex("#EEAA67").WithAlpha(power * .35f));
                _floor.Mote(nozzle, new Vector2(.3f, .25f), Color.FromHex("#F6DCAC").WithAlpha(power * .48f));
            }
        }
        h.SetTransform(Matrix3x2.Identity);
        _floor.Draw(h, (float) now.TotalSeconds);
    }

    protected override void DisposeBehavior()
    {
        _floor.Dispose();
        base.DisposeBehavior();
    }
}
