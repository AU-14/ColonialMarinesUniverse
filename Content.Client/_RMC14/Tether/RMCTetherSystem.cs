using Content.Shared._RMC14.Tether;
using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client._RMC14.Tether;

public sealed partial class RMCTetherSystem : SharedRMCTetherSystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new RMCTetherOverlay(EntityManager, _playerManager));
    }
}
