using Content.Server.CMU14.Round.Antags.EarlyWarning.Melding;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;
using Content.Shared.Alert;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Melding;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// N7 (FLOW-05, TEST-02): River-Melded selection is planet only, and a miss is a meld debt rather than an
/// immediate copy. The river retries every <see cref="EWJoeComponent.MeldRetry"/> and grants the copy only
/// when <see cref="EWJoeComponent.MeldSearchWindow"/> closes with debts left. The test shortens both.
/// </summary>
[TestFixture]
public sealed class EWJoeMeldingDebtTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";
    private const string RespawnAlert = "CMUEWJoeRespawn";

    /// <summary>A Working Joe body with its ghost role stripped, so only a test mind can take it.</summary>
    private static EntityUid SpawnSynthBody(IEntityManager entities, EntityCoordinates coords)
    {
        var body = entities.SpawnEntity(ColonyJoe, coords);
        entities.RemoveComponent<GhostTakeoverAvailableComponent>(body);
        entities.RemoveComponent<GhostRoleComponent>(body);
        return body;
    }

    [Test]
    public async Task OffNetworkSynthIsRefusedAndRankPrefersNonWorkingJoes()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var player = pair.Player!;

        await server.WaitAssertion(() =>
        {
            var mindSys = entities.System<MindSystem>();
            var melding = entities.System<EWJoeMeldingSystem>();
            var mapSys = entities.System<SharedMapSystem>();

            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            comp.MeldSearchWindow = TimeSpan.FromSeconds(30);

            // a synthetic player on a map outside the Joe's z-network (the ship, say)
            var otherMapUid = mapSys.CreateMap(out var otherMap);
            var victim = SpawnSynthBody(entities, new EntityCoordinates(otherMapUid, 0, 0));
            var mind = mindSys.CreateMind(player.UserId, "Offworld");
            mindSys.TransferTo(mind, victim);

            Assert.Multiple(() =>
            {
                Assert.That(melding.IsEligible((joe, comp), victim, null), "eligible with the map gate skipped");
                Assert.That(melding.IsEligible((joe, comp), victim, new HashSet<MapId> { map.MapId }), Is.False,
                    "refused when its map is not on the Joe's network");
                Assert.That(melding.IsEligible((joe, comp), victim, new HashSet<MapId> { map.MapId, otherMap }),
                    "accepted once its map is on the network");
                Assert.That(melding.PickMeldTarget((joe, comp)), Is.Null, "planet only: the real pick refuses it");
                Assert.That(melding.TryStartMeld((joe, comp)), Is.False);
                Assert.That(entities.HasComponent<RiverMeldingComponent>(victim), Is.False);
                Assert.That(comp.PendingMelds, Is.EqualTo(1), "the miss is a debt");
            });

            // ranking: anything that is not a Working Joe comes before a Working Joe, every time. Rank only
            // orders; eligibility (synthetic, planet, player) is checked before it, so a plain body will do.
            var workingJoe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var other = entities.SpawnEntity(Human, map.GridCoords);
            Assert.That(melding.IsWorkingJoe(workingJoe));
            Assert.That(melding.IsWorkingJoe(other), Is.False);
            for (var i = 0; i < 20; i++)
            {
                Assert.That(melding.Rank(new[] { workingJoe, other }), Is.EqualTo(other), "the non-Working Joe tier wins");
            }

            Assert.That(melding.Rank(new[] { workingJoe }), Is.EqualTo(workingJoe), "a Working Joe when it is all there is");
            Assert.That(melding.Rank(Array.Empty<EntityUid>()), Is.Null);

            entities.DeleteEntity(victim);
            entities.DeleteEntity(otherMapUid);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ASynthWhoArrivesInsideTheWindowIsMeldedAndNoCopyIsGiven()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();
        var player = pair.Player!;

        EntityUid joe = default, body = default;

        await server.WaitAssertion(() =>
        {
            var melding = entities.System<EWJoeMeldingSystem>();

            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            comp.MeldRetry = TimeSpan.FromSeconds(2);
            comp.MeldSearchWindow = TimeSpan.FromSeconds(12);

            // a synthetic body nobody plays yet: rejected (no session) at assimilation
            body = SpawnSynthBody(entities, map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(melding.TryStartMeld((joe, comp)), Is.False, "nobody to meld at assimilation");
                Assert.That(comp.PendingMelds, Is.EqualTo(1), "one debt");
                Assert.That(comp.MeldSearchUntil, Is.EqualTo(timing.CurTime + comp.MeldSearchWindow), "the window opened");
                Assert.That(comp.RespawnReady, Is.False, "no copy straight away");
            });
        });

        // a few retries with still nobody to take
        await pair.RunSeconds(3);

        await server.WaitAssertion(() =>
        {
            var comp = entities.GetComponent<EWJoeComponent>(joe);
            Assert.Multiple(() =>
            {
                Assert.That(comp.PendingMelds, Is.EqualTo(1), "still searching");
                Assert.That(comp.RespawnReady, Is.False);
                Assert.That(entities.HasComponent<RiverMeldingComponent>(body), Is.False);
            });

            // a player takes the synthetic body
            var mindSys = entities.System<MindSystem>();
            var mind = mindSys.CreateMind(player.UserId, "Arrival");
            mindSys.TransferTo(mind, body);
        });

        // the next retry finds them
        await pair.RunSeconds(4);

        await server.WaitAssertion(() =>
        {
            var comp = entities.GetComponent<EWJoeComponent>(joe);
            Assert.Multiple(() =>
            {
                Assert.That(entities.TryGetComponent(body, out RiverMeldingComponent? pending), "the debt started a meld");
                Assert.That(pending!.Master, Is.EqualTo(joe));
                Assert.That(comp.PendingMelds, Is.Zero, "the debt is paid");
                Assert.That(comp.RespawnReady, Is.False, "and no copy");
            });
        });

        // past the window: a paid debt never turns into a copy
        await pair.RunSeconds(7);

        await server.WaitAssertion(() =>
        {
            var comp = entities.GetComponent<EWJoeComponent>(joe);
            Assert.That(comp.RespawnReady, Is.False, "no copy after the window either");
            Assert.That(comp.PendingMelds, Is.Zero);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NoSynthForTheWholeWindowGivesOneCopyAndDeathDropsTheDebt()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        EntityUid joe = default, doomed = default;
        var searchUntil = TimeSpan.Zero;

        await server.WaitAssertion(() =>
        {
            var melding = entities.System<EWJoeMeldingSystem>();

            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            comp.MeldRetry = TimeSpan.FromSeconds(1);
            comp.MeldSearchWindow = TimeSpan.FromSeconds(3);

            // two assimilations with nobody to take: two debts, one search window
            Assert.That(melding.TryStartMeld((joe, comp)), Is.False);
            searchUntil = comp.MeldSearchUntil;
            Assert.That(melding.TryStartMeld((joe, comp)), Is.False);

            Assert.Multiple(() =>
            {
                Assert.That(comp.PendingMelds, Is.EqualTo(2), "two debts");
                Assert.That(comp.MeldSearchUntil, Is.EqualTo(searchUntil), "the second debt joins the open search");
                Assert.That(comp.RespawnReady, Is.False, "no copy while searching");
            });

            // a second Joe with a debt dies: its debt goes with it
            doomed = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var doomedComp = entities.EnsureComponent<EWJoeComponent>(doomed);
            doomedComp.MeldRetry = TimeSpan.FromSeconds(1);
            doomedComp.MeldSearchWindow = TimeSpan.FromSeconds(3);
            Assert.That(melding.TryStartMeld((doomed, doomedComp)), Is.False);
            Assert.That(doomedComp.PendingMelds, Is.EqualTo(1));

            entities.System<MobStateSystem>().ChangeMobState(doomed, MobState.Dead);
            Assert.That(doomedComp.PendingMelds, Is.Zero, "death clears the debt");
        });

        await pair.RunSeconds(5);

        await server.WaitAssertion(() =>
        {
            var comp = entities.GetComponent<EWJoeComponent>(joe);
            var alerts = entities.System<AlertsSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(comp.RespawnReady, "the window closed with no synthetic: the copy");
                Assert.That(alerts.IsShowingAlert(joe, RespawnAlert), "and it shows");
                Assert.That(comp.PendingMelds, Is.Zero, "both debts settled by the one copy");
                Assert.That(comp.Respawns, Is.Zero, "granted, not spent");
                Assert.That(entities.TryGetComponent(doomed, out EWJoeComponent? doomedComp) && doomedComp.RespawnReady, Is.False,
                    "the dead Joe's dropped debt gave nothing");
            });
        });

        await pair.CleanReturnAsync();
    }
}
