using Content.Shared._RMC14.Construction;

namespace Content.Shared.Construction.EntitySystems;

public sealed partial class AnchorableSystem
{
    private bool RMCAllowsAnchorOverlap(EntityUid anchoringEntity, EntityUid anchoredEntity)
    {
        var ev = new RMCCheckTileFreeEvent(anchoredEntity);
        RaiseLocalEvent(anchoringEntity, ref ev);
        return ev.IsTileFree;
    }
}
