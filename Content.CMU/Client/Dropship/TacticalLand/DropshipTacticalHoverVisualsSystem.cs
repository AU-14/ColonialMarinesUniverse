using System.Numerics;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.Dropship.TacticalLand;

public sealed partial class DropshipTacticalHoverVisualsSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    private DropshipTacticalHoverOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new DropshipTacticalHoverOverlay(EntityManager);
        _overlays.AddOverlay(_overlay);
        SubscribeLocalEvent<DropshipTacticalHoverShadowComponent, AfterAutoHandleStateEvent>(OnShadowState);
        SubscribeLocalEvent<DropshipTacticalHoverShadowComponent, ComponentShutdown>(OnShadowShutdown);
        SubscribeLocalEvent<DropshipTacticalHoverDownwashComponent, ComponentShutdown>(OnNozzleShutdown);
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<DropshipTacticalHoverOverlay>();
        base.Shutdown();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var shadows = EntityQueryEnumerator<DropshipTacticalHoverShadowComponent, SpriteComponent>();
        while (shadows.MoveNext(out var uid, out var shadow, out var sprite))
        {
            var scale = new Vector2(Math.Max(1, shadow.Footprint.X), Math.Max(1, shadow.Footprint.Y));
            if (sprite.Scale != scale)
                _sprite.SetScale((uid, sprite), scale);
        }
    }

    private void OnShadowState(Entity<DropshipTacticalHoverShadowComponent> ent, ref AfterAutoHandleStateEvent args)
        => _overlay.Invalidate(ent.Owner);

    private void OnShadowShutdown(Entity<DropshipTacticalHoverShadowComponent> ent, ref ComponentShutdown args)
        => _overlay.Invalidate(ent.Owner);

    private void OnNozzleShutdown(Entity<DropshipTacticalHoverDownwashComponent> ent, ref ComponentShutdown args)
        => _overlay.RemoveNozzle(ent.Owner);
}
