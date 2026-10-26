using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Xenomorphs.Reaper;

public sealed partial class CMUFleshBloomTelegraphSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new CMUFleshBloomTelegraphOverlay(EntityManager, _timing, _transform));
        _overlays.AddOverlay(new CMUReaperRitualOverlay(EntityManager, _timing, _transform));
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<CMUFleshBloomTelegraphOverlay>();
        _overlays.RemoveOverlay<CMUReaperRitualOverlay>();
        base.Shutdown();
    }
}
