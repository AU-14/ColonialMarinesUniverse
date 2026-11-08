using Robust.Client.Graphics;

namespace Content.Client.CMU14.Round.Antags.EarlyWarning;

/// <summary>Registers THE LIGHT's vignette once; it only paints while the local player is caught.</summary>
public sealed class EWJoeCablesOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        if (!_overlay.HasOverlay<EWJoeCablesOverlay>())
            _overlay.AddOverlay(new EWJoeCablesOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<EWJoeCablesOverlay>();
    }
}
