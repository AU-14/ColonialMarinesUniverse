using Content.Shared.AU14;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.AU14.Cultist;

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
        if (_prototype.TryIndex(ent.Comp.StatusIcon, out var iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }
}

