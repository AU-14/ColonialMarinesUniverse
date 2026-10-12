using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client.CMU14.Visor;

public sealed partial class CMUFriendlyGrenadeOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _player = default!;

    public override void Initialize()
    {
        _overlay.AddOverlay(new CMUFriendlyGrenadeOverlay(EntityManager, _player));
    }

    public override void Shutdown()
    {
        _overlay.RemoveOverlay<CMUFriendlyGrenadeOverlay>();
    }
}
