#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server._RMC14.Requisitions;
using Content.Server.AU14.Round;
using Content.Server.AU14.Scenario;
using Content.Server.CMU.Round;
using Content.Server.GameTicking.Presets;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Access.Components;
using Content.Shared.AU14;
using Content.Shared.AU14.ColonyEconomy;
using Content.Shared.AU14.util;
using Content.Shared.CMU.Round;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests._CMU14.Round;

[TestFixture]
public sealed class AuRoundCutoffSelectionTest
{
    private const int PlayerCount = 40;
    private const string FixedFactionPresetId = "CMUTestFixedFactionPreset";
    private const string FixedBothSidesPresetId = "CMUTestFixedBothSidesPreset";
    private const string MissingAsrsPresetId = "CMUTestMissingAsrsPreset";
    private const string ShepherdsPridePlanetId = "AUPlanetShepherdsPride";
    private static readonly ProtoId<PlatoonPrototype> HazopsPlatoon = "HAZOPS";
    private static readonly ProtoId<GamePresetPrototype> JailbreakPreset = "Jailbreak";
    private static readonly ProtoId<GamePresetPrototype> PrometheusPreset = "Prometheus";
    private static readonly ProtoId<PlatoonPrototype> RmcPlatoon = "RMC";
    private static readonly ProtoId<PlatoonPrototype> UppPlatoon = "UPP";
    private static readonly ProtoId<PlatoonPrototype> UscmPlatoon = "USCM";
    private static readonly ProtoId<PlatoonPrototype> WeyuPlatoon = "WEYU";

    private const string DuplicateAsrsProfile = """
        - type: entity
          id: CMUTestDuplicateUSCMAsrsProfile
          categories: [ HideSpawnMenu ]
          components:
          - type: RoundForceAsrsProfile
            forceId: USCM
        """;

    private const string ReplenishingAsrsProfile = """
        - type: entity
          id: CMUTestReplenishingAsrsProfile
          categories: [ HideSpawnMenu ]
          components:
          - type: RoundForceAsrsProfile
            forceId: CMUTestMissingAsrsPlatoon
            categories:
            - id: Test
              name: Test
              offers:
              - id: Test_Limited
                crate: RMCCrateClothingMagazinePouchesLarge
                cost: 100
                stock:
                  maximum: 2
                  replenishDelay: 1
                  startingStock: 1
                  replenishAmount: 5
        """;

    [TestPrototypes]
    private static readonly string FixedFactionPreset = $"""
        - type: GamePlanetPool
          id: CMUTestFixedFactionPlanetPool
          planets:
          - AUPlanetShepherdsPride

        - type: platoon
          id: CMUTestMissingAsrsPlatoon
          name: CMU missing ASRS profile test
          possibleships:
          - USSBushRedux

        - type: entity
          id: CMUTestMissingAsrsPlanet
          components:
          - type: RMCPlanetMapPrototype
            map: /Maps/_CMU14/sheperds.yml
            mapId: Sheperds
            platoonsGovfor:
            - CMUTestMissingAsrsPlatoon
            defaultgovfor: CMUTestMissingAsrsPlatoon

        - type: GamePlanetPool
          id: CMUTestMissingAsrsPlanetPool
          planets:
          - CMUTestMissingAsrsPlanet

        - type: gamePreset
          id: {FixedFactionPresetId}
          name: CMU fixed faction test
          description: Tests pool-only fixed-faction round planning.
          showInVote: false
          usesGovforPlatoon: true
          threatSelectionMode: PostRoundstartVote
          usesThreatSpawnDelay: true
          planetPool: CMUTestFixedFactionPlanetPool
          rules: []

        - type: gamePreset
          id: {FixedBothSidesPresetId}
          name: CMU fixed both-sides test
          description: Tests committed typed assignments for both military sides.
          showInVote: false
          usesGovforPlatoon: true
          usesOpforPlatoon: true
          planetPool: CMUTestFixedFactionPlanetPool
          rules: []

        - type: gamePreset
          id: {MissingAsrsPresetId}
          name: CMU missing ASRS profile test
          description: Tests catalog validation before the round plan is committed.
          showInVote: false
          usesGovforPlatoon: true
          planetPool: CMUTestMissingAsrsPlanetPool
          rules: []
        """;

    [Test]
    public async Task FixedBothSidesCommitTypedDefaultAssignments()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var round = server.System<AuRoundSystem>();
            var director = server.System<CMURoundDirectorSystem>();
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

            var selection = director.FreezeSelection(PlayerCount, FixedBothSidesPresetId);

