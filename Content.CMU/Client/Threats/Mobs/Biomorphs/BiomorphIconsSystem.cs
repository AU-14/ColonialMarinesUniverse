using Content.Shared.CMU14.Threats.Mobs.Biomorph;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Threats.Mobs.Biomorphs;

/// <summary>
///     Client-side overlay that paints the AbominationFaction icon on every
///     currently-disguised mimic. The FactionIcon prototype's showTo filter
///     gates it to viewers that have AbominationComponent, so the icon is
///     only ever rendered for other abominations.
/// </summary>
public sealed partial class BiomorphIconsSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    public static readonly ProtoId<FactionIconPrototype> BiomorphFactionIcon = "BiomorphFaction";

    public override void Initialize()
    {
        SubscribeLocalEvent<BiomorphMimicComponent, GetStatusIconsEvent>(OnGetStatusIcons);
    }

    private void OnGetStatusIcons(Entity<BiomorphMimicComponent> ent, ref GetStatusIconsEvent args)
    {
        if (_prototype.TryIndex(BiomorphFactionIcon, out FactionIconPrototype? iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }
}
