using Content.Shared._RMC14.Xenonids.Screech;
using Robust.Client.Graphics;

namespace Content.Client._RMC14.Xenonids.Screech;

public sealed partial class ScreechBlindOverlay
{
    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return ShouldDrawForViewport(args.Viewport);
    }

    internal bool ShouldDrawForViewport(IClydeViewport viewport)
    {
        // Attachment changes can leave the overlay installed after its local effect stops
        // applying. Match Draw's prerequisites before Clyde copies the screen.
        return _entityManager.TryGetComponent(_playerManager.LocalEntity, out ScreechBlindComponent? _) &&
               viewport.Eye != null;
    }
}
