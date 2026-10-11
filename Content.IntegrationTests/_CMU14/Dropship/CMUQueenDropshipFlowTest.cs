using Content.IntegrationTests.Fixtures;
using Content.Server.Shuttles.Components;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Interaction;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class CMUQueenDropshipFlowTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task QueenCallsRestrictedDropshipFromPlanetsideTerminal()
    {
        var planet = await Pair.CreateTestMap();
        var ship = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var config = Server.ResolveDependency<IConfigurationManager>();
            config.SetCVar(RMCCVars.RMCDropshipInitialDelayMinutes, 0f);
            config.SetCVar(RMCCVars.RMCDropshipHijackInitialDelayMinutes, 0);
            SEntMan.EnsureComponent<RMCPlanetComponent>(planet.Grid);
            SEntMan.EnsureComponent<ShuttleComponent>(ship.Grid);
            var computer = SEntMan.SpawnEntity("CMComputerDropshipNavigationGovfor", ship.GridCoords);
            var terminal = SEntMan.SpawnEntity(null, planet.GridCoords);
            SEntMan.AddComponent<DropshipTerminalComponent>(terminal);
            var destination = SEntMan.SpawnEntity("CMDropshipDestination", planet.GridCoords);
            var queen = SEntMan.SpawnEntity("CMXenoQueen", planet.GridCoords);
            var system = Server.System<SharedDropshipSystem>();
            system.SetFactionController(destination, "govfor");
            Assert.That(system.CanUseNavigation(computer, queen), Is.False);

            SEntMan.EventBus.RaiseLocalEvent(terminal, new ActivateInWorldEvent(queen, terminal, true));

            Assert.That(SEntMan.HasComponent<FTLComponent>(ship.Grid), Is.True,
                "a validated queen call must dispatch the ship without a pilot's access card");
            Assert.That(SEntMan.GetComponent<DropshipDestinationComponent>(destination).Ship, Is.EqualTo(ship.Grid.Owner));
        });
    }

    [Test]
    public async Task HijackSurgeGrantsEvolutionWithoutAnOvipositor()
    {
        var map = await Pair.CreateTestMap();
        EntityUid drone = default;
        await Server.WaitPost(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(RMCCVars.RMCEvolutionPointsRequireOvipositorMinutes, -1);
            var hive = SEntMan.SpawnEntity("CMUAlphaHive", map.GridCoords);
            drone = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            Server.System<SharedXenoHiveSystem>().SetHive(drone, hive);
            Server.System<XenoEvolutionSystem>().SetPoints((drone, SEntMan.GetComponent<XenoEvolutionComponent>(drone)), 0);
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            var points = SEntMan.GetComponent<XenoEvolutionComponent>(drone).Points;
            Assert.That(points.Float(), Is.Zero);
            var hijack = new DropshipHijackStartEvent(null);
            SEntMan.EventBus.RaiseLocalEvent(drone, ref hijack, broadcast: true);
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            var points = SEntMan.GetComponent<XenoEvolutionComponent>(drone).Points;
            Assert.That(points.Float(), Is.GreaterThanOrEqualTo(10f), "the hijack surge must accrue after the queen uproots");
        });
    }
}
