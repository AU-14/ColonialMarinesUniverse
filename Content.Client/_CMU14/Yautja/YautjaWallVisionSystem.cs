using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.GameObjects;

namespace Content.Client._CMU14.Yautja;

public sealed class YautjaWallVisionSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    public override void Initialize()
    {
        _overlay.AddOverlay(new YautjaWallVisionOverlay(EntityManager, _players));
    }
}
