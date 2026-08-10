using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Server.Administration.Managers;
using Content.Server._RMC14.Xenonids.JoinXeno;
using Content.Server._RMC14.Xenonids.Parasite;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Voting.Managers;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared._RMC14.Thunderdome;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.JoinXeno;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Ghost;
using Content.Shared.Ghost.Components;
using Content.Shared.Ghost.Systems;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Preferences;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class LarvaPoolTest
{
    private const string SelectableXenoRole = "CMXenoSelectableXeno";

    [Test]
    public async Task EligibleGhostMustAcceptAutomaticOffer()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        await pair.SetJobPriority(SelectableXenoRole, JobPriority.High);

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        server.CfgMan.SetCVar(RMCCVars.RMCLarvaPoolWaitSeconds, 0);

        var entMan = server.EntMan;
        var mind = entMan.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();
        await DeAdmin(pair, player);

        EntityUid ghost = default;
        EntityUid larva = default;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            larva = entMan.SpawnEntity("CMXenoLarva", map.GridCoords.Offset(new Vector2(1, 0)));

            var mindId = mind.CreateMind(player.UserId, "Observer");
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            var status = new GetLarvaPoolStatusEvent(player.UserId);
            entMan.EventBus.RaiseEvent(EventSource.Local, status);
            var pool = status.Pools.Single().Value;
            Assert.That(pool.Status, Is.EqualTo(LarvaPoolStatus.Eligible));
            Assert.That(pool.PreferenceLoaded, Is.True);
            Assert.That(pool.OptedIn, Is.True);
            AssertLarvaPoolOffer(entMan, ghost, entMan.GetComponent<MetaDataComponent>(larva).EntityName);
        });

        await AcceptLarvaPoolOffer(pair, ghost);
        await server.WaitAssertion(() => Assert.That(player.AttachedEntity, Is.EqualTo(larva)));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task XenoPreferenceDoesNotBlockAutomaticOffer()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        await pair.SetJobPriority(SelectableXenoRole, JobPriority.Never);

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        server.CfgMan.SetCVar(RMCCVars.RMCLarvaPoolWaitSeconds, 0);

        var entMan = server.EntMan;
        var hiveSystem = entMan.System<SharedXenoHiveSystem>();
        var mind = entMan.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();
        await DeAdmin(pair, player);

        EntityUid ghost = default;
        EntityUid larva = default;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            var hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            larva = entMan.SpawnEntity("CMXenoLarva", map.GridCoords.Offset(new Vector2(2, 0)));
            hiveSystem.SetHive(larva, hive);

            var mindId = mind.CreateMind(player.UserId, "Observer");
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);
        });

        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            AssertLarvaPoolOffer(entMan, ghost, entMan.GetComponent<MetaDataComponent>(larva).EntityName);
        });

        await AcceptLarvaPoolOffer(pair, ghost);
        await server.WaitAssertion(() => Assert.That(player.AttachedEntity, Is.EqualTo(larva)));
        await pair.CleanReturnAsync();
    }

    [TestCase("CMXenoLesserDrone")]
    [TestCase("CMXenoParasite")]
    public async Task ActivePoolRoleReceivesLarvaOffer(string activeRolePrototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        await pair.SetJobPriority(SelectableXenoRole, JobPriority.High);

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        server.CfgMan.SetCVar(RMCCVars.RMCLarvaPoolWaitSeconds, 300);

        var entMan = server.EntMan;
        var hiveSystem = entMan.System<SharedXenoHiveSystem>();
        var mind = entMan.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();
        await DeAdmin(pair, player);

        EntityUid activeRole = default;
        EntityUid larva = default;
        await server.WaitAssertion(() =>
        {
            var hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            activeRole = entMan.SpawnEntity(activeRolePrototype, map.GridCoords.Offset(new Vector2(2, 0)));
            larva = entMan.SpawnEntity("CMXenoLarva", map.GridCoords.Offset(new Vector2(3, 0)));
            hiveSystem.SetHive(activeRole, hive);
            hiveSystem.SetHive(larva, hive);

            var mindId = mind.CreateMind(player.UserId, "Temporary xeno role");
            mind.TransferTo(mindId, activeRole);
            mind.SetUserId(mindId, player.UserId);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(activeRole));
            AssertLarvaPoolOffer(entMan, activeRole, entMan.GetComponent<MetaDataComponent>(larva).EntityName);
        });

        await AcceptLarvaPoolOffer(pair, activeRole);
        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(larva));
            Assert.That(entMan.HasComponent<DialogComponent>(activeRole), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OpenPoolWindowUpdatesEligibilityLive()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        await pair.SetJobPriority(SelectableXenoRole, JobPriority.High);

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        server.CfgMan.SetCVar(RMCCVars.RMCLarvaPoolWaitSeconds, 1);

        var entMan = server.EntMan;
        var mind = entMan.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();
        await DeAdmin(pair, player);

        EntityUid ghost = default;
        EntityUid hive = default;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));

            var mindId = mind.CreateMind(player.UserId, "Observer");
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);

            OpenLarvaPoolUi(entMan, ghost);
            var entry = GetLarvaPoolState(entMan, ghost).Entries.Single(e => e.Hive == entMan.GetNetEntity(hive));
            Assert.That(entry.Status, Is.EqualTo(LarvaPoolStatus.Waiting));
            Assert.That(entry.Position, Is.EqualTo(1));
            Assert.That(entry.IneligibilityReason, Is.EqualTo(LarvaPoolIneligibilityReason.None));
            Assert.That(entry.PreferenceLoaded, Is.True);
            Assert.That(entry.OptedIn, Is.True);
        });

        await pair.RunSeconds(2);

        await server.WaitAssertion(() =>
        {
            var entry = GetLarvaPoolState(entMan, ghost).Entries.Single(e => e.Hive == entMan.GetNetEntity(hive));
            Assert.That(entry.Status, Is.EqualTo(LarvaPoolStatus.Eligible));
            Assert.That(entry.Position, Is.EqualTo(1));
            Assert.That(entry.IneligibilityReason, Is.EqualTo(LarvaPoolIneligibilityReason.None));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PerHiveOptOutDoesNotOccupyQueuePositionOrReceiveOffer()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        await pair.SetJobPriority(SelectableXenoRole, JobPriority.High);

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        server.CfgMan.SetCVar(RMCCVars.RMCLarvaPoolWaitSeconds, 0);

        var entMan = server.EntMan;
        var hiveSystem = entMan.System<SharedXenoHiveSystem>();
        var mind = entMan.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();
        await DeAdmin(pair, player);

        EntityUid primaryGhost = default;
        EntityUid standardHive = default;
        EntityUid corruptedHive = default;
        EntityUid larva = default;
        await server.WaitAssertion(() =>
        {
            primaryGhost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            standardHive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            corruptedHive = entMan.SpawnEntity("CMUCorruptedHive", map.GridCoords.Offset(new Vector2(2, 0)));
            larva = entMan.SpawnEntity("CMXenoLarva", map.GridCoords.Offset(new Vector2(3, 0)));
            hiveSystem.SetHive(larva, standardHive);

            var mindId = mind.CreateMind(player.UserId, "Observer");
            mind.TransferTo(mindId, primaryGhost);
            mind.SetUserId(mindId, player.UserId);
        });

        var (secondPlayer, secondGhost) = await AddDummyCandidate(
            pair,
            map.GridCoords.Offset(new Vector2(4, 0)));
        await DeAdmin(pair, secondPlayer);
        await pair.RunSeconds(2);

        await server.WaitAssertion(() =>
        {
            AssertLarvaPoolOffer(entMan, primaryGhost, entMan.GetComponent<MetaDataComponent>(larva).EntityName);
            Assert.That(entMan.HasComponent<DialogComponent>(secondGhost), Is.False);

            OpenLarvaPoolUi(entMan, primaryGhost);
            OpenLarvaPoolUi(entMan, secondGhost);
            var primaryEntries = GetLarvaPoolState(entMan, primaryGhost).Entries;
            var secondEntries = GetLarvaPoolState(entMan, secondGhost).Entries;

            Assert.That(primaryEntries.Single(e => e.Hive == entMan.GetNetEntity(standardHive)).Position, Is.EqualTo(1));
            Assert.That(secondEntries.Single(e => e.Hive == entMan.GetNetEntity(standardHive)).Position, Is.EqualTo(2));
            Assert.That(primaryEntries.Single(e => e.Hive == entMan.GetNetEntity(corruptedHive)).Position, Is.EqualTo(1));
            Assert.That(secondEntries.Single(e => e.Hive == entMan.GetNetEntity(corruptedHive)).Position, Is.EqualTo(2));

            SetLarvaPoolOptIn(entMan, primaryGhost, standardHive, false);
        });

        await pair.RunSeconds(2);

        await server.WaitAssertion(() =>
        {
            var primaryEntries = GetLarvaPoolState(entMan, primaryGhost).Entries;
            var secondEntries = GetLarvaPoolState(entMan, secondGhost).Entries;
            var optedOut = primaryEntries.Single(e => e.Hive == entMan.GetNetEntity(standardHive));

            Assert.That(optedOut.Status, Is.EqualTo(LarvaPoolStatus.Ineligible));
            Assert.That(optedOut.IneligibilityReason, Is.EqualTo(LarvaPoolIneligibilityReason.OptedOut));
            Assert.That(optedOut.OptedIn, Is.False);
            Assert.That(optedOut.Position, Is.EqualTo(1));
            Assert.That(secondEntries.Single(e => e.Hive == entMan.GetNetEntity(standardHive)).Position, Is.EqualTo(1));

            var otherHive = primaryEntries.Single(e => e.Hive == entMan.GetNetEntity(corruptedHive));
            Assert.That(otherHive.Status, Is.EqualTo(LarvaPoolStatus.Eligible));
            Assert.That(otherHive.OptedIn, Is.True);
            Assert.That(otherHive.Position, Is.EqualTo(1));
            Assert.That(secondEntries.Single(e => e.Hive == entMan.GetNetEntity(corruptedHive)).Position, Is.EqualTo(2));

            Assert.That(entMan.HasComponent<DialogComponent>(primaryGhost), Is.False);
            AssertLarvaPoolOffer(entMan, secondGhost, entMan.GetComponent<MetaDataComponent>(larva).EntityName);
        });

        var db = server.ResolveDependency<IServerDbManager>();
        var optOuts = await db.GetLarvaPoolOptOuts(player.UserId);
        Assert.That(optOuts, Does.Contain("CMXenoHive"));

        await AcceptLarvaPoolOffer(pair, secondGhost);
        await server.WaitAssertion(() => Assert.That(secondPlayer.AttachedEntity, Is.EqualTo(larva)));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RevivableBodyBlocksAssignmentUntilItBecomesUnrevivable()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        await pair.SetJobPriority(SelectableXenoRole, JobPriority.High);

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        server.CfgMan.SetCVar(RMCCVars.RMCLarvaPoolWaitSeconds, 0);

        var entMan = server.EntMan;
        var hiveSystem = entMan.System<SharedXenoHiveSystem>();
        var mind = entMan.System<MindSystem>();
        var mobState = entMan.System<MobStateSystem>();
        var unrevivable = entMan.System<RMCUnrevivableSystem>();
        var player = server.PlayerMan.Sessions.Single();
        await DeAdmin(pair, player);

        EntityUid body = default;
        EntityUid ghost = default;
        EntityUid larva = default;
        await server.WaitAssertion(() =>
        {
            body = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords.Offset(new Vector2(1, 0)));
            var hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(2, 0)));
            larva = entMan.SpawnEntity("CMXenoLarva", map.GridCoords.Offset(new Vector2(3, 0)));
            hiveSystem.SetHive(larva, hive);

            var mindId = mind.CreateMind(player.UserId, "Marine");
            mind.TransferTo(mindId, body);
            mobState.ChangeMobState(body, MobState.Dead);
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);
        });

        await pair.RunTicksSync(5);
        await server.WaitAssertion(() => Assert.That(player.AttachedEntity, Is.EqualTo(ghost)));

        await server.WaitAssertion(() => unrevivable.MakeUnrevivable(body));
        await pair.RunSeconds(2);

        await AcceptLarvaPoolOffer(pair, ghost);
        await server.WaitAssertion(() => Assert.That(player.AttachedEntity, Is.EqualTo(larva)));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StaffMustOptInForEachObserver()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        await pair.SetJobPriority(SelectableXenoRole, JobPriority.High);

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        server.CfgMan.SetCVar(RMCCVars.RMCLarvaPoolWaitSeconds, 0);

        var entMan = server.EntMan;
        var admins = server.ResolveDependency<IAdminManager>();
        var hiveSystem = entMan.System<SharedXenoHiveSystem>();
        var mind = entMan.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();
        await server.WaitAssertion(() =>
        {
            if (!admins.HasAdminFlag(player, AdminFlags.Moderator))
                admins.PromoteHost(player);
        });

        EntityUid ghost = default;
        EntityUid hive = default;
        EntityUid larva = default;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            var possessedXeno = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(2, 0)));
            larva = entMan.SpawnEntity("CMXenoLarva", map.GridCoords.Offset(new Vector2(3, 0)));
            hiveSystem.SetHive(possessedXeno, hive);
            hiveSystem.SetHive(larva, hive);

            var mindId = mind.CreateMind(player.UserId, "Observer");
            mind.TransferTo(mindId, possessedXeno);
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);
        });

        await pair.RunTicksSync(5);
        await server.WaitAssertion(() => Assert.That(player.AttachedEntity, Is.EqualTo(ghost)));

        await server.WaitAssertion(() => OpenLarvaPoolUi(entMan, ghost));
        await pair.RunTicksSync(5);

        await AcceptLarvaPoolOffer(pair, ghost);
        await server.WaitAssertion(() => Assert.That(player.AttachedEntity, Is.EqualTo(larva)));

        EntityUid nextGhost = default;
        await server.WaitAssertion(() =>
        {
            nextGhost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords.Offset(new Vector2(4, 0)));
            var nextLarva = entMan.SpawnEntity("CMXenoLarva", map.GridCoords.Offset(new Vector2(5, 0)));
            hiveSystem.SetHive(nextLarva, hive);

            Assert.That(mind.TryGetMind(player, out var mindId, out _), Is.True);
            mind.TransferTo(mindId, nextGhost);
            mind.SetUserId(mindId, player.UserId);
        });

        await pair.RunTicksSync(5);
        await server.WaitAssertion(() => Assert.That(player.AttachedEntity, Is.EqualTo(nextGhost)));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeathTimerEligibilityRecalculatesWithoutRejoining()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        await pair.SetJobPriority(SelectableXenoRole, JobPriority.High);

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        server.CfgMan.SetCVar(RMCCVars.RMCLarvaPoolWaitSeconds, 1);

        var entMan = server.EntMan;
        var hiveSystem = entMan.System<SharedXenoHiveSystem>();
        var mind = entMan.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();
        await DeAdmin(pair, player);

        EntityUid ghost = default;
        EntityUid larva = default;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            var hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            larva = entMan.SpawnEntity("CMXenoLarva", map.GridCoords.Offset(new Vector2(2, 0)));
            hiveSystem.SetHive(larva, hive);

            var mindId = mind.CreateMind(player.UserId, "Observer");
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);
        });

        await pair.RunTicksSync(5);
        await server.WaitAssertion(() => Assert.That(player.AttachedEntity, Is.EqualTo(ghost)));

        await pair.RunSeconds(2);

        await AcceptLarvaPoolOffer(pair, ghost);
        await server.WaitAssertion(() => Assert.That(player.AttachedEntity, Is.EqualTo(larva)));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeathTimerStartsWhenBodyDiesRatherThanWhenPlayerGhosts()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        await pair.SetJobPriority(SelectableXenoRole, JobPriority.High);

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        server.CfgMan.SetCVar(RMCCVars.RMCLarvaPoolWaitSeconds, 1);

        var entMan = server.EntMan;
        var hiveSystem = entMan.System<SharedXenoHiveSystem>();
        var mind = entMan.System<MindSystem>();
        var mobState = entMan.System<MobStateSystem>();
        var unrevivable = entMan.System<RMCUnrevivableSystem>();
        var player = server.PlayerMan.Sessions.Single();
        await DeAdmin(pair, player);

        EntityUid body = default;
        EntityUid ghost = default;
        EntityUid larva = default;
        EntityUid mindId = default;
        await server.WaitAssertion(() =>
        {
            body = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords.Offset(new Vector2(1, 0)));
            var hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(2, 0)));
            larva = entMan.SpawnEntity("CMXenoLarva", map.GridCoords.Offset(new Vector2(3, 0)));
            hiveSystem.SetHive(larva, hive);

            mindId = mind.CreateMind(player.UserId, "Marine");
            mind.TransferTo(mindId, body);
            mobState.ChangeMobState(body, MobState.Dead);
        });

        await pair.RunSeconds(2);
        await server.WaitAssertion(() =>
        {
            unrevivable.MakeUnrevivable(body);
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);
        });
        await pair.RunTicksSync(5);

        await AcceptLarvaPoolOffer(pair, ghost);
        await server.WaitAssertion(() => Assert.That(player.AttachedEntity, Is.EqualTo(larva)));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AvailableLarvaTakesPriorityOverAbandonedAdult()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        await pair.SetJobPriority(SelectableXenoRole, JobPriority.High);

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        server.CfgMan.SetCVar(RMCCVars.RMCLarvaPoolWaitSeconds, 0);

        var entMan = server.EntMan;
        var hiveSystem = entMan.System<SharedXenoHiveSystem>();
        var mind = entMan.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();
        await DeAdmin(pair, player);

        EntityUid ghost = default;
        EntityUid runner = default;
        EntityUid larva = default;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            var hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            runner = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(2, 0)));
            larva = entMan.SpawnEntity("CMXenoLarva", map.GridCoords.Offset(new Vector2(3, 0)));
            hiveSystem.SetHive(runner, hive);
            hiveSystem.SetHive(larva, hive);

            var mindId = mind.CreateMind(player.UserId, "Runner");
            mind.TransferTo(mindId, runner);
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            Assert.That(player.AttachedEntity, Is.Not.EqualTo(runner));
            AssertLarvaPoolOffer(entMan, ghost, entMan.GetComponent<MetaDataComponent>(larva).EntityName);
        });

        await AcceptLarvaPoolOffer(pair, ghost);
        await server.WaitAssertion(() => Assert.That(player.AttachedEntity, Is.EqualTo(larva)));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AbandonedAdultIsAssignedWhenNoLarvaExists()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        await pair.SetJobPriority(SelectableXenoRole, JobPriority.High);

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        server.CfgMan.SetCVar(RMCCVars.RMCLarvaPoolWaitSeconds, 0);

        var entMan = server.EntMan;
        var hiveSystem = entMan.System<SharedXenoHiveSystem>();
        var mind = entMan.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();
        await DeAdmin(pair, player);

        EntityUid ghost = default;
        EntityUid runner = default;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            var hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            runner = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(2, 0)));
            hiveSystem.SetHive(runner, hive);

            var mindId = mind.CreateMind(player.UserId, "Runner");
            mind.TransferTo(mindId, runner);
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            AssertLarvaPoolOffer(entMan, ghost, entMan.GetComponent<MetaDataComponent>(runner).EntityName);
        });

        await AcceptLarvaPoolOffer(pair, ghost);
        await server.WaitAssertion(() => Assert.That(player.AttachedEntity, Is.EqualTo(runner)));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GhostCommandOffersCriticalAdultToAnotherPlayer()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
            InLobby = true,
        });

        await pair.SetJobPriority(SelectableXenoRole, JobPriority.High);

        var server = pair.Server;
        server.CfgMan.SetCVar(RMCCVars.RMCLarvaPoolWaitSeconds, 0);
        server.CfgMan.SetCVar(CCVars.VoteEnabled, false);

        var entMan = server.EntMan;
        var hiveSystem = entMan.System<SharedXenoHiveSystem>();
        var mind = entMan.System<MindSystem>();
        var mobState = entMan.System<MobStateSystem>();
        var console = server.ResolveDependency<IConsoleHost>();
        var player = server.PlayerMan.Sessions.Single();
        await DeAdmin(pair, player);

        await CancelActiveVotes(pair);
        var candidate = await server.AddDummySession();
        await pair.RunTicksSync(5);
        await DeAdmin(pair, candidate);
        var userDb = server.ResolveDependency<UserDbDataManager>();
        await userDb.WaitLoadComplete(candidate);
        await pair.SetJobPriority(SelectableXenoRole, JobPriority.High, candidate.UserId);

        var ticker = entMan.System<GameTicker>();
        await server.WaitPost(() => ticker.StartRound());
        await server.WaitPost(() =>
        {
            ticker.JoinAsObserver(player);
            ticker.JoinAsObserver(candidate);
        });
        await pair.RunTicksSync(5);
        server.CfgMan.SetCVar(CCVars.GameLobbyEnabled, false);

        var map = await pair.CreateTestMap();

        EntityUid candidateGhost = default;
        EntityUid runner = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(candidate.AttachedEntity, Is.Not.Null);
            candidateGhost = candidate.AttachedEntity.Value;
            Assert.That(entMan.HasComponent<GhostComponent>(candidateGhost), Is.True);
            entMan.EnsureComponent<JoinXenoCooldownIgnoreComponent>(candidateGhost);

            var hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            runner = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(2, 0)));
            hiveSystem.SetHive(runner, hive);

            EntityUid mindId;
            if (!mind.TryGetMind(player, out mindId, out _))
                mindId = mind.CreateMind(player.UserId, "Runner");

            mind.TransferTo(mindId, runner);
            mind.SetUserId(mindId, player.UserId);
            mobState.ChangeMobState(runner, MobState.Critical);
        });

        await server.WaitPost(() => console.GetSessionShell(player).ExecuteCommand("ghost"));
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.Not.Null);
            Assert.That(entMan.HasComponent<GhostComponent>(player.AttachedEntity), Is.True);
            Assert.That(entMan.HasComponent<AbandonedXenoPoolAvailableComponent>(runner), Is.True);
            Assert.That(mobState.IsCritical(runner), Is.True);
            AssertLarvaPoolOffer(entMan, candidateGhost, entMan.GetComponent<MetaDataComponent>(runner).EntityName);
        });

        await AcceptLarvaPoolOffer(pair, candidateGhost);
        await server.WaitAssertion(() => Assert.That(candidate.AttachedEntity, Is.EqualTo(runner)));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ParasiteReservedLarvaIsNotAssignedFromPool()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        await pair.SetJobPriority(SelectableXenoRole, JobPriority.High);

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        server.CfgMan.SetCVar(RMCCVars.RMCLarvaPoolWaitSeconds, 0);

        var entMan = server.EntMan;
        var hiveSystem = entMan.System<SharedXenoHiveSystem>();
        var mind = entMan.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();
        await DeAdmin(pair, player);

        EntityUid ghost = default;
        EntityUid hive = default;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));

            var mindId = mind.CreateMind(player.UserId, "Observer");
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);

            var victim = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var infected = entMan.EnsureComponent<VictimInfectedComponent>(victim);