            Assert.Multiple(() =>
            {
                Assert.That(
                    selection.GovforAssignment,
                    Is.EqualTo(
                        new RoundForceAssignment(
                            RoundSide.Govfor,
                            new RoundForceId("USCM"),
                            "USSBushRedux")));
                Assert.That(
                    selection.OpforAssignment,
                    Is.EqualTo(
                        new RoundForceAssignment(
                            RoundSide.Opfor,
                            new RoundForceId("UPP"),
                            "USSBushRedux")));
                Assert.That(director.Selection, Is.EqualTo(selection));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FreezeCommitsStablePerSideAsrsCatalogsUntilReset()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var round = server.System<AuRoundSystem>();
            var director = server.System<CMURoundDirectorSystem>();
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

            Assert.Multiple(() =>
            {
                Assert.That(
                    director.TryGetCommittedAsrsCatalog(RoundSide.Govfor, out var govforBeforeFreeze),
                    Is.False);
                Assert.That(govforBeforeFreeze, Is.Null);
                Assert.That(
                    director.TryGetCommittedAsrsCatalog(RoundSide.Opfor, out var opforBeforeFreeze),
                    Is.False);
                Assert.That(opforBeforeFreeze, Is.Null);
            });

            round.SetPreset(prototypes.Index<GamePresetPrototype>(FixedBothSidesPresetId));
            Assert.Multiple(() =>
            {
                Assert.That(
                    director.TrySetLegacyPlanet(ShepherdsPridePlanetId),
                    Is.EqualTo(CMURoundSelectionMutationResult.Applied));
                Assert.That(
                    director.TrySetLegacyForce(RoundSide.Govfor, prototypes.Index(WeyuPlatoon)),
                    Is.EqualTo(CMURoundSelectionMutationResult.Applied));
                Assert.That(
                    director.TrySetLegacyForce(RoundSide.Opfor, prototypes.Index(UppPlatoon)),
                    Is.EqualTo(CMURoundSelectionMutationResult.Applied));
            });

            var selection = director.FreezeSelection(PlayerCount, FixedBothSidesPresetId);
            Assert.That(
                director.TryGetCommittedAsrsCatalog(RoundSide.Govfor, out var govforCatalog),
                Is.True);
            Assert.That(
                director.TryGetCommittedAsrsCatalog(RoundSide.Opfor, out var opforCatalog),
                Is.True);
            Assert.That(govforCatalog, Is.Not.Null);
            Assert.That(opforCatalog, Is.Not.Null);

            var pouch = new RoundAsrsOfferId("Pouches_RMCCrateClothingMagazinePouchesLarge");
            Assert.That(govforCatalog!.TryGetOffer(pouch, out var govforPouch), Is.True);
            Assert.That(opforCatalog!.TryGetOffer(pouch, out var opforPouch), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(selection.GovforAssignment?.Force, Is.EqualTo(govforCatalog.Force));
                Assert.That(selection.OpforAssignment?.Force, Is.EqualTo(opforCatalog.Force));
                Assert.That(govforPouch!.Crate.Id, Is.EqualTo("RMCCrateClothingMagazinePouchesLargePMC"));
                Assert.That(opforPouch!.Crate.Id, Is.EqualTo("RMCCrateClothingMagazinePouchesLarge"));
            });

            Assert.That(
                director.FreezeSelection(PlayerCount, FixedBothSidesPresetId),
                Is.EqualTo(selection));
            Assert.That(
                director.TryGetCommittedAsrsCatalog(RoundSide.Govfor, out var repeatedGovforCatalog),
                Is.True);
            Assert.That(
                director.TryGetCommittedAsrsCatalog(RoundSide.Opfor, out var repeatedOpforCatalog),
                Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(repeatedGovforCatalog, Is.SameAs(govforCatalog));
                Assert.That(repeatedOpforCatalog, Is.SameAs(opforCatalog));
            });

            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            Assert.Multiple(() =>
            {
                Assert.That(
                    director.TryGetCommittedAsrsCatalog(RoundSide.Govfor, out var govforAfterReset),
                    Is.False);
                Assert.That(govforAfterReset, Is.Null);
                Assert.That(
                    director.TryGetCommittedAsrsCatalog(RoundSide.Opfor, out var opforAfterReset),
                    Is.False);
                Assert.That(opforAfterReset, Is.Null);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SideAsrsConsolesProjectCommittedCatalogsWithoutChangingSideInfrastructure()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid govforConsole = default;
        EntityUid opforConsole = default;
        EntityUid secondGovforConsole = default;
        EntityUid genericConsole = default;
        EntityUid govforElevator = default;
        EntityUid opforElevator = default;
        NetEntity govforConsoleNet = default;
        NetEntity opforConsoleNet = default;
        EntityUid? govforAccount = null;
        string? govforPrototype = null;
        string? opforPrototype = null;
        string[] govforAccess = [];
        string[] opforAccess = [];
        string[] expectedGovforCatalog = [];
        string[] expectedOpforCatalog = [];
        string[] initialGovforCatalog = [];
        string[] initialGenericCatalog = [];

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var round = server.System<AuRoundSystem>();
            var director = server.System<CMURoundDirectorSystem>();
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

            govforConsole = server.EntMan.SpawnEntity("CMASRSConsoleGovfor", map.GridCoords);
            genericConsole = server.EntMan.SpawnEntity("CMASRSConsole", map.GridCoords);
            govforElevator = server.EntMan.SpawnEntity("CMCargoElevatorGovfor", map.GridCoords);
            govforConsoleNet = server.EntMan.GetNetEntity(govforConsole);

            var govforComputer = server.EntMan.GetComponent<RequisitionsComputerComponent>(govforConsole);
            govforAccount = govforComputer.Account;
            govforPrototype = server.EntMan.GetComponent<MetaDataComponent>(govforConsole).EntityPrototype?.ID;
            govforAccess = SnapshotAccess(server.EntMan.GetComponent<AccessReaderComponent>(govforConsole));
            initialGovforCatalog = SnapshotCatalog(govforComputer);
            initialGenericCatalog = SnapshotCatalog(
                server.EntMan.GetComponent<RequisitionsComputerComponent>(genericConsole));

            round.SetPreset(prototypes.Index<GamePresetPrototype>(FixedBothSidesPresetId));
            Assert.Multiple(() =>
            {
                Assert.That(govforAccount, Is.Not.Null);
                Assert.That(govforPrototype, Is.EqualTo("CMASRSConsoleGovfor"));
                Assert.That(
                    govforAccess,
                    Is.EqualTo(new[] { "AU14AccessGovforCommand", "AU14AccessGovforReq" }));
                Assert.That(
                    director.TrySetLegacyPlanet(ShepherdsPridePlanetId),
                    Is.EqualTo(CMURoundSelectionMutationResult.Applied));
                Assert.That(
                    director.TrySetLegacyForce(RoundSide.Govfor, prototypes.Index(WeyuPlatoon)),
                    Is.EqualTo(CMURoundSelectionMutationResult.Applied));
                Assert.That(
                    director.TrySetLegacyForce(RoundSide.Opfor, prototypes.Index(UppPlatoon)),
                    Is.EqualTo(CMURoundSelectionMutationResult.Applied));
            });

            director.FreezeSelection(PlayerCount, FixedBothSidesPresetId);
            director.MarkMapsLoaded();
            Assert.Multiple(() =>
            {
                Assert.That(server.EntMan.HasComponent<RoundAsrsConsoleCatalogComponent>(govforConsole), Is.False);
                Assert.That(SnapshotCatalog(govforComputer), Is.EqualTo(initialGovforCatalog));
            });
        });

        await pair.RunTicksSync(5);
        await client.WaitAssertion(() =>
        {
            var clientGovfor = client.EntMan.GetEntity(govforConsoleNet);
            Assert.That(
                SnapshotCatalog(client.EntMan.GetComponent<RequisitionsComputerComponent>(clientGovfor)),
                Is.EqualTo(initialGovforCatalog));
        });

