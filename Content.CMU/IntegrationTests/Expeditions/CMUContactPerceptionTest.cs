using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Expeditions;
using Content.Server.NPC.Components;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Expeditions;

[TestFixture]
public sealed class CMUContactPerceptionTest : GameTest
{
    private static readonly ProtoId<NpcFactionPrototype> EnemyFaction = "GOVFOR";
    private delegate bool AimPoint(EntityUid uid, CMUExpeditionAgentComponent agent, Entity<GunComponent> gun,
        out EntityCoordinates point);

    [Test]
    public async Task DistantRecognitionRequiresExposureAndAnOcclusionBreaksIt()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var (uid, agent) = Observer(map.GridCoords);
            var target = Enemy(map.GridCoords.Offset(new Vector2(12, 0)));
            var observe = Observe();
            var transform = SEntMan.GetComponent<TransformComponent>(uid);
            var now = SGameTiming.CurTime;
            Assert.That(observe(uid, agent, transform, now), Is.Null);
            for (var i = 0; i < 10; i++)
                Assert.That(observe(uid, agent, transform, now), Is.Null, "Repeated updates at one timestamp are not exposure.");
            Assert.That(observe(uid, agent, transform, now + TimeSpan.FromSeconds(0.15)), Is.Null);
            var wall = SEntMan.SpawnEntity("CMUExpeditionHull", map.GridCoords.Offset(new Vector2(6, 0)));
            Assert.That(observe(uid, agent, transform, now + TimeSpan.FromSeconds(0.3)), Is.Null);
            SEntMan.DeleteEntity(wall);
            Assert.That(observe(uid, agent, transform, now + TimeSpan.FromSeconds(0.45)), Is.Null);
            Assert.That(observe(uid, agent, transform, now + TimeSpan.FromSeconds(0.6)), Is.Null);
            Assert.That(observe(uid, agent, transform, now + TimeSpan.FromSeconds(0.75)), Is.EqualTo(target));
            var snapshot = agent.LastSeen;
            Server.System<SharedTransformSystem>().SetCoordinates(target, map.GridCoords.Offset(new Vector2(100, 0)));
            Assert.That(observe(uid, agent, transform, now + TimeSpan.FromSeconds(0.9)), Is.Null);
            Assert.That(agent.LastSeen, Is.EqualTo(snapshot), "Losing sight must preserve the observation, not follow the hidden body.");
        });
    }

    [Test]
    public async Task RotatingSightBudgetEventuallyRecognizesCrowdAndPrioritizesCloseThreat()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var (uid, agent) = Observer(map.GridCoords);
            var targets = new List<EntityUid>();
            for (var i = 0; i < 40; i++)
                targets.Add(Enemy(map.GridCoords.Offset(new Vector2(8 + i % 4, (i / 4) * 0.4f))));
            var observe = Observe();
            var transform = SEntMan.GetComponent<TransformComponent>(uid);
            var now = SGameTiming.CurTime;
            var recognized = new HashSet<EntityUid>();
            for (var tick = 0; tick < 80; tick++)
            {
                observe(uid, agent, transform, now + TimeSpan.FromSeconds(tick * 0.15));
                Assert.That(agent.SightChecksLastThink, Is.LessThanOrEqualTo(agent.SightChecksPerThink));
                Assert.That(agent.ContactRecognition.Count, Is.LessThanOrEqualTo(32));
                foreach (var (target, memory) in agent.ContactRecognition)
                    if (memory.RecognizedUntil > now + TimeSpan.FromSeconds(tick * 0.15))
                        recognized.Add(target);
            }
            Assert.That(targets.All(recognized.Contains), Is.True,
                "The current target must not starve the rotating distant candidates.");
            var close = Enemy(map.GridCoords.Offset(new Vector2(3, 0)));
            observe(uid, agent, transform, now + TimeSpan.FromSeconds(12));
            Assert.That(agent.ContactRecognition[close].RecognizedUntil, Is.GreaterThan(now + TimeSpan.FromSeconds(12)),
                "A newly appearing nearby threat is checked immediately, regardless of scan cursor.");
        });
    }

    [Test]
    public async Task RadioIdentityDoesNotAuthorizeAShotBeforeVisualRecognition()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var (uid, agent) = Observer(map.GridCoords);
            var target = Enemy(map.GridCoords.Offset(new Vector2(9, 0)));
            var now = SGameTiming.CurTime;
            agent.Target = target;
            agent.LastSeen = SEntMan.GetComponent<TransformComponent>(target).Coordinates;
            agent.ContactFromRadio = true;
            agent.LastContact = now;
            agent.ForgetAt = now + agent.MemoryDuration;
            var system = Server.System<CMUExpeditionAgentSystem>();
            var aim = Method("TryAimPoint").CreateDelegate<AimPoint>(system);
            Assert.That(Server.System<GunSystem>().TryGetGun(uid, out var gun), Is.True);
            Assert.That(aim(uid, agent, gun, out _), Is.False);
            var observe = Observe();
            var transform = SEntMan.GetComponent<TransformComponent>(uid);
            Assert.That(observe(uid, agent, transform, now), Is.Null);
            Assert.That(aim(uid, agent, gun, out _), Is.False);
            observe(uid, agent, transform, now + TimeSpan.FromSeconds(0.15));
            Assert.That(observe(uid, agent, transform, now + TimeSpan.FromSeconds(0.3)), Is.EqualTo(target));
            Assert.That(aim(uid, agent, gun, out _), Is.True);
        });
    }

    [Test]
    public async Task SearchDoesNotInspectThroughWallAndExpiresWithoutNewEvidence()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var (uid, agent) = Observer(map.GridCoords);
            var point = map.GridCoords.Offset(new Vector2(2, 0));
            var wall = SEntMan.SpawnEntity("CMUExpeditionHull", map.GridCoords.Offset(new Vector2(1, 0)));
            var search = Method("SearchContact").CreateDelegate<Func<EntityUid, CMUExpeditionAgentComponent,
                EntityCoordinates, TimeSpan, TimeSpan, bool, bool>>(Server.System<CMUExpeditionAgentSystem>());
            var now = SGameTiming.CurTime;
            Assert.That(search(uid, agent, point, now, now, false), Is.True);
            Assert.That(agent.ContactInspectUntil, Is.Null, "A wall separates proximity from an inspected location.");
            var deadline = agent.ContactSearchUntil;
            Assert.That(search(uid, agent, point, now, now + TimeSpan.FromSeconds(2), false), Is.True);
            Assert.That(agent.ContactSearchUntil, Is.EqualTo(deadline), "Route retries must not renew sensory evidence.");
            Assert.That(search(uid, agent, point, now, deadline, false), Is.False);
            Assert.That(agent.ContactSearchPoint, Is.Null);
            SEntMan.DeleteEntity(wall);
            Assert.That(search(uid, agent, point, deadline, deadline, false), Is.True, "Fresh evidence can reopen a search.");
            Assert.That(search(uid, agent, point, deadline, deadline + TimeSpan.FromSeconds(0.5), false), Is.True,
                "An accessible area must be inspected before returning to orders.");
            Assert.That(search(uid, agent, point, deadline, deadline + agent.ContactInspectDuration, false), Is.False);
            var renewed = deadline + agent.ContactInspectDuration + TimeSpan.FromSeconds(1);
            search(uid, agent, point, renewed, renewed, false);
            Server.System<CMUExpeditionAgentSystem>().ResetOrders(uid, agent);
            Assert.That(agent.ContactSearchPoint, Is.Null, "An explicit command cancels transient search work.");
        });
    }

    [Test]
    public async Task SoundSearchApproachesWithoutInheritingAnOldMeleeStandoff()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Floor(map.Grid, map.Tile.Tile);
            var start = new EntityCoordinates(map.Grid, 7.5f, 6.5f);
            var (uid, agent) = Observer(start);
            agent.LastContactWasMelee = true;
            agent.MeleeStandoffRange = 6;
            var point = start.Offset(new Vector2(4, 0));
            var search = Method("SearchContact").CreateDelegate<Func<EntityUid, CMUExpeditionAgentComponent,
                EntityCoordinates, TimeSpan, TimeSpan, bool, bool>>(Server.System<CMUExpeditionAgentSystem>());
            Assert.That(search(uid, agent, point, SGameTiming.CurTime, SGameTiming.CurTime, true), Is.True);
            var destination = SEntMan.GetComponent<NPCSteeringComponent>(uid).Coordinates;
            Assert.That(destination.TryDistance(SEntMan, point, out var distance), Is.True);
            Assert.That(distance, Is.LessThan(4), "A new anonymous sound should produce an approach, not a stale melee hold.");
        });
    }

    [Test]
    public async Task CheckedContactReturnsToStandingOrderThroughController()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Floor(map.Grid, map.Tile.Tile);
            var start = new EntityCoordinates(map.Grid, 7.5f, 6.5f);
            var (uid, agent) = Observer(start);
            var system = Server.System<CMUExpeditionAgentSystem>();
            var order = start.Offset(new Vector2(0, 4));
            Assert.That(system.OrderPosition(uid, order, false), Is.True);
            agent.PlanningEnabled = false;
            agent.ContactInspectDuration = TimeSpan.Zero;
            agent.LastSeen = start.Offset(new Vector2(2, 0));
            agent.LastContact = SGameTiming.CurTime - TimeSpan.FromSeconds(2);
            agent.ForgetAt = SGameTiming.CurTime + TimeSpan.FromSeconds(4);
            agent.State = CMUExpeditionAgentState.Watch;
            agent.NextThink = SGameTiming.CurTime;
            system.Update(0);
            Assert.That(agent.LastSeen, Is.Null, $"A checked observation must be released: {agent.DecisionOwner}/{agent.DecisionReason}");
            Assert.That(agent.OrderedDestination, Is.EqualTo(order));
            Assert.That(SEntMan.GetComponent<NPCSteeringComponent>(uid).Coordinates, Is.EqualTo(order),
                "The existing order controller must resume native movement toward the order.");
        });
    }

    private (EntityUid Uid, CMUExpeditionAgentComponent Agent) Observer(EntityCoordinates point)
    {
        Server.System<SharedMapSystem>().SetAmbientLight(
            SEntMan.GetComponent<TransformComponent>(point.EntityId).MapID, Color.White);
        var uid = SEntMan.SpawnEntity("CMUExpeditionScavenger", point);
        var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(uid);
        agent.Home = point;
        agent.NextThink = TimeSpan.MaxValue;
        return (uid, agent);
    }

    private void Floor(EntityUid grid, Tile tile)
    {
        var tiles = new List<(Vector2i, Tile)>();
        for (var x = 0; x < 16; x++)
        for (var y = 0; y < 14; y++)
            tiles.Add((new Vector2i(x, y), tile));
        Server.System<SharedMapSystem>().SetTiles(grid, SEntMan.GetComponent<MapGridComponent>(grid), tiles);
    }

    private EntityUid Enemy(EntityCoordinates point)
    {
        var uid = SEntMan.SpawnEntity("CMMobHuman", point);
        Server.System<NpcFactionSystem>().AddFaction(uid, EnemyFaction);
        return uid;
    }

    // Invoke the real sensor/search with controlled decision timestamps. Simulation time
    // otherwise couples these tests to unrelated random combat and movement controllers.
    private Func<EntityUid, CMUExpeditionAgentComponent, TransformComponent, TimeSpan, EntityUid?> Observe() =>
        Method("Observe").CreateDelegate<Func<EntityUid, CMUExpeditionAgentComponent, TransformComponent, TimeSpan, EntityUid?>>(
            Server.System<CMUExpeditionAgentSystem>());

    private static MethodInfo Method(string name) =>
        typeof(CMUExpeditionAgentSystem).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
}
