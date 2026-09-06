using Content.Client._CMU14.Dropship.TacticalLand;
using Robust.Client.UserInterface;

// ReSharper disable once CheckNamespace
namespace Content.Client.Viewport;

public sealed partial class ScalingViewport
{
    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        if (_entityManager.System<GunshipPilotInputSystem>().TryAdjustThrustFromMouseWheel(args.Delta.Y))
            args.Handle();
    }
}
