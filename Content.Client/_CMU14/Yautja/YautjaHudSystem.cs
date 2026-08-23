using System.Linq;
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
    private readonly Dictionary<YautjaRank, StatusIconData> _rankIcons = new();
    private readonly Dictionary<(YautjaMilitaryCaste Caste, bool Whitelist), StatusIconData> _militaryIcons = new();
    private StatusIconData? _bloodedThrallIcon;
    private bool _cached;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaMarkComponent, GetStatusIconsEvent>(OnGetStatusIcons);
        SubscribeLocalEvent<YautjaComponent, GetStatusIconsEvent>(OnGetRankStatusIcons);
        SubscribeLocalEvent<YautjaMilitaryCasteComponent, GetStatusIconsEvent>(OnGetMilitaryStatusIcons);
        SubscribeLocalEvent<YautjaFalconHudIconComponent, GetStatusIconsEvent>(OnFalconGetStatusIcons);
    }

    private void OnGetRankStatusIcons(Entity<YautjaComponent> ent, ref GetStatusIconsEvent args)
    {
        if (HasComp<YautjaMilitaryCasteComponent>(ent.Owner))
            return;

        if (!CanSeeYautjaRankIcon(ent.Owner))
            return;

        EnsureCached();
        var rank = Enum.IsDefined(ent.Comp.ClanRank) ? ent.Comp.ClanRank : YautjaRank.Blooded;
        if (_rankIcons.TryGetValue(rank, out var icon))
            args.StatusIcons.Add(icon);
    }

    private void OnGetMilitaryStatusIcons(Entity<YautjaMilitaryCasteComponent> ent, ref GetStatusIconsEvent args)
    {
        if (!CanSeeYautjaRankIcon(ent.Owner))
            return;

        EnsureCached();
        if (_militaryIcons.TryGetValue((ent.Comp.Caste, ent.Comp.WhitelistIcon), out var icon))
            args.StatusIcons.Add(icon);
    }

    private void OnGetStatusIcons(Entity<YautjaMarkComponent> ent, ref GetStatusIconsEvent args)
    {
        if (!HasYautjaHudViewer())
            return;

        EnsureCached();
        AddIconsForMarks(ent.Comp.Marks.Keys, args.StatusIcons);
    }

    private void OnFalconGetStatusIcons(Entity<YautjaFalconHudIconComponent> ent, ref GetStatusIconsEvent args)
    {
        if (!HasYautjaHudViewer())
            return;

        if (_prototypes.TryIndex(ent.Comp.Icon, out var icon))
            args.StatusIcons.Add(icon);
    }

    public void AddIconsForMarks(IReadOnlyCollection<YautjaMarkKind> marks, List<StatusIconData> icons)
    {
        EnsureCached();

        AddIfPresent(marks, icons, YautjaMarkKind.Prey);

        if (marks.Contains(YautjaMarkKind.Dishonored))
            AddIfPresent(marks, icons, YautjaMarkKind.Dishonored);
        else
            AddIfPresent(marks, icons, YautjaMarkKind.Honored);

        var thralled = marks.Contains(YautjaMarkKind.Thrall);
        var blooded = marks.Contains(YautjaMarkKind.Blooded);

        if (thralled)
        {
            if (blooded && _bloodedThrallIcon is { } bloodedThrall)
                icons.Add(bloodedThrall);
            else
                AddIfPresent(marks, icons, YautjaMarkKind.Thrall);
        }
        else
        {
            AddIfPresent(marks, icons, YautjaMarkKind.GearCarrier);

            if (blooded)
                AddIfPresent(marks, icons, YautjaMarkKind.Blooded);
        }

        AddIfPresent(marks, icons, YautjaMarkKind.Student);
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

        foreach (var rank in YautjaRankMetadata.Order)
        {
            var id = new ProtoId<HealthIconPrototype>($"CMUYautjaRankIcon{rank}");
            if (_prototypes.TryIndex(id, out var icon))
                _rankIcons[rank] = icon;
        }

        CacheMilitary(YautjaMilitaryCaste.Soldier, "CMUYautjaMilitarySoldierIcon");
        CacheMilitary(YautjaMilitaryCaste.Enforcer, "CMUYautjaMilitaryEnforcerIcon");
        CacheMilitary(YautjaMilitaryCaste.Soldier, "CMUYautjaMilitarySoldierWhitelistIcon", whitelist: true);
        CacheMilitary(YautjaMilitaryCaste.Enforcer, "CMUYautjaMilitaryEnforcerWhitelistIcon", whitelist: true);

        var bloodedThrallId = new ProtoId<HealthIconPrototype>("CMUYautjaIconBloodedThrall");
        if (_prototypes.TryIndex(bloodedThrallId, out var bloodedThrall))
            _bloodedThrallIcon = bloodedThrall;
    }

    private void Cache(YautjaMarkKind kind, ProtoId<HealthIconPrototype> id)
    {
        if (_prototypes.TryIndex(id, out var proto))
            _icons[kind] = proto;
    }

    private void CacheMilitary(YautjaMilitaryCaste caste, ProtoId<HealthIconPrototype> id, bool whitelist = false)
    {
        if (_prototypes.TryIndex(id, out var proto))
            _militaryIcons[(caste, whitelist)] = proto;
    }

    private void AddIfPresent(IReadOnlyCollection<YautjaMarkKind> marks, List<StatusIconData> icons, YautjaMarkKind kind)
    {
        if (marks.Contains(kind) && _icons.TryGetValue(kind, out var icon))
            icons.Add(icon);
    }

    private bool HasYautjaHudViewer()
    {
        return _player.LocalEntity is { } viewer && HasComp<YautjaHudViewerComponent>(viewer);
    }

    private bool CanSeeYautjaRankIcon(EntityUid target)
    {
        if (_player.LocalEntity is not { } viewer)
            return false;

        // A Yautja must see their own rank in-game even before equipping the mask.
        // Other entities only receive rank icons through the mask HUD, as in CMSS13.
        return viewer == target || HasComp<YautjaHudViewerComponent>(viewer);
    }
}
