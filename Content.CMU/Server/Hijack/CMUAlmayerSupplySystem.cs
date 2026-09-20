using System.Linq;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.GameTicking;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14;
using Content.Shared.GameTicking;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Hijack;

public sealed class CMUAlmayerSupplySystem : EntitySystem
{
    [Dependency] private readonly PlatoonSpawnRuleSystem _platoons = default!;
    [Dependency] private readonly CMUZLevelsSystem _zLevels = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedIdCardSystem _cards = default!;
    [Dependency] private readonly SharedAccessSystem _access = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New != GameRunLevel.InRound)
            return;

        var query = AllEntityQuery<CMUAlmayerSupplyComponent>();
        while (query.MoveNext(out var map, out var supply))
            InitializeSupplies(map, supply);
    }

    public void InitializeSupplies(EntityUid map, CMUAlmayerSupplyComponent supply)
    {
        var query = AllEntityQuery<MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var grid, out _, out var transform))
        {
            if (transform.MapUid != map)
                continue;

            var faction = TryComp<ShipFactionComponent>(grid, out var ship) ? ship.Faction ?? "govfor" : "govfor";
            var selected = faction == "opfor" ? _platoons.SelectedOpforPlatoon : _platoons.SelectedGovforPlatoon;
            var platoon = selected ?? _prototypes.Index(supply.DefaultPlatoon);
            _platoons.SpawnShipVendors(grid, platoon, faction);
            Log.Info($"Almayer ship vendors initialized for {faction} platoon {platoon.ID}.");
        }
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (Transform(ev.Mob).MapUid is not { } map || !_cards.TryFindIdCard(ev.Mob, out var card))
            return;

        foreach (var deck in _zLevels.GetAllNetworkMaps(map))
        {
            if (!TryComp<CMUAlmayerSupplyComponent>(deck, out var supply))
                continue;

            AdaptLegacyCard(card.Owner, supply);
            break;
        }
    }

    /// <summary>
    /// The standalone map still offers RMC crew jobs. Preserve their existing ID
    /// rights and add equivalent Govfor rights, without granting access to visitors.
    /// CMU jobs already carry Govfor access and need no changes.
    /// </summary>
    public void AdaptLegacyCard(EntityUid card, CMUAlmayerSupplyComponent supply)
    {
        if (!TryComp<AccessComponent>(card, out var access))
            return;

        var tags = access.Tags.ToHashSet();
        foreach (var tag in access.Tags)
            if (supply.LegacyAccess.TryGetValue(tag, out var mapped))
                tags.Add(mapped);
        _access.TrySetTags(card, tags, access);
    }
}
