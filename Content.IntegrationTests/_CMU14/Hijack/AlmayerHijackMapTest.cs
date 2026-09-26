using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.VendorMarker;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.util;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Doors.Systems;
using Robust.Shared.Prototypes;
using Content.IntegrationTests.Pair;
using Content.Server.CMU14.Hijack;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Station.Systems;
using Content.Server.Shuttles.Components;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Hijack;
using Content.Shared._RMC14.Power;
using Content.Shared._RMC14.Camera;
using Content.Shared._RMC14.Chemistry.SmartFridge;
using Robust.Shared.Containers;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.CMU14.Hijack;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Verbs;
using Content.Shared._RMC14.Doors;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared._RMC14.Teleporter;
using Content.Shared._RMC14.Ladder;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Content.Shared.Maps;
using Content.Shared.Atmos.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Hijack;

[TestFixture]
[NonParallelizable]
public sealed partial class AlmayerHijackMapTest
{
    [Test]
    public async Task UpperCicPreservesNativeRoomAroundItsLadders()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var server = pair.Server;
            var entities = server.EntMan;
            var map = LoadAlmayer(pair);
            var upper = server.System<CMUZLevelsSystem>().GetAllNetworkMaps(map)
                .Single(uid => entities.GetComponent<CMUZLevelMapComponent>(uid).Depth == 2);
            var room = new List<(EntityUid Uid, string? Prototype, Vector2 Position)>();
            var query = entities.AllEntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var metadata, out var transform))
                if (transform.MapUid == upper && transform.ParentUid == transform.GridUid)
                    room.Add((uid, metadata.EntityPrototype?.ID, transform.LocalPosition));
            (string Prototype, Vector2 Position)[] expected =
            [
                ("CMCarpetPrison", new(-89.5f, 1.5f)),
                ("CMCarpetPrison", new(-89.5f, -2.5f)),
                ("RMCLightFixture", new(-64.5f, -3.5f)),
                ("RMCStairs", new(-68.5f, -5.5f)),
                ("CMWallReinforcedAlmayer", new(-64.5f, 3.5f)),
                ("CMWallReinforcedAlmayer", new(-59.5f, 3.5f)),
                ("CMWallReinforcedAlmayer", new(-59.5f, -4.5f)),
            ];
            foreach (var (prototype, position) in expected)
                Assert.That(room.Count(e => e.Prototype == prototype && e.Position == position), Is.EqualTo(1),
                    $"Missing or duplicated native CIC object: {prototype} at {position}");
            foreach (var position in new[] { new Vector2(-89.5f, 1.5f), new Vector2(-89.5f, -2.5f) })
                Assert.That(room.Any(e => e.Position == position && entities.HasComponent<DoorComponent>(e.Uid)),
                    Is.False, $"An imported conference door is standing in the room at {position}");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryGovforPlatoonSpawnsItsConfiguredVendorsOnAlmayer()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var server = pair.Server;
            var entities = server.EntMan;
            var map = LoadAlmayer(pair);
            var network = server.System<CMUZLevelsSystem>().GetAllNetworkMaps(map).ToHashSet();
            var rule = server.System<PlatoonSpawnRuleSystem>();
            var supply = entities.GetComponent<CMUAlmayerSupplyComponent>(map);
            var system = server.System<CMUAlmayerSupplySystem>();
            var markers = new List<(EntityUid Uid, VendorMarkerComponent Marker, TransformComponent Transform)>();
            var markerQuery = entities.AllEntityQueryEnumerator<VendorMarkerComponent, TransformComponent>();
            while (markerQuery.MoveNext(out var uid, out var marker, out var transform))
                if (marker.Ship && transform.MapUid is { } deck && network.Contains(deck))
                    markers.Add((uid, marker, transform));
            Assert.That(markers.Count, Is.EqualTo(257));

            var spawned = new List<EntityUid>();
            string[] platoons = ["USCM", "LACN", "UPP", "WEYU", "CMBCIU", "HAZOPS", "ProdigySF", "VAIPO", "RMC"];
            foreach (var id in platoons)
            {
                foreach (var uid in spawned)
                    entities.DeleteEntity(uid);
                spawned.Clear();
                foreach (var (_, marker, _) in markers)
                    marker.Spawned = false;

                var platoon = server.ProtoMan.Index<PlatoonPrototype>(id);
                rule.SelectedGovforPlatoon = platoon;
                var vendors = server.ProtoMan.Index(platoon.VendorSet!.Value).Vendors;
                system.InitializeSupplies(map, supply);
                system.InitializeSupplies(map, supply);
                var actual = new List<(EntityUid Uid, string? Prototype, TransformComponent Transform)>();
                var query = entities.AllEntityQueryEnumerator<CMAutomatedVendorComponent, MetaDataComponent, TransformComponent>();
                while (query.MoveNext(out var uid, out _, out var metadata, out var transform))
                    if (transform.MapUid is { } deck && network.Contains(deck))
                        actual.Add((uid, metadata.EntityPrototype?.ID, transform));
                foreach (var (_, marker, transform) in markers)
                {
                    var expected = platoon.VendorOverrides.GetValueOrDefault(marker.Class,
                        platoon.VendorMarkersByClass.GetValueOrDefault(marker.Class, vendors[marker.Class]));
                    var found = actual.Where(v => v.Transform.Coordinates == transform.Coordinates).ToArray();
                    Assert.That(found, Has.Length.EqualTo(1), $"{id}: {marker.Class} at {transform.Coordinates}");
                    Assert.That(found[0].Prototype, Is.EqualTo(expected.Id), $"{id}: {marker.Class}");
                    Assert.That(found[0].Transform.LocalRotation, Is.EqualTo(transform.LocalRotation));
                    Assert.That(marker.Spawned, Is.True);
                    spawned.Add(found[0].Uid);
                }
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AlmayerIsAvailableToGovforPlatoonsInShipSelection()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var prototypes = pair.Server.ProtoMan;
            string[] platoons = ["USCM", "LACN", "UPP", "WEYU", "CMBCIU", "HAZOPS", "ProdigySF", "VAIPO", "RMC"];
            foreach (var id in platoons)
            {
                var platoon = prototypes.Index<PlatoonPrototype>(id);
                Assert.That(platoon.GovforShips, Is.EquivalentTo(new[] { "USSBushRedux", "Almayer" }), id);
                Assert.That(platoon.PossibleShips, Does.Not.Contain("Almayer"), $"Opfor ship list changed for {id}");
                foreach (var ship in platoon.GovforShips!)
                    Assert.That(prototypes.HasIndex<GameMapPrototype>(ship), Is.True, $"Unknown ship {ship} for {id}");
            }

            var map = prototypes.Index<GameMapPrototype>("Almayer");
            Assert.That(map.MapsAbove.Count + map.MapsBelow.Count + 1, Is.EqualTo(5));
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(null, "AU14USCMclothingequipmentvendor", "AU14USCMWeaponsVendor")]
    [TestCase("UPP", "AU14UPPclothingapparelvendor", "AU14UPPWeaponsVendor")]
    public async Task AlmayerGovforVendorsFollowPlatoonAcrossDecks(string? platoon, string clothing, string weapons)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var server = pair.Server;
            var entities = server.EntMan;
            var map = LoadAlmayer(pair);
            var network = server.System<CMUZLevelsSystem>().GetAllNetworkMaps(map).ToHashSet();
            var rule = server.System<PlatoonSpawnRuleSystem>();
            rule.SelectedGovforPlatoon = platoon == null ? null : server.ProtoMan.Index<PlatoonPrototype>(platoon);
            var markers = new List<(EntityUid Uid, PlatoonMarkerClass Class, TransformComponent Transform)>();
            var query = entities.AllEntityQueryEnumerator<VendorMarkerComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var marker, out var transform))
                if (transform.MapUid is { } owner && network.Contains(owner) && marker.Ship)
                    markers.Add((uid, marker.Class, transform));
            Assert.That(markers.Count, Is.GreaterThan(200));
            Assert.That(markers.Select(m => m.Transform.MapUid).Distinct().Count(), Is.GreaterThanOrEqualTo(2));

            var supply = entities.GetComponent<CMUAlmayerSupplyComponent>(map);
            var system = server.System<CMUAlmayerSupplySystem>();
            // Ship setup can run while the map is still paused by round loading.
            server.System<SharedMapSystem>().SetPaused(map, true);
            system.InitializeSupplies(map, supply);
            // Normal platoon-rule initialization followed by standalone fallback
            // must not materialize a second machine at any of the markers.
            system.InitializeSupplies(map, supply);
            server.System<SharedMapSystem>().SetPaused(map, false);
            var vendors = new List<(EntityUid Uid, string? Prototype, TransformComponent Transform)>();
            var vendorQuery = entities.AllEntityQueryEnumerator<CMAutomatedVendorComponent, TransformComponent>();
            while (vendorQuery.MoveNext(out var uid, out _, out var transform))
                if (transform.MapUid is { } owner && network.Contains(owner))
                    vendors.Add((uid, entities.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID, transform));
            foreach (var marker in markers)
            {
                Assert.That(entities.GetComponent<VendorMarkerComponent>(marker.Uid).Spawned, Is.True);
                var found = vendors.Where(v => v.Transform.Coordinates == marker.Transform.Coordinates).ToArray();
                Assert.That(found, Has.Length.EqualTo(1), $"Missing or duplicated {marker.Class} at {marker.Transform.Coordinates}");
                Assert.That(found[0].Transform.LocalRotation, Is.EqualTo(marker.Transform.LocalRotation));
                if (marker.Class == PlatoonMarkerClass.Clothing)
                    Assert.That(found[0].Prototype, Is.EqualTo(clothing));
                if (marker.Class == PlatoonMarkerClass.Weapons)
                    Assert.That(found[0].Prototype, Is.EqualTo(weapons));
                if (marker.Class == PlatoonMarkerClass.ReqVend)
                {
                    var reader = entities.GetComponent<AccessReaderComponent>(found[0].Uid);
                    Assert.That(reader.AccessLists.SelectMany(g => g), Is.EquivalentTo(new[] { new ProtoId<AccessLevelPrototype>("AU14AccessGovforReq") }));
                }
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AlmayerDoorsEnforceGovforRoleAccessAndKeepLegacyCrewUsable()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var server = pair.Server;
            var entities = server.EntMan;
            var map = LoadAlmayer(pair);
            var network = server.System<CMUZLevelsSystem>().GetAllNetworkMaps(map).ToHashSet();
            var access = server.System<AccessReaderSystem>();
            var cards = server.System<SharedAccessSystem>();
            var doors = server.System<SharedDoorSystem>();
            var actor = entities.SpawnEntity(null, new EntityCoordinates(map, Vector2.Zero));
            entities.AddComponent<AccessComponent>(actor);
            var tagged = new List<(EntityUid Uid, AccessReaderComponent Reader)>();
            var query = entities.AllEntityQueryEnumerator<DoorComponent, AccessReaderComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out _, out var reader, out var transform))
            {
                if (transform.MapUid is not { } owner || !network.Contains(owner) || reader.AccessLists.Count == 0)
                    continue;
                Assert.That(reader.AccessLists.SelectMany(g => g).All(t =>
                    t.Id.StartsWith("AU14AccessGovfor") || t.Id == "AU14AccessCorporateWEYU"), Is.True, $"Old door access on {uid}");
                tagged.Add((uid, reader));
                cards.TrySetTags(actor, new[] { new ProtoId<AccessLevelPrototype>("AU14AccessOpforCommand") });
                Assert.That(access.IsAllowed(actor, uid), Is.False, $"Opfor opened Govfor door {uid}");
                cards.TrySetTags(actor, reader.AccessLists[0]);
                Assert.That(access.IsAllowed(actor, uid), Is.True, $"Matching access cannot open door {uid}");
            }
            Assert.That(tagged.Count, Is.GreaterThan(250));
            foreach (var department in new[] { "Medical", "Engineering", "Command", "Security", "Req" })
            {
                var tag = new ProtoId<AccessLevelPrototype>("AU14AccessGovfor" + department);
                var door = tagged.First(d => d.Reader.AccessLists.Count == 1 &&
                    d.Reader.AccessLists[0].Count == 1 && d.Reader.AccessLists[0].First() == tag);
                cards.TrySetTags(actor, new[] { new ProtoId<AccessLevelPrototype>("AU14AccessGovfor"), new ProtoId<AccessLevelPrototype>("AU14AccessGovforSquad") });
                Assert.That(doors.HasAccess(door.Uid, actor), Is.False, $"Rifleman can enter {department}");
                cards.TrySetTags(actor, new[] { tag });
                Assert.That(doors.HasAccess(door.Uid, actor), Is.True);
            }

            var supply = entities.GetComponent<CMUAlmayerSupplyComponent>(map);
            cards.TrySetTags(actor, new ProtoId<AccessLevelPrototype>[] { "RMCMarineDefaultAccess", "CMAccessMarinePrep", "CMAccessMedPrep", "CMAccessMedical" });
            server.System<CMUAlmayerSupplySystem>().AdaptLegacyCard(actor, supply);
            var tags = entities.GetComponent<AccessComponent>(actor).Tags;
            Assert.That(tags.Contains("AU14AccessGovforMedical") && tags.Contains("AU14AccessGovforSquad") && tags.Contains("AU14AccessGovfor"), Is.True);
            Assert.That(tags.Contains("AU14AccessGovforCommand") || tags.Contains("AU14AccessGovforEngineering"), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StairShutterBanksKeepAllSegmentsAndWorkingControls()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var banks = new List<(EntityUid[] Doors, EntityUid[] Buttons)>();
        EntityUid actor = default;
        await pair.Server.WaitAssertion(() =>
        {
            var server = pair.Server;
            var entities = server.EntMan;
            var map = LoadAlmayer(pair);
            var transforms = server.System<SharedTransformSystem>();
            actor = entities.SpawnEntity(null, new Robust.Shared.Map.EntityCoordinates(map, Vector2.Zero));
            foreach (var (id, rows) in new[]
                     { ("laddersouthwest", new[] { -18.5f, -14.5f }), ("laddernorthwest", new[] { 13.5f, 17.5f }) })
            foreach (var row in rows)
            {
                var link = $"CMUReference:{id}:{(int) System.MathF.Floor(row)}";
                var doors = new List<EntityUid>();
                var query = entities.AllEntityQueryEnumerator<RMCPodDoorComponent, TransformComponent>();
                while (query.MoveNext(out var door, out var pod, out var transform))
                    if (transform.MapUid == map && pod.Id == link)
                        doors.Add(door);
                Assert.That(doors.Select(transforms.GetWorldPosition), Is.EquivalentTo(
                    from x in new[] { -18.5f, -17.5f, -16.5f } select new Vector2(x, row)),
                    $"{id}: both three-tile entrances must survive the stair import.");
                foreach (var x in new[] { -19.5f, -15.5f })
                foreach (var y in new[] { row })
                    Assert.That(server.System<EntityLookupSystem>().GetEntitiesIntersecting(
                        entities.GetComponent<MapComponent>(map).MapId,
                        Box2.CenteredAround(new Vector2(x, y), new Vector2(0.3f)))
                        .Any(uid => !entities.HasComponent<MapGridComponent>(uid) &&
                                    entities.TryGetComponent<PhysicsComponent>(uid, out var body) && body.CanCollide &&
                                    (body.CollisionLayer & (int) CollisionGroup.MobMask) != 0),
                        Is.True, $"{id}: missing wall jamb at {x}, {y}.");
                var buttons = new List<EntityUid>();
                var controls = entities.AllEntityQueryEnumerator<RMCDoorButtonComponent, TransformComponent>();
                while (controls.MoveNext(out var button, out var control, out var transform))
                    if (transform.MapUid == map && control.Id == link)
                        buttons.Add(button);
                Assert.That(buttons, Has.Count.EqualTo(2), $"{link}: each entrance needs its own pair of controls.");
                banks.Add((doors.ToArray(), buttons.ToArray()));
            }
            // The kitchen control shares a boundary tile with the south bank.
            var kitchen = entities.AllEntityQueryEnumerator<RMCDoorButtonComponent, TransformComponent>();
            var retained = false;
            while (kitchen.MoveNext(out _, out var button, out var transform))
                retained |= transform.MapUid == map && button.Id == "Kitchen" &&
                            transform.LocalPosition == new Vector2(-15.25f, -17.5f);
            Assert.That(retained, Is.True, "Replacing stair shutters must preserve the neighbouring kitchen control.");
        });
        await pair.Server.WaitRunTicks(60);
        foreach (var selected in banks)
        foreach (var (buttonIndex, expectedState) in new[] { (0, DoorState.Open), (1, DoorState.Closed) })
        {
            await pair.Server.WaitAssertion(() =>
            {
                {
                    var button = selected.Buttons[buttonIndex];
                    var activate = new ActivateInWorldEvent(actor, button, true);
                    pair.Server.EntMan.EventBus.RaiseLocalEvent(button, activate, true);
                }
            });
            await pair.Server.WaitRunTicks(90);
            await pair.Server.WaitAssertion(() =>
            {
                foreach (var door in banks.SelectMany(b => b.Doors))
                {
                    var state = selected.Doors.Contains(door) ? expectedState : DoorState.Closed;
                    Assert.That(pair.Server.EntMan.GetComponent<DoorComponent>(door).State, Is.EqualTo(state));
                    Assert.That(pair.Server.EntMan.GetComponent<PhysicsComponent>(door).CanCollide,
                        Is.EqualTo(state == DoorState.Closed));
                }
            });
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MedicalStairShaftHasItsOriginalNorthWall()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var server = pair.Server;
            var map = LoadAlmayer(pair);
            var upper = server.System<CMUZLevelsSystem>().GetAllNetworkMaps(map)
                .Single(uid => server.EntMan.GetComponent<CMUZLevelMapComponent>(uid).Depth == 2);
            foreach (var x in new[] { -15.5f, -14.5f, -13.5f, -12.5f })
                Assert.That(server.System<EntityLookupSystem>().GetEntitiesIntersecting(
                    server.EntMan.GetComponent<MapComponent>(upper).MapId,
                    Box2.CenteredAround(new Vector2(x, -20.5f), new Vector2(0.3f)))
                    .Any(uid => !server.EntMan.HasComponent<MapGridComponent>(uid) &&
                                server.EntMan.TryGetComponent<PhysicsComponent>(uid, out var body) &&
                                body.CanCollide && (body.CollisionLayer & (int) CollisionGroup.MobMask) != 0),
                    Is.True, $"The medical stair shaft needs its north cap and corners at {x}, -20.5.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HijackBurstsAuthoredPipesAndContinuesBarrage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        EntityUid map = default;
        EntityUid[] warned = [];
        var witnesses = new List<EntityUid>();
        var positions = new List<Robust.Shared.Map.MapCoordinates>();
        await pair.Server.WaitAssertion(() =>
        {
            var server = pair.Server;
            var entities = server.EntMan;
            map = LoadAlmayer(pair);
            entities.GetComponent<CMUShipHijackComponent>(map).CheckMarinePresence = false;
            var ev = new DropshipHijackLandedEvent(map);
            entities.EventBus.RaiseEvent(EventSource.Local, ref ev);
            var active = entities.GetComponent<RMCHijackActiveMapComponent>(map);
            warned = active.Explode.ToArray();
            Assert.That(warned, Has.Length.EqualTo(5));
            Assert.That(active.ExplodeAt, Is.Not.Null);
            var transforms = server.System<SharedTransformSystem>();
            foreach (var pipe in warned)
            {
                var position = transforms.GetMapCoordinates(pipe);
                positions.Add(position);
                // A real damageable object at the epicentre verifies that the
                // warning is followed by an actual processed explosion.
                var witness = entities.SpawnEntity("RMCCrateBase", position);
                entities.RemoveComponent<Content.Server.Destructible.DestructibleComponent>(witness);
                witnesses.Add(witness);
            }
            Assert.That(CountPrototype("RMCHijackPipeExplosionWarning"), Is.GreaterThanOrEqualTo(5));
            active.ExplodeAt = System.TimeSpan.Zero;
            server.System<Content.Server._RMC14.Hijack.RMCHijackRandomDamageSystem>().Update(0);
            Assert.That(warned.All(entities.Deleted), Is.True);
            Assert.That(active.Explode, Is.Empty);
            Assert.That(CountPrototype("RMCHijackPipeFire"), Is.GreaterThan(0));
            foreach (var position in positions)
                Assert.That(server.System<EntityLookupSystem>().GetEntitiesIntersecting(position.MapId,
                    Box2.CenteredAround(position.Position, new Vector2(0.3f)))
                    .Any(uid => entities.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "GasPipeBroken"),
                    Is.True, $"Burst pipe at {position} must leave a broken pipe.");
        });
        await pair.Server.WaitRunTicks(120);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(witnesses.Any(uid => pair.Server.System<Content.Shared.Damage.Systems.DamageableSystem>()
                .GetPositiveDamage((uid, entities.GetComponent<Content.Shared.Damage.Components.DamageableComponent>(uid)), "Brute").GetTotal() > 0),
                Is.True, "The queued pipe explosion must damage nearby objects.");
            var active = entities.GetComponent<RMCHijackActiveMapComponent>(map);
            active.Next = System.TimeSpan.Zero;
            pair.Server.System<Content.Server._RMC14.Hijack.RMCHijackRandomDamageSystem>().Update(0);
            Assert.That(active.Explode, Has.Count.EqualTo(5), "Barrage must continue after the first wave.");
            Assert.That(active.Explode.Intersect(warned), Is.Empty);
        });
        await pair.CleanReturnAsync();

        int CountPrototype(string prototype)
        {
            var count = 0;
            var query = pair.Server.EntMan.AllEntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out _, out var meta))
                if (meta.EntityPrototype?.ID == prototype)
                    count++;
            return count;
        }
    }

    [Test]
    public async Task LandingBoundariesPreserveAdjacentDoorsWallsAndHallwayLighting()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        EntityUid lower = default;
        await pair.Server.WaitAssertion(() => lower = LoadAlmayer(pair));
        await pair.Server.WaitRunTicks(120);
        await pair.Server.WaitAssertion(() =>
        {
            var server = pair.Server;
            var entities = server.EntMan;
            var upper = server.System<CMUZLevelsSystem>().GetAllNetworkMaps(lower)
                .Single(uid => entities.GetComponent<CMUZLevelMapComponent>(uid).Depth == 2);
            using (Assert.EnterMultipleScope())
            {
                foreach (var (map, position) in new[]
                {
                    (lower, new Vector2(-32.5f, -15.5f)), (lower, new Vector2(-31.5f, -15.5f)),
                    (upper, new Vector2(-30.5f, -7.5f)),
                })
                    Assert.That(EntitiesAt(map, position).Any(entities.HasComponent<CMDoubleDoorComponent>), Is.True,
                        $"Missing door leaf at {position}.");
                foreach (var position in new[]
                {
                    new Vector2(-29.5f, 6.5f), new Vector2(-30.5f, 6.5f),
                    new Vector2(-1.5f, 7.5f), new Vector2(-31.5f, -9.5f),
                    new Vector2(-30.5f, -9.5f), new Vector2(-29.5f, -9.5f),
                    new Vector2(-27.5f, 7.5f), new Vector2(-27.5f, 8.5f),
                })
                    Assert.That(At(upper, position).Any(uid => !entities.HasComponent<MapGridComponent>(uid) &&
                        entities.TryGetComponent<PhysicsComponent>(uid, out var body) && body.CanCollide &&
                        (body.CollisionLayer & (int) CollisionGroup.MobMask) != 0), Is.True,
                        $"Missing boundary wall at {position}.");
                foreach (var position in new[]
                {
                    new Vector2(-26.5f, -11.5f), new Vector2(-19.5f, -11.5f),
                    new Vector2(-11.5f, -11.5f), new Vector2(-8.5f, -13.5f), new Vector2(-3.5f, -11.5f),
                })
                {
                    var lights = EntitiesAt(lower, position).Where(entities.HasComponent<Content.Shared.Light.Components.PoweredLightComponent>).ToArray();
                    Assert.That(lights, Has.Length.EqualTo(1), $"Missing light at {position}.");
                    foreach (var uid in lights)
                        Assert.That(entities.GetComponent<Content.Shared.Light.Components.PoweredLightComponent>(uid).CurrentLit,
                            Is.True, $"Hallway light at {position} has lost its power area/APC.");
                }
            }
            IEnumerable<EntityUid> EntitiesAt(EntityUid map, Vector2 position)
            {
                var found = new List<EntityUid>();
                var query = entities.AllEntityQueryEnumerator<TransformComponent>();
                while (query.MoveNext(out var uid, out var transform))
                    if (transform.MapUid == map && server.System<SharedTransformSystem>().GetWorldPosition(uid) == position)
                        found.Add(uid);
                return found;
            }
            IEnumerable<EntityUid> At(EntityUid map, Vector2 position) =>
                server.System<EntityLookupSystem>().GetEntitiesIntersecting(
                    entities.GetComponent<MapComponent>(map).MapId, Box2.CenteredAround(position, new Vector2(0.3f)));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ImportedDisposalPipesHideUnderFloorAndRevealWhenUncovered()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        EntityUid sample = default;
        Entity<MapGridComponent> hull = default;
        Vector2i indices = default;
        Tile original = default;
        await pair.Server.WaitAssertion(() =>
        {
            var server = pair.Server;
            var entities = server.EntMan;
            LoadAlmayer(pair);
            var maps = server.System<SharedMapSystem>();
            var definitions = server.ResolveDependency<ITileDefinitionManager>();
            var covered = 0;
            var query = entities.AllEntityQueryEnumerator<Content.Shared.SubFloor.SubFloorHideComponent, TransformComponent, MetaDataComponent>();
            while (query.MoveNext(out var uid, out var hide, out var transform, out var meta))
            {
                if (meta.EntityPrototype?.ID.StartsWith("CMUAlmayerObject") != true || transform.GridUid is not { } grid)
                    continue;
                var gridComp = entities.GetComponent<MapGridComponent>(grid);
                var cell = maps.TileIndicesFor(grid, gridComp, transform.Coordinates);
                var tile = maps.GetTileRef(grid, gridComp, cell).Tile;
                var hasCover = !((ContentTileDefinition) definitions[tile.TypeId]).IsSubFloor;
                Assert.That(hide.IsUnderCover, Is.EqualTo(hasCover));
                if (!hasCover)
                    continue;
                covered++;
                sample = uid;
                hull = (grid, gridComp);
                indices = cell;
                original = tile;
            }
            Assert.That(covered, Is.GreaterThan(0), "Finished imported floors must cover disposal pipes.");
            maps.SetTile(hull, hull.Comp, indices, new Tile(definitions["CMFloorPlating"].TileId));
        });
        await pair.Server.WaitRunTicks(1);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(pair.Server.EntMan.GetComponent<Content.Shared.SubFloor.SubFloorHideComponent>(sample).IsUnderCover, Is.False);
            pair.Server.System<SharedMapSystem>().SetTile(hull, hull.Comp, indices, original);
        });
        await pair.Server.WaitRunTicks(1);
        await pair.Server.WaitAssertion(() =>
            Assert.That(pair.Server.EntMan.GetComponent<Content.Shared.SubFloor.SubFloorHideComponent>(sample).IsUnderCover, Is.True));
        await pair.CleanReturnAsync();
    }

    private static EntityUid LoadAlmayer(TestPair pair)
    {
        var server = pair.Server;
        var maps = server.System<SharedMapSystem>();
        var prototype = server.ProtoMan.Index<GameMapPrototype>("Almayer");
        server.System<GameTicker>().LoadGameMap(prototype, out var mapId,
            DeserializationOptions.Default with { InitializeMaps = false });
        maps.InitializeMap(mapId);
        var map = maps.GetMap(mapId);
        var network = server.System<CMUZLevelsSystem>().GetAllNetworkMaps(map).ToArray();
        Assert.That(network, Has.Length.EqualTo(5));
        Assert.That(network.Select(m => server.EntMan.GetComponent<CMUZLevelMapComponent>(m).Depth),
            Is.EquivalentTo(new[] { -2, -1, 0, 1, 2 }));
        Assert.That(network.Select(m => server.System<CMUZLevelsSystem>().GetZLevelVisualOffset(m)),
            Is.All.Zero, "The original ship's floors and walls must align across every deck.");
        return map;
    }

    [Test]
    public async Task AllStairsAndLaddersArriveOnAccessibleDeckCells()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var server = pair.Server;
            var entities = server.EntMan;
            var map = LoadAlmayer(pair);
            var z = server.System<CMUZLevelsSystem>();
            var transforms = server.System<SharedTransformSystem>();
            var stairsSystem = server.System<AlmayerStairsSystem>();
            var shipMaps = z.GetAllNetworkMaps(map).ToHashSet();
            var actor = entities.SpawnEntity(null, new Robust.Shared.Map.MapCoordinates(Vector2.Zero,
                entities.GetComponent<MapComponent>(map).MapId));
            entities.AddComponent<MobStateComponent>(actor);
            entities.AddComponent<DoAfterComponent>(actor);
            // Both flights share each original eastern compartment on Middle.
            // Their Upper exits align with it, away from the Lower dorms.
            var mappedStairs = new List<(int Depth, Vector2 Position)>();
            var mappedQuery = entities.AllEntityQueryEnumerator<CMUAlmayerStairsComponent, TransformComponent>();
            while (mappedQuery.MoveNext(out var stairUid, out _, out var stairTransform))
                if (stairTransform.MapUid is { } stairMap && shipMaps.Contains(stairMap))
                    mappedStairs.Add((entities.GetComponent<CMUZLevelMapComponent>(stairMap).Depth,
                        transforms.GetWorldPosition(stairUid)));
            foreach (var x in new[] { 32.5f, 33.5f, 34.5f })
            {
                Assert.That(mappedStairs, Does.Contain((1, new Vector2(x, 33.5f))));
                Assert.That(mappedStairs, Does.Contain((2, new Vector2(x, 32.5f))));
                Assert.That(mappedStairs, Does.Contain((1, new Vector2(x, -34.5f))));
                Assert.That(mappedStairs, Does.Contain((2, new Vector2(x, -33.5f))));
            }
            foreach (var x in new[] { 28.5f, 29.5f, 30.5f })
            {
                Assert.That(mappedStairs, Does.Contain((0, new Vector2(x, 37.5f))));
                Assert.That(mappedStairs, Does.Contain((1, new Vector2(x, 38.5f))));
                Assert.That(mappedStairs, Does.Contain((0, new Vector2(x, -38.5f))));
                Assert.That(mappedStairs, Does.Contain((1, new Vector2(x, -39.5f))));
            }
            Assert.That(mappedStairs.Any(p => p.Position.X > 8 && p.Position.X < 17 &&
                System.Math.Abs(p.Position.Y) > 25), Is.False, "No duplicate western stair bay may remain.");
            var upper = shipMaps.Single(uid => entities.GetComponent<CMUZLevelMapComponent>(uid).Depth == 2);
            var upperMapId = entities.GetComponent<MapComponent>(upper).MapId;
            var retiredLookup = server.System<EntityLookupSystem>();
            // Reaching the stair itself is insufficient: all three approach
            // lanes must stay open through the seam with the supplied hall.
            foreach (var x in new[] { 32.5f, 33.5f, 34.5f })
            foreach (var y in new[] { 29.5f, 30.5f, 31.5f, -30.5f, -31.5f, -32.5f })
            {
                var point = new Vector2(x, y);
                Assert.That(retiredLookup.GetEntitiesIntersecting(upperMapId, Box2.CenteredAround(point, new Vector2(0.1f)))
                    .Any(uid => !entities.HasComponent<MapGridComponent>(uid) &&
                        entities.TryGetComponent<PhysicsComponent>(uid, out var body) && body.CanCollide &&
                        (body.CollisionLayer & (int) CollisionGroup.MobMask) != 0), Is.False,
                    $"A retained wall narrows the Upper stair approach at {point}.");
            }
            // The full pod-side passage must align with the reference, not
            // merely offer a wide opening at its stair end.
            foreach (var point in new[]
            {
                new Vector2(30.5f, 28.5f), new Vector2(31.5f, 29.5f), new Vector2(31.5f, 24.5f),
                new Vector2(30.5f, -29.5f), new Vector2(31.5f, -30.5f), new Vector2(31.5f, -25.5f),
            })
                Assert.That(retiredLookup.GetEntitiesIntersecting(upperMapId, Box2.CenteredAround(point, new Vector2(0.1f)))
                    .Count(entities.HasComponent<AirlockComponent>), Is.EqualTo(1),
                    $"Missing or misplaced pod passage door at {point}.");
            foreach (var y in new[] { 24.5f, 25.5f, 26.5f, 27.5f, -25.5f, -26.5f, -27.5f, -28.5f })
            {
                var point = new Vector2(30.5f, y);
                Assert.That(IsSolidAt(point), Is.False, $"The pod-side corridor is obstructed at {point}.");
            }
            foreach (var y in new[] { 23.5f, 25.5f, 26.5f, 27.5f, 28.5f, 30.5f,
                         -24.5f, -26.5f, -27.5f, -28.5f, -29.5f, -31.5f })
                Assert.That(IsSolidAt(new Vector2(31.5f, y)), Is.True, $"Missing pod passage wall at y={y}.");
            foreach (var y in Enumerable.Range(24, 7).Concat(Enumerable.Range(-32, 7)))
                Assert.That(IsSolidAt(new Vector2(32.5f, y + 0.5f)), Is.False,
                    $"Old offset passage wall or door remains at (32.5, {y + 0.5f}).");

            bool IsSolidAt(Vector2 point) => retiredLookup.GetEntitiesIntersecting(upperMapId,
                    Box2.CenteredAround(point, new Vector2(0.1f)))
                .Any(uid => !entities.HasComponent<MapGridComponent>(uid) &&
                    entities.TryGetComponent<PhysicsComponent>(uid, out var body) && body.CanCollide &&
                    (body.CollisionLayer & (int) CollisionGroup.MobMask) != 0);

            foreach (var (bottom, top) in new[] { (28, 39), (-41, -30) })
            for (var x = 8; x <= 16; x++)
            for (var y = bottom; y <= top; y++)
            {
                var point = new Vector2(x + 0.5f, y + 0.5f);
                Assert.That(retiredLookup.GetEntitiesIntersecting(upperMapId, Box2.CenteredAround(point, new Vector2(0.1f)))
                    .Any(uid => !entities.HasComponent<MapGridComponent>(uid) &&
                        entities.TryGetComponent<PhysicsComponent>(uid, out var body) && body.CanCollide &&
                        (body.CollisionLayer & (int) CollisionGroup.MobMask) != 0), Is.True,
                    $"Retired western Upper bay still has an empty cavity at {point}.");
            }
            var dormBeds = new List<Vector2>();
            var bedQuery = entities.AllEntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (bedQuery.MoveNext(out var bedUid, out var meta, out var bedTransform))
                if (meta.EntityPrototype?.ID == "CMBed" && bedTransform.MapUid == map)
                    dormBeds.Add(transforms.GetWorldPosition(bedUid));
            foreach (var x in new[] { 9.5f, 10.5f, 12.5f, 13.5f })
            foreach (var y in new[] { 27.5f, 29.5f, -28.5f, -30.5f })
                Assert.That(dormBeds, Does.Contain(new Vector2(x, y)), "Preserve the supplied dorm furniture.");
            foreach (var deck in shipMaps.Where(uid => entities.GetComponent<CMUZLevelMapComponent>(uid).Depth >= 0))
            foreach (var y in new[] { -8.5f, 7.5f })
            {
                var medical = new List<Vector2>();
                var medicalQuery = entities.AllEntityQueryEnumerator<CMUZLevelLadderComponent, TransformComponent>();
                while (medicalQuery.MoveNext(out var ladderUid, out _, out var ladderTransform))
                    if (ladderTransform.MapUid == deck)
                    {
                        var at = transforms.GetWorldPosition(ladderUid);
                        if (at.Y == y && at.X > -32 && at.X < -27)
                            medical.Add(at);
                    }
                var west = entities.GetComponent<CMUZLevelMapComponent>(deck).Depth <= 1 ? -30.5f : -29.5f;
                Assert.That(medical, Is.EquivalentTo(new[] { new Vector2(west, y), new Vector2(west + 1, y) }));
            }
            var checkedEdges = 0;
            var query = entities.AllEntityQueryEnumerator<CMUAlmayerStairsComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var stairs, out var transform))
            {
                if (transform.MapUid is not { } from || !shipMaps.Contains(from))
                    continue;
                transforms.SetCoordinates(actor, transform.Coordinates);
                var start = transforms.GetWorldPosition(uid);
                var expectedPosition = start + stairs.Direction;
                Assert.That(stairsSystem.Traverse(actor, (uid, stairs)), Is.True,
                    $"Blocked authored stair at {transform.LocalPosition}, depth {entities.GetComponent<CMUZLevelMapComponent>(from).Depth}");
                var to = entities.GetComponent<TransformComponent>(actor).MapUid!.Value;
                Assert.That(entities.GetComponent<CMUZLevelMapComponent>(to).Depth,
                    Is.EqualTo(entities.GetComponent<CMUZLevelMapComponent>(from).Depth + stairs.Offset));
                Assert.That(transforms.GetWorldPosition(actor), Is.EqualTo(expectedPosition));
                checkedEdges++;
            }
            Assert.That(checkedEdges, Is.EqualTo(72), "36 original paired stair tiles, both directions.");
            var throughLadders = 0;
            var ladderEdges = 0;
            var lookup = server.System<EntityLookupSystem>();
            var areas = server.System<AreaSystem>();
            var maps = server.System<SharedMapSystem>();
            var hulls = new Dictionary<EntityUid, EntityUid>();
            var hullQuery = entities.AllEntityQueryEnumerator<CMUShipHullComponent, TransformComponent>();
            while (hullQuery.MoveNext(out var hull, out _, out var hullTransform))
                if (hullTransform.MapUid is { } deck && shipMaps.Contains(deck))
                    hulls[deck] = hull;
            var ladderQuery = entities.AllEntityQueryEnumerator<CMUZLevelLadderComponent, TransformComponent>();
            using (Assert.EnterMultipleScope())
            while (ladderQuery.MoveNext(out var ladderUid, out var ladder, out var transform))
            {
                if (transform.MapUid is not { } from || !shipMaps.Contains(from))
                    continue;
                foreach (var offset in ladder.AdditionalOffset is { } extra ? new[] { ladder.Offset, extra } : new[] { ladder.Offset })
                {
                    var position = transforms.GetWorldPosition(ladderUid);
                    var landing = position + (offset == ladder.Offset ? ladder.LandingOffset : ladder.AdditionalLandingOffset);
                    var route = $"Ladder {entities.GetComponent<MetaDataComponent>(ladderUid).EntityPrototype?.ID} at {position}, depth {entities.GetComponent<CMUZLevelMapComponent>(from).Depth}, offset {offset}";
                    Assert.That(z.TryProjectToZMap((from, null), offset, landing, out var destination, out _), Is.True);
                    var destinationMap = maps.GetMap(destination.MapId);
                    Assert.That(InRoom(landing), Is.True, $"{route}: exit outside an authored room.");
                    Assert.That(CanStandAt(landing), Is.True, $"{route}: exit obstructed.");
                    Assert.That(new[] { Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY }
                        .Any(direction => CanStandAt(landing + direction)), Is.True, $"{route}: no walkable exit from the ladder.");

                    bool CanStandAt(Vector2 point)
                    {
                        if (!InRoom(point))
                            return false;
                        return !lookup.GetEntitiesIntersecting(destination.MapId, Box2.CenteredAround(point, new Vector2(0.6f)))
                            .Any(uid => uid != actor && !entities.HasComponent<MapGridComponent>(uid) &&
                                entities.TryGetComponent<PhysicsComponent>(uid, out var body) && body.CanCollide &&
                                (body.CollisionLayer & (int) CollisionGroup.MobMask) != 0);
                    }

                    bool InRoom(Vector2 point)
                    {
                        var coordinates = transforms.ToCoordinates(hulls[destinationMap],
                            new Robust.Shared.Map.MapCoordinates(point, destination.MapId));
                        return areas.TryGetArea(coordinates, out _, out _);
                    }

                    var reverse = entities.AllEntityQueryEnumerator<CMUZLevelLadderComponent, TransformComponent>();
                    var found = false;
                    while (reverse.MoveNext(out var otherUid, out var other, out var otherTransform))
                        if (otherTransform.MapUid == destinationMap && transforms.GetWorldPosition(otherUid) == landing &&
                            (other.Offset == -offset || other.AdditionalOffset == -offset) &&
                            landing + (other.Offset == -offset ? other.LandingOffset : other.AdditionalLandingOffset) == position)
                            found = true;
                    Assert.That(found, Is.True, $"Ladder at {position}, deck {entities.GetComponent<CMUZLevelMapComponent>(from).Depth} has no matching endpoint.");

                    transforms.SetCoordinates(actor, transform.Coordinates);
                    var delay = ladder.Delay;
                    ladder.Delay = System.TimeSpan.Zero;
                    if (offset == ladder.Offset)
                    {
                        var activate = new ActivateInWorldEvent(actor, ladderUid, true);
                        entities.EventBus.RaiseLocalEvent(ladderUid, activate, true);
                    }
                    else
                    {
                        var verbs = new GetVerbsEvent<AlternativeVerb>(actor, ladderUid, null, null, true, true, true, []);
                        entities.EventBus.RaiseLocalEvent(ladderUid, verbs, true);
                        var key = offset > 0 ? "cmu-zlevel-ladder-climb-up" : "cmu-zlevel-ladder-climb-down";
                        verbs.Verbs.Single(v => v.Text == Robust.Shared.Localization.Loc.GetString(key)).Act!();
                    }
                    ladder.Delay = delay;
                    Assert.That(entities.GetComponent<TransformComponent>(actor).MapUid, Is.EqualTo(destinationMap), route);
                    Assert.That(transforms.GetWorldPosition(actor), Is.EqualTo(landing), route);
                    ladderEdges++;
                }
                if (ladder.AdditionalOffset == null)
                    continue;
                Assert.That(entities.GetComponent<CMUZLevelMapComponent>(from).Depth, Is.EqualTo(1));
                Assert.That(new[] { ladder.Offset, ladder.AdditionalOffset.Value }, Is.EquivalentTo(new[] { 1, -1 }));
                throughLadders++;
            }
            Assert.That(throughLadders, Is.GreaterThan(0), "Middle ladders must allow both ascent and descent.");
            Assert.That(ladderEdges, Is.EqualTo(100), "92 original directions plus all eight custom Under Deck hatch directions.");
            entities.DeleteEntity(actor);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EvacuationGridsRemainAlignedWithTheirBays()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var poses = new Dictionary<EntityUid, (Vector2 Position, Angle Rotation)>();
        await pair.Server.WaitAssertion(() =>
        {
            var server = pair.Server;
            var entities = server.EntMan;
            var map = LoadAlmayer(pair);
            var shipMaps = server.System<CMUZLevelsSystem>().GetAllNetworkMaps(map).ToHashSet();
            var hulls = new Dictionary<EntityUid, EntityUid>();
            var hullQuery = entities.AllEntityQueryEnumerator<CMUShipHullComponent, TransformComponent>();
            while (hullQuery.MoveNext(out var uid, out _, out var transform))
                if (transform.MapUid is { } deck && shipMaps.Contains(deck))
                    hulls.Add(deck, uid);
            Assert.That(hulls, Has.Count.EqualTo(5));
            var maps = server.System<SharedMapSystem>();
            var station = server.System<StationSystem>().GetOwningStation(hulls[map]);
            Assert.That(station, Is.Not.Null);
            foreach (var hull in hulls.Values)
            {
                Assert.That(entities.GetComponent<ShuttleComponent>(hull).Enabled, Is.False);
                Assert.That(server.System<StationSystem>().GetOwningStation(hull), Is.EqualTo(station));
                Assert.That(entities.HasComponent<CMUZLevelDeckComponent>(hull), Is.True);
            }
            var bottom = hulls.Single(h => entities.GetComponent<CMUZLevelMapComponent>(h.Key).Depth == -2).Value;
            var under = hulls.Single(h => entities.GetComponent<CMUZLevelMapComponent>(h.Key).Depth == -1).Value;
            Assert.That(entities.GetComponent<MapGridComponent>(bottom).LocalAABB.Left, Is.LessThan(-100));
            Assert.That(entities.GetComponent<MapGridComponent>(under).LocalAABB.Left, Is.GreaterThanOrEqualTo(30),
                "The original technical block must be a separate deck, without the CM-SS13 bottom hull.");
            var query = entities.AllEntityQueryEnumerator<MapGridComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var grid, out var xform))
            {
                if (xform.MapUid is not { } deck || !shipMaps.Contains(deck) || entities.HasComponent<MapComponent>(uid))
                    continue;
                poses[uid] = (xform.LocalPosition, xform.LocalRotation);
                Assert.That(entities.GetComponent<PhysicsComponent>(uid).BodyType, Is.EqualTo(BodyType.Static));
                Assert.That(xform.LocalRotation, Is.EqualTo(Angle.Zero));
                Assert.That(entities.GetComponent<GridAtmosphereComponent>(uid).Tiles.Values.Any(t => t.Air?.TotalMoles > 0),
                    Is.True, $"Grid {uid} must contain authored air.");
                var hull = hulls[deck];
                if (uid == hull)
                    continue;

                Assert.That(xform.LocalPosition.X % 1, Is.Zero);
                Assert.That(xform.LocalPosition.Y % 1, Is.Zero);
                var offset = new Vector2i((int) xform.LocalPosition.X, (int) xform.LocalPosition.Y);
                var tiles = maps.GetAllTiles(uid, grid);
                while (tiles.MoveNext(out var tile))
                    Assert.That(maps.GetTileRef(hull, entities.GetComponent<MapGridComponent>(hull), tile.Value.GridIndices + offset).Tile.IsEmpty, Is.True,
                        $"Evacuation grid {uid} overlaps hull tile {tile.Value.GridIndices + offset}.");
            }
            Assert.That(poses, Has.Count.EqualTo(26), "Five hull decks, 18 supplied pods, the original Middle CL pod and two lifeboats must load.");
        });
        await pair.Server.WaitRunTicks(120);
        await pair.Server.WaitAssertion(() =>
        {
            foreach (var (uid, pose) in poses)
            {
                var xform = pair.Server.EntMan.GetComponent<TransformComponent>(uid);
                Assert.That(xform.LocalPosition, Is.EqualTo(pose.Position), $"Grid {uid} drifted in its bay.");
                Assert.That(xform.LocalRotation, Is.EqualTo(pose.Rotation), $"Grid {uid} rotated in its bay.");
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SuppliedAlmayerLoadsAndRegistersAllFuelPumps()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var server = pair.Server;
            var entities = server.EntMan;
            var map = LoadAlmayer(pair);
            var state = entities.GetComponent<CMUShipHijackComponent>(map);
            var shipMaps = server.System<CMUZLevelsSystem>().GetAllNetworkMaps(map).ToHashSet();
            var query = entities.AllEntityQueryEnumerator<TransformComponent>();
            while (query.MoveNext(out var uid, out var transform))
            {
                if (transform.MapUid is not { } deck || !shipMaps.Contains(deck))
                    continue;
                Assert.That(entities.HasComponent<RMCTeleporterComponent>(uid), Is.False, "Old XY teleports must be removed.");
                Assert.That(entities.HasComponent<LadderComponent>(uid), Is.False, "Old linked ladders must be replaced.");
                if (entities.HasComponent<CMUShipHullComponent>(uid))
                    Assert.That(entities.GetComponent<GridAtmosphereComponent>(uid).Tiles, Is.Not.Empty);
            }
            var ev = new DropshipHijackLandedEvent(map);
            entities.EventBus.RaiseEvent(EventSource.Local, ref ev);
            Assert.Multiple(() =>
            {
                Assert.That(state.Stage, Is.EqualTo(CMUShipHijackStage.Sublight));
                Assert.That(state.Pumps, Has.Count.EqualTo(4), "Each original pump must belong to a distinct objective area.");
                Assert.That(state.ShipGrids, Has.Count.EqualTo(5));
                Assert.That(state.ShipMaps, Is.EquivalentTo(shipMaps));
            });
            foreach (var (area, pump) in state.Pumps)
            {
                Assert.That(entities.GetComponent<AreaComponent>(area).HijackEvacuationArea, Is.True);
                Assert.That(pump, Is.Not.Null);
                Assert.That(entities.HasComponent<CMUHijackPumpComponent>(pump!.Value), Is.True);
                Assert.That(server.System<ShipHijackSystem>().IsFuelAreaOperational((map, state), area), Is.True);
            }
            var reactors = entities.AllEntityQueryEnumerator<RMCFusionReactorComponent, TransformComponent>();
            var count = 0;
            while (reactors.MoveNext(out _, out _, out var transform))
                if (transform.MapUid is { } deck && shipMaps.Contains(deck))
                    count++;
            Assert.That(count, Is.EqualTo(16));
            Assert.That(entities.GetComponent<EvacuationProgressComponent>(map).SelfDestructAt, Is.Null);
            var pipes = entities.GetComponent<RMCHijackActiveMapComponent>(map);
            Assert.That(pipes.Explode, Has.Count.EqualTo(5), "The authored pipes must participate in the crash barrage.");
            Assert.That(pipes.Pipes, Is.Not.Empty);

            var cameras = entities.AllEntityQueryEnumerator<RMCCameraComponent, TransformComponent, MetaDataComponent>();
            var importedCameras = 0;
            while (cameras.MoveNext(out _, out _, out var cameraTransform, out var cameraMeta))
                if (cameraTransform.MapUid is { } deck && shipMaps.Contains(deck) && cameraMeta.EntityPrototype?.ID.StartsWith("CMUAlmayerObject") == true)
                    importedCameras++;
            // Authored imported cameras: Lower 4, Bottom 18, Middle 30, Upper 8.
            Assert.That(importedCameras, Is.EqualTo(60), "Mapped cameras, including the relocated stair rooms, must function.");

            var storageSystem = server.System<SharedEntityStorageSystem>();
            var storages = entities.AllEntityQueryEnumerator<EntityStorageComponent, TransformComponent, MetaDataComponent>();
            var storedItems = 0;
            EntityUid? example = null;
            while (storages.MoveNext(out var uid, out var storage, out var storageTransform, out var storageMeta))
            {
                if (storageTransform.MapUid is not { } deck || !shipMaps.Contains(deck) || storageMeta.EntityPrototype?.ID.StartsWith("CMUAlmayerObject") != true)
                    continue;
                storedItems += storage.Contents.ContainedEntities.Count;
                if (storage.Contents.ContainedEntities.Count > 1)
                    example = uid;
                foreach (var item in storage.Contents.ContainedEntities)
                    Assert.That(entities.GetComponent<TransformComponent>(item).ParentUid, Is.EqualTo(uid));
            }
            Assert.That(storedItems, Is.EqualTo(33), "Items authored on closed closets/crates must load inside those containers.");
            Assert.That(example, Is.Not.Null);
            var exampleStorage = entities.GetComponent<EntityStorageComponent>(example!.Value);
            var contents = exampleStorage.Contents.ContainedEntities.ToArray();
            storageSystem.OpenStorage(example.Value);
            Assert.That(exampleStorage.Contents.ContainedEntities, Is.Empty);
            storageSystem.CloseStorage(example.Value);
            Assert.That(exampleStorage.Contents.ContainedEntities, Is.EquivalentTo(contents), "Opening and closing must release and recover the actual items.");

            var containers = server.System<SharedContainerSystem>();
            var fridges = entities.AllEntityQueryEnumerator<RMCSmartFridgeComponent, TransformComponent>();
            var storedBoxes = 0;
            while (fridges.MoveNext(out var fridge, out _, out var fridgeTransform))
            {
                if (fridgeTransform.MapUid is not { } deck || !shipMaps.Contains(deck) ||
                    !containers.TryGetContainer(fridge, "rmc_smart_fridge", out var stock))
                    continue;
                foreach (var box in stock.ContainedEntities.ToArray())
                {
                    Assert.That(entities.HasComponent<RMCSmartFridgeInsertableComponent>(box), Is.True);
                    Assert.That(entities.GetComponent<TransformComponent>(box).ParentUid, Is.EqualTo(fridge));
                    Assert.That(containers.TryGetContainer(box, "storagebase", out var cubes), Is.True);
                    Assert.That(cubes!.ContainedEntities, Has.Count.EqualTo(8), "The mapped box must contain usable monkey cubes.");
                    Assert.That(containers.Remove(box, stock), Is.True);
                    Assert.That(containers.Insert(box, stock), Is.True, "Dispensed stock must be returnable.");
                    storedBoxes++;
                }
            }
            Assert.That(storedBoxes, Is.EqualTo(1), "Research's monkey cube box belongs inside the chemical fridge.");
        });
        await pair.CleanReturnAsync();
    }
}
