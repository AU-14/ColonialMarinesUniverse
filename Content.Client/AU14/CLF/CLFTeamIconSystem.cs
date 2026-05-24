using Content.Shared.AU14.CLF;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.AU14.CLF;

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
        if (_prototype.TryIndex(ent.Comp.StatusIcon, out var iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }
}