        await server.WaitAssertion(() =>
        {
            var director = server.System<CMURoundDirectorSystem>();
            director.MarkWorldInitialized();

            opforConsole = server.EntMan.SpawnEntity("CMASRSConsoleOpfor", map.GridCoords);
            secondGovforConsole = server.EntMan.SpawnEntity("CMASRSConsoleGovfor", map.GridCoords);
            opforElevator = server.EntMan.SpawnEntity("CMCargoElevatorOpfor", map.GridCoords);
            opforConsoleNet = server.EntMan.GetNetEntity(opforConsole);
            opforPrototype = server.EntMan.GetComponent<MetaDataComponent>(opforConsole).EntityPrototype?.ID;
            opforAccess = SnapshotAccess(server.EntMan.GetComponent<AccessReaderComponent>(opforConsole));

            Assert.That(director.TryGetCommittedAsrsCatalog(RoundSide.Govfor, out var govforCatalog), Is.True);
            Assert.That(director.TryGetCommittedAsrsCatalog(RoundSide.Opfor, out var opforCatalog), Is.True);
            expectedGovforCatalog = SnapshotCatalog(govforCatalog!);
            expectedOpforCatalog = SnapshotCatalog(opforCatalog!);
            var govforBinding = server.EntMan.GetComponent<RoundAsrsConsoleCatalogComponent>(govforConsole);
            var opforBinding = server.EntMan.GetComponent<RoundAsrsConsoleCatalogComponent>(opforConsole);
            var firstGovforComputer = server.EntMan.GetComponent<RequisitionsComputerComponent>(govforConsole);
            var secondGovforComputer = server.EntMan.GetComponent<RequisitionsComputerComponent>(secondGovforConsole);

            Assert.Multiple(() =>
            {
                Assert.That(initialGovforCatalog, Is.Not.EqualTo(expectedGovforCatalog));
                Assert.That(
                    SnapshotCatalog(server.EntMan.GetComponent<RequisitionsComputerComponent>(govforConsole)),
                    Is.EqualTo(expectedGovforCatalog));
                Assert.That(
                    SnapshotCatalog(server.EntMan.GetComponent<RequisitionsComputerComponent>(opforConsole)),
                    Is.EqualTo(expectedOpforCatalog));
                Assert.That(SnapshotCatalog(secondGovforComputer), Is.EqualTo(expectedGovforCatalog));
                Assert.That(govforBinding.Generation, Is.EqualTo(director.Generation));
                Assert.That(opforBinding.Generation, Is.EqualTo(director.Generation));
                Assert.That(govforBinding.Force, Is.EqualTo(govforCatalog!.Force));
                Assert.That(opforBinding.Force, Is.EqualTo(opforCatalog!.Force));
                Assert.That(SnapshotCatalogIds(govforBinding), Is.EqualTo(SnapshotCatalogIds(govforCatalog)));
                Assert.That(SnapshotCatalogIds(opforBinding), Is.EqualTo(SnapshotCatalogIds(opforCatalog)));
                Assert.That(SnapshotStock(govforBinding), Is.EqualTo(SnapshotStock(govforCatalog)));
                Assert.That(SnapshotStock(opforBinding), Is.EqualTo(SnapshotStock(opforCatalog)));
                Assert.That(
                    SnapshotCatalog(server.EntMan.GetComponent<RequisitionsComputerComponent>(genericConsole)),
                    Is.EqualTo(initialGenericCatalog));
                Assert.That(
                    server.EntMan.HasComponent<RoundAsrsConsoleCatalogComponent>(genericConsole),
                    Is.False,
                    "A force-neutral, unscoped console must remain untouched until semantic endpoint migration.");
                Assert.That(firstGovforComputer.Categories, Is.Not.SameAs(secondGovforComputer.Categories));
                Assert.That(firstGovforComputer.Categories[0], Is.Not.SameAs(secondGovforComputer.Categories[0]));
                Assert.That(
                    firstGovforComputer.Categories[0].Entries[0],
                    Is.Not.SameAs(secondGovforComputer.Categories[0].Entries[0]));
                Assert.That(
                    server.EntMan.GetComponent<RequisitionsComputerComponent>(govforConsole).Faction,
                    Is.EqualTo("govfor"));
                Assert.That(
                    server.EntMan.GetComponent<RequisitionsComputerComponent>(opforConsole).Faction,
                    Is.EqualTo("opfor"));
                Assert.That(
                    server.EntMan.GetComponent<RequisitionsComputerComponent>(govforConsole).Account,
                    Is.EqualTo(govforAccount));
                Assert.That(
                    server.EntMan.GetComponent<RequisitionsComputerComponent>(opforConsole).Account,
                    Is.EqualTo(govforAccount),
                    "The existing requisitions system deliberately shares one account across side shells.");
                Assert.That(
                    server.EntMan.GetComponent<MetaDataComponent>(govforConsole).EntityPrototype?.ID,
                    Is.EqualTo(govforPrototype));
                Assert.That(
                    server.EntMan.GetComponent<MetaDataComponent>(opforConsole).EntityPrototype?.ID,
                    Is.EqualTo(opforPrototype));
                Assert.That(opforPrototype, Is.EqualTo("CMASRSConsoleOpfor"));
                Assert.That(
                    SnapshotAccess(server.EntMan.GetComponent<AccessReaderComponent>(govforConsole)),
                    Is.EqualTo(govforAccess));
                Assert.That(
                    SnapshotAccess(server.EntMan.GetComponent<AccessReaderComponent>(opforConsole)),
                    Is.EqualTo(opforAccess));
                Assert.That(
                    opforAccess,
                    Is.EqualTo(new[] { "AU14AccessOpforCommand", "AU14AccessOpforReq" }));
                Assert.That(
                    server.EntMan.GetComponent<RequisitionsElevatorComponent>(govforElevator).Faction,
                    Is.EqualTo("govfor"));
                Assert.That(
                    server.EntMan.GetComponent<RequisitionsElevatorComponent>(opforElevator).Faction,
                    Is.EqualTo("opfor"));
            });
        });

