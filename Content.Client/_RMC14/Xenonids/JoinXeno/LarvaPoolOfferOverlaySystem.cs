using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.Xenonids.JoinXeno;

public sealed partial class LarvaPoolOfferOverlaySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IResourceCache _resources = default!;

    public override void Initialize()
    {
        if (!_overlays.HasOverlay<LarvaPoolOfferOverlay>())
        {
            _overlays.AddOverlay(new LarvaPoolOfferOverlay(
                EntityManager,
                _timing,
                _players,
                _resources));
        }
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<LarvaPoolOfferOverlay>();
    }
}
