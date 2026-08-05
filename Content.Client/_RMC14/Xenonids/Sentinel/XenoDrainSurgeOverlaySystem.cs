using Robust.Client.Graphics;

namespace Content.Client._RMC14.Xenonids.Sentinel;

public sealed partial class XenoDrainSurgeOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        _overlay.AddOverlay(new XenoDrainSurgeOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        _overlay.RemoveOverlay<XenoDrainSurgeOverlay>();
    }
}
