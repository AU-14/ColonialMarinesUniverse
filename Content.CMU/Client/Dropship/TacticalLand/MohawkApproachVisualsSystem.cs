using System.Numerics;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Dropship.TacticalLand;

public sealed partial class MohawkApproachVisualsSystem : EntitySystem
{
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private SpriteSystem _sprites = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly EntProtoId BeaconPrototype = "CMUMohawkApproachBeacon";
    private MohawkApproachRoofOverlay _roof = default!;

    public override void Initialize()
    {
        _roof = new MohawkApproachRoofOverlay(EntityManager, _timing);
        _overlays.AddOverlay(_roof);
        _overlays.AddOverlay(new MohawkLandingBorderOverlay(EntityManager, _timing));
        SubscribeLocalEvent<MohawkLandingBorderComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<MohawkLandingBorderComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MohawkApproachVisualsComponent, ComponentShutdown>(OnVisualsShutdown);
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<MohawkApproachRoofOverlay>();
        _overlays.RemoveOverlay<MohawkLandingBorderOverlay>();
    }

    private void OnState(Entity<MohawkLandingBorderComponent> ent, ref AfterAutoHandleStateEvent args)
        => _roof.Invalidate(ent);

    private void OnShutdown(Entity<MohawkLandingBorderComponent> ent, ref ComponentShutdown args)
    {
        _roof.Invalidate(ent);
        RemComp<MohawkApproachVisualsComponent>(ent);
    }

    private void OnVisualsShutdown(Entity<MohawkApproachVisualsComponent> ent, ref ComponentShutdown args)
        => ClearLights(ent.Comp);

    public override void FrameUpdate(float frameTime)
    {
        var seconds = (float) _timing.CurTime.TotalSeconds;
        var query = EntityQueryEnumerator<MohawkLandingBorderComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var approach, out var metadata))
        {
            if (approach.Bounds.IsEmpty())
                continue;

            var visuals = EnsureComp<MohawkApproachVisualsComponent>(uid);
            if ((metadata.Flags & MetaDataFlags.Detached) != 0)
            {
                ClearLights(visuals);
                continue;
            }

            if (visuals.Bounds != approach.Bounds || visuals.Lights.Count == 0)
            {
                ClearLights(visuals);
                visuals.Bounds = approach.Bounds;
                for (var i = 0; i < MohawkApproachAnimation.BeaconCount; i++)
                {
                    visuals.Lights.Add(SpawnAttachedTo(BeaconPrototype,
                        new EntityCoordinates(uid, MohawkApproachAnimation.BeaconPosition(approach.Bounds, i))));
                }
            }

            for (var i = 0; i < visuals.Lights.Count; i++)
                _lights.SetEnergy(visuals.Lights[i], 2.2f + 1.6f * MohawkApproachAnimation.Pulse(seconds, i));

            if (TryComp<SpriteComponent>(uid, out var sprite))
            {
                var progress = MohawkApproachAnimation.Progress(approach, _timing.CurTime);
                var height = MohawkApproachAnimation.Height(progress);
                _sprites.SetScale((uid, sprite), new Vector2(1f + height * 0.025f));
                _sprites.SetColor((uid, sprite), Color.White.WithAlpha(0.6f + progress * 0.4f));
            }
        }
    }

    private void ClearLights(MohawkApproachVisualsComponent visuals)
    {
        foreach (var light in visuals.Lights)
        {
            if (TerminatingOrDeleted(light))
                continue;

            _lights.SetEnabled(light, false);
            QueueDel(light);
        }
        visuals.Lights.Clear();
    }
}
