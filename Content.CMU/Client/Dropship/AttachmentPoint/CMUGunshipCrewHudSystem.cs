using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client.CMU14.Dropship.AttachmentPoint;

public sealed partial class CMUGunshipCrewHudSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;

    public override void Initialize()
        => _overlays.AddOverlay(new CMUGunshipCrewHudOverlay(EntityManager, _player));

    public override void Shutdown()
        => _overlays.RemoveOverlay<CMUGunshipCrewHudOverlay>();
}
