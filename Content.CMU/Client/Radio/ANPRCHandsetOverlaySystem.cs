using Robust.Client.Graphics;

namespace Content.Client._AU14.Radio;

public sealed partial class ANPRCHandsetOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        if (!_overlay.HasOverlay<ANPRCHandsetOverlay>())
            _overlay.AddOverlay(new ANPRCHandsetOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<ANPRCHandsetOverlay>();
    }
}
