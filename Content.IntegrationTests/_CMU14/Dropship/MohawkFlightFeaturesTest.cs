using System.Linq;
using Content.Server.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Xenonids.Maturing;
using Content.Shared.ActionBlocker;
using Content.Shared.Doors.Components;
using Content.Shared.Interaction;
using Content.Shared.ParaDrop;
using Content.Shared.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkFlightFeaturesTest
{
    [TestCase("omaha")]
    [TestCase("midway")]
    public async Task ParadropTargetingOpensSideExitsAndClosesThemWhenTargetIsLost(string variant)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Connected = true });
        EntityUid ship = default;
        EntityUid target = default;
        EntityUid passenger = default;
        EntityUid ground = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            ship = LoadShip(entities, variant);
            pair.Server.ResolveDependency<IConfigurationManager>().SetCVar(RMCCVars.RMCDropshipCASDebug, true);
            var maps = entities.System<SharedMapSystem>();
            ground = maps.CreateMap();
            var grid = entities.EnsureComponent<MapGridComponent>(ground);
            var plating = new Tile(pair.Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            maps.SetTile(ground, grid, new Vector2i(20, 20), plating);
            target = entities.SpawnEntity(null, new EntityCoordinates(ground, 20.5f, 20.5f));
            entities.AddComponent<DropshipTargetComponent>(target);
            entities.AddComponent<DropshipDestinationComponent>(target);
            var cabin = new EntityCoordinates(ship, 0.5f, 0.5f);
            passenger = entities.SpawnEntity("CMMobHuman", cabin);
            var parachute = entities.SpawnEntity("RMCParachute", cabin);
            Assert.That(entities.System<InventorySystem>().TryEquip(passenger, parachute, "back"), Is.True);
            var nav = OnShip<DropshipNavigationComputerComponent>(entities, ship);
            Assert.That(entities.System<SharedDropshipSystem>().FlyTo((nav.Owner, nav), target, null,
                startupTime: 0.5f, hyperspaceTime: 30f), Is.True);
        });
        await pair.RunSeconds(1);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            pair.Server.PlayerMan.SetAttachedEntity(pair.Player!, passenger);
            var terminal = OnShip<DropshipTerminalWeaponsComponent>(entities, ship, t => !t.Comp.Gunnery);
#pragma warning disable RA0002 // Supply a selected target without the unrelated laser-designator setup.
            terminal.Comp.Target = target;
#pragma warning restore RA0002
            entities.EventBus.RaiseLocalEvent(terminal.Owner,
                new DropShipTerminalWeaponsParaDropTargetSelectMsg(true) { Actor = passenger, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(entities.GetComponent<ActiveParaDropComponent>(ship).DropTarget, Is.EqualTo(target));
        });
        await pair.RunSeconds(1);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var sideDoors = AllOnShip<DoorComponent>(entities, ship)
                .Where(d => d.Comp.Location is DoorLocation.Port or DoorLocation.Starboard)
                .ToList();
            Assert.That(sideDoors, Is.Not.Empty);
            foreach (var door in sideDoors)
                Assert.That(door.Comp.State, Is.EqualTo(DoorState.Open));
            Assert.That(entities.GetComponent<MohawkMechanismsComponent>(ship).RampDeployed, Is.False);

            // Leave the cabin onto its real FTL map, as a passenger exiting a side hatch does.
            var flightMap = entities.GetComponent<TransformComponent>(ship).MapUid!.Value;
            entities.System<SharedTransformSystem>().SetCoordinates(passenger, new EntityCoordinates(flightMap, 50, 50));
            Assert.That(entities.HasComponent<ParaDroppingComponent>(passenger), Is.True);
            entities.DeleteEntity(target);
        });
        await pair.RunSeconds(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.HasComponent<ActiveParaDropComponent>(ship), Is.False);
            foreach (var door in AllOnShip<DoorComponent>(entities, ship)
                         .Where(d => d.Comp.Location is DoorLocation.Port or DoorLocation.Starboard))
                Assert.That(door.Comp.State, Is.EqualTo(DoorState.Closed));
        });
        await pair.RunSeconds(4);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.EntityExists(passenger), Is.True, "The parachutist must survive the entire descent.");
            Assert.That(entities.System<MobStateSystem>().IsAlive(passenger), Is.True);
            Assert.That(entities.GetComponent<TransformComponent>(passenger).MapUid, Is.EqualTo(ground));
            Assert.That(pair.Player!.AttachedEntity, Is.EqualTo(passenger));
            Assert.That(entities.System<ActionBlockerSystem>().CanMove(passenger), Is.True);
            entities.DeleteEntity(passenger);
            entities.DeleteEntity(ship);
            entities.DeleteEntity(ground);
        });
        await pair.CleanReturnAsync();
    }

    // pooled servers can still have consoles and doors from earlier tests, so only look at this ship
    private static Entity<T> OnShip<T>(IEntityManager entities, EntityUid ship, Func<Entity<T>, bool>? filter = null) where T : IComponent
    {
        return AllOnShip<T>(entities, ship).First(e => filter?.Invoke(e) ?? true);
    }

    private static IEnumerable<Entity<T>> AllOnShip<T>(IEntityManager entities, EntityUid ship) where T : IComponent
    {
        var grids = new HashSet<EntityUid> { ship };
        if (entities.TryGetComponent(ship, out MultiDeckDropshipComponent? decks))
            grids.UnionWith(decks.Decks.Values);

        return entities.QueryEntities<T>()
            .Where(e => entities.GetComponent<TransformComponent>(e.Owner).GridUid is { } grid && grids.Contains(grid));
    }

    private static EntityUid LoadShip(IEntityManager entities, string variant)
    {
        entities.System<SharedMapSystem>().CreateMap(out var mapId);
        Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
            new ResPath($"/Maps/CMU14/Dropships/Mohawk/{variant}.yml"), out var loaded), Is.True);
        return loaded!.Value.Owner;
    }
}
