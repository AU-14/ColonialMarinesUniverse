using Content.Shared._AU14.Abominations;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._AU14.Abominations;

/// <summary>
/// Client-side overlay that paints the AbominationFaction icon on every
/// currently-disguised mimic. The FactionIcon prototype's showTo filter
/// gates it to viewers that have AbominationComponent, so the icon is
/// only ever rendered for other abominations.
/// </summary>
public sealed class ShowAbominationIconsSystem : EntitySystem
{
    public static readonly ProtoId<FactionIconPrototype> AbominationFactionIcon = "AbominationFaction";

    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationMimicTransformedComponent, GetStatusIconsEvent>(OnGetStatusIcons);
    }

    private void OnGetStatusIcons(Entity<AbominationMimicTransformedComponent> ent, ref GetStatusIconsEvent args)
    {
        if (_prototype.TryIndex(AbominationFactionIcon, out var iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }
}