        await pair.RunTicksSync(5);
        await client.WaitAssertion(() =>
        {
            var clientGovfor = client.EntMan.GetEntity(govforConsoleNet);
            var clientOpfor = client.EntMan.GetEntity(opforConsoleNet);
            Assert.Multiple(() =>
            {
                Assert.That(
                    SnapshotCatalog(client.EntMan.GetComponent<RequisitionsComputerComponent>(clientGovfor)),
                    Is.EqualTo(expectedGovforCatalog));
                Assert.That(
                    SnapshotCatalog(client.EntMan.GetComponent<RequisitionsComputerComponent>(clientOpfor)),
                    Is.EqualTo(expectedOpforCatalog));
            });
        });

        await server.WaitPost(() =>
        {
            server.EntMan.DeleteEntity(govforConsole);
            server.EntMan.DeleteEntity(opforConsole);
            server.EntMan.DeleteEntity(secondGovforConsole);
            server.EntMan.DeleteEntity(genericConsole);
            server.EntMan.DeleteEntity(govforElevator);
            server.EntMan.DeleteEntity(opforElevator);
        });
        await pair.RunUntilSynced();
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SemanticAsrsEndpointsResolveInPlaceFromOwningShipSide()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid console = default;
        EntityUid elevator = default;
        EntityUid lateConsole = default;
        EntityUid? account = null;
        var consoleRotation = Angle.FromDegrees(90);
        await server.WaitAssertion(() =>
        {
            var director = server.System<CMURoundDirectorSystem>();
            var metadata = server.System<MetaDataSystem>();
            var transform = server.System<SharedTransformSystem>();
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

            var shipFaction = server.EntMan.EnsureComponent<ShipFactionComponent>(map.Grid.Owner);
            shipFaction.Faction = "govfor";
            console = server.EntMan.SpawnEntity("CMURoundAsrsConsole", map.GridCoords);
            elevator = server.EntMan.SpawnEntity("CMURoundAsrsLift", map.GridCoords);
            metadata.SetEntityName(console, "Mapped round ASRS");
            transform.SetLocalRotation(console, consoleRotation);

            var computer = server.EntMan.GetComponent<RequisitionsComputerComponent>(console);
            account = computer.Account;
            Assert.Multiple(() =>
            {
                Assert.That(account, Is.Not.Null);
                Assert.That(computer.Faction, Is.EqualTo("none"));
                Assert.That(
                    server.EntMan.GetComponent<RequisitionsElevatorComponent>(elevator).Faction,
                    Is.EqualTo("none"));
                Assert.That(server.EntMan.HasComponent<RoundAsrsConsoleCatalogComponent>(console), Is.False);
            });

            director.FreezeSelection(PlayerCount, FixedBothSidesPresetId);
            director.MarkMapsLoaded();
            director.MarkWorldInitialized();

            var resolvedComputer = server.EntMan.GetComponent<RequisitionsComputerComponent>(console);
            var binding = server.EntMan.GetComponent<RoundAsrsConsoleCatalogComponent>(console);
            Assert.Multiple(() =>
            {
                Assert.That(
                    server.EntMan.GetComponent<MetaDataComponent>(console).EntityPrototype?.ID,
                    Is.EqualTo("CMURoundAsrsConsole"));
                Assert.That(
                    server.EntMan.GetComponent<MetaDataComponent>(console).EntityName,
                    Is.EqualTo("Mapped round ASRS"));
                Assert.That(
                    server.EntMan.GetComponent<TransformComponent>(console).LocalRotation,
                    Is.EqualTo(consoleRotation));
                Assert.That(resolvedComputer.Account, Is.EqualTo(account));
                Assert.That(resolvedComputer.Faction, Is.EqualTo("govfor"));
                Assert.That(binding.Force, Is.EqualTo(new RoundForceId("USCM")));
                Assert.That(resolvedComputer.Categories, Has.Count.EqualTo(18));
                Assert.That(
                    SnapshotAccess(server.EntMan.GetComponent<AccessReaderComponent>(console)),
                    Is.EqualTo(new[] { "AU14AccessGovforCommand", "AU14AccessGovforReq" }));
                Assert.That(
                    server.EntMan.GetComponent<RequisitionsElevatorComponent>(elevator).Faction,
                    Is.EqualTo("govfor"));
                Assert.That(server.EntMan.HasComponent<RoundSetupEndpointComponent>(console), Is.True);
                Assert.That(server.EntMan.HasComponent<RoundSetupEndpointComponent>(elevator), Is.True);
            });

            lateConsole = server.EntMan.SpawnEntity("CMURoundAsrsConsole", map.GridCoords);
            Assert.Multiple(() =>
            {
                Assert.That(
                    server.EntMan.GetComponent<RequisitionsComputerComponent>(lateConsole).Faction,
                    Is.EqualTo("govfor"));
                Assert.That(
                    server.EntMan.GetComponent<RoundAsrsConsoleCatalogComponent>(lateConsole).Force,
                    Is.EqualTo(new RoundForceId("USCM")));
            });
        });

