using Robust.Client.Graphics;

namespace Content.Client.CMU14.Hypospray;

public sealed partial class CMUHyposprayOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new CMUHyposprayOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<CMUHyposprayOverlay>();
    }
}
