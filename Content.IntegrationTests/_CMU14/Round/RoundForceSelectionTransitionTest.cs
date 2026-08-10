#nullable enable

using System.Collections;
using Content.IntegrationTests.Pair;
using Content.Server.AU14.Round;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Intel.Tech;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.AU14.util;
using Content.Shared.CMU.Round;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests._CMU14.Round;

[TestFixture]
public sealed class RoundForceSelectionTransitionTest
{
    private const string ShepherdsPridePlanetId = "AUPlanetShepherdsPride";
    private static readonly ProtoId<PlatoonPrototype> UppPlatoon = "UPP";

    [Test]
    public async Task DirectorMutationAndRoundRestartApplyTechTreeTransitions()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var intel = server.System<IntelSystem>();
            try
            {
                var prototypes = server.ResolveDependency<IPrototypeManager>();
                var round = server.System<AuRoundSystem>();
                var director = server.System<CMURoundDirectorSystem>();
                var platoons = server.System<PlatoonSpawnRuleSystem>();
                server.EntMan.EventBus.RaiseEvent(
                    EventSource.Local,
                    new RoundRestartCleanupEvent());
                intel.ClearTeamTechTreeOverrides();
                DeleteFactionTree(server.EntMan, intel);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        director.TrySetLegacyPlanet(ShepherdsPridePlanetId),
                        Is.EqualTo(CMURoundSelectionMutationResult.Applied));
                    Assert.That(
                        director.TrySetMainShip(RoundSide.Govfor, "USSBushRedux"),
                        Is.EqualTo(CMURoundSelectionMutationResult.Applied));
                    Assert.That(
                        director.TrySetMainShip(RoundSide.Opfor, "USSBushRedux"),
                        Is.EqualTo(CMURoundSelectionMutationResult.Applied));
                });
                Assert.That(
                    director.TrySetLegacyForce(
                        RoundSide.Opfor,
                        prototypes.Index(UppPlatoon)),
                    Is.EqualTo(CMURoundSelectionMutationResult.Applied));
                Assert.That(platoons.SelectedOpforPlatoon?.ID, Is.EqualTo("UPP"));

                var overridden = intel.EnsureTechTree(Team.OpFor);
                Assert.That(
                    server.EntMan.GetComponent<MetaDataComponent>(overridden.Owner).EntityPrototype?.ID,
                    Is.EqualTo("RMCIntelTechTree_ua"));
                server.EntMan.DeleteEntity(overridden.Owner);

                var generation = director.Generation;
                server.EntMan.EventBus.RaiseEvent(
                    EventSource.Local,
                    new RoundRestartCleanupEvent());
                Assert.Multiple(() =>
                {
                    Assert.That(director.Generation, Is.EqualTo(generation + 1));
                    Assert.That(director.Phase, Is.EqualTo(CMURoundPhase.AwaitingSelection));
                    Assert.That(platoons.SelectedOpforPlatoon, Is.Null);
                    Assert.That(round.GetSelectedPlanetId(), Is.Null);
                    Assert.That(round.GetSelectedGovforShip(), Is.Null);
                    Assert.That(round.GetSelectedOpforShip(), Is.Null);
                });

                var fallback = intel.EnsureTechTree(Team.OpFor);
                Assert.That(
                    server.EntMan.GetComponent<MetaDataComponent>(fallback.Owner).EntityPrototype?.ID,
                    Is.EqualTo("RMCIntelTechTree_opfor"));
            }
            finally
            {
                DeleteFactionTree(server.EntMan, intel);
                intel.ClearTeamTechTreeOverrides();
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DirectorMutationRefreshesExistingRequisitionsComputers()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        EntityUid console = default;
        NetEntity consoleNet = default;
        var initialCategoryCount = 0;

        await server.WaitAssertion(() =>
        {
            server.EntMan.EventBus.RaiseEvent(
                EventSource.Local,
                new RoundRestartCleanupEvent());
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            Assert.That(
                server.System<CMURoundDirectorSystem>().TrySetLegacyForce(
                    RoundSide.Opfor,
                    prototypes.Index(UppPlatoon)),
                Is.EqualTo(CMURoundSelectionMutationResult.Applied));
            console = server.EntMan.SpawnEntity("CMASRSConsole", map.GridCoords);
            consoleNet = server.EntMan.GetNetEntity(console);
        });
        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            var clientConsole = client.EntMan.GetEntity(consoleNet);
            initialCategoryCount = client.EntMan
                .GetComponent<RequisitionsComputerComponent>(clientConsole)
                .Categories.Count;
            Assert.That(initialCategoryCount, Is.GreaterThan(0));
        });

        await server.WaitAssertion(() =>
        {
            // Deliberately bypass Dirty() to prove the force transition performs the refresh.
            var component = server.EntMan.GetComponent<RequisitionsComputerComponent>(console);
            var categories = (IList) typeof(RequisitionsComputerComponent)
                .GetField(nameof(RequisitionsComputerComponent.Categories))!
                .GetValue(component)!;
            categories.Clear();
        });
        await pair.RunTicksSync(3);

        await client.WaitAssertion(() =>
        {
            var clientConsole = client.EntMan.GetEntity(consoleNet);
            Assert.That(
                client.EntMan.GetComponent<RequisitionsComputerComponent>(clientConsole)
                    .Categories.Count,
                Is.EqualTo(initialCategoryCount));
        });

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            // Accepted same-force applications retain the legacy refresh contract.
            Assert.That(
                server.System<CMURoundDirectorSystem>().TrySetLegacyForce(
                    RoundSide.Opfor,
                    prototypes.Index(UppPlatoon)),
                Is.EqualTo(CMURoundSelectionMutationResult.Applied));
        });
        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            var clientConsole = client.EntMan.GetEntity(consoleNet);
            Assert.That(
                client.EntMan.GetComponent<RequisitionsComputerComponent>(clientConsole)
                    .Categories,
                Is.Empty);
        });

        await server.WaitPost(() => server.EntMan.DeleteEntity(console));
        await pair.RunUntilSynced();
        await pair.CleanReturnAsync();
    }

    private static void DeleteFactionTree(
        IEntityManager entities,
        IntelSystem intel)
    {
        if (intel.TryGetTechTree(Team.OpFor, out var existing))
            entities.DeleteEntity(existing.Value.Owner);
    }
}
