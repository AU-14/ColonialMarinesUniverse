using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Visuals;

public sealed partial class CMUGasMaskVignetteSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private CMUGasMaskVignetteOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new CMUGasMaskVignetteOverlay(EntityManager, _player, _prototypes);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
    }
}
