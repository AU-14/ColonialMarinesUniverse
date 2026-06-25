using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;
using CLFMemberComponent = Content.Shared._CMU14.Threats.Mobs.CLF.CLFMemberComponent;

namespace Content.Client._CMU14.Threats.Mobs.CLF;

public sealed partial class CLFTeamIconSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CLFMemberComponent, GetStatusIconsEvent>(OnGetCLFIcon);
    }

    private void OnGetCLFIcon(Entity<CLFMemberComponent> ent, ref GetStatusIconsEvent args)
    {
        if (_prototype.TryIndex(ent.Comp.StatusIcon, out FactionIconPrototype? iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }
}
