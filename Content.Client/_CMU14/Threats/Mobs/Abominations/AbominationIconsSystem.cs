using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;
using AbominationMimicComponent = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationMimicComponent;

namespace Content.Client._CMU14.Threats.Mobs.Abominations;

/// <summary>
///     Client-side overlay that paints the AbominationFaction icon on every
///     currently-disguised mimic. The FactionIcon prototype's showTo filter
///     gates it to viewers that have AbominationComponent, so the icon is
///     only ever rendered for other abominations.
/// </summary>
public sealed partial class AbominationIconsSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    public static readonly ProtoId<FactionIconPrototype> AbominationFactionIcon = "AbominationFaction";

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationMimicComponent, GetStatusIconsEvent>(OnGetStatusIcons);
    }

    private void OnGetStatusIcons(Entity<AbominationMimicComponent> ent, ref GetStatusIconsEvent args)
    {
        if (_prototype.TryIndex(AbominationFactionIcon, out FactionIconPrototype? iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }
}
