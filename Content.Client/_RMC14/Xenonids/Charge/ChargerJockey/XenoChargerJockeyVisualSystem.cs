using Content.Client._RMC14.Buckle;
using Content.Client._RMC14.Sprite;
using Content.Client._RMC14.Xenonids;
using Content.Client._RMC14.Xenonids.Hide;
using Content.Shared._RMC14.Sprite;
using Content.Shared._RMC14.Xenonids.Charge.ChargerJockey;
using RmcDrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._RMC14.Xenonids.Charge.ChargerJockey;

public sealed partial class XenoChargerJockeyVisualSystem : EntitySystem
{
    [Dependency] private RMCSpriteSystem _rmcSprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoChargerRidingComponent, AfterAutoHandleStateEvent>(OnRiderState);
        SubscribeLocalEvent<XenoChargerRidingComponent, GetDrawDepthEvent>(
            OnGetDrawDepth,
            after: [typeof(XenoHideVisualizerSystem), typeof(XenoVisualizerSystem), typeof(RMCBuckleVisualsSystem)]);

        EntityManager.ComponentRemoved += OnComponentRemoved;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        EntityManager.ComponentRemoved -= OnComponentRemoved;
    }

    private void OnRiderState(Entity<XenoChargerRidingComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _rmcSprite.UpdateDrawDepth(ent.Owner);
    }

    private void OnComponentRemoved(RemovedComponentEventArgs args)
    {
        if (args.Terminating || args.BaseArgs.Component is not XenoChargerRidingComponent)
            return;

        _rmcSprite.UpdateDrawDepth(args.BaseArgs.Owner);
    }

    private void OnGetDrawDepth(Entity<XenoChargerRidingComponent> ent, ref GetDrawDepthEvent args)
    {
        args.DrawDepth = (RmcDrawDepth) ent.Comp.DrawDepth;
    }
}
