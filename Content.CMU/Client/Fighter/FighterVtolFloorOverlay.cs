using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Fighter;

/// <summary>Grounded silhouettes and light pools give the hull a readable height above the pad.</summary>
public sealed class FighterVtolFloorOverlay(IEntityManager entities) : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;
    private readonly SharedTransformSystem _transform = entities.System<SharedTransformSystem>();
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private readonly SpriteSystem _sprites = entities.System<SpriteSystem>();
    private readonly IPrototypeManager _prototypes = IoCManager.Resolve<IPrototypeManager>();
    private readonly Dictionary<(ResPath, string), Texture> _silhouettes = new();
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
            // Keep the silhouette on the surface while the sprite rises; soften using several faint copies.
            var definition = _prototypes.Index(entities.GetComponentOrNull<CMUFighterAppearanceComponent>(uid)?.Airframe ?? CMUFighterAirframePrototype.Default);
            var lift = active ? CMUFighterTransition.Lift(FighterVtol.Progress(now, ground.StartedAt, ground.EndsAt), ground.State == FighterGroundState.Landing, definition) : 0;
            var state = ground.State == FighterGroundState.Grounded ? definition.GroundState : definition.VtolState;
            var key = (definition.Sprite, state);
            if (!_silhouettes.TryGetValue(key, out var silhouette))
                _silhouettes.Add(key, silhouette = _sprites.Frame0(new SpriteSpecifier.Rsi(definition.Sprite, state)));
            h.SetTransform(Matrix3x2.CreateRotation((float) definition.SpriteRotation.Theta) * _transform.GetWorldMatrix(uid));
            var half = definition.SpriteSize / 128 * FighterGroundComponent.SizeMultiplier * (1 - lift * .15f);
            var offset = new Vector2(.12f + lift * .28f, .15f + lift * .2f);
            for (var i = 0; i < 5; i++)
            {
                var drift = new Vector2(MathF.Cos(i * MathF.Tau / 5), MathF.Sin(i * MathF.Tau / 5)) * (.02f + lift * .1f);
                h.DrawTextureRect(silhouette,
                    new Box2(offset + drift - half, offset + drift + half),
                    Color.Black.WithAlpha((1 - lift * .8f) * .14f));
            }
        }
        _floor.Clear();
        var effects = entities.EntityQueryEnumerator<FighterVtolVisualComponent, TransformComponent>();
        while (effects.MoveNext(out var uid, out var visual, out var xform))
        {
            if (xform.MapID != args.MapId || !args.WorldAABB.Enlarged(6).Contains(_transform.GetWorldPosition(uid))) continue;
            var definition = _prototypes.Index(visual.Airframe);
            var power = FighterVtolPresentation.Power(visual, definition, now);
            var rotation = _transform.GetWorldRotation(uid);
            var origin = _transform.GetWorldPosition(uid) + rotation.RotateVec(FighterVtolPresentation.Roll(visual, definition, now));
            foreach (var socket in definition.EngineOffsets)
            {
                var nozzle = origin + rotation.RotateVec(FighterVtolPresentation.Socket(socket));
                _floor.Mote(nozzle, new Vector2(.5f, .7f), definition.ExhaustColor.WithAlpha(power * .2f));
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
