using Robust.Client.Graphics;

namespace Content.Client._RMC14.Flora;

public sealed partial class TallGrassOcclusionSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        if (!_overlay.HasOverlay<TallGrassOcclusionOverlay>())
            _overlay.AddOverlay(new TallGrassOcclusionOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<TallGrassOcclusionOverlay>();
    }
}
