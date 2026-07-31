using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._CMU14.Dropship.TacticalLand;

public sealed partial class HolographicWarningSignSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        _overlay.AddOverlay(new HolographicWarningSignOverlay(EntityManager, _timing));
    }

    public override void Shutdown()
    {
        _overlay.RemoveOverlay<HolographicWarningSignOverlay>();
    }
}
