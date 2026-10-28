using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Ambassador;
using Content.Server.CMU14.Round.ColonyShift;
using Content.Server.GameTicking;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14;
using Content.Shared.Mind;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Station;

// greenshift ending: the rotation transport really loads, lands on the colony pad, leaves, and the
// tally counts who rode it out and what cargo went with them
[TestFixture]
public sealed class CMUColonyRotationTest : GameTest
{
    private static readonly string FastRule = "CMUTestColonyRotationFast";
    private static readonly string SlowRule = "CMUTestColonyRotationSlow";

    [TestPrototypes]
    private const string Prototypes = """
    - type: entity
      parent: ColonyRotation
      id: CMUTestColonyRotationFast
      components:
      - type: ColonyRotationRule
        autoDispatchDelay: 0
        inboundTime: 3
        flightStartup: 1
        flightTime: 1
        dockTime: 4
        dockWarnings: []
        retryDelay: 1

    - type: entity
      parent: ColonyRotation
      id: CMUTestColonyRotationSlow
      components:
      - type: ColonyRotationRule
        autoDispatchDelay: 0
        inboundTime: 600
        recallExtension: 1800
        callCooldown: 0

    - type: entity
      parent: BaseItem
      id: CMUTestColonyRotationLumber
      components:
      - type: Tag
        tags:
        - AUwood
    """;

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true, Destructive = true };

    [Test]
    public async Task TransportLandsLeavesAndTalliesTheShift()
    {
        EntityUid rule = default;
        EntityUid planet = default;
        await Server.WaitAssertion(() =>
        {
            SEntMan.System<SharedMapSystem>().CreateMap(out var mapId);
            planet = SEntMan.System<SharedMapSystem>().GetMap(mapId);
            SEntMan.EnsureComponent<RMCPlanetComponent>(planet);
            SEntMan.SpawnEntity("CMDropshipDestination", new MapCoordinates(Vector2.Zero, mapId));
            // the colony's submission point is the price list for the shipment
            SEntMan.SpawnEntity("AUSubmissionPointWood", new MapCoordinates(new Vector2(30, 30), mapId));
            Assert.That(SEntMan.System<GameTicker>().StartGameRule(FastRule, out rule));
        });

        var rotation = SEntMan.GetComponent<ColonyRotationRuleComponent>(rule);
        await RunUntil(() => rotation.Status == ColonyRotationTransportStatus.Docked, 60);
        Assert.That(rotation.Status, Is.EqualTo(ColonyRotationTransportStatus.Docked), "transport never landed");

        EntityUid colonist = default;
        await Server.WaitAssertion(() =>
        {
            var ship = rotation.Shuttle!.Value;
            Assert.That(SEntMan.GetComponent<TransformComponent>(ship).MapUid, Is.EqualTo(planet));

            var aboard = new EntityCoordinates(ship, Vector2.Zero);
            colonist = SEntMan.SpawnEntity("CMMobHuman", aboard);
            var minds = SEntMan.System<SharedMindSystem>();
            var mind = minds.CreateMind(null, "Rotating Colonist");
            minds.TransferTo(mind, colonist);
            SEntMan.SpawnEntity("CMUTestColonyRotationLumber", aboard);
            SEntMan.SpawnEntity("CMUTestColonyRotationLumber", aboard);
        });

        await RunUntil(() => rotation.Status == ColonyRotationTransportStatus.None, 60);

        await Server.WaitAssertion(() =>
        {
            Assert.That(rotation.Status, Is.EqualTo(ColonyRotationTransportStatus.None), "transport never left");
            Assert.That(rotation.RotatedOut, Is.EquivalentTo(new[] { SEntMan.GetComponent<MetaDataComponent>(colonist).EntityName }));
            Assert.That(rotation.StayedBehind, Is.Zero);
            var multiplier = SEntMan.System<AmbassadorConsoleSystem>().GetSubmissionMultiplier();
            Assert.That(rotation.ShipmentValue, Is.EqualTo(40f * multiplier).Within(0.01f));
            Assert.That(rotation.ShipmentCounts.Values, Is.EquivalentTo(new[] { 2 }));
        });
    }

    [Test]
    public async Task AutoTransportCanBeRecalled()
    {
        EntityUid rule = default;
        await Server.WaitAssertion(() =>
        {
            SEntMan.System<SharedMapSystem>().CreateMap(out var mapId);
            var planet = SEntMan.System<SharedMapSystem>().GetMap(mapId);
            SEntMan.EnsureComponent<RMCPlanetComponent>(planet);
            SEntMan.SpawnEntity("CMDropshipDestination", new MapCoordinates(Vector2.Zero, mapId));
            Assert.That(SEntMan.System<GameTicker>().StartGameRule(SlowRule, out rule));
        });

        var rotation = SEntMan.GetComponent<ColonyRotationRuleComponent>(rule);
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var system = SEntMan.System<ColonyRotationRuleSystem>();
            var now = Server.ResolveDependency<IGameTiming>().CurTime;
            Assert.That(rotation.Status, Is.EqualTo(ColonyRotationTransportStatus.Inbound));
            Assert.That(rotation.Shuttle, Is.Null, "shuttle shouldn't load until it's about to fly");

            Assert.That(system.TryRecall(out _), Is.True, "the automatic transport should be recallable");
            Assert.That(rotation.Status, Is.EqualTo(ColonyRotationTransportStatus.Scheduled));
            Assert.That(rotation.AutoDispatchAt, Is.GreaterThanOrEqualTo(now + TimeSpan.FromMinutes(29)));

            Assert.That(system.TryRecall(out _), Is.False, "nothing inbound to recall");
            Assert.That(system.TryCall(out var reason), Is.False, "early call must respect the shift's minimum length");
            Assert.That(reason, Is.Not.Null);
        });

        // admins skip the minimum shift length and pick the eta
        await Server.WaitPost(() => Server.ResolveDependency<IConsoleHost>().ExecuteCommand(null, "rotationcall 5"));
        await Server.WaitAssertion(() =>
        {
            var now = Server.ResolveDependency<IGameTiming>().CurTime;
            Assert.That(rotation.Status, Is.EqualTo(ColonyRotationTransportStatus.Inbound));
            Assert.That(rotation.ArriveAt, Is.EqualTo(now + TimeSpan.FromMinutes(5)).Within(TimeSpan.FromSeconds(2)));
        });

        await Server.WaitPost(() => Server.ResolveDependency<IConsoleHost>().ExecuteCommand(null, "rotationrecall"));
        await Server.WaitAssertion(() =>
        {
            Assert.That(rotation.Status, Is.EqualTo(ColonyRotationTransportStatus.Scheduled), "admin recall should send it back");
        });

        // 0 means as fast as it can fly, which is also too late to recall
        await Server.WaitPost(() => Server.ResolveDependency<IConsoleHost>().ExecuteCommand(null, "rotationcall 0"));
        await Server.WaitAssertion(() =>
        {
            var now = Server.ResolveDependency<IGameTiming>().CurTime;
            var lead = TimeSpan.FromSeconds(rotation.FlightStartup + rotation.FlightTime);
            Assert.That(rotation.ArriveAt, Is.EqualTo(now + lead).Within(TimeSpan.FromSeconds(2)));
            Assert.That(SEntMan.System<ColonyRotationRuleSystem>().TryAdminRecall(out _), Is.False);
        });
    }

    private async Task RunUntil(Func<bool> done, float seconds)
    {
        for (var i = 0; i < seconds * 2 && !done(); i++)
            await RunSeconds(0.5f);
    }
}
