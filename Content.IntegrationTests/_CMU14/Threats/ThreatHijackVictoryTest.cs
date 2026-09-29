using Content.Server.CMU14.Threats.Rules;
using Content.Server.CMU14.Dropship.Integrity;
using Content.Server.GameTicking;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.CMU14.Threats.Rules;
using Content.Shared.CMU14.Dropship.Integrity;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Threats;

[TestFixture]
public sealed class ThreatHijackVictoryTest
{
    [Test, Timeout(180000)]
    public async Task WreckCannotEndRoundWithSevenSurvivorsAndAReplacementQueen()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = false, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var survivors = new List<EntityUid>();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var ticker = entities.System<GameTicker>();
            var mobs = entities.System<MobStateSystem>();
            var hives = entities.System<SharedXenoHiveSystem>();
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            entities.EnsureComponent<RMCPlanetComponent>(map.MapUid);
            var hive = entities.SpawnEntity("CMXenoHive", MapCoordinates.Nullspace);
            var oldQueen = entities.SpawnEntity("CMXenoQueen", map.GridCoords);
            hives.SetHive(oldQueen, hive);
            for (var i = 0; i < 6; i++)
            {
                var drone = entities.SpawnEntity("CMXenoDrone", map.GridCoords);
                hives.SetHive(drone, hive);
                survivors.Add(drone);
            }

            Assert.That(ticker.StartGameRule("KillAllXenoRule"), Is.True);
            var collapse = ticker.AddGameRule("HiveCollapseRule");
            entities.GetComponent<HiveCollapseRuleComponent>(collapse).HiveCollapseDuration = TimeSpan.FromSeconds(1);
            Assert.That(ticker.StartGameRule(collapse), Is.True);
            mobs.ChangeMobState(oldQueen, MobState.Dead);
            var queen = entities.SpawnEntity("CMXenoQueen", map.GridCoords);
            hives.SetHive(queen, hive);
            survivors.Add(queen);
            Assert.That(entities.GetComponent<HiveComponent>(hive).CurrentQueen, Is.EqualTo(queen));

            // Exercise the Lexington's hull-failure route instead of just setting a wreck flag.
            var wreck = entities.System<SharedMapSystem>().CreateGridEntity(map.MapId);
            entities.AddComponent<DropshipComponent>(wreck);
            var integrity = entities.EnsureComponent<DropshipIntegrityComponent>(wreck);
            integrity.CrashWarningTime = TimeSpan.Zero;
            entities.System<DropshipIntegritySystem>().DamageIntegrity((wreck, integrity), integrity.MaxIntegrity);
            Assert.That(integrity.Crashing, Is.True);
            Assert.That(entities.System<ThreatRuleHelper>().HasLandedDropshipHijack(), Is.False);
            var casualty = entities.SpawnEntity("CMXenoDrone", map.GridCoords);
            mobs.ChangeMobState(casualty, MobState.Dead);
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound),
                "A ground-side wreck must not turn living Xenos into a threat-eliminated result.");
        });
        await pair.RunTicksSync(pair.SecondsToTicks(2));
        await server.WaitAssertion(() =>
        {
            var ticker = server.System<GameTicker>();
            var mobs = server.System<MobStateSystem>();
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound),
                "The replacement Queen must cancel the previous Queen's collapse timer.");
            foreach (var survivor in survivors)
            {
                Assert.That(mobs.IsDead(survivor), Is.False);
                mobs.ChangeMobState(survivor, MobState.Dead);
            }
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PostRound),
                "The round must still end when the entire Xeno faction is defeated.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OnlyACompletedXenoHijackAbandonsPlanetSideSurvivors()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var rules = entities.System<ThreatRuleHelper>();
            var wreck = entities.SpawnEntity(null, map.GridCoords);
            var dropship = entities.AddComponent<DropshipComponent>(wreck);
            entities.System<SharedDropshipSystem>().SetDropshipCrashed((wreck, dropship), true);
            Assert.That(rules.HasLandedDropshipHijack(), Is.False,
                "An ordinary hull-integrity wreck must not exclude living Xenos from victory checks.");

            var starting = new DropshipHijackStartEvent(wreck);
            entities.EventBus.RaiseEvent(EventSource.Local, ref starting);
            Assert.That(rules.HasLandedDropshipHijack(), Is.False,
                "A hijack that has only launched must still count planet-side survivors.");

            var humanLanding = new DropshipHijackLandedEvent(map.MapUid, IsHumanHijack: true);
            entities.EventBus.RaiseEvent(EventSource.Local, ref humanLanding);
            Assert.That(rules.HasLandedDropshipHijack(), Is.False,
                "A human landing at a rival LZ does not start the ship-side Xeno endgame.");

            var xenoLanding = new DropshipHijackLandedEvent(map.MapUid);
            entities.EventBus.RaiseEvent(EventSource.Local, ref xenoLanding);
            Assert.That(rules.HasLandedDropshipHijack(), Is.True);
            entities.DeleteEntity(wreck);
            Assert.That(rules.HasLandedDropshipHijack(), Is.True,
                "Removing the landed wreck cannot undo a completed hijack.");

            entities.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            Assert.That(rules.HasLandedDropshipHijack(), Is.False,
                "The next round must count all surviving Xenos again.");
        });
        await pair.CleanReturnAsync();
    }
}
