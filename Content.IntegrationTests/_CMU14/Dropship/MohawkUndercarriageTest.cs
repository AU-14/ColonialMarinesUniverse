using System.Linq;
using System.Numerics;
using Content.Server.CMU14.Dropship.MultiDeck;
using Content.Server.Shuttles.Systems;
using Content.Shared._RMC14.Dropship;
using Content.Shared.Parallax;
using Content.Shared.Shuttles.Components;
using Content.Shared.Warps;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Robust.Shared.Audio.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkUndercarriageTest
{
    [TestCase("omaha", 0)]
    [TestCase("midway", 90)]
    [TestCase("omaha_navy", 90)]
    [TestCase("midway_navy", 0)]
    public async Task FlightKeepsUndercarriageAttachedAndLeavesGroundOccupantsBehind(string variant, int degrees)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Connected = true });
        EntityUid ship = default;
        EntityUid lower = default;
        EntityUid originalGround = default;
        EntityUid destinationGround = default;
        NetEntity lowerNet = default;
        NetEntity cabinNet = default;
        NetEntity cabinWarp = default;
        var rampEdges = new Dictionary<NetEntity, Vector2>();
        var originalTiles = new Dictionary<NetEntity, Dictionary<Vector2i, string>>();
        var fixedParts = new Dictionary<NetEntity, Vector2>();
        var peopleBelow = new Dictionary<NetEntity, Vector2>();
        var flightMaps = new List<NetEntity>();
        NetEntity originalGroundNet = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}.yml"), out var loaded), Is.True);
            ship = loaded!.Value.Owner;
            cabinNet = entities.GetNetEntity(ship);
            var warp = entities.EntityQuery<WarpPointComponent>()
                .Single(w => entities.GetComponent<TransformComponent>(w.Owner).ParentUid == ship);
            cabinWarp = entities.GetNetEntity(warp.Owner);
            var expectedName = variant.StartsWith("omaha") ? "Omaha" : "Midway";
            if (variant.EndsWith("_navy"))
                expectedName += " (Navy)";
            Assert.That(entities.GetComponent<MetaDataComponent>(warp.Owner).EntityName, Is.EqualTo(expectedName + " cabin"));
            var assembly = entities.GetComponent<MultiDeckDropshipComponent>(ship);
            lower = assembly.Decks[-1];
            var tileDefinitions = pair.Server.ResolveDependency<ITileDefinitionManager>();
            foreach (var deck in assembly.Decks.Values.Append(ship))
            {
                originalTiles[entities.GetNetEntity(deck)] = maps.GetAllTiles(deck, entities.GetComponent<MapGridComponent>(deck))
                    .ToDictionary(t => t.GridIndices, t => tileDefinitions[t.Tile.TypeId].ID);
            }
            foreach (var edge in entities.EntityQuery<MohawkRampEdgingComponent>())
                rampEdges.Add(entities.GetNetEntity(edge.Owner), entities.GetComponent<TransformComponent>(edge.Owner).LocalPosition);
            Assert.That(rampEdges, Has.Count.EqualTo(10));
            Assert.That(entities.System<MohawkSystem>().SetRampDeployed(ship, true, true), Is.True);
            foreach (var net in rampEdges.Keys)
                Assert.That(entities.GetComponent<TransformComponent>(entities.GetEntity(net)).ParentUid, Is.EqualTo(ship),
                    "Opening the ramp must not detach its edging when cabin floor tiles disappear.");
            lowerNet = entities.GetNetEntity(lower);
            originalGround = entities.GetComponent<TransformComponent>(lower).MapUid!.Value;
            originalGroundNet = entities.GetNetEntity(originalGround);
            var transform = entities.System<SharedTransformSystem>();
            transform.SetWorldRotation(ship, Angle.FromDegrees(degrees));
            Assert.That(entities.System<MultiDeckDropshipSystem>().Synchronize((ship, assembly)), Is.True);

            // Fuel-line artwork has its origin over an empty cell. It must be
            // attached even before moving, without adding an invisible floor.
            var belly = entities.EntityQuery<MetaDataComponent>().Single(m => m.EntityName == "underside fuel lines");
            Assert.That(entities.GetComponent<TransformComponent>(belly.Owner).ParentUid, Is.EqualTo(lower));
            var children = entities.GetComponent<TransformComponent>(lower).ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                var xform = entities.GetComponent<TransformComponent>(child);
                fixedParts.Add(entities.GetNetEntity(child), xform.LocalPosition);
            }
            Assert.That(fixedParts.ContainsKey(entities.GetNetEntity(belly.Owner)), Is.True);

            var terrain = entities.EnsureComponent<MapGridComponent>(originalGround);
            var tile = maps.GetAllTiles(lower, entities.GetComponent<MapGridComponent>(lower)).First().Tile;
            for (var x = -10; x <= 10; x++)
            for (var y = -10; y <= 10; y++)
                maps.SetTile(originalGround, terrain, new Vector2i(x, y), tile);

            // Deliberately reproduce clipping/parenting onto undercarriage
            // tiles: the servicing mount and the external ramp button.
            foreach (var position in new[] { new Vector2(-2.5f, 1.5f), new Vector2(0.5f, -6.5f) })
            {
                var coordinates = new EntityCoordinates(lower, position);
                var person = entities.SpawnEntity("CMMobHuman", coordinates);
                transform.SetCoordinates((person, entities.GetComponent<TransformComponent>(person), entities.GetComponent<MetaDataComponent>(person)), coordinates);
                Assert.That(entities.GetComponent<TransformComponent>(person).ParentUid, Is.EqualTo(lower));
                peopleBelow.Add(entities.GetNetEntity(person), transform.GetWorldPosition(person));
            }

            destinationGround = maps.CreateMap();
            var marker = entities.SpawnEntity(null, new EntityCoordinates(destinationGround, 22, -18));
            entities.AddComponent<DropshipDestinationComponent>(marker);
            transform.SetWorldRotation(marker, Angle.FromDegrees(180));
            entities.System<ShuttleSystem>().DefaultArrivalTime = 0.5f;
            var nav = entities.EntityQuery<DropshipNavigationComputerComponent>()
                .First(c => entities.GetComponent<TransformComponent>(c.Owner).GridUid == ship);
            Assert.That(entities.System<SharedDropshipSystem>().FlyTo((nav.Owner, nav), marker, null,
                startupTime: 0.5f, hyperspaceTime: 10f), Is.True);
            var startup = entities.GetComponent<FTLComponent>(ship).StartupStream;
            Assert.That(startup, Is.Not.Null);
            Assert.That(entities.GetComponent<AudioComponent>(startup!.Value).FileName,
                Is.EqualTo("/Audio/_RMC14/Machines/Shuttle/engine_startup.ogg"));
        });
        await pair.RunSeconds(1);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var decks = entities.GetComponent<MultiDeckDropshipComponent>(ship).Decks.Values.Append(ship);
            foreach (var deck in decks)
            {
                var map = entities.GetComponent<TransformComponent>(deck).MapUid!.Value;
                Assert.That(entities.GetComponent<ParallaxComponent>(map).Parallax, Is.EqualTo("FastSpace"),
                    "Every deck needs the moving space background during flight.");
                flightMaps.Add(entities.GetNetEntity(map));
            }
        });
        await pair.RunUntilSynced();
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            foreach (var map in flightMaps)
                Assert.That(entities.GetComponent<ParallaxComponent>(entities.GetEntity(map)).Parallax, Is.EqualTo("FastSpace"));
        });
        await pair.RunSeconds(12);
        await pair.RunUntilSynced();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var transform = entities.System<SharedTransformSystem>();
            Assert.That(entities.GetComponent<TransformComponent>(lower).MapUid, Is.EqualTo(destinationGround));
            Assert.That(entities.HasComponent<ParallaxComponent>(destinationGround), Is.False,
                "Landing must not overwrite a destination's background with the flight effect.");
            Assert.That(entities.EntityQuery<AudioComponent>().Any(a =>
                a.FileName == "/Audio/_RMC14/Machines/Shuttle/engine_landing.ogg"), Is.True);
            Assert.That(entities.GetComponent<TransformComponent>(entities.GetEntity(cabinWarp)).ParentUid, Is.EqualTo(ship));
            foreach (var (net, position) in rampEdges)
            {
                var edge = entities.GetComponent<TransformComponent>(entities.GetEntity(net));
                Assert.That(edge.ParentUid, Is.EqualTo(ship), "Ramp edging must travel with the cabin.");
                Assert.That(edge.LocalPosition, Is.EqualTo(position));
            }
            foreach (var (net, position) in fixedParts)
            {
                var part = entities.GetEntity(net);
                var xform = entities.GetComponent<TransformComponent>(part);
                Assert.That(xform.ParentUid, Is.EqualTo(lower), $"Undercarriage part {entities.GetComponent<MetaDataComponent>(part).EntityPrototype?.ID} detached.");
                Assert.That(Vector2.Distance(xform.LocalPosition, position), Is.LessThan(0.001f));
            }
            foreach (var (net, position) in peopleBelow)
            {
                var person = entities.GetEntity(net);
                Assert.That(entities.GetComponent<TransformComponent>(person).MapUid, Is.EqualTo(originalGround));
                Assert.That(Vector2.Distance(transform.GetWorldPosition(person), position), Is.LessThan(0.05f),
                    "Standing beneath the ship must not take a passenger into hyperspace.");
            }
        });
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            var clientLower = entities.GetEntity(lowerNet);
            var clientCabin = entities.GetEntity(cabinNet);
            foreach (var (net, position) in rampEdges)
            {
                var edge = entities.GetComponent<TransformComponent>(entities.GetEntity(net));
                Assert.That(edge.ParentUid, Is.EqualTo(clientCabin), "The connected client must move the ramp edging.");
                Assert.That(edge.LocalPosition, Is.EqualTo(position));
            }
            var maps = entities.System<SharedMapSystem>();
            var tileDefinitions = pair.Client.ResolveDependency<ITileDefinitionManager>();
            foreach (var (net, expected) in originalTiles)
            {
                var deck = entities.GetEntity(net);
                var actual = maps.GetAllTiles(deck, entities.GetComponent<MapGridComponent>(deck))
                    .ToDictionary(t => t.GridIndices, t => tileDefinitions[t.Tile.TypeId].ID);
                Assert.That(actual, Is.EquivalentTo(expected), "All three decks must retain their tile artwork through ramp use and flight.");
            }
            var warp = entities.GetComponent<TransformComponent>(entities.GetEntity(cabinWarp));
            Assert.That(entities.GetComponent<MetaDataComponent>(warp.ParentUid).EntityName,
                Does.StartWith(variant.StartsWith("omaha") ? "Omaha" : "Midway"));
            foreach (var (net, position) in fixedParts)
            {
                var xform = entities.GetComponent<TransformComponent>(entities.GetEntity(net));
                Assert.That(xform.ParentUid, Is.EqualTo(clientLower), "The connected client must move every underside part with the ship.");
                Assert.That(Vector2.Distance(xform.LocalPosition, position), Is.LessThan(0.001f));
            }
            foreach (var net in peopleBelow.Keys)
                Assert.That(entities.GetComponent<TransformComponent>(entities.GetEntity(net)).MapUid,
                    Is.EqualTo(entities.GetEntity(originalGroundNet)));
        });
        await pair.Server.WaitAssertion(() => pair.Server.EntMan.DeleteEntity(ship));
        await pair.CleanReturnAsync();
    }
}
