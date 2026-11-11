using Robust.Client.Graphics;

namespace Content.Client.CMU14.Threats.Mobs.Wendigo.Scent;

/// <summary>Registers the Wendigo smell trail overlay.</summary>
public sealed partial class CMUWendigoScentOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new CMUWendigoScentOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<CMUWendigoScentOverlay>();
    }
}
