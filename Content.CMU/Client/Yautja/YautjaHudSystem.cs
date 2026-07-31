using Content.Shared._CMU14.Yautja;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._CMU14.Yautja;

public sealed partial class YautjaHudSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly Dictionary<YautjaMarkKind, StatusIconData> _icons = new();
    private bool _cached;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaMarkComponent, GetStatusIconsEvent>(OnGetStatusIcons);
    }

    private void OnGetStatusIcons(Entity<YautjaMarkComponent> ent, ref GetStatusIconsEvent args)
    {
        if (HasComp<YautjaComponent>(ent))
            return;

        if (_player.LocalEntity is not { } viewer || !HasComp<YautjaHudViewerComponent>(viewer))
            return;

        EnsureCached();
        foreach (var mark in ent.Comp.Marks.Keys)
        {
            if (_icons.TryGetValue(mark, out var icon))
                args.StatusIcons.Add(icon);
        }
    }

    private void EnsureCached()
    {
        if (_cached)
            return;

        _cached = true;
        Cache(YautjaMarkKind.Prey, "CMUYautjaIconPrey");
        Cache(YautjaMarkKind.Honored, "CMUYautjaIconHonored");
        Cache(YautjaMarkKind.Dishonored, "CMUYautjaIconDishonored");
        Cache(YautjaMarkKind.GearCarrier, "CMUYautjaIconGearCarrier");
        Cache(YautjaMarkKind.Thrall, "CMUYautjaIconThrall");
        Cache(YautjaMarkKind.Student, "CMUYautjaIconStudent");
        Cache(YautjaMarkKind.Blooded, "CMUYautjaIconBlooded");
    }

    private void Cache(YautjaMarkKind kind, ProtoId<HealthIconPrototype> id)
    {
        if (_prototypes.TryIndex(id, out var proto))
            _icons[kind] = proto;
    }
}
