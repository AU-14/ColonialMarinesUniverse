using Content.Shared.CMU14.Yautja;
using Content.Shared.Ghost.Components;

namespace Content.Client.Verbs;

public sealed partial class VerbSystem
{
    // the cloak tags preds HideContextMenu so players can't right-click spam to find them.
    // ghosts aren't playing, let them still inspect
    public bool CMUShowHiddenInMenu(EntityUid viewer, EntityUid target)
    {
        return HasComp<GhostComponent>(viewer) && HasComp<YautjaComponent>(target);
    }
}
