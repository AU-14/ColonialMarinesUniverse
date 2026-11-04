using Robust.Client.Graphics;

namespace Content.Client.CMU14.Vehicle;

public sealed class CMUBlackoutSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        _overlays.AddOverlay(new CMUBlackoutOverlay());
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<CMUBlackoutOverlay>();
    }
}