        await server.WaitPost(() =>
        {
            server.EntMan.DeleteEntity(console);
            server.EntMan.DeleteEntity(elevator);
            server.EntMan.DeleteEntity(lateConsole);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SideAsrsLimitedOffersStopReservationsAtMaximum()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid console = default;
        await server.WaitAssertion(() =>
        {
            var director = server.System<CMURoundDirectorSystem>();
            var requisitions = server.System<RequisitionsSystem>();
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

            director.FreezeSelection(PlayerCount, FixedBothSidesPresetId);
            director.MarkMapsLoaded();
            director.MarkWorldInitialized();
            console = server.EntMan.SpawnEntity("CMASRSConsoleGovfor", map.GridCoords);

            var computer = server.EntMan.GetComponent<RequisitionsComputerComponent>(console);
            var binding = server.EntMan.GetComponent<RoundAsrsConsoleCatalogComponent>(console);
            var limitedOffer = new RoundAsrsOfferId("Medical_CMUCrateMedicalFieldTreatments");
            var categoryIndex = -1;
            var offerIndex = -1;
            var offerIdsByCategory = binding.OfferIdsByCategory;
            for (var category = 0; category < offerIdsByCategory.Length; category++)
            {
                var index = offerIdsByCategory[category].IndexOf(limitedOffer);
                if (index < 0)
                    continue;

                categoryIndex = category;
                offerIndex = index;
                break;
            }

            Assert.Multiple(() =>
            {
                Assert.That(categoryIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(offerIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(
                    binding.StockPolicies[limitedOffer],
                    Is.EqualTo(new RoundAsrsStockPolicy(2, TimeSpan.FromMinutes(8))));
                Assert.That(requisitions.TryReserveStock((console, computer), categoryIndex, offerIndex), Is.True);
                Assert.That(requisitions.TryReserveStock((console, computer), categoryIndex, offerIndex), Is.True);
                Assert.That(requisitions.TryReserveStock((console, computer), categoryIndex, offerIndex), Is.False);
            });
        });

        await server.WaitPost(() => server.EntMan.DeleteEntity(console));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SideAsrsLimitedOffersReplenishWithoutExceedingMaximum()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Destructive = true,
            Dirty = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var changed = new Dictionary<Type, HashSet<string>>();
        server.ProtoMan.LoadString(ReplenishingAsrsProfile, changed: changed);
        await server.WaitPost(() => server.ProtoMan.ReloadPrototypes(changed));

        EntityUid console = default;
        var categoryIndex = -1;
        var offerIndex = -1;
        await server.WaitAssertion(() =>
        {
            var director = server.System<CMURoundDirectorSystem>();
            var requisitions = server.System<RequisitionsSystem>();
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

            director.FreezeSelection(PlayerCount, MissingAsrsPresetId);
            director.MarkMapsLoaded();
            director.MarkWorldInitialized();
            console = server.EntMan.SpawnEntity("CMASRSConsoleGovfor", map.GridCoords);

            var computer = server.EntMan.GetComponent<RequisitionsComputerComponent>(console);
            var binding = server.EntMan.GetComponent<RoundAsrsConsoleCatalogComponent>(console);
            var offerIds = binding.OfferIdsByCategory;
            categoryIndex = 0;
            offerIndex = offerIds[categoryIndex].IndexOf(new RoundAsrsOfferId("Test_Limited"));

            Assert.Multiple(() =>
            {
                Assert.That(offerIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(requisitions.TryReserveStock((console, computer), categoryIndex, offerIndex), Is.True);
                Assert.That(requisitions.TryReserveStock((console, computer), categoryIndex, offerIndex), Is.False);
            });
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1) + 2);

        await server.WaitAssertion(() =>
        {
            var computer = server.EntMan.GetComponent<RequisitionsComputerComponent>(console);
            var requisitions = server.System<RequisitionsSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(requisitions.TryReserveStock((console, computer), categoryIndex, offerIndex), Is.True);
                Assert.That(requisitions.TryReserveStock((console, computer), categoryIndex, offerIndex), Is.True);
                Assert.That(requisitions.TryReserveStock((console, computer), categoryIndex, offerIndex), Is.False);
            });
        });

        await server.WaitPost(() => server.EntMan.DeleteEntity(console));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SideAsrsDirectAndDepartmentOrdersSharePerConsoleStock()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid console = default;
        EntityUid elevator = default;
        EntityUid departmentConsole = default;
        EntityUid actor = default;
        await server.WaitAssertion(() =>
        {
            var director = server.System<CMURoundDirectorSystem>();
            var requisitions = server.System<RequisitionsSystem>();
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

            director.FreezeSelection(PlayerCount, FixedBothSidesPresetId);
            director.MarkMapsLoaded();
            director.MarkWorldInitialized();
            console = server.EntMan.SpawnEntity("CMASRSConsoleGovfor", map.GridCoords);
            elevator = server.EntMan.SpawnEntity("CMCargoElevatorGovfor", map.GridCoords);
            departmentConsole = server.EntMan.SpawnEntity("AUDepartmentConsoleMedical", map.GridCoords);
            actor = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);

            var computer = server.EntMan.GetComponent<RequisitionsComputerComponent>(console);
            var binding = server.EntMan.GetComponent<RoundAsrsConsoleCatalogComponent>(console);
            var elevatorComponent = server.EntMan.GetComponent<RequisitionsElevatorComponent>(elevator);
            var department = server.EntMan.GetComponent<DepartmentConsoleComponent>(departmentConsole);
            department.AsrsFaction = "govfor";
            department.DepartmentId = null;
            department.DepartmentBudget = 20_000;
            requisitions.ChangeBudget(20_000);

            var accountUid = computer.Account;
            Assert.That(accountUid, Is.Not.Null);
            var account = server.EntMan.GetComponent<RequisitionsAccountComponent>(accountUid!.Value);
            var monkey = FindOffer(binding, new RoundAsrsOfferId("Research_CMUMonkeyCubeCrate"));
            var exotic = FindOffer(binding, new RoundAsrsOfferId("Research_CMUExoticCubeCrate"));
            var monkeyCost = computer.Categories[monkey.Category].Entries[monkey.Offer].Cost;
            var exoticCost = computer.Categories[exotic.Category].Entries[exotic.Offer].Cost;
            var initialBalance = account.Balance;
            var initialDepartmentBudget = department.DepartmentBudget;

            var directMonkey = new RequisitionsBuyMsg(monkey.Category, monkey.Offer)
            {
                Actor = actor,
                UiKey = RequisitionsUIKey.Key,
            };
            server.EntMan.EventBus.RaiseLocalEvent(console, directMonkey);
            Assert.Multiple(() =>
            {
                Assert.That(account.Balance, Is.EqualTo(initialBalance - monkeyCost));
                Assert.That(department.DepartmentBudget, Is.EqualTo(initialDepartmentBudget));
                Assert.That(elevatorComponent.Orders, Has.Count.EqualTo(1));
            });

            var departmentMonkey = new DepartmentConsoleOrderBuiMsg(
                monkey.Category,
                monkey.Offer,
                "Medical research",
                "Laboratory")
            {
                Actor = actor,
                UiKey = DepartmentConsoleUi.Key,
            };
            server.EntMan.EventBus.RaiseLocalEvent(departmentConsole, departmentMonkey);
            Assert.Multiple(() =>
            {
                Assert.That(account.Balance, Is.EqualTo(initialBalance - monkeyCost));
                Assert.That(department.DepartmentBudget, Is.EqualTo(initialDepartmentBudget));
                Assert.That(elevatorComponent.Orders, Has.Count.EqualTo(1));
            });

            var departmentExotic = new DepartmentConsoleOrderBuiMsg(
                exotic.Category,
                exotic.Offer,
                "Exotic research",
                "Secure laboratory")
            {
                Actor = actor,
                UiKey = DepartmentConsoleUi.Key,
            };
            server.EntMan.EventBus.RaiseLocalEvent(departmentConsole, departmentExotic);
            Assert.Multiple(() =>
            {
                Assert.That(account.Balance, Is.EqualTo(initialBalance - monkeyCost));
                Assert.That(department.DepartmentBudget, Is.EqualTo(initialDepartmentBudget - exoticCost));
                Assert.That(elevatorComponent.Orders, Has.Count.EqualTo(2));
                Assert.That(elevatorComponent.Orders[1].DeptReason, Is.EqualTo("Exotic research"));
                Assert.That(elevatorComponent.Orders[1].DeptDeliverTo, Is.EqualTo("Secure laboratory"));
            });

            var directExotic = new RequisitionsBuyMsg(exotic.Category, exotic.Offer)
            {
                Actor = actor,
                UiKey = RequisitionsUIKey.Key,
            };
            server.EntMan.EventBus.RaiseLocalEvent(console, directExotic);
            Assert.Multiple(() =>
            {
                Assert.That(account.Balance, Is.EqualTo(initialBalance - monkeyCost));
                Assert.That(department.DepartmentBudget, Is.EqualTo(initialDepartmentBudget - exoticCost));
                Assert.That(elevatorComponent.Orders, Has.Count.EqualTo(2));
            });
        });

        await server.WaitPost(() =>
        {
            server.EntMan.DeleteEntity(console);
            server.EntMan.DeleteEntity(elevator);
            server.EntMan.DeleteEntity(departmentConsole);
            server.EntMan.DeleteEntity(actor);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DirectAsrsRejectsNegativeOrderIndexes()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid console = default;
        EntityUid elevator = default;
        EntityUid actor = default;
        await server.WaitAssertion(() =>
        {
            console = server.EntMan.SpawnEntity("CMASRSConsole", map.GridCoords);
            elevator = server.EntMan.SpawnEntity("CMCargoElevator", map.GridCoords);
            actor = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);

            var computer = server.EntMan.GetComponent<RequisitionsComputerComponent>(console);
            var accountUid = computer.Account;
            Assert.That(accountUid, Is.Not.Null);
            var account = server.EntMan.GetComponent<RequisitionsAccountComponent>(accountUid!.Value);
            var elevatorComponent = server.EntMan.GetComponent<RequisitionsElevatorComponent>(elevator);
            var initialBalance = account.Balance;

            var negativeCategory = new RequisitionsBuyMsg(-1, 0)
            {
                Actor = actor,
                UiKey = RequisitionsUIKey.Key,
            };
            Assert.DoesNotThrow(() => server.EntMan.EventBus.RaiseLocalEvent(console, negativeCategory));

            var negativeOrder = new RequisitionsBuyMsg(0, -1)
            {
                Actor = actor,
                UiKey = RequisitionsUIKey.Key,
            };
            Assert.DoesNotThrow(() => server.EntMan.EventBus.RaiseLocalEvent(console, negativeOrder));

            Assert.Multiple(() =>
            {
                Assert.That(account.Balance, Is.EqualTo(initialBalance));
                Assert.That(elevatorComponent.Orders, Is.Empty);
            });
        });

        await server.WaitPost(() =>
        {
            server.EntMan.DeleteEntity(console);
            server.EntMan.DeleteEntity(elevator);
            server.EntMan.DeleteEntity(actor);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InvalidAsrsProfilesDoNotLatchSelectionOrCatalogs()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var server = pair.Server;

        var changed = new Dictionary<Type, HashSet<string>>();
        server.ProtoMan.LoadString(DuplicateAsrsProfile, changed: changed);
        await server.WaitPost(() => server.ProtoMan.ReloadPrototypes(changed));

        await server.WaitAssertion(() =>
        {
            var director = server.System<CMURoundDirectorSystem>();
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

            var missing = Assert.Throws<InvalidOperationException>(() =>
                director.FreezeSelection(PlayerCount, MissingAsrsPresetId));
            Assert.That(missing!.Message, Does.Contain("has no ASRS profile"));
            AssertDirectorHasNoCommittedPlan(director);

            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            var duplicate = Assert.Throws<InvalidOperationException>(() =>
                director.FreezeSelection(PlayerCount, FixedBothSidesPresetId));
            Assert.That(duplicate!.Message, Does.Contain("has multiple ASRS profiles"));
            AssertDirectorHasNoCommittedPlan(director);
        });

        await pair.CleanReturnAsync();
    }

    private static string[] SnapshotAccess(AccessReaderComponent access)
    {
        return access.AccessLists
            .Select(group => string.Join(",", group.OrderBy(level => level.Id, StringComparer.Ordinal)))
            .OrderBy(group => group, StringComparer.Ordinal)
            .ToArray();
    }

    private static (int Category, int Offer) FindOffer(
        RoundAsrsConsoleCatalogComponent catalog,
        RoundAsrsOfferId offer)
    {
        var offersByCategory = catalog.OfferIdsByCategory;
        for (var category = 0; category < offersByCategory.Length; category++)
        {
            var index = offersByCategory[category].IndexOf(offer);
            if (index >= 0)
                return (category, index);
        }

        throw new AssertionException($"Committed ASRS catalog has no offer '{offer}'.");
    }

    private static string[] SnapshotCatalog(ResolvedRoundAsrsCatalog catalog)
    {
        var snapshot = new List<string>();
        for (var categoryIndex = 0; categoryIndex < catalog.Categories.Length; categoryIndex++)
        {
            var category = catalog.Categories[categoryIndex];
            snapshot.Add($"C|{categoryIndex}|{category.Name}|{category.Offers.Length}");
            for (var offerIndex = 0; offerIndex < category.Offers.Length; offerIndex++)
            {
                var offer = category.Offers[offerIndex];
                snapshot.Add($"O|{categoryIndex}|{offerIndex}|{offer.Crate.Id}|{offer.Cost}");
            }
        }

        return snapshot.ToArray();
    }

    private static string[] SnapshotCatalog(RequisitionsComputerComponent computer)
    {
        var snapshot = new List<string>();
        for (var categoryIndex = 0; categoryIndex < computer.Categories.Count; categoryIndex++)
        {
            var category = computer.Categories[categoryIndex];
            snapshot.Add($"C|{categoryIndex}|{category.Name}|{category.Entries.Count}");
            for (var offerIndex = 0; offerIndex < category.Entries.Count; offerIndex++)
            {
                var offer = category.Entries[offerIndex];
                snapshot.Add($"O|{categoryIndex}|{offerIndex}|{offer.Crate.Id}|{offer.Cost}");
            }
        }

        return snapshot.ToArray();
    }

    private static string[] SnapshotCatalogIds(ResolvedRoundAsrsCatalog catalog)
    {
        return catalog.Categories
            .SelectMany(category =>
                new[] { $"C|{category.Id}" }
                    .Concat(category.Offers.Select(offer => $"O|{offer.Id}")))
            .ToArray();
    }

    private static string[] SnapshotCatalogIds(RoundAsrsConsoleCatalogComponent catalog)
    {
        return catalog.CategoryIds
            .SelectMany((category, index) =>
                new[] { $"C|{category}" }
                    .Concat(catalog.OfferIdsByCategory[index].Select(offer => $"O|{offer}")))
            .ToArray();
    }

    private static string[] SnapshotStock(ResolvedRoundAsrsCatalog catalog)
    {
        return catalog.Categories
            .SelectMany(category => category.Offers)
            .Where(offer => offer.Stock != null)
            .Select(offer => SnapshotStock(offer.Id, offer.Stock!.Value))
            .OrderBy(stock => stock, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] SnapshotStock(RoundAsrsConsoleCatalogComponent catalog)
    {
        return catalog.StockPolicies
            .Select(stock => SnapshotStock(stock.Key, stock.Value))
            .OrderBy(stock => stock, StringComparer.Ordinal)
            .ToArray();
    }

    private static string SnapshotStock(RoundAsrsOfferId offer, RoundAsrsStockPolicy stock)
    {
        return $"{offer}|{stock.Maximum}|{stock.ReplenishDelay.Ticks}|{stock.StartingStock}|{stock.ReplenishAmount}";
    }

    private static void AssertDirectorHasNoCommittedPlan(CMURoundDirectorSystem director)
    {
        Assert.Multiple(() =>
        {
            Assert.That(director.Selection, Is.Null);
            Assert.That(
                director.TryGetCommittedAsrsCatalog(RoundSide.Govfor, out var govforCatalog),
                Is.False);
            Assert.That(govforCatalog, Is.Null);
            Assert.That(
                director.TryGetCommittedAsrsCatalog(RoundSide.Opfor, out var opforCatalog),
                Is.False);
            Assert.That(opforCatalog, Is.Null);
        });
    }

    [Test]
    public async Task PreFreezeOverridesCommitAndPostFreezeOverridesAreRejected()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var round = server.System<AuRoundSystem>();
            var director = server.System<CMURoundDirectorSystem>();
            var platoons = server.System<PlatoonSpawnRuleSystem>();
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            round.SetPreset(prototypes.Index<GamePresetPrototype>(FixedBothSidesPresetId));

            Assert.Multiple(() =>
            {
                Assert.That(
                    director.TrySetLegacyPlanet(ShepherdsPridePlanetId),
                    Is.EqualTo(CMURoundSelectionMutationResult.Applied));
                Assert.That(
                    director.TrySetLegacyPlanet("CMUMissingPlanet"),
                    Is.EqualTo(CMURoundSelectionMutationResult.InvalidSelection));
                Assert.That(round.GetSelectedPlanetId(), Is.EqualTo(ShepherdsPridePlanetId));
                Assert.That(
                    director.TrySetLegacyForce(
                        RoundSide.Govfor,
                        prototypes.Index(RmcPlatoon)),
                    Is.EqualTo(CMURoundSelectionMutationResult.Applied));
                Assert.That(
                    director.TrySetLegacyForce(
                        RoundSide.Opfor,
                        prototypes.Index(HazopsPlatoon)),
                    Is.EqualTo(CMURoundSelectionMutationResult.Applied));
                Assert.That(
                    director.TrySetMainShip(RoundSide.Govfor, "USSBushRedux"),
                    Is.EqualTo(CMURoundSelectionMutationResult.Applied));
                Assert.That(
                    director.TrySetMainShip(RoundSide.Opfor, "USSBushRedux"),
                    Is.EqualTo(CMURoundSelectionMutationResult.Applied));
            });

            var committed = director.FreezeSelection(PlayerCount, FixedBothSidesPresetId);

            Assert.Multiple(() =>
            {
                Assert.That(
                    director.TrySetLegacyForce(
                        RoundSide.Govfor,
                        prototypes.Index(UscmPlatoon)),
                    Is.EqualTo(CMURoundSelectionMutationResult.SelectionFrozen));
                Assert.That(
                    director.TrySetLegacyForce(
                        RoundSide.Opfor,
                        prototypes.Index(UppPlatoon)),
                    Is.EqualTo(CMURoundSelectionMutationResult.SelectionFrozen));
                Assert.That(
                    director.TrySetMainShip(RoundSide.Govfor, "LaterGovforShip"),
                    Is.EqualTo(CMURoundSelectionMutationResult.SelectionFrozen));
                Assert.That(
                    director.TrySetMainShip(RoundSide.Opfor, "LaterOpforShip"),
                    Is.EqualTo(CMURoundSelectionMutationResult.SelectionFrozen));
                Assert.That(
                    director.TrySetLegacyPlanet("AUPlanetLV747"),
                    Is.EqualTo(CMURoundSelectionMutationResult.SelectionFrozen));
                Assert.That(
                    committed.GovforAssignment,
                    Is.EqualTo(new RoundForceAssignment(
                        RoundSide.Govfor,
                        new RoundForceId("RMC"),
                        "USSBushRedux")));
                Assert.That(
                    committed.OpforAssignment,
                    Is.EqualTo(new RoundForceAssignment(
                        RoundSide.Opfor,
                        new RoundForceId("HAZOPS"),
                        "USSBushRedux")));
                Assert.That(director.Selection, Is.EqualTo(committed));
                Assert.That(platoons.SelectedGovforPlatoon?.ID, Is.EqualTo("RMC"));
                Assert.That(platoons.SelectedOpforPlatoon?.ID, Is.EqualTo("HAZOPS"));
                Assert.That(round.GetSelectedGovforShip(), Is.EqualTo("USSBushRedux"));
                Assert.That(round.GetSelectedOpforShip(), Is.EqualTo("USSBushRedux"));
                Assert.That(committed.PlanetId, Is.EqualTo(ShepherdsPridePlanetId));
                Assert.That(round.GetSelectedPlanetId(), Is.EqualTo(ShepherdsPridePlanetId));
            });
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(
        "DistressSignal",
        "AUPlanetTrijent",
        "AUTrijentMap",
        "RMC",
        "USSBushRedux",
        null,
        null,
        CmuThreatSelectionMode.PostRoundstartVote,
        false)]
    [TestCase(
        "ColonyFall",
        "AUPlanetShepherdsPride",
        "Sheperds",
        null,
        null,
        null,
        null,
        CmuThreatSelectionMode.PostRoundstartVote,
        true)]
    [TestCase(
        "Insurgency",
        "AUPlanetShepherdsPride",
        "Sheperds",
        "USCM",
        "USSBushRedux",
        null,
        null,
        CmuThreatSelectionMode.Disabled,
        false)]
    [TestCase(
        "CMDistressSignal",
        null,
        null,
        null,
        null,
        null,
        null,
        CmuThreatSelectionMode.Disabled,
        false)]
    public async Task CutoffUsesPrototypeBackedDefaults(
        string presetId,
        string? expectedPlanetId,
        string? expectedMapId,
        string? expectedGovforPlatoonId,
        string? expectedGovforShipId,
        string? expectedOpforPlatoonId,
        string? expectedOpforShipId,
        CmuThreatSelectionMode expectedThreatSelectionMode,
        bool expectedThreatSpawnDelay)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var round = server.System<AuRoundSystem>();
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            round.FinalizeVoteSequence(PlayerCount, presetId);

            var selectedPresetId = round.SelectedPreset?.ID ?? presetId;
            var selection = round.CaptureRoundPlanSelection(
                PlayerCount,
                selectedPresetId,
                round.SelectedThreat?.ID);

            Assert.Multiple(() =>
            {
                Assert.That(selection.PresetId, Is.EqualTo(presetId));
                Assert.That(selection.PlanetId, Is.EqualTo(expectedPlanetId));
                Assert.That(selection.MapId, Is.EqualTo(expectedMapId));
                Assert.That(selection.GovforPlatoonId, Is.EqualTo(expectedGovforPlatoonId));
                Assert.That(selection.GovforShipId, Is.EqualTo(expectedGovforShipId));
                Assert.That(selection.OpforPlatoonId, Is.EqualTo(expectedOpforPlatoonId));
                Assert.That(selection.OpforShipId, Is.EqualTo(expectedOpforShipId));
                Assert.That(selection.SelectedThreatId, Is.Null);
                Assert.That(round.SelectedPreset?.ThreatSelectionMode, Is.EqualTo(expectedThreatSelectionMode));
                Assert.That(
                    round.UsesPostRoundstartThreatVote(),
                    Is.EqualTo(expectedThreatSelectionMode == CmuThreatSelectionMode.PostRoundstartVote));
                Assert.That(round.SelectedPreset?.UsesThreatSpawnDelay, Is.EqualTo(expectedThreatSpawnDelay));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PoolOnlyFixedFactionFlowsIntoScenarioPlan()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var round = server.System<AuRoundSystem>();
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            round.FinalizeVoteSequence(PlayerCount, FixedFactionPresetId);

            var selection = round.CaptureRoundPlanSelection(
                PlayerCount,
                FixedFactionPresetId,
                round.SelectedThreat?.ID);
            var plan = server.System<ScenarioPlanSystem>()
                .GeneratePlans(selection.ToScenarioPlanRequest())
                .Single();
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            Assert.Multiple(() =>
            {
                Assert.That(selection.PlanetId, Is.EqualTo("AUPlanetShepherdsPride"));
                Assert.That(selection.MapId, Is.EqualTo("Sheperds"));
                Assert.That(selection.GovforPlatoonId, Is.EqualTo("USCM"));
                Assert.That(selection.GovforShipId, Is.EqualTo("USSBushRedux"));
                Assert.That(selection.OpforPlatoonId, Is.Null);
                Assert.That(selection.OpforShipId, Is.Null);
                Assert.That(round.UsesPostRoundstartThreatVote(), Is.True);
                Assert.That(round.SelectedPreset?.UsesThreatSpawnDelay, Is.True);
                Assert.That(plan.PlanetId, Is.EqualTo(selection.PlanetId));
                Assert.That(
                    plan.Forces.Any(force =>
                        force.ForceId == "GovforPlatoon:USCM" &&
                        force.SourcePrototypeId == "USCM"),
                    Is.True);
                Assert.That(
                    plan.DeferredForceChoices.Any(choice => choice.ChoiceId == "GovforPlatoon"),
                    Is.False);
                Assert.That(
                    plan.DeferredForceChoices.Any(choice =>
                        choice.ChoiceId.StartsWith("DeferredThreat:", StringComparison.Ordinal)),
                    Is.True);
                Assert.That(
                    plan.DeferredForceChoices
                        .Where(choice => choice.ChoiceId.StartsWith("DeferredThreat:", StringComparison.Ordinal))
                        .SelectMany(choice => choice.Candidates)
                        .All(force => force.Timing.HasDelay),
                    Is.True);
                Assert.That(
                    prototypes.Index(PrometheusPreset).ThreatSelectionMode,
                    Is.EqualTo(CmuThreatSelectionMode.PreRoundstart));
                Assert.That(
                    prototypes.Index(JailbreakPreset).ThreatSelectionMode,
                    Is.EqualTo(CmuThreatSelectionMode.PreRoundstart));
            });
        });

        await pair.CleanReturnAsync();
    }
}