#pragma warning disable RA0002
            infected.InfectorUser = player.UserId;
            infected.InfectorWantsLarva = true;

            var larva = entMan.SpawnEntity("CMXenoLarva", map.GridCoords.Offset(new Vector2(3, 0)));
            infected.SpawnedLarva = larva;
#pragma warning restore RA0002
            entMan.Dirty(victim, infected);

            var burster = entMan.EnsureComponent<BursterComponent>(larva);
            burster.BurstFrom = victim;
            entMan.Dirty(larva, burster);
            hiveSystem.SetHive(larva, hive);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            Assert.That(entMan.HasComponent<DialogComponent>(ghost), Is.False);

            OpenLarvaPoolUi(entMan, ghost);
            var entry = GetLarvaPoolState(entMan, ghost).Entries.Single(e => e.Hive == entMan.GetNetEntity(hive));
            Assert.That(entry.Status, Is.EqualTo(LarvaPoolStatus.Eligible));
            Assert.That(entry.Position, Is.EqualTo(1));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TemporarilyBlockedLarvaIsAssignedWhenUnblocked()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        await pair.SetJobPriority(SelectableXenoRole, JobPriority.High);

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        server.CfgMan.SetCVar(RMCCVars.RMCLarvaPoolWaitSeconds, 0);

        var entMan = server.EntMan;
        var hiveSystem = entMan.System<SharedXenoHiveSystem>();
        var mind = entMan.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();
        await DeAdmin(pair, player);

        EntityUid ghost = default;
        EntityUid larva = default;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            var hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));

            var mindId = mind.CreateMind(player.UserId, "Observer");
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);

            larva = entMan.SpawnEntity("CMXenoLarva", map.GridCoords.Offset(new Vector2(2, 0)));
            entMan.EnsureComponent<LarvaPoolClaimBlockedComponent>(larva);
            hiveSystem.SetHive(larva, hive);
        });

        await pair.RunTicksSync(5);
        await server.WaitAssertion(() => Assert.That(player.AttachedEntity, Is.EqualTo(ghost)));

        await server.WaitAssertion(() => entMan.RemoveComponent<LarvaPoolClaimBlockedComponent>(larva));
        await pair.RunSeconds(2);

        await AcceptLarvaPoolOffer(pair, ghost);
        await server.WaitAssertion(() => Assert.That(player.AttachedEntity, Is.EqualTo(larva)));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PausedLarvaIsAssignedOnlyAfterItIsUnpaused()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        await pair.SetJobPriority(SelectableXenoRole, JobPriority.High);

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        server.CfgMan.SetCVar(RMCCVars.RMCLarvaPoolWaitSeconds, 0);

        var entMan = server.EntMan;
        var hiveSystem = entMan.System<SharedXenoHiveSystem>();
        var metadata = entMan.System<MetaDataSystem>();
        var mind = entMan.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();
        await DeAdmin(pair, player);

        EntityUid ghost = default;
        EntityUid larva = default;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            var hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            larva = entMan.SpawnEntity("CMXenoLarva", map.GridCoords.Offset(new Vector2(2, 0)));
            metadata.SetEntityPaused(larva, true);
            hiveSystem.SetHive(larva, hive);

            var mindId = mind.CreateMind(player.UserId, "Observer");
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);
        });

        await pair.RunTicksSync(5);
        await server.WaitAssertion(() => Assert.That(player.AttachedEntity, Is.EqualTo(ghost)));

        await server.WaitAssertion(() => metadata.SetEntityPaused(larva, false));
        await pair.RunSeconds(2);

        await AcceptLarvaPoolOffer(pair, ghost);
        await server.WaitAssertion(() => Assert.That(player.AttachedEntity, Is.EqualTo(larva)));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LesserXenoIsNotAssignedFromPool()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        await pair.SetJobPriority(SelectableXenoRole, JobPriority.High);

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        server.CfgMan.SetCVar(RMCCVars.RMCLarvaPoolWaitSeconds, 300);

        var entMan = server.EntMan;
        var hiveSystem = entMan.System<SharedXenoHiveSystem>();
        var mind = entMan.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();
        await DeAdmin(pair, player);

        EntityUid ghost = default;
        EntityUid hive = default;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            var lesser = entMan.SpawnEntity("CMXenoLesserDrone", map.GridCoords.Offset(new Vector2(2, 0)));
            hiveSystem.SetHive(lesser, hive);

            var mindId = mind.CreateMind(player.UserId, "Lesser Drone");
            mind.TransferTo(mindId, lesser);
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            OpenLarvaPoolUi(entMan, ghost);
            var entry = GetLarvaPoolState(entMan, ghost).Entries.Single(e => e.Hive == entMan.GetNetEntity(hive));
            Assert.That(entry.Status, Is.EqualTo(LarvaPoolStatus.Eligible));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThunderdomeTransferDoesNotBypassDeathTimer()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        await pair.SetJobPriority(SelectableXenoRole, JobPriority.High);

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        server.CfgMan.SetCVar(RMCCVars.RMCLarvaPoolWaitSeconds, 300);

        var entMan = server.EntMan;
        var mind = entMan.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();
        await DeAdmin(pair, player);

        EntityUid ghost = default;
        EntityUid hive = default;
        await server.WaitAssertion(() =>
        {
            entMan.EnsureComponent<ThunderdomeMapComponent>(map.MapUid);
            var body = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords.Offset(new Vector2(1, 0)));
            hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(2, 0)));

            var mindId = mind.CreateMind(player.UserId, "Thunderdome Player");
            mind.TransferTo(mindId, body);
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            Assert.That(entMan.HasComponent<JoinXenoCooldownIgnoreComponent>(ghost), Is.False);
            OpenLarvaPoolUi(entMan, ghost);
            var entry = GetLarvaPoolState(entMan, ghost).Entries.Single(e => e.Hive == entMan.GetNetEntity(hive));
            Assert.That(entry.Status, Is.EqualTo(LarvaPoolStatus.Waiting));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StrandedXenoReceivesCreditedBurrowedLarvaBeforeEarlierCandidate()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
            InLobby = true,
        });

        await pair.SetJobPriority(SelectableXenoRole, JobPriority.High);

        var server = pair.Server;
        server.CfgMan.SetCVar(RMCCVars.RMCLarvaPoolWaitSeconds, 0);
        server.CfgMan.SetCVar(CCVars.VoteEnabled, false);

        var entMan = server.EntMan;
        var hiveSystem = entMan.System<SharedXenoHiveSystem>();
        var larvaPool = entMan.System<LarvaPoolSystem>();
        var mind = entMan.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();
        await DeAdmin(pair, player);

        var dummyPlayer = await server.AddDummySession();
        await pair.RunTicksSync(5);
        await DeAdmin(pair, dummyPlayer);
        var userDb = server.ResolveDependency<UserDbDataManager>();
        await userDb.WaitLoadComplete(dummyPlayer);
        await pair.SetJobPriority(SelectableXenoRole, JobPriority.High, dummyPlayer.UserId);

        await CancelActiveVotes(pair);
        var ticker = entMan.System<GameTicker>();
        await server.WaitPost(() => ticker.StartRound());
        await server.WaitPost(() =>
        {
            ticker.JoinAsObserver(player);
            ticker.JoinAsObserver(dummyPlayer);
        });
        await pair.RunTicksSync(5);
        server.CfgMan.SetCVar(CCVars.GameLobbyEnabled, false);

        var map = await pair.CreateTestMap();
        EntityUid primaryGhost = default;
        EntityUid dummyGhost = default;
        EntityUid hive = default;
        EntityUid runner = default;
        EntityUid spawnPoint = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.Not.Null);
            Assert.That(dummyPlayer.AttachedEntity, Is.Not.Null);
            primaryGhost = player.AttachedEntity.Value;
            dummyGhost = dummyPlayer.AttachedEntity.Value;
            Assert.That(entMan.HasComponent<GhostComponent>(primaryGhost), Is.True);
            Assert.That(entMan.HasComponent<GhostComponent>(dummyGhost), Is.True);

            hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            runner = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(2, 0)));
            spawnPoint = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(3, 0)));
            hiveSystem.SetHive(runner, hive);
            hiveSystem.SetHive(spawnPoint, hive);
        });

        ICommonSession earlierPlayer = default!;
        ICommonSession strandedPlayer = default!;
        EntityUid earlierGhost = default;
        EntityUid strandedGhost = default;
        await server.WaitAssertion(() =>
        {
            OpenLarvaPoolUi(entMan, primaryGhost);
            OpenLarvaPoolUi(entMan, dummyGhost);
            var primaryStatus = GetLarvaPoolState(entMan, primaryGhost).Entries
                .Single(e => e.Hive == entMan.GetNetEntity(hive));
            var dummyStatus = GetLarvaPoolState(entMan, dummyGhost).Entries
                .Single(e => e.Hive == entMan.GetNetEntity(hive));

            Assert.That(primaryStatus.Status, Is.EqualTo(LarvaPoolStatus.Eligible));
            Assert.That(dummyStatus.Status, Is.EqualTo(LarvaPoolStatus.Eligible));
            Assert.That(primaryStatus.Position, Is.Not.EqualTo(dummyStatus.Position));
            if (primaryStatus.Position < dummyStatus.Position)
            {
                earlierPlayer = player;
                earlierGhost = primaryGhost;
                strandedPlayer = dummyPlayer;
                strandedGhost = dummyGhost;
            }
            else
            {
                earlierPlayer = dummyPlayer;
                earlierGhost = dummyGhost;
                strandedPlayer = player;
                strandedGhost = primaryGhost;
            }
        });

        await server.WaitAssertion(() =>
        {
            var hiveComp = entMan.GetComponent<HiveComponent>(hive);
            Assert.That(mind.TryGetMind(strandedPlayer, out var mindId, out _), Is.True);
            mind.TransferTo(mindId, runner);
            larvaPool.CreditStrandedXeno((hive, hiveComp), strandedPlayer.UserId);
            mind.TransferTo(mindId, strandedGhost);
            mind.SetUserId(mindId, strandedPlayer.UserId);

            entMan.DeleteEntity(runner);
            hiveSystem.ChangeBurrowedLarva((hive, hiveComp), 1);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(earlierPlayer.AttachedEntity, Is.EqualTo(earlierGhost));
            Assert.That(strandedPlayer.AttachedEntity, Is.EqualTo(strandedGhost));
            Assert.That(entMan.HasComponent<DialogComponent>(earlierGhost), Is.False);
            AssertLarvaPoolOffer(entMan, strandedGhost, "a burrowed larva");
        });

        await AcceptLarvaPoolOffer(pair, strandedGhost);

        await server.WaitAssertion(() =>
        {
            Assert.That(earlierPlayer.AttachedEntity, Is.EqualTo(earlierGhost));
            Assert.That(strandedPlayer.AttachedEntity, Is.Not.EqualTo(strandedGhost));
            Assert.That(strandedPlayer.AttachedEntity, Is.Not.EqualTo(spawnPoint));
            Assert.That(entMan.TryGetComponent(strandedPlayer.AttachedEntity, out XenoComponent xeno), Is.True);
            Assert.That(xeno!.Role.Id, Is.EqualTo("CMXenoLarva"));
            Assert.That(entMan.HasComponent<DialogComponent>(strandedGhost), Is.False);
            Assert.That(entMan.GetComponent<HiveComponent>(hive).BurrowedLarva, Is.Zero);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReconnectedCandidateAutomaticallyClaimsAbandonedAdult()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        await pair.SetJobPriority(SelectableXenoRole, JobPriority.High);

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        server.CfgMan.SetCVar(RMCCVars.RMCLarvaPoolWaitSeconds, 0);
        server.CfgMan.SetCVar(RMCCVars.RMCDisconnectedXenoGhostRoleTimeSeconds, 1);

        var entMan = server.EntMan;
        var hiveSystem = entMan.System<SharedXenoHiveSystem>();
        var mind = entMan.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();
        await DeAdmin(pair, player);

        EntityUid runner = default;
        await server.WaitAssertion(() =>
        {
            var hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            runner = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(2, 0)));
            hiveSystem.SetHive(runner, hive);

            var mindId = mind.CreateMind(player.UserId, "Runner");
            mind.TransferTo(mindId, runner);
            mind.SetUserId(mindId, player.UserId);
        });

        var playerName = await Disconnect(pair);
        await pair.RunSeconds(2);
        await server.WaitAssertion(() =>
            Assert.That(entMan.HasComponent<AbandonedXenoPoolAvailableComponent>(runner), Is.True));

        player = await Connect(pair, playerName);
        await DeAdmin(pair, player);
        await pair.RunSeconds(2);

        EntityUid reconnectedGhost = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.Not.Null);
            reconnectedGhost = player.AttachedEntity.Value;
            Assert.That(entMan.HasComponent<GhostComponent>(reconnectedGhost), Is.True);
            AssertLarvaPoolOffer(entMan, reconnectedGhost, entMan.GetComponent<MetaDataComponent>(runner).EntityName);
        });

        await AcceptLarvaPoolOffer(pair, reconnectedGhost);
        await server.WaitAssertion(() => Assert.That(player.AttachedEntity, Is.EqualTo(runner)));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DisconnectReconnectPreservesPoolPriority()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
            InLobby = true,
        });

        await pair.SetJobPriority(SelectableXenoRole, JobPriority.High);

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        server.CfgMan.SetCVar(RMCCVars.RMCLarvaPoolWaitSeconds, 300);
        server.CfgMan.SetCVar(RMCCVars.RMCDisconnectedXenoGhostRoleTimeSeconds, 5);
        server.CfgMan.SetCVar(CCVars.VoteEnabled, false);

        var entMan = server.EntMan;
        var hiveSystem = entMan.System<SharedXenoHiveSystem>();
        var mind = entMan.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();
        await DeAdmin(pair, player);

        EntityUid hive = default;
        EntityUid runner = default;
        await server.WaitAssertion(() =>
        {
            hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            runner = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(2, 0)));
            hiveSystem.SetHive(runner, hive);
            entMan.EnsureComponent<LarvaPoolClaimBlockedComponent>(runner);

            var mindId = mind.CreateMind(player.UserId, "Runner");
            mind.TransferTo(mindId, runner);
            mind.SetUserId(mindId, player.UserId);
        });

        await CancelActiveVotes(pair);
        var (_, laterGhost) = await AddDummyCandidate(pair, map.GridCoords.Offset(new Vector2(3, 0)));
        var ticker = entMan.System<GameTicker>();
        await server.WaitPost(() => ticker.StartRound());
        await pair.RunTicksSync(5);
        server.CfgMan.SetCVar(CCVars.GameLobbyEnabled, false);

        var playerName = await Disconnect(pair);
        await pair.RunSeconds(6);
        await server.WaitAssertion(() =>
            Assert.That(entMan.HasComponent<AbandonedXenoPoolAvailableComponent>(runner), Is.True));

        player = await Connect(pair, playerName);
        await DeAdmin(pair, player);
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var reconnectedGhost = player.AttachedEntity;
            Assert.That(reconnectedGhost, Is.Not.Null);
            Assert.That(entMan.HasComponent<GhostComponent>(reconnectedGhost), Is.True);
            Assert.That(entMan.HasComponent<JoinXenoCooldownIgnoreComponent>(reconnectedGhost), Is.True);

            OpenLarvaPoolUi(entMan, reconnectedGhost!.Value);
            OpenLarvaPoolUi(entMan, laterGhost);
            var first = GetLarvaPoolState(entMan, reconnectedGhost.Value).Entries
                .Single(e => e.Hive == entMan.GetNetEntity(hive));
            var second = GetLarvaPoolState(entMan, laterGhost).Entries
                .Single(e => e.Hive == entMan.GetNetEntity(hive));

            Assert.That(first.Status, Is.EqualTo(LarvaPoolStatus.Eligible));
            Assert.That(first.Position, Is.EqualTo(1));
            Assert.That(second.Status, Is.EqualTo(LarvaPoolStatus.Eligible));
            Assert.That(second.Position, Is.EqualTo(2));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DisconnectedBodyDeathResetsPriorityAndDeathTimer()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
            InLobby = true,
        });

        await pair.SetJobPriority(SelectableXenoRole, JobPriority.High);

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        server.CfgMan.SetCVar(RMCCVars.RMCLarvaPoolWaitSeconds, 300);
        server.CfgMan.SetCVar(RMCCVars.RMCDisconnectedXenoGhostRoleTimeSeconds, 5);
        server.CfgMan.SetCVar(CCVars.VoteEnabled, false);

        var entMan = server.EntMan;
        var hiveSystem = entMan.System<SharedXenoHiveSystem>();
        var mind = entMan.System<MindSystem>();
        var mobState = entMan.System<MobStateSystem>();
        var player = server.PlayerMan.Sessions.Single();
        await DeAdmin(pair, player);

        EntityUid hive = default;
        EntityUid runner = default;
        await server.WaitAssertion(() =>
        {
            hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));
            runner = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(2, 0)));
            hiveSystem.SetHive(runner, hive);

            var mindId = mind.CreateMind(player.UserId, "Runner");
            mind.TransferTo(mindId, runner);
            mind.SetUserId(mindId, player.UserId);
        });

        await CancelActiveVotes(pair);
        var (_, laterGhost) = await AddDummyCandidate(
            pair,
            map.GridCoords.Offset(new Vector2(3, 0)),
            ignoreCooldown: true);
        var ticker = entMan.System<GameTicker>();
        await server.WaitPost(() => ticker.StartRound());
        await pair.RunTicksSync(5);
        server.CfgMan.SetCVar(CCVars.GameLobbyEnabled, false);

        var playerName = await Disconnect(pair);
        await server.WaitAssertion(() => mobState.ChangeMobState(runner, MobState.Dead));
        await pair.RunSeconds(6);

        player = await Connect(pair, playerName);
        await DeAdmin(pair, player);
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var reconnectedGhost = player.AttachedEntity;
            Assert.That(reconnectedGhost, Is.Not.Null);
            Assert.That(entMan.HasComponent<GhostComponent>(reconnectedGhost), Is.True);
            Assert.That(entMan.HasComponent<JoinXenoCooldownIgnoreComponent>(reconnectedGhost), Is.False);

            OpenLarvaPoolUi(entMan, reconnectedGhost!.Value);
            OpenLarvaPoolUi(entMan, laterGhost);
            var status = GetLarvaPoolState(entMan, reconnectedGhost.Value).Entries
                .Single(e => e.Hive == entMan.GetNetEntity(hive));
            var laterStatus = GetLarvaPoolState(entMan, laterGhost).Entries
                .Single(e => e.Hive == entMan.GetNetEntity(hive));
            Assert.Multiple(() =>
            {
                Assert.That(status.Status, Is.EqualTo(LarvaPoolStatus.Waiting));
                Assert.That(laterStatus.Status, Is.EqualTo(LarvaPoolStatus.Eligible));
                Assert.That(status.Position, Is.EqualTo(2));
            });
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

        await server.WaitAssertion(() =>
        {
            var ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
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

        await server.WaitAssertion(() =>
        {
            var ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            var ghostComp = entMan.GetComponent<GhostComponent>(ghost);
            ghostSystem.SetTimeOfDeath((ghost, ghostComp), timing.CurTime - TimeSpan.FromMinutes(3));

            Assert.That(entMan.HasComponent<GhostComponent>(ghost), Is.True);
            Assert.That(parasiteRoles.UserCheck(ghost), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertLarvaPoolOffer(IEntityManager entMan, EntityUid offerEntity, string xenoName)
    {
        Assert.That(entMan.TryGetComponent<DialogComponent>(offerEntity, out var dialog), Is.True);
        Assert.That(dialog!.Message.Text, Does.Contain(xenoName));
        Assert.That(dialog.Options.Any(option => option.Event is LarvaPoolClaimConfirmEvent), Is.True);
        Assert.That(dialog.Options.Any(option => option.Event is LarvaPoolClaimDeclineEvent), Is.True);
    }

    private static async Task AcceptLarvaPoolOffer(TestPair pair, EntityUid offerEntity)
    {
        LarvaPoolClaimConfirmEvent confirm = default!;
        await pair.Server.WaitAssertion(() =>
        {
            var entMan = pair.Server.EntMan;
            Assert.That(entMan.TryGetComponent<DialogComponent>(offerEntity, out var dialog), Is.True);
            confirm = dialog!.Options
                .Select(option => option.Event)
                .OfType<LarvaPoolClaimConfirmEvent>()
                .Single();
        });

        await pair.Server.WaitPost(() =>
            pair.Server.EntMan.EventBus.RaiseEvent(EventSource.Local, confirm));
        await pair.RunTicksSync(5);
    }

    private static void SetLarvaPoolOptIn(
        IEntityManager entMan,
        EntityUid ghost,
        EntityUid hive,
        bool optedIn)
    {
        var message = new SetLarvaPoolOptInBuiMsg(entMan.GetNetEntity(hive), optedIn)
        {
            Actor = ghost,
            UiKey = JoinXenoUIKey.Key,
        };
        entMan.EventBus.RaiseLocalEvent(ghost, message);
    }

    private static void OpenLarvaPoolUi(IEntityManager entMan, EntityUid ghost)
    {
        var ev = new JoinXenoActionEvent
        {
            Performer = ghost,
        };

        entMan.EventBus.RaiseLocalEvent(ghost, ev);
    }

    private static JoinXenoBuiState GetLarvaPoolState(IEntityManager entMan, EntityUid ghost)
    {
        var ui = entMan.System<SharedUserInterfaceSystem>();
        Assert.That(ui.TryGetUiState<JoinXenoBuiState>(ghost, JoinXenoUIKey.Key, out var state), Is.True);
        return (JoinXenoBuiState) state!;
    }

    private static async Task<(ICommonSession Player, EntityUid Ghost)> AddDummyCandidate(
        TestPair pair,
        EntityCoordinates coordinates,
        bool ignoreCooldown = false)
    {
        var player = await pair.Server.AddDummySession();
        await pair.RunTicksSync(5);
        var userDb = pair.Server.ResolveDependency<UserDbDataManager>();
        await userDb.WaitLoadComplete(player);
        await pair.SetJobPriority(SelectableXenoRole, JobPriority.High, player.UserId);

        var entMan = pair.Server.EntMan;
        var ghostSystem = entMan.System<SharedGhostSystem>();
        var larvaPool = entMan.System<LarvaPoolSystem>();
        var mind = entMan.System<MindSystem>();
        var timing = pair.Server.ResolveDependency<IGameTiming>();
        EntityUid ghost = default;
        await pair.Server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, coordinates);

            EntityUid mindId;
            if (!mind.TryGetMind(player, out mindId, out _))
                mindId = mind.CreateMind(player.UserId, "Observer");

            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);
            larvaPool.OptInStaff(player.UserId);

            if (ignoreCooldown)
            {
                entMan.EnsureComponent<JoinXenoCooldownIgnoreComponent>(ghost);
                var ghostComp = entMan.GetComponent<GhostComponent>(ghost);
                ghostSystem.SetTimeOfDeath(
                    (ghost, ghostComp),
                    timing.CurTime - TimeSpan.FromMinutes(10));
            }
        });

        return (player, ghost);
    }

    private static async Task CancelActiveVotes(TestPair pair)
    {
        var votes = pair.Server.ResolveDependency<IVoteManager>();
        await pair.Server.WaitPost(() =>
        {
            foreach (var vote in votes.ActiveVotes.ToArray())
                vote.Cancel();
        });
        await pair.RunTicksSync(1);
    }

    private static async Task DeAdmin(TestPair pair, ICommonSession player)
    {
        await pair.Server.WaitAssertion(() =>
        {
            var admins = pair.Server.ResolveDependency<IAdminManager>();
            if (admins.IsAdmin(player))
                admins.DeAdmin(player);
        });
    }

    private static async Task<string> Disconnect(TestPair pair)
    {
        var net = pair.Client.ResolveDependency<IClientNetManager>();
        var player = pair.Player ?? throw new InvalidOperationException("Client session was not found on the server.");
        var name = player.Name;

        await pair.Client.WaitPost(() => net.ClientDisconnect("Abandoned xeno pool test disconnect."));
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
        return pair.Player ?? throw new InvalidOperationException("Reconnected client session was not found on the server.");
    }
}
