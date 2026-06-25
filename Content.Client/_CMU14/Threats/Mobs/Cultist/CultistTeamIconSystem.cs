using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;
using CultistComponent = Content.Shared._CMU14.Threats.Mobs.Cultist.CultistComponent;

namespace Content.Client._CMU14.Threats.Mobs.Cultist;

public sealed partial class CultistTeamIconSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CultistComponent, GetStatusIconsEvent>(OnGetCultistIcon);
    }

    private void OnGetCultistIcon(Entity<CultistComponent> ent, ref GetStatusIconsEvent args)
    {
        if (_prototype.TryIndex(ent.Comp.StatusIcon, out FactionIconPrototype? iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }
}
