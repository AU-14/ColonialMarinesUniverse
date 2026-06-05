using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Server._RMC14.Xenonids.Parasite;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.JoinXeno;
using Content.Shared.DoAfter;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class LarvaQueueJoinXenoUiTest
{
    [Test]
    public async Task LarvaQueueOffersLarvaInsteadOfGhostedAdult()
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
        var player = server.PlayerMan.Sessions.Single();

        EntityUid ghost = default;
        EntityUid hive = default;
        EntityUid runner = default;
        EntityUid larva = default;
        NetEntity ghostNet = default;
        string larvaName = string.Empty;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            BypassRoundstartDelay(entMan, ghost);
            hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            runner = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(2, 0)));
            hiveSystem.SetHive(runner, hive);
            larva = entMan.SpawnEntity("CMXenoLarva", map.GridCoords.Offset(new Vector2(3, 0)));
            hiveSystem.SetHive(larva, hive);
            larvaName = entMan.GetComponent<MetaDataComponent>(larva).EntityName;

            var mindId = mind.CreateMind(player.UserId, "Runner");
            mind.TransferTo(mindId, runner);
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);
            ghostNet = entMan.GetNetEntity(ghost);

            entMan.EventBus.RaiseLocalEvent(ghost, new JoinLarvaQueueEvent(entMan.GetNetEntity(hive)));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            AssertConfirmDialog(entMan, ghost, larvaName);
        });

        await ConfirmDialog(pair, ghostNet);
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(larva));
            Assert.That(player.AttachedEntity, Is.Not.EqualTo(runner));
            Assert.That(mind.TryGetMind(player.UserId, out _, out var mindComp), Is.True);
            Assert.That(mindComp!.CurrentEntity, Is.EqualTo(larva));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LarvaQueueOffersGhostedAdultWhenNoLarvaAvailable()
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
        var player = server.PlayerMan.Sessions.Single();

        EntityUid ghost = default;
        EntityUid hive = default;
        EntityUid runner = default;
        NetEntity ghostNet = default;
        string runnerName = string.Empty;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            BypassRoundstartDelay(entMan, ghost);
            hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            runner = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(2, 0)));
            hiveSystem.SetHive(runner, hive);
            runnerName = entMan.GetComponent<MetaDataComponent>(runner).EntityName;

            var mindId = mind.CreateMind(player.UserId, "Runner");
            mind.TransferTo(mindId, runner);
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);
            ghostNet = entMan.GetNetEntity(ghost);

            entMan.EventBus.RaiseLocalEvent(ghost, new JoinLarvaQueueEvent(entMan.GetNetEntity(hive)));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            AssertConfirmDialog(entMan, ghost, runnerName);
        });

        await ConfirmDialog(pair, ghostNet);
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(runner));
            Assert.That(mind.TryGetMind(player.UserId, out _, out var mindComp), Is.True);
            Assert.That(mindComp!.CurrentEntity, Is.EqualTo(runner));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LarvaQueueOffersDisconnectedAdultAfterTimeout()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        server.CfgMan.SetCVar(RMCCVars.RMCDisconnectedXenoGhostRoleTimeSeconds, 1);

        var entMan = server.EntMan;
        var hiveSystem = entMan.System<SharedXenoHiveSystem>();
        var mind = entMan.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();

        EntityUid ghost = default;
        EntityUid hive = default;
        EntityUid runner = default;
        NetEntity ghostNet = default;
        string runnerName = string.Empty;
        string playerName = player.Name;
        await server.WaitAssertion(() =>
        {
            hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            runner = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(2, 0)));
            hiveSystem.SetHive(runner, hive);
            runnerName = entMan.GetComponent<MetaDataComponent>(runner).EntityName;

            var mindId = mind.CreateMind(player.UserId, "Runner");
            mind.TransferTo(mindId, runner);
            mind.SetUserId(mindId, player.UserId);
        });

        playerName = await Disconnect(pair);
        await pair.RunSeconds(2);
        player = await Connect(pair, playerName);
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            ghost = player.AttachedEntity!.Value;
            Assert.That(entMan.HasComponent<GhostComponent>(ghost), Is.True);
            BypassRoundstartDelay(entMan, ghost);
            ghostNet = entMan.GetNetEntity(ghost);
            entMan.EventBus.RaiseLocalEvent(ghost, new JoinLarvaQueueEvent(entMan.GetNetEntity(hive)));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            AssertConfirmDialog(entMan, ghost, runnerName);
        });

        await ConfirmDialog(pair, ghostNet);
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(runner));
            Assert.That(mind.TryGetMind(player.UserId, out _, out var mindComp), Is.True);
            Assert.That(mindComp!.CurrentEntity, Is.EqualTo(runner));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LarvaQueueConfirmationTimeoutRemovesQueueSpot()
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
        var player = server.PlayerMan.Sessions.Single();

        EntityUid ghost = default;
        EntityUid hive = default;
        EntityUid larva = default;
        string larvaName = string.Empty;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            BypassRoundstartDelay(entMan, ghost);
            hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            larva = entMan.SpawnEntity("CMXenoLarva", map.GridCoords.Offset(new Vector2(2, 0)));
            hiveSystem.SetHive(larva, hive);
            larvaName = entMan.GetComponent<MetaDataComponent>(larva).EntityName;

            var mindId = mind.CreateMind(player.UserId, "Observer");
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);

            entMan.EventBus.RaiseLocalEvent(ghost, new JoinLarvaQueueEvent(entMan.GetNetEntity(hive)));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            AssertConfirmDialog(entMan, ghost, larvaName);
        });

        await pair.RunSeconds(31);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            Assert.That(entMan.HasComponent<DialogComponent>(ghost), Is.False);

            OpenJoinXenoUi(entMan, ghost);
            var state = GetJoinXenoState(entMan, ghost);
            var entry = state.Entries.Single(e => e.Hive == entMan.GetNetEntity(hive));
            Assert.That(entry.Status, Is.EqualTo(JoinXenoQueueStatus.NotQueued));
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task LarvaQueueInvalidConfirmedClaimKeepsQueueSpot(bool deleteLarva)
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
        var player = server.PlayerMan.Sessions.Single();

        EntityUid ghost = default;
        EntityUid hive = default;
        EntityUid larva = default;
        NetEntity ghostNet = default;
        string larvaName = string.Empty;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            BypassRoundstartDelay(entMan, ghost);
            hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            larva = entMan.SpawnEntity("CMXenoLarva", map.GridCoords.Offset(new Vector2(2, 0)));
            hiveSystem.SetHive(larva, hive);
            larvaName = entMan.GetComponent<MetaDataComponent>(larva).EntityName;

            var mindId = mind.CreateMind(player.UserId, "Observer");
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);
            ghostNet = entMan.GetNetEntity(ghost);

            entMan.EventBus.RaiseLocalEvent(ghost, new JoinLarvaQueueEvent(entMan.GetNetEntity(hive)));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            AssertConfirmDialog(entMan, ghost, larvaName);
        });

        await server.WaitAssertion(() =>
        {
            if (deleteLarva)
            {
                entMan.DeleteEntity(larva);
            }
            else
            {
                var larvaMind = mind.CreateMind(null, "Claimed Larva");
                mind.TransferTo(larvaMind, larva);
                Assert.That(entMan.GetComponent<MindContainerComponent>(larva).HasMind, Is.True);
            }
        });

        await pair.RunTicksSync(5);
        await ConfirmDialog(pair, ghostNet);
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            Assert.That(entMan.HasComponent<DialogComponent>(ghost), Is.False);

            OpenJoinXenoUi(entMan, ghost);
            var state = GetJoinXenoState(entMan, ghost);
            var entry = state.Entries.Single(e => e.Hive == entMan.GetNetEntity(hive));
            Assert.That(entry.Status, Is.EqualTo(JoinXenoQueueStatus.Queued));
            Assert.That(entry.Position, Is.EqualTo(1));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LarvaQueueRoundstartDelayBlocksQueueJoin()
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
        var player = server.PlayerMan.Sessions.Single();

        EntityUid ghost = default;
        EntityUid hive = default;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));

            var mindId = mind.CreateMind(player.UserId, "Observer");
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);

            entMan.EventBus.RaiseLocalEvent(ghost, new JoinLarvaQueueEvent(entMan.GetNetEntity(hive)));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            BypassRoundstartDelay(entMan, ghost);
            OpenJoinXenoUi(entMan, ghost);
            var state = GetJoinXenoState(entMan, ghost);
            var entry = state.Entries.Single(e => e.Hive == entMan.GetNetEntity(hive));
            Assert.That(entry.Status, Is.EqualTo(JoinXenoQueueStatus.NotQueued));
            Assert.That(entry.Position, Is.EqualTo(0));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LarvaQueueIgnoresLarvaMindRemovalDuringEvolution()
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
        var evolution = entMan.System<XenoEvolutionSystem>();
        var doAfter = entMan.System<SharedDoAfterSystem>();
        var player = server.PlayerMan.Sessions.Single();

        EntityUid ghost = default;
        EntityUid hive = default;
        EntityUid larva = default;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            BypassRoundstartDelay(entMan, ghost);
            hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            larva = entMan.SpawnEntity("CMXenoLarva", map.GridCoords.Offset(new Vector2(2, 0)));
            hiveSystem.SetHive(larva, hive);

            var ghostMind = mind.CreateMind(player.UserId, "Observer");
            mind.TransferTo(ghostMind, ghost);
            mind.SetUserId(ghostMind, player.UserId);

            var larvaMind = mind.CreateMind(null, "Larva");
            mind.TransferTo(larvaMind, larva);

            entMan.EventBus.RaiseLocalEvent(ghost, new JoinLarvaQueueEvent(entMan.GetNetEntity(hive)));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<DialogComponent>(ghost), Is.False);
            OpenJoinXenoUi(entMan, ghost);
            var state = GetJoinXenoState(entMan, ghost);
            var entry = state.Entries.Single(e => e.Hive == entMan.GetNetEntity(hive));
            Assert.That(entry.Status, Is.EqualTo(JoinXenoQueueStatus.Queued));
            Assert.That(entry.Position, Is.EqualTo(1));
        });

        await server.WaitAssertion(() =>
        {
            var evolutionComp = entMan.GetComponent<XenoEvolutionComponent>(larva);
            evolution.SetPoints((larva, evolutionComp), evolutionComp.Max);

            if (entMan.HasComponent<RestrictEvolveOffWeedsComponent>(larva))
                entMan.RemoveComponent<RestrictEvolveOffWeedsComponent>(larva);

            var ev = new XenoEvolutionDoAfterEvent("CMXenoRunner");
            var args = new DoAfterArgs(entMan, larva, TimeSpan.FromMilliseconds(1), ev, larva)
            {
                BreakOnRest = false,
            };
            Assert.That(doAfter.TryStartDoAfter(args), Is.True);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            Assert.That(entMan.HasComponent<DialogComponent>(ghost), Is.False);

            OpenJoinXenoUi(entMan, ghost);
            var state = GetJoinXenoState(entMan, ghost);
            var entry = state.Entries.Single(e => e.Hive == entMan.GetNetEntity(hive));
            Assert.That(entry.Status, Is.EqualTo(JoinXenoQueueStatus.Queued));
            Assert.That(entry.Position, Is.EqualTo(1));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LarvaQueueIgnoresEvolvedBodyAfterMindRemoval()
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
        var player = server.PlayerMan.Sessions.Single();

        EntityUid ghost = default;
        EntityUid hive = default;
        EntityUid runner = default;
        EntityUid runnerMind = default;

        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            BypassRoundstartDelay(entMan, ghost);
            hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            runner = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(2, 0)));
            entMan.EnsureComponent<XenoRecentlyDevolvedComponent>(runner);
            hiveSystem.SetHive(runner, hive);

            var ghostMind = mind.CreateMind(player.UserId, "Observer");
            mind.TransferTo(ghostMind, ghost);
            mind.SetUserId(ghostMind, player.UserId);

            runnerMind = mind.CreateMind(null, "Evolved Runner");
            mind.TransferTo(runnerMind, runner);

            entMan.EventBus.RaiseLocalEvent(ghost, new JoinLarvaQueueEvent(entMan.GetNetEntity(hive)));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            Assert.That(entMan.HasComponent<DialogComponent>(ghost), Is.False);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<MindContainerComponent>(runner).HasMind, Is.True);
        });

        await server.WaitAssertion(() =>
        {
            mind.TransferTo(runnerMind, null, createGhost: false);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            Assert.That(entMan.HasComponent<DialogComponent>(ghost), Is.False);

            OpenJoinXenoUi(entMan, ghost);
            var state = GetJoinXenoState(entMan, ghost);
            var entry = state.Entries.Single(e => e.Hive == entMan.GetNetEntity(hive));
            Assert.That(entry.Status, Is.EqualTo(JoinXenoQueueStatus.Queued));
            Assert.That(entry.Position, Is.EqualTo(1));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LarvaQueueIgnoresLarvaCreatedByDevolution()
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
        var evolution = entMan.System<XenoEvolutionSystem>();
        var player = server.PlayerMan.Sessions.Single();

        EntityUid ghost = default;
        EntityUid hive = default;
        EntityUid runner = default;
        EntityUid newLarva = default;

        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            BypassRoundstartDelay(entMan, ghost);
            hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            runner = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(2, 0)));
            hiveSystem.SetHive(runner, hive);

            var ghostMind = mind.CreateMind(player.UserId, "Observer");
            mind.TransferTo(ghostMind, ghost);
            mind.SetUserId(ghostMind, player.UserId);

            var runnerMind = mind.CreateMind(null, "Runner");
            mind.TransferTo(runnerMind, runner);

            entMan.EventBus.RaiseLocalEvent(ghost, new JoinLarvaQueueEvent(entMan.GetNetEntity(hive)));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            Assert.That(entMan.HasComponent<DialogComponent>(ghost), Is.False);
        });

        await server.WaitAssertion(() =>
        {
            var devolve = entMan.GetComponent<XenoDevolveComponent>(runner);
            var result = evolution.Devolve((runner, devolve), "CMXenoLarva");

            Assert.That(result, Is.Not.Null);
            newLarva = result.Value;
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            Assert.That(entMan.HasComponent<DialogComponent>(ghost), Is.False);
            Assert.That(entMan.HasComponent<LarvaQueueClaimBlockedComponent>(newLarva), Is.True);
            Assert.That(entMan.HasComponent<XenoRecentlyDevolvedComponent>(newLarva), Is.True);
            Assert.That(entMan.GetComponent<MindContainerComponent>(newLarva).HasMind, Is.True);

            OpenJoinXenoUi(entMan, ghost);
            var state = GetJoinXenoState(entMan, ghost);
            var entry = state.Entries.Single(e => e.Hive == entMan.GetNetEntity(hive));
            Assert.That(entry.Status, Is.EqualTo(JoinXenoQueueStatus.Queued));
            Assert.That(entry.Position, Is.EqualTo(1));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ParasiteRoleBlocksRecentlyDeadGhost()
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
        var parasiteRoles = entMan.System<XenoEggRoleSystem>();

        EntityUid ghost = default;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            Assert.That(entMan.HasComponent<GhostComponent>(ghost), Is.True);
            Assert.That(parasiteRoles.UserCheck(ghost), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ParasiteRoleAllowsGhostAfterDeathTimer()
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
        var parasiteRoles = entMan.System<XenoEggRoleSystem>();
        var ghostSystem = entMan.System<SharedGhostSystem>();
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid ghost = default;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            var ghostComp = entMan.GetComponent<GhostComponent>(ghost);
            ghostSystem.SetTimeOfDeath((ghost, ghostComp), timing.CurTime - TimeSpan.FromMinutes(3));

            Assert.That(entMan.HasComponent<GhostComponent>(ghost), Is.True);
            Assert.That(parasiteRoles.UserCheck(ghost), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task JoinXenoUiShowsJoinOrLeaveWithQueuePosition()
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
        var player = server.PlayerMan.Sessions.Single();

        EntityUid ghost = default;
        EntityUid hive = default;

        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            BypassRoundstartDelay(entMan, ghost);
            hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));

            var mindId = mind.CreateMind(player.UserId, "Observer");
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);

            Assert.That(entMan.HasComponent<GhostComponent>(ghost), Is.True);
            OpenJoinXenoUi(entMan, ghost);

            var state = GetJoinXenoState(entMan, ghost);
            var entry = state.Entries.Single(e => e.Hive == entMan.GetNetEntity(hive));
            Assert.That(entry.Status, Is.EqualTo(JoinXenoQueueStatus.NotQueued));
            Assert.That(entry.Position, Is.EqualTo(0));
        });

        await server.WaitAssertion(() =>
        {
            entMan.EventBus.RaiseLocalEvent(ghost, new JoinLarvaQueueEvent(entMan.GetNetEntity(hive)));
            OpenJoinXenoUi(entMan, ghost);

            var state = GetJoinXenoState(entMan, ghost);
            var entry = state.Entries.Single(e => e.Hive == entMan.GetNetEntity(hive));
            Assert.That(entry.Status, Is.EqualTo(JoinXenoQueueStatus.Queued));
            Assert.That(entry.Position, Is.EqualTo(1));
        });

        await pair.CleanReturnAsync();
    }

    private static void OpenJoinXenoUi(IEntityManager entMan, EntityUid ghost)
    {
        var ev = new JoinXenoActionEvent
        {
            Performer = ghost,
        };

        entMan.EventBus.RaiseLocalEvent(ghost, ev);
    }

    private static void BypassRoundstartDelay(IEntityManager entMan, EntityUid ghost)
    {
        entMan.EnsureComponent<JoinXenoCooldownIgnoreComponent>(ghost);
    }

    private static void AssertConfirmDialog(IEntityManager entMan, EntityUid ghost, string xenoName)
    {
        Assert.That(entMan.TryGetComponent<DialogComponent>(ghost, out var dialog), Is.True);
        Assert.That(dialog!.Title, Is.EqualTo("Join as Xeno"));
        Assert.That(dialog.Message.Text, Does.Contain(xenoName));
        Assert.That(dialog.Options.Select(o => o.Text), Is.EqualTo(new[] { "Click here to confirm", "Decline" }));
    }

    private static async Task ConfirmDialog(TestPair pair, NetEntity ghostNet)
    {
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
    }

    private static JoinXenoBuiState GetJoinXenoState(IEntityManager entMan, EntityUid ghost)
    {
        var ui = entMan.System<SharedUserInterfaceSystem>();
        Assert.That(ui.TryGetUiState<JoinXenoBuiState>(ghost, JoinXenoUIKey.Key, out var state), Is.True);
        return (JoinXenoBuiState) state!;
    }

    private static async Task<string> Disconnect(TestPair pair)
    {
        var net = pair.Client.ResolveDependency<IClientNetManager>();
        var player = pair.Server.PlayerMan.Sessions.Single();
        var name = player.Name;

        await pair.Client.WaitPost(() => net.ClientDisconnect("Abandoned xeno queue test disconnect."));
        await pair.RunTicksSync(5);
        return name;
    }

    private static async Task<ICommonSession> Connect(TestPair pair, string username)
    {
        var net = pair.Client.ResolveDependency<IClientNetManager>();
        await Task.WhenAll(pair.Client.WaitIdleAsync(), pair.Server.WaitIdleAsync());
        pair.Client.SetConnectTarget(pair.Server);
        await pair.Client.WaitPost(() => net.ClientConnect(null!, 0, username));
        await pair.RunTicksSync(5);
        return pair.Server.PlayerMan.Sessions.Single();
    }
}
