using System.Linq;
using Content.Server.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Xenonids.Maturing;
using Content.Shared.Doors.Components;
using Content.Shared.Interaction;
using Content.Shared.ParaDrop;
using Content.Shared.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
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
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        EntityUid ship = default;
        EntityUid target = default;
        EntityUid passenger = default;
        EntityUid ground = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            ship = LoadShip(entities, variant);
            pair.Server.ResolveDependency<IConfigurationManager>().SetCVar(RMCCVars.RMCDropshipCASDebug, true);
            ground = entities.System<SharedMapSystem>().CreateMap();
            target = entities.SpawnEntity(null, new EntityCoordinates(ground, 20, 20));
            entities.AddComponent<DropshipTargetComponent>(target);
            entities.AddComponent<DropshipDestinationComponent>(target);
            passenger = entities.SpawnEntity("CMMobHuman", new EntityCoordinates(ship, 0.5f, 0.5f));
            entities.EnsureComponent<ParaDroppableComponent>(passenger);
            var nav = entities.EntityQuery<DropshipNavigationComputerComponent>().Single();
            Assert.That(entities.System<SharedDropshipSystem>().FlyTo((nav.Owner, nav), target, null,
                startupTime: 0.5f, hyperspaceTime: 30f), Is.True);
        });
        await pair.RunSeconds(1);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var terminal = entities.EntityQuery<DropshipTerminalWeaponsComponent>().First(t => !t.Gunnery);
#pragma warning disable RA0002 // Supply a selected target without the unrelated laser-designator setup.
            terminal.Target = target;
#pragma warning restore RA0002
            entities.EventBus.RaiseLocalEvent(terminal.Owner,
                new DropShipTerminalWeaponsParaDropTargetSelectMsg(true) { Actor = passenger, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(entities.GetComponent<ActiveParaDropComponent>(ship).DropTarget, Is.EqualTo(target));
        });
        await pair.RunSeconds(1);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            foreach (var door in entities.EntityQuery<DoorComponent>()
                         .Where(d => d.Location is DoorLocation.Port or DoorLocation.Starboard))
                Assert.That(door.State, Is.EqualTo(DoorState.Open));
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
            foreach (var door in entities.EntityQuery<DoorComponent>()
                         .Where(d => d.Location is DoorLocation.Port or DoorLocation.Starboard))
                Assert.That(door.State, Is.EqualTo(DoorState.Closed));
            entities.DeleteEntity(passenger);
            entities.DeleteEntity(ship);
            entities.DeleteEntity(ground);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("omaha")]
    [TestCase("midway")]
    public async Task QueenCanUseTheHijackPointFromTheCabinAboveAColony(string variant)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var config = pair.Server.ResolveDependency<IConfigurationManager>();
            config.SetCVar(RMCCVars.RMCDropshipInitialDelayMinutes, 0f);
            config.SetCVar(RMCCVars.RMCDropshipHijackInitialDelayMinutes, 0);
            var ship = LoadShip(entities, variant);
            var lower = entities.GetComponent<MultiDeckDropshipComponent>(ship).Decks[-1];
            var ground = entities.GetComponent<TransformComponent>(lower).MapUid!.Value;
            entities.AddComponent<RMCPlanetComponent>(ground);
            var carrier = entities.System<SharedMapSystem>().CreateMap();
            entities.AddComponent<ShipFactionComponent>(carrier);
            var marker = entities.SpawnEntity(null, new EntityCoordinates(carrier, 10, 10));
            entities.AddComponent<DropshipHijackDestinationComponent>(marker);
            var nav = entities.EntityQuery<DropshipNavigationComputerComponent>().Single();
            var queen = entities.SpawnEntity("CMXenoQueen", entities.GetComponent<TransformComponent>(nav.Owner).Coordinates.Offset(new(0, -1)));
            entities.EnsureComponent<XenoMaturingComponent>(queen);
#pragma warning disable RA0002 // Skip the tested-elsewhere three-second console lockout do-after.
            nav.LockedOutUntil = pair.Server.ResolveDependency<IGameTiming>().CurTime + TimeSpan.FromMinutes(1);
#pragma warning restore RA0002
            var attempt = new ActivateInWorldEvent(queen, nav.Owner, true);
            entities.EventBus.RaiseLocalEvent(nav.Owner, attempt);
            var ui = entities.System<SharedUserInterfaceSystem>();
            Assert.That(ui.IsUiOpen(nav.Owner, DropshipHijackerUiKey.Key, queen), Is.True,
                "The cabin is one Z level above the RMCPlanet map; it must still permit a planetside queen hijack.");
            var state = (DropshipHijackerBuiState) entities.GetComponent<UserInterfaceComponent>(nav.Owner).States[DropshipHijackerUiKey.Key];
            Assert.That(state.CanHijack, Is.True, "The single hijack action must enable when the server has a valid carrier destination.");
            Assert.That(state.CanDeclineHijack, Is.True);
            entities.DeleteEntity(ship);
            entities.DeleteEntity(carrier);
        });
        await pair.CleanReturnAsync();
    }

    private static EntityUid LoadShip(IEntityManager entities, string variant)
    {
        entities.System<SharedMapSystem>().CreateMap(out var mapId);
        Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
            new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}.yml"), out var loaded), Is.True);
        return loaded!.Value.Owner;
    }
}
