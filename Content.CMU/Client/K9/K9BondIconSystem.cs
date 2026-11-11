using Content.Shared._RMC14.K9.Components;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.K9;

/// <summary>
/// Draws the bond icon over a K9 dog's bonded owner, visible only to the dog itself.
/// </summary>
public sealed partial class K9BondIconSystem : EntitySystem
{
    private static readonly ProtoId<FactionIconPrototype> BondIcon = "CMUK9Bond";

    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        // MobStateComponent is already taken for GetStatusIconsEvent by XenoHudSystem;
        // the engine allows only one subscription per component/event pair.
        SubscribeLocalEvent<K9HandlerComponent, GetStatusIconsEvent>(OnGetStatusIcons);
    }

    private void OnGetStatusIcons(Entity<K9HandlerComponent> ent, ref GetStatusIconsEvent args)
    {
        if (_player.LocalEntity is not { } viewer
            || !TryComp<K9DogComponent>(viewer, out var dog)
            || !ent.Comp.Dogs.Contains(viewer))
            return;

        args.StatusIcons.Add(_prototypes.Index(BondIcon));
    }
}
