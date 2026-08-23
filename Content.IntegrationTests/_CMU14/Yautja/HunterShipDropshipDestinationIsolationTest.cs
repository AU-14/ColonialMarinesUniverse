using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server._RMC14.Dropship;
using Content.Shared._RMC14.Dropship;
using Content.Shared.AU14.Round;
using Content.Shared.Shuttles.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class HunterShipDropshipDestinationIsolationTest
{
    private static readonly string[] HunterDestinationPrototypes =
    [
        "CMUHunterShipYautjaLandingPadAFTLBeacon",
        "CMUHunterShipYautjaLandingPadBFTLBeacon",
        "CMUHunterShipYautjaHangarA",
    ];

    [Test]
    public async Task HunterDestinationsAreOfferedOnlyToYautjaConsole()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid ertConsole = default;
        EntityUid ordinaryConsole = default;
        EntityUid yautjaConsole = default;
        EntityUid human = default;
        EntityUid yautja = default;
        var destinations = new List<EntityUid>();

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            foreach (var prototype in HunterDestinationPrototypes)
                destinations.Add(entMan.SpawnEntity(prototype, map.GridCoords));

            ertConsole = entMan.SpawnEntity("CMComputerDropshipNavigationThirdParty", map.GridCoords);
            ordinaryConsole = entMan.SpawnEntity("CMComputerDropshipNavigationOpfor", map.GridCoords.Offset(new Vector2(2, 0)));
            yautjaConsole = entMan.SpawnEntity("CMUYautjaHunterShuttleConsole", map.GridCoords.Offset(new Vector2(4, 0)));

            var transform = entMan.System<SharedTransformSystem>();
            transform.SetCoordinates(ertConsole, map.GridCoords);
            transform.SetCoordinates(ordinaryConsole, map.GridCoords.Offset(new Vector2(2, 0)));
            transform.SetCoordinates(yautjaConsole, map.GridCoords.Offset(new Vector2(4, 0)));
            human = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            yautja = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(5, 0)));

            var ui = entMan.System<UserInterfaceSystem>();
            Assert.That(ui.TryOpenUi(ertConsole, DropshipNavigationUiKey.Key, human), Is.True);
            Assert.That(ui.TryOpenUi(ordinaryConsole, DropshipNavigationUiKey.Key, human), Is.True);
            Assert.That(ui.TryOpenUi(yautjaConsole, DropshipNavigationUiKey.Key, yautja), Is.True);
        });

        await pair.RunTicksSync(1);

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            entMan.EventBus.RaiseLocalEvent(ertConsole, new AfterActivatableUIOpenEvent(human, human));
            entMan.EventBus.RaiseLocalEvent(ordinaryConsole, new AfterActivatableUIOpenEvent(human, human));
            entMan.EventBus.RaiseLocalEvent(yautjaConsole, new AfterActivatableUIOpenEvent(yautja, yautja));
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ui = entMan.System<UserInterfaceSystem>();
            var hunterNetEntities = destinations.Select(uid => entMan.GetNetEntity(uid)).ToHashSet();

            Assert.That(ui.TryGetUiState<DropshipNavigationDestinationsBuiState>(
                ertConsole, DropshipNavigationUiKey.Key, out var ertState), Is.True);
            Assert.That(ui.TryGetUiState<DropshipNavigationDestinationsBuiState>(
                ordinaryConsole, DropshipNavigationUiKey.Key, out var ordinaryState), Is.True);
            Assert.That(ui.TryGetUiState<DropshipNavigationDestinationsBuiState>(
                yautjaConsole, DropshipNavigationUiKey.Key, out var yautjaState), Is.True);

            Assert.That(ertState!.Destinations.Select(x => x.Id).Intersect(hunterNetEntities), Is.Empty);
            Assert.That(ordinaryState!.Destinations.Select(x => x.Id).Intersect(hunterNetEntities), Is.Empty);
            Assert.That(hunterNetEntities.IsSubsetOf(yautjaState!.Destinations.Select(x => x.Id)), Is.True);

            Assert.That(entMan.GetComponent<WhitelistedShuttleComponent>(ertConsole).Faction, Is.EqualTo("thirdparty"));
            Assert.That(entMan.GetComponent<WhitelistedShuttleComponent>(ordinaryConsole).Faction, Is.EqualTo("opfor"));
            Assert.That(entMan.GetComponent<WhitelistedShuttleComponent>(yautjaConsole).Faction, Is.EqualTo("yautja"));

            foreach (var destination in destinations)
                Assert.That(entMan.GetComponent<DropshipDestinationComponent>(destination).FactionController, Is.EqualTo("yautja"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonYautjaFlyToRequestsAreRejectedBeforeFtl()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid destination = default;
        EntityUid ertConsole = default;
        EntityUid ordinaryConsole = default;
        EntityUid yautjaConsole = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            entMan.EnsureComponent<ShuttleComponent>(map.Grid.Owner);

            destination = entMan.SpawnEntity("CMUHunterShipYautjaLandingPadAFTLBeacon", map.GridCoords.Offset(new Vector2(8, 0)));
            ertConsole = entMan.SpawnEntity("CMComputerDropshipNavigationThirdParty", map.GridCoords);
            ordinaryConsole = entMan.SpawnEntity("CMComputerDropshipNavigationOpfor", map.GridCoords.Offset(new Vector2(2, 0)));
            yautjaConsole = entMan.SpawnEntity("CMUYautjaHunterShuttleConsole", map.GridCoords.Offset(new Vector2(4, 0)));

            var transform = entMan.System<SharedTransformSystem>();
            transform.SetCoordinates(ertConsole, map.GridCoords);
            transform.SetCoordinates(ordinaryConsole, map.GridCoords.Offset(new Vector2(2, 0)));
            transform.SetCoordinates(yautjaConsole, map.GridCoords.Offset(new Vector2(4, 0)));
        });

        await pair.RunTicksSync(1);

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var dropship = entMan.System<DropshipSystem>();
            var ertComputer = (ertConsole, entMan.GetComponent<DropshipNavigationComputerComponent>(ertConsole));
            var ordinaryComputer = (ordinaryConsole, entMan.GetComponent<DropshipNavigationComputerComponent>(ordinaryConsole));
            var yautjaComputer = (yautjaConsole, entMan.GetComponent<DropshipNavigationComputerComponent>(yautjaConsole));

            Assert.That(entMan.GetComponent<TransformComponent>(yautjaConsole).GridUid, Is.EqualTo(map.Grid.Owner));
            Assert.That(entMan.HasComponent<ShuttleComponent>(map.Grid.Owner), Is.True);

            Assert.That(dropship.FlyTo(ertComputer, destination, null), Is.False);
            Assert.That(dropship.FlyTo(ordinaryComputer, destination, null), Is.False);
            Assert.That(entMan.HasComponent<FTLComponent>(map.Grid.Owner), Is.False);
            Assert.That(entMan.GetComponent<DropshipDestinationComponent>(destination).Ship, Is.Null);

            Assert.That(dropship.FlyTo(yautjaComputer, destination, null), Is.True);
            Assert.That(entMan.GetComponent<DropshipDestinationComponent>(destination).Ship, Is.EqualTo(map.Grid.Owner));
        });

        await pair.CleanReturnAsync();
    }
}
