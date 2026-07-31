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

    // The icon prototype whose visibility rules every CLF/INSFOR faction icon is rendered by: only fellow
    // cell members (and antag-icon viewers) see it, and it stays lit in the dark. Change here if the base
    // CLF team icon prototype is ever renamed.
    private const string BaseIconId = "CLFFaction";

    private void OnGetCLFIcon(Entity<CLFMemberComponent> ent, ref GetStatusIconsEvent args)
    {
        if (!_prototype.TryIndex(ent.Comp.StatusIcon, out FactionIconPrototype? iconPrototype))
            return;

        // Render whatever icon the faction chose using the CLF team icon's own visibility rules rather than
        // the picked prototype's. A custom faction icon otherwise brings its own (usually empty) showTo and
        // shading, so it wouldn't restrict to teammates or stay visible in the dark like the default one.
        var showTo = iconPrototype.ShowTo;
        var isShaded = iconPrototype.IsShaded;
        if (_prototype.TryIndex<FactionIconPrototype>(BaseIconId, out var baseIcon))
        {
            showTo = baseIcon.ShowTo;
            isShaded = false;
        }

        args.StatusIcons.Add(new StatusIconData
        {
            Icon = iconPrototype.Icon,
            Priority = iconPrototype.Priority,
            LocationPreference = iconPrototype.LocationPreference,
            Layer = iconPrototype.Layer,
            Offset = iconPrototype.Offset,
            VisibleToGhosts = iconPrototype.VisibleToGhosts,
            HideInContainer = iconPrototype.HideInContainer,
            HideOnStealth = iconPrototype.HideOnStealth,
            ShowTo = showTo,
            IsShaded = isShaded,
        });
    }
}
