using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._CMU14.Medical.Overlays;

public sealed class CMUOverlaysSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private CMUPainShockOverlay? _painOverlay;

    public override void Initialize()
    {
        base.Initialize();
        _painOverlay = new CMUPainShockOverlay(EntityManager, _player, _proto, _timing);
        _overlayManager.AddOverlay(_painOverlay);
    }

    public override void Shutdown()
    {
        if (_painOverlay is not null)
        {
            _overlayManager.RemoveOverlay(_painOverlay);
            _painOverlay = null;
        }
        base.Shutdown();
    }
}
