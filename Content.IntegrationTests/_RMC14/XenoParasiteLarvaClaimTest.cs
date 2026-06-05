using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Server.Mind;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.JoinXeno;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class XenoParasiteLarvaClaimTest
{
    [TestPrototypes]
    private const string Prototypes = """
    - type: entity
      parent: CMXenoParasite
      id: RMCTestXenoParasiteClaim
      components:
      - type: XenoParasite
        fallOffDelay: 0
    """;

    [Test]
    public async Task PlayerParasiteControlsLarvaSpawnedFromInfectedHost()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var mind = entMan.System<MindSystem>();
        var parasiteSystem = entMan.System<SharedXenoParasiteSystem>();
        var player = server.PlayerMan.Sessions.Single();

        EntityUid parasite = default;
        EntityUid victim = default;
        EntityUid ghost = default;
        NetEntity ghostNet = default;

        await server.WaitAssertion(() =>
        {
            parasite = entMan.SpawnEntity("RMCTestXenoParasiteClaim", map.GridCoords);
            victim = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

            var mindId = mind.CreateMind(player.UserId, "Parasite");
            mind.TransferTo(mindId, parasite);
            mind.SetUserId(mindId, player.UserId);

            var parasiteComp = entMan.GetComponent<XenoParasiteComponent>(parasite);
            Assert.That(parasiteSystem.Infect((parasite, parasiteComp), victim, force: true), Is.True);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.Not.EqualTo(parasite));
            Assert.That(entMan.HasComponent<GhostComponent>(player.AttachedEntity), Is.True);
            ghost = player.AttachedEntity!.Value;
            ghostNet = entMan.GetNetEntity(ghost);

            Assert.That(entMan.TryGetComponent<DialogComponent>(ghost, out var dialog), Is.True);
            Assert.That(dialog!.Options.Select(o => o.Text), Is.EquivalentTo(new[] { "Yes", "No" }));
        });

        await pair.Client.WaitAssertion(() =>
        {
            var clientEntMan = pair.Client.EntMan;
            var clientGhost = clientEntMan.GetEntity(ghostNet);
            Assert.That(clientEntMan.TryGetComponent<UserInterfaceComponent>(clientGhost, out var ui), Is.True);
            Assert.That(ui!.ClientOpenInterfaces.ContainsKey(DialogUiKey.Key), Is.True);
        });

        await pair.Client.WaitPost(() =>
        {
            var clientEntMan = pair.Client.EntMan;
            var clientGhost = clientEntMan.GetEntity(ghostNet);
            var ui = clientEntMan.GetComponent<UserInterfaceComponent>(clientGhost);
            ui.ClientOpenInterfaces[DialogUiKey.Key].SendPredictedMessage(new DialogOptionBuiMsg(0));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var infected = entMan.GetComponent<VictimInfectedComponent>(victim);
            parasiteSystem.SetBurstDelay(new Entity<VictimInfectedComponent>(victim, infected), TimeSpan.Zero);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var infected = entMan.GetComponent<VictimInfectedComponent>(victim);
            Assert.That(infected.SpawnedLarva, Is.Not.Null);
            Assert.That(player.AttachedEntity, Is.EqualTo(infected.SpawnedLarva));

            Assert.That(mind.TryGetMind(player.UserId, out _, out var mindComp), Is.True);
            Assert.That(mindComp!.CurrentEntity, Is.EqualTo(infected.SpawnedLarva));
        });

        await CleanReturnDisconnected(pair);
    }

    [Test]
    public async Task QueuedInfectorIsNotMovedIntoNonLarvaBeforeClaimedInfectionLarva()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var hiveSystem = entMan.System<SharedXenoHiveSystem>();
        var mind = entMan.System<MindSystem>();
        var parasiteSystem = entMan.System<SharedXenoParasiteSystem>();
        var player = server.PlayerMan.Sessions.Single();

        EntityUid hive = default;
        EntityUid parasite = default;
        EntityUid victim = default;
        EntityUid ghost = default;
        NetEntity ghostNet = default;
        EntityUid drone = default;

        await server.WaitAssertion(() =>
        {
            hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords);
            parasite = entMan.SpawnEntity("RMCTestXenoParasiteClaim", map.GridCoords.Offset(new Vector2(1, 0)));
            victim = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));

            hiveSystem.SetHive(parasite, hive);

            var mindId = mind.CreateMind(player.UserId, "Parasite");
            mind.TransferTo(mindId, parasite);
            mind.SetUserId(mindId, player.UserId);

            var parasiteComp = entMan.GetComponent<XenoParasiteComponent>(parasite);
            Assert.That(parasiteSystem.Infect((parasite, parasiteComp), victim, force: true), Is.True);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.Not.EqualTo(parasite));
            Assert.That(entMan.HasComponent<GhostComponent>(player.AttachedEntity), Is.True);
            ghost = player.AttachedEntity!.Value;
            ghostNet = entMan.GetNetEntity(ghost);

            Assert.That(entMan.TryGetComponent<DialogComponent>(ghost, out var dialog), Is.True);
            Assert.That(dialog!.Options.Select(o => o.Text), Is.EquivalentTo(new[] { "Yes", "No" }));
        });

        await pair.Client.WaitPost(() =>
        {
            var clientEntMan = pair.Client.EntMan;
            var clientGhost = clientEntMan.GetEntity(ghostNet);
            var ui = clientEntMan.GetComponent<UserInterfaceComponent>(clientGhost);
            ui.ClientOpenInterfaces[DialogUiKey.Key].SendPredictedMessage(new DialogOptionBuiMsg(0));
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            entMan.EventBus.RaiseLocalEvent(ghost, new JoinLarvaQueueEvent(entMan.GetNetEntity(hive)));
            drone = entMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(3, 0)));
            hiveSystem.SetHive(drone, hive);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            Assert.That(player.AttachedEntity, Is.Not.EqualTo(drone));
        });

        await server.WaitAssertion(() =>
        {
            var infected = entMan.GetComponent<VictimInfectedComponent>(victim);
            parasiteSystem.SetBurstDelay(new Entity<VictimInfectedComponent>(victim, infected), TimeSpan.Zero);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var infected = entMan.GetComponent<VictimInfectedComponent>(victim);
            Assert.That(infected.SpawnedLarva, Is.Not.Null);
            Assert.That(player.AttachedEntity, Is.EqualTo(infected.SpawnedLarva));

            Assert.That(mind.TryGetMind(player.UserId, out _, out var mindComp), Is.True);
            Assert.That(mindComp!.CurrentEntity, Is.EqualTo(infected.SpawnedLarva));
        });

        await CleanReturnDisconnected(pair);
    }

    [Test]
    public async Task PlayerParasiteDoesNotControlLarvaWithoutAcceptingPrompt()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var mind = entMan.System<MindSystem>();
        var parasiteSystem = entMan.System<SharedXenoParasiteSystem>();
        var player = server.PlayerMan.Sessions.Single();

        EntityUid parasite = default;
        EntityUid victim = default;

        await server.WaitAssertion(() =>
        {
            parasite = entMan.SpawnEntity("RMCTestXenoParasiteClaim", map.GridCoords);
            victim = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

            var mindId = mind.CreateMind(player.UserId, "Parasite");
            mind.TransferTo(mindId, parasite);
            mind.SetUserId(mindId, player.UserId);

            var parasiteComp = entMan.GetComponent<XenoParasiteComponent>(parasite);
            Assert.That(parasiteSystem.Infect((parasite, parasiteComp), victim, force: true), Is.True);
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var infected = entMan.GetComponent<VictimInfectedComponent>(victim);
            parasiteSystem.SetBurstDelay(new Entity<VictimInfectedComponent>(victim, infected), TimeSpan.Zero);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var infected = entMan.GetComponent<VictimInfectedComponent>(victim);
            Assert.That(infected.SpawnedLarva, Is.Not.Null);
            Assert.That(player.AttachedEntity, Is.Not.EqualTo(infected.SpawnedLarva));
            Assert.That(entMan.HasComponent<GhostComponent>(player.AttachedEntity), Is.True);
        });

        await CleanReturnDisconnected(pair);
    }

    private static async Task CleanReturnDisconnected(TestPair pair)
    {
        var net = pair.Client.ResolveDependency<IClientNetManager>();
        if (net.IsConnected)
        {
            await pair.Client.WaitPost(() => net.ClientDisconnect("Xeno parasite larva claim test cleanup disconnect."));
            await pair.RunTicksSync(1);
        }

        await pair.CleanReturnAsync();
    }
}
