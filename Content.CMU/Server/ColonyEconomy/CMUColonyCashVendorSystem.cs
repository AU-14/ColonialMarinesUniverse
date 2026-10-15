using Content.Server.GameTicking;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.Marines;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.GameTicking;
using Content.Shared.VendingMachines.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.ColonyEconomy;

/// <summary>
/// Food, drinks, smokes and entertainment cost money. At round start this swaps the free food, drink,
/// cigarette, recreation and clothing vendors on the planet and on the GOVFOR/OPFOR ships for
/// cash-operated CMU versions.
/// </summary>
public sealed partial class CMUColonyCashVendorSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly Dictionary<string, EntProtoId> Replacements = new()
    {
        // Food & drink
        ["CMVendorSnack"] = "AU14CashVendorSnackMachine",
        ["RMCVendorSnackSimple"] = "CMUCashVendorSnackSimple",
        ["CMVendorCoffee"] = "AU14CashVendorHotDrinks",
        ["RMCVendorCoffeeSimple"] = "CMUCashVendorCoffeeSimple",
        ["CMVendorCola"] = "AU14CashVendorSodas",
        ["RMCVendorColaResearch"] = "CMUCashVendorColaResearch",
        ["RMCVendorColaSPP"] = "CMUCashVendorColaSPP",
        ["CMVendorSodaSoviet"] = "CMUCashVendorSodaSoviet",
        ["CMVendorBooze"] = "CMUCashVendorBooze",

        // Cigarettes
        ["CMVendorCigs"] = "AU14CashVendorCigarettes",
        ["AU14VendorKoorCigs"] = "CMUCashVendorKoorCigs",
        ["RMCVendorCigsWeYa"] = "CMUCashVendorCigsWeYa",
        ["RMCVendorCigsElectro"] = "CMUCashVendorCigsElectro",
        ["RMCVendorCigsSPP"] = "CMUCashVendorCigsSPP",

        // Recreation
        ["CMVendorCassettes"] = "AU14CashVendorGames",
        ["CMVendorChess"] = "CMUCashVendorChess",

        // Clothing
        ["AU14CivilianClothingVendor"] = "CMUCashVendorCivilianClothing",
    };

    private readonly List<(EntityUid Uid, EntProtoId Replacement)> _toReplace = new();
    private readonly HashSet<EntityUid> _shipMaps = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        // Maps are loaded and initialized by the time the round goes live.
        if (ev.New != GameRunLevel.InRound)
            return;

        _toReplace.Clear();
        CollectShipMaps();
        Collect(EntityQueryEnumerator<VendingMachineComponent>());
        Collect(EntityQueryEnumerator<CMAutomatedVendorComponent>());

        foreach (var (uid, replacement) in _toReplace)
        {
            Replace(uid, replacement);
        }

        if (_toReplace.Count > 0)
            Log.Info($"Replaced {_toReplace.Count} free vendors on the planet and ships with cash vendors.");

        _toReplace.Clear();
        _shipMaps.Clear();
    }

    /// <summary>
    /// Maps holding a GOVFOR/OPFOR ship grid, or marked as a warship. Multi-deck dropships also carry a
    /// ship faction but are dropships, so they are left alone.
    /// </summary>
    private void CollectShipMaps()
    {
        _shipMaps.Clear();

        var ships = EntityQueryEnumerator<ShipFactionComponent, TransformComponent>();
        while (ships.MoveNext(out var uid, out _, out var xform))
        {
            if (!HasComp<MultiDeckDropshipComponent>(uid) && xform.MapUid is { } map)
                _shipMaps.Add(map);
        }

        var warships = EntityQueryEnumerator<WarshipComponent, TransformComponent>();
        while (warships.MoveNext(out var uid, out _, out var xform))
        {
            _shipMaps.Add(xform.MapUid ?? uid);
        }
    }

    private void Collect<T>(EntityQueryEnumerator<T> query) where T : IComponent
    {
        while (query.MoveNext(out var uid, out _))
        {
            if (MetaData(uid).EntityPrototype?.ID is not { } id ||
                !Replacements.TryGetValue(id, out var replacement) ||
                !IsOnPlanetOrShip(uid))
            {
                continue;
            }

            _toReplace.Add((uid, replacement));
        }
    }

    private bool IsOnPlanetOrShip(EntityUid uid)
    {
        if (Transform(uid).MapUid is not { } map)
            return false;

        if (IsPlanetOrShipMap(map))
            return true;

        // Only the main level is marked, so check the rest of its Z-levels.
        if (!TryComp(map, out CMUZLevelMapComponent? level))
            return false;

        return IsLevelOnPlanetOrShip(level, true) || IsLevelOnPlanetOrShip(level, false);
    }

    private bool IsPlanetOrShipMap(EntityUid map)
    {
        return HasComp<RMCPlanetComponent>(map) || _shipMaps.Contains(map);
    }

    private bool IsLevelOnPlanetOrShip(CMUZLevelMapComponent level, bool up)
    {
        var next = up ? level.MapAbove : level.MapBelow;
        while (next is { } map)
        {
            if (IsPlanetOrShipMap(map))
                return true;

            if (!TryComp(map, out CMUZLevelMapComponent? nextLevel))
                return false;

            next = up ? nextLevel.MapAbove : nextLevel.MapBelow;
        }

        return false;
    }

    private void Replace(EntityUid uid, EntProtoId replacement)
    {
        var xform = Transform(uid);
        var coordinates = xform.Coordinates;
        var rotation = xform.LocalRotation;
        var anchored = xform.Anchored;

        QueueDel(uid);

        var vendor = Spawn(replacement, coordinates);
        var vendorXform = Transform(vendor);
        _transform.SetLocalRotation(vendor, rotation, vendorXform);
        if (anchored && !vendorXform.Anchored)
            _transform.AnchorEntity(vendor, vendorXform);
    }
}
