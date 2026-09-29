#pragma warning disable RA0002 // Arrange map and faction settings for initialization tests.

using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Round;
using Content.Shared.CMU14.util;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.ForceOnForce;

[TestFixture]
public sealed class ForceOnForceLandingZoneTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true };

    [Test]
    public async Task ReviewedPoolAndShipStarts()
    {
        await Server.WaitAssertion(() =>
        {
            var preset = SProtoMan.Index<GamePresetPrototype>("ForceOnForce");
            var planets = GamePlanetPoolPrototype.ExpandPlanetIds(SProtoMan, preset.PlanetPool, preset.SupportedPlanets);
            Assert.That(planets, Is.EquivalentTo(new[]
            {
                "CMUPlanetHopesRetreat", "AUPlanetLV759", "AUPlanetTrijent", "AUPlanetBosenmoriBasho",
                "AuPlanetChances", "AUPlanetCorsatStation", "AUPlanetLV624", "AUPlanetShepherdsPride",
                "AUPlanetLV747", "CMUPlanetStableGarrisonRedux", "AUPlanetSorokyne",
            }));
            var groundBases = new[] { "CMUPlanetHopesRetreat", "AUPlanetLV759", "CMUPlanetStableGarrisonRedux" };
            foreach (var id in planets)
            {
                var proto = SProtoMan.Index<EntityPrototype>(id);
                Assert.That(proto.TryComp<RMCPlanetMapPrototypeComponent>(out var planet, SEntMan.ComponentFactory), Is.True);
                Assert.That(planet!.GovforInShip, Is.EqualTo(!groundBases.Contains(id)), id);
                Assert.That(planet.OpforInShip, Is.True, id);
            }
        });
    }

    [TestCase("ForceOnForce", "opfor")]
    [TestCase("DistressSignal", "govfor")]
    public async Task MapOverridesOnlyApplyInForceOnForce(string preset, string expected)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            typeof(GameTicker).GetProperty(nameof(GameTicker.CurrentPreset))!.SetValue(Server.System<GameTicker>(),
                SProtoMan.Index<GamePresetPrototype>(preset));
            var destination = SEntMan.SpawnEntity("CMDropshipDestinationGovforWhitelist", map.GridCoords);
            var component = SEntMan.GetComponent<DropshipDestinationComponent>(destination);
            component.ForceOnForceFaction = "opfor";
            var init = new MapInitEvent();
            SEntMan.EventBus.RaiseLocalEvent(destination, init);
            Assert.That(component.FactionController, Is.EqualTo(expected));
        });
    }

    [TestCase("govfor", true)]
    [TestCase("opfor", true)]
    [TestCase("govfor", false)]
    public async Task StartingAircraftUsesItsCarrierOrItsGroundBase(string faction, bool inShip)
    {
        var ground = await Pair.CreateTestMap();
        var carrier = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<RMCPlanetComponent>(ground.MapUid);
            SEntMan.EnsureComponent<ShipFactionComponent>(carrier.GridCoords.EntityId).Faction = faction;
            var groundPad = SEntMan.SpawnEntity("CMDropshipDestination", ground.GridCoords);
            var carrierPad = SEntMan.SpawnEntity("CMDropshipDestinationHome", carrier.GridCoords);
            var dropships = Server.System<SharedDropshipSystem>();
            dropships.SetFactionController(groundPad, faction);
            dropships.SetFactionController(carrierPad, faction);
            var planet = new RMCPlanetMapPrototypeComponent { GovforInShip = inShip, OpforInShip = inShip };
            var find = typeof(PlatoonSpawnRuleSystem).GetMethod("FindDestination", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var used = new HashSet<EntityUid>();
            object? Pick() => find.Invoke(Server.System<PlatoonSpawnRuleSystem>(),
                [faction, DropshipDestinationComponent.DestinationType.Dropship, used, new Random(1), planet]);
            var expected = inShip ? carrierPad : groundPad;
            Assert.That(Pick(), Is.EqualTo(expected));
            used.Add(expected);
            Assert.That(Pick(), Is.Null, "An occupied home pad must not silently fall back to a remote ground LZ.");
        });
    }
}
