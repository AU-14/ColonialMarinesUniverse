using Content.Shared._RMC14.Xenonids.Doom;
using Robust.Client.Graphics;

namespace Content.Client._RMC14.Xenonids.Doom;

public sealed partial class DoomOverlay
{
    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return ShouldDrawForViewport(args.Viewport);
    }

    internal bool ShouldDrawForViewport(IClydeViewport viewport)
    {
        // The owner can register this overlay for a remote doomed entity. Reject the pass
        // before Clyde copies the screen, using the same local prerequisites as Draw.
        return _entityManager.TryGetComponent(_playerManager.LocalEntity, out MobDoomedComponent? _) &&
               viewport.Eye != null;
    }
}
