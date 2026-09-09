using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Server._RMC14.Xenonids.JoinXeno;
using Content.Server.Mind;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.NightVision;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.JoinXeno;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.DoAfter;
using Content.Shared.Ghost.Components;
using Content.Shared.Mind;
using Content.Shared.Stunnable;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Timing;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

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

    - type: entity
      parent: RMCXenoParasiteWatcher
      id: RMCTestXenoWatcherClaim
      components:
      - type: XenoParasite
        fallOffDelay: 0

    - type: entity
      parent: CMMobHuman
      id: RMCTestLarvaBurstHost
      components:
      - type: VictimInfected
        didBurstWarning: true
        insanePainChance: 0
    """;

    [TestCase(false, "RMCTestXenoParasiteClaim")]
    [TestCase(true, "RMCTestXenoParasiteClaim")]
    [TestCase(true, "RMCTestXenoWatcherClaim")]
    public async Task PlayerParasiteControlsLarvaSpawnedFromInfectedHost(bool pvs, string parasitePrototype)
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
            server.ResolveDependency<IConfigurationManager>().SetCVar(CVars.NetPVS, pvs);
            parasite = entMan.SpawnEntity(parasitePrototype, map.GridCoords);
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

        await AssertClaimedLarvaCanBurst(pair, victim);
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
    public async Task QueuedPlayerCanBurstFromHostOutsideGhostVisibility()
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
        var player = server.PlayerMan.Sessions.Single();
        EntityUid victim = default;
        NetEntity ghostNet = default;

        await server.WaitAssertion(() =>
        {
            server.ResolveDependency<IConfigurationManager>().SetCVar(CVars.NetPVS, true);
            var ghost = entMan.SpawnEntity("MobObserver", map.GridCoords);
            ghostNet = entMan.GetNetEntity(ghost);
            var mind = entMan.System<MindSystem>();
            var mindId = mind.CreateMind(player.UserId, "Observer");
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);

            var hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords);
            entMan.System<LarvaQueueSystem>().AddToLarvaQueueFront(
                (hive, entMan.GetComponent<HiveComponent>(hive)), player.UserId);
            victim = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(100, 0)));
            var infected = entMan.EnsureComponent<VictimInfectedComponent>(victim);
            var parasite = entMan.System<SharedXenoParasiteSystem>();
            parasite.SetHive((victim, infected), hive);
            parasite.SetBurstDelay((victim, infected), TimeSpan.Zero);
        });
        await pair.RunTicksSync(10);

        await pair.Client.WaitPost(() =>
        {
            var clientEntMan = pair.Client.EntMan;
            var ghost = clientEntMan.GetEntity(ghostNet);
            var ui = clientEntMan.GetComponent<UserInterfaceComponent>(ghost);
            ui.ClientOpenInterfaces[DialogUiKey.Key].SendPredictedMessage(new DialogOptionBuiMsg(0));
        });
        await pair.RunTicksSync(10);

        await AssertClaimedLarvaCanBurst(pair, victim);
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

    [TestCase(false)]
    [TestCase(true)]
    public async Task LarvaCanRetryFailedBurst(bool cancelAfterStarting)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid victim = default;
        EntityUid larva = default;

        await server.WaitAssertion(() =>
        {
            victim = entMan.SpawnEntity("RMCTestLarvaBurstHost", map.GridCoords);
            var infected = entMan.EnsureComponent<VictimInfectedComponent>(victim);
            var parasite = entMan.System<SharedXenoParasiteSystem>();
            parasite.SpawnLarva((victim, infected), out larva);
            var mind = entMan.System<MindSystem>();
            var player = server.PlayerMan.Sessions.Single();
            var mindId = mind.CreateMind(player.UserId, "Larva");
            mind.TransferTo(mindId, larva);

            if (cancelAfterStarting)
            {
                parasite.TryStartBurst((victim, infected));
                var doAfter = entMan.GetComponent<DoAfterComponent>(larva).DoAfters.Values.Single();
                entMan.System<SharedDoAfterSystem>().Cancel(doAfter.Id);
            }
            else
            {
                Assert.That(entMan.System<SharedStunSystem>().TryParalyze(larva, TimeSpan.FromSeconds(1), true), Is.True);
                parasite.TryStartBurst((victim, infected));
            }
        });
        await pair.RunSeconds(2);

        await server.WaitAssertion(() =>
            Assert.That(entMan.GetComponent<VictimInfectedComponent>(victim).IsBursting, Is.False,
                "A rejected or cancelled burst must allow the larva to try again."));
        await AssertClaimedLarvaCanBurst(pair, victim);
        await CleanReturnDisconnected(pair);
    }

    [Test]
    public async Task DeletingControlledLarvaDoesNotUpdateRemovedEye()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        EntityUid larva = default;
        await pair.Server.WaitPost(() =>
        {
            var entMan = pair.Server.EntMan;
            larva = entMan.SpawnEntity("CMXenoLarva", map.GridCoords);
            var mind = entMan.System<MindSystem>();
            var mindId = mind.CreateMind(pair.Server.PlayerMan.Sessions.Single().UserId, "Larva");
            mind.TransferTo(mindId, larva);
        });
        await pair.RunTicksSync(5);
        // Network deletions can dispose the controlled entity before the session detaches from it.
        await pair.Client.WaitPost(() =>
        {
            using var state = pair.Client.ResolveDependency<IClientGameTiming>().StartStateApplicationArea();
            var entMan = pair.Client.EntMan;
            var localLarva = pair.Client.PlayerMan.LocalEntity!.Value;
            entMan.RemoveComponent<EyeComponent>(localLarva);
            entMan.RemoveComponent<NightVisionComponent>(localLarva);
            entMan.DeleteEntity(localLarva);
        });
        await pair.Server.WaitPost(() => pair.Server.EntMan.DeleteEntity(larva));
        await pair.RunTicksSync(5);
        await CleanReturnDisconnected(pair);
    }

    private static async Task AssertClaimedLarvaCanBurst(TestPair pair, EntityUid victim)
    {
        var server = pair.Server;
        var client = pair.Client;
        var entMan = server.EntMan;
        EntityUid larva = default;
        NetEntity larvaNet = default;
        NetEntity victimNet = default;

        await server.WaitAssertion(() =>
        {
            larva = entMan.GetComponent<VictimInfectedComponent>(victim).SpawnedLarva!.Value;
            larvaNet = entMan.GetNetEntity(larva);
            victimNet = entMan.GetNetEntity(victim);
        });
        await pair.RunUntilSynced();

        await client.WaitAssertion(() =>
        {
            var clientEntMan = client.EntMan;
            var clientLarva = clientEntMan.GetEntity(larvaNet);
            var clientVictim = clientEntMan.GetEntity(victimNet);
            Assert.That(client.PlayerMan.LocalEntity, Is.EqualTo(clientLarva));
            Assert.That(clientEntMan.GetComponent<TransformComponent>(clientLarva).MapID, Is.Not.EqualTo(MapId.Nullspace),
                "The larva's view must be on the game map.");
            Assert.That(clientEntMan.GetComponent<TransformComponent>(clientLarva).MapID,
                Is.EqualTo(clientEntMan.GetComponent<TransformComponent>(clientVictim).MapID));
            Assert.That(clientEntMan.GetComponent<BursterComponent>(clientLarva).BurstFrom,
                Is.EqualTo(clientVictim));
            clientEntMan.System<Robust.Client.GameObjects.EyeSystem>().FrameUpdate(0);
            var camera = client.ResolveDependency<IEyeManager>().CurrentEye;
            Assert.That(camera, Is.SameAs(clientEntMan.GetComponent<EyeComponent>(clientLarva).Eye));
            Assert.That(camera.Position.MapId, Is.EqualTo(clientEntMan.GetComponent<TransformComponent>(clientVictim).MapID));
            Assert.That(float.IsFinite(camera.Position.Position.X) && float.IsFinite(camera.Position.Position.Y), Is.True);
        });

        foreach (var state in new[] { BoundKeyState.Down, BoundKeyState.Up })
        {
            await client.WaitPost(() =>
            {
                var input = client.ResolveDependency<IInputManager>();
                var timing = client.ResolveDependency<IGameTiming>();
                var function = EngineKeyFunctions.MoveRight;
                var message = new ClientFullInputCmdMessage(timing.CurTick, timing.TickFraction,
                    input.NetworkBindMap.KeyFunctionID(function))
                {
                    State = state,
                };
                client.EntMan.System<Robust.Client.GameObjects.InputSystem>()
                    .HandleInputCommand(client.PlayerMan.LocalSession, function, message);
            });
            await pair.RunTicksSync(5);
        }

        await server.WaitAssertion(() =>
            Assert.That(entMan.GetComponent<VictimInfectedComponent>(victim).IsBursting, Is.True,
                "Movement input from the claimed larva must start bursting."));

        await pair.RunSeconds(4);
        await pair.RunUntilSynced();

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<BursterComponent>(larva), Is.False,
                "The larva must finish bursting and leave its host.");
            Assert.That(entMan.System<SharedContainerSystem>().IsEntityInContainer(larva), Is.False);
            Assert.That(server.PlayerMan.Sessions.Single().AttachedEntity, Is.EqualTo(larva));
        });
    }

    private static async Task CleanReturnDisconnected(TestPair pair)
    {
        var net = pair.Client.ResolveDependency<IClientNetManager>();
        if (net.IsConnected)
        {
            await pair.Client.WaitPost(() => net.ClientDisconnect("Xeno parasite larva claim test cleanup disconnect."));
            await pair.RunTicksSync(1);
        }

        await pair.Server.WaitPost(() => pair.Server.ResolveDependency<IConfigurationManager>().SetCVar(CVars.NetPVS, false));
        await pair.CleanReturnAsync();
    }
}
