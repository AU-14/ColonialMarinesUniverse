using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.GameObjects;

namespace Content.Client.CMU14.Yautja;

public sealed partial class YautjaWallVisionSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _players = default!;

    public override void Initialize()
    {
        _overlay.AddOverlay(new YautjaWallVisionOverlay(EntityManager, _players));
    }
}
