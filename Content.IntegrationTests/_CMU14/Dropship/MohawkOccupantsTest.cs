using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Dropship.MultiDeck;
using Content.Server.CMU14.Dropship.MultiDeck;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server._RMC14.Shuttles;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Physics;
using Content.Shared.Stunnable;
using Robust.Client.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkOccupantsTest
{
    [TestCase("omaha")]
    [TestCase("midway")]
    public async Task SeatsKeepPassengersOutOfWallsAndReplicateVisualOffsets(string variant)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Connected = true });
        EntityUid ship = default;
        var passengers = new Dictionary<NetEntity, NetEntity>();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            ship = LoadShip(entities, variant);
            var transform = entities.System<SharedTransformSystem>();
            var seats = entities.EntityQuery<MohawkSeatComponent>().ToArray();
            Assert.That(seats, Has.Length.EqualTo(variant == "omaha" ? 64 : 19));
            foreach (var seat in seats)
            {
                var xform = entities.GetComponent<TransformComponent>(seat.Owner);
                Assert.That(xform.Anchored, Is.True);
                var rider = entities.SpawnEntity("CMMobHuman", xform.Coordinates);
                Assert.That(entities.System<SharedBuckleSystem>().TryBuckle(rider, null, seat.Owner, popup: false), Is.True);
                Assert.That(transform.GetWorldPosition(rider), Is.EqualTo(transform.GetWorldPosition(seat.Owner)),
                    "Source pixel offsets must never move the passenger's physical body toward a wall.");
                Assert.That(WallFixturesAt(entities, rider), Is.Empty, $"Seat at {xform.LocalPosition} buckles into a wall.");
                passengers.Add(entities.GetNetEntity(rider), entities.GetNetEntity(seat.Owner));
            }
        });

        foreach (var degrees in new[] { 0, 90 })
        {
            await pair.Server.WaitAssertion(() =>
                pair.Server.EntMan.System<SharedTransformSystem>().SetWorldRotation(ship, Angle.FromDegrees(degrees)));
            await pair.RunUntilSynced();
            await pair.Client.WaitAssertion(() =>
            {
                var entities = pair.Client.EntMan;
                entities.System<AppearanceSystem>().FrameUpdate(0f);
                entities.System<MohawkSeatSystem>().FrameUpdate(0f);
                var transform = entities.System<SharedTransformSystem>();
                foreach (var (riderNet, seatNet) in passengers)
                {
                    var rider = entities.GetEntity(riderNet);
                    var seat = entities.GetEntity(seatNet);
                    var sprite = entities.GetComponent<SpriteComponent>(rider);
                    var seatSprite = entities.GetComponent<SpriteComponent>(seat);
                    var visual = entities.GetComponent<MohawkSeatComponent>(seat);
                    Assert.That(entities.GetComponent<TransformComponent>(rider).ParentUid, Is.EqualTo(seat));
                    Assert.That(sprite.Offset.X, Is.EqualTo(transform.GetWorldRotation(seat).RotateVec(visual.VisualOffset).X).Within(0.001f));
                    Assert.That(sprite.Offset.Y, Is.EqualTo(transform.GetWorldRotation(seat).RotateVec(visual.VisualOffset).Y).Within(0.001f));
                    Assert.That(seatSprite.NoRotation, Is.False, "Seat art and passenger offsets must rotate with the cabin.");
                    var state = seatSprite[MohawkVisuals.Layer].RsiState.ToString();
                    Assert.That(state, Is.AnyOf("passenger_chair_buckled", "command_chair"));
                }
            });
        }
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            foreach (var riderNet in passengers.Keys)
            {
                var rider = entities.GetEntity(riderNet);
                var before = entities.GetComponent<TransformComponent>(rider).Coordinates;
                var seat = entities.GetEntity(passengers[riderNet]);
                entities.System<SharedBuckleSystem>().Unbuckle(rider, null);
                Assert.That(WallFixturesAt(entities, rider).Select(f => $"{entities.GetComponent<MetaDataComponent>(f.Entity).EntityPrototype?.ID} at {entities.GetComponent<TransformComponent>(f.Entity).Coordinates}"),
                    Is.Empty, $"Unbuckling seat {entities.GetComponent<TransformComponent>(seat).Coordinates} from {before} to {entities.GetComponent<TransformComponent>(rider).Coordinates} must leave a walkable position.");
            }
        });
        await pair.RunUntilSynced();
        await pair.Client.WaitAssertion(() =>
        {
            foreach (var riderNet in passengers.Keys)
                Assert.That(pair.Client.EntMan.GetComponent<SpriteComponent>(pair.Client.EntMan.GetEntity(riderNet)).Offset,
                    Is.EqualTo(Vector2.Zero), "Unbuckling restores the original passenger artwork offset.");
        });
        await pair.Server.WaitAssertion(() => pair.Server.EntMan.DeleteEntity(ship));
        await pair.CleanReturnAsync();
    }

    [TestCase("omaha", 0, false)]
    [TestCase("omaha", 90, true)]
    [TestCase("midway", 0, true)]
    [TestCase("midway", 90, false)]
    public async Task RetractionCarriesRampOccupantsFromBothShipAndLandingPad(string variant, int degrees, bool takeoff)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        EntityUid ship = default;
        EntityUid lowerMap = default;
        var bystanders = new List<(EntityUid Uid, Vector2 Position)>();
        var riders = new Dictionary<EntityUid, Vector2>();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            ship = LoadShip(entities, variant);
            var assembly = entities.GetComponent<MultiDeckDropshipComponent>(ship);
            var lower = assembly.Decks[-1];
            var transform = entities.System<SharedTransformSystem>();
            transform.SetWorldRotation(ship, Angle.FromDegrees(degrees));
            entities.System<MultiDeckDropshipSystem>().Synchronize((ship, assembly));
            lowerMap = entities.GetComponent<TransformComponent>(lower).MapUid!.Value;
            var maps = entities.System<SharedMapSystem>();
            var terrain = entities.EnsureComponent<MapGridComponent>(lowerMap);
            var tile = maps.GetAllTiles(lower, entities.GetComponent<MapGridComponent>(lower)).First().Tile;
            for (var x = -8; x <= 8; x++)
            for (var y = -8; y <= 8; y++)
                maps.SetTile(lowerMap, terrain, new Vector2i(x, y), tile);
            var mechanisms = entities.System<MohawkSystem>();
            mechanisms.SetRampDeployed(ship, true, true);
            mechanisms.SetHatchDeployed(ship, true, true);
            for (var row = 1; row < 4; row++)
            for (var column = 0; column < 2; column++)
            {
                var coordinates = new EntityCoordinates(lower, new Vector2(column == 0 ? -0.5f : 1.85f, row - 6.5f));
                if (column == 0)
                    coordinates = new EntityCoordinates(lowerMap, transform.ToMapCoordinates(coordinates).Position);
                var rider = entities.SpawnEntity("CMMobHuman", coordinates);
                var physics = entities.GetComponent<CMUZPhysicsComponent>(rider);
                entities.System<CMUZLevelsSystem>().SetZLocalPosition((rider, physics), row == 3 ? 0.8125f : 0f);
                // Map initialization chooses an overlapping grid automatically.
                // Exercise both legitimate movement parents explicitly.
                transform.SetCoordinates((rider, entities.GetComponent<TransformComponent>(rider), entities.GetComponent<MetaDataComponent>(rider)), coordinates);
                Assert.That(entities.GetComponent<TransformComponent>(rider).ParentUid, Is.EqualTo(column == 0 ? lowerMap : lower));
                riders.Add(rider, new Vector2(column == 0 ? -0.5f : 1.85f, row - 5.5f));
            }
            foreach (var position in new[] { new Vector2(2.6f, -4.5f), new Vector2(0.5f, -6.5f), new Vector2(0.5f, -6.15f) })
            {
                var outside = transform.ToMapCoordinates(new EntityCoordinates(lower, position));
                bystanders.Add((entities.SpawnEntity("CMMobHuman", new EntityCoordinates(lowerMap, outside.Position)), outside.Position));
            }
            if (takeoff)
            {
                // Departure must interrupt an in-progress mechanism as well.
                mechanisms.SetRampDeployed(ship, false);
                var before = new BeforeFTLStartedEvent();
                entities.EventBus.RaiseLocalEvent(ship, ref before);
                Assert.That(entities.GetComponent<MohawkMechanismsComponent>(ship).HatchDeployed, Is.False);
                Assert.That(entities.HasComponent<MohawkRampMovingComponent>(ship), Is.False);
            }
            else
                Assert.That(mechanisms.SetRampDeployed(ship, false), Is.True);
        });
        await pair.RunSeconds(6);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.GetComponent<MohawkMechanismsComponent>(ship).RampDeployed, Is.False);
            foreach (var (rider, expected) in riders)
            {
                Assert.That(entities.GetComponent<TransformComponent>(rider).GridUid, Is.EqualTo(ship), "Ramp riders must reach the cabin.");
                Assert.That(Vector2.Distance(entities.GetComponent<TransformComponent>(rider).LocalPosition, expected), Is.LessThan(0.05f),
                    "Retraction carries passengers one tile forward onto the original raised floor.");
                Assert.That(entities.GetComponent<CMUZPhysicsComponent>(rider).LocalPosition, Is.EqualTo(0f).Within(0.01f));
                Assert.That(entities.System<DamageableSystem>().GetTotalDamage(rider).Float(), Is.EqualTo(0f));
            }
            foreach (var (bystander, position) in bystanders)
            {
                Assert.That(entities.GetComponent<TransformComponent>(bystander).MapUid, Is.EqualTo(lowerMap));
                Assert.That(Vector2.Distance(entities.System<SharedTransformSystem>().GetWorldPosition(bystander), position), Is.LessThan(0.05f),
                    "Someone beyond the visible ramp edge must not be picked up, even when their body overlaps it.");
            }
            // Repeated forced closure must not pick up people underneath a closed ramp.
            entities.System<MohawkSystem>().SetRampDeployed(ship, false, true);
            foreach (var (bystander, _) in bystanders)
                Assert.That(entities.GetComponent<TransformComponent>(bystander).MapUid, Is.EqualTo(lowerMap));
            entities.DeleteEntity(ship);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("omaha")]
    [TestCase("midway")]
    public async Task PassengersOnTheClosedRampDescendOntoItsLoweredSurface(string variant)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        EntityUid ship = default;
        EntityUid lowerMap = default;
        var riders = new List<(EntityUid Uid, float Height, Vector2 Position)>();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            ship = LoadShip(entities, variant);
            var lower = entities.GetComponent<MultiDeckDropshipComponent>(ship).Decks[-1];
            lowerMap = entities.GetComponent<TransformComponent>(lower).MapUid!.Value;
            foreach (var row in new[] { 0, 3 })
            {
                var rider = entities.SpawnEntity("CMMobHuman", new EntityCoordinates(ship, 0.5f, row - 5.5f));
                riders.Add((rider, row == 3 ? 0.8125f : 0f, new Vector2(0.5f, row - 6.5f)));
            }
            entities.System<MohawkSystem>().SetRampDeployed(ship, true);
        });
        await pair.RunSeconds(7);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            foreach (var (rider, height, position) in riders)
            {
                Assert.That(entities.GetComponent<TransformComponent>(rider).MapUid, Is.EqualTo(lowerMap));
                Assert.That(Vector2.Distance(entities.GetComponent<TransformComponent>(rider).LocalPosition, position), Is.LessThan(0.05f),
                    "The passenger follows the entire ramp one tile aft, without moving the raised floor.");
                Assert.That(entities.GetComponent<CMUZPhysicsComponent>(rider).LocalPosition, Is.EqualTo(height).Within(0.05f));
                Assert.That(entities.System<DamageableSystem>().GetTotalDamage(rider).Float(), Is.EqualTo(0f),
                    "Standing on a lowering ramp must not inflict the crush damage intended for people underneath.");
            }
            entities.DeleteEntity(ship);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("omaha")]
    [TestCase("midway")]
    public async Task LoweringHitsSomeoneUnderTheRampOnce(string variant)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var ship = LoadShip(entities, variant);
            var lower = entities.GetComponent<MultiDeckDropshipComponent>(ship).Decks[-1];
            var rider = entities.SpawnEntity("CMMobHuman", new EntityCoordinates(lower, 0.5f, -5.5f));
            entities.System<MohawkSystem>().SetRampDeployed(ship, true);
            var damage = entities.System<DamageableSystem>().GetAllDamage(rider);
            Assert.That(damage.DamageDict["Blunt"].Float(), Is.EqualTo(40f));
            Assert.That(entities.HasComponent<KnockedDownComponent>(rider), Is.True);
            Assert.That(entities.GetComponent<PhysicsComponent>(rider).LinearVelocity.Length(), Is.GreaterThan(0f));
            entities.DeleteEntity(ship);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("omaha", 0)]
    [TestCase("omaha", 90)]
    [TestCase("midway", 0)]
    [TestCase("midway", 90)]
    public async Task LandingGearCollidesAcrossItsWholeFootprint(string variant, int degrees)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var ship = LoadShip(entities, variant);
            var transform = entities.System<SharedTransformSystem>();
            transform.SetWorldRotation(ship, Angle.FromDegrees(degrees));
            var assembly = entities.GetComponent<MultiDeckDropshipComponent>(ship);
            entities.System<MultiDeckDropshipSystem>().Synchronize((ship, assembly));
            var gears = entities.EntityQuery<MohawkLandingGearComponent>().ToArray();
            Assert.That(gears, Has.Length.EqualTo(4));
            foreach (var gear in gears)
            {
                var xform = entities.GetComponent<TransformComponent>(gear.Owner);
                Assert.That(xform.Anchored, Is.True);
                Assert.That(xform.LocalRotation.GetCardinalDir(), Is.EqualTo(xform.LocalPosition.Y > 0 ? Direction.North : Direction.South));
                for (var x = 0; x < 2; x++)
                for (var y = 0; y < 2; y++)
                {
                    var position = transform.ToMapCoordinates(new EntityCoordinates(assembly.Decks[-1], xform.LocalPosition + new Vector2(x, y)));
                    var fixtures = FixturesAt(entities, position, CollisionGroup.MobMask);
                    Assert.That(fixtures.Any(f => f.Entity == gear.Owner && f.Fixture.Hard), Is.True,
                        $"Landing gear at {xform.LocalPosition} must block a human in footprint cell {x},{y}.");
                }
            }
            entities.DeleteEntity(ship);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("omaha", 0)]
    [TestCase("omaha", 90)]
    [TestCase("midway", 0)]
    [TestCase("midway", 90)]
    public async Task RampBulkheadHasFullTileCollisionOnlyWhenDeployed(string variant, int degrees)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var ship = LoadShip(entities, variant);
            var assembly = entities.GetComponent<MultiDeckDropshipComponent>(ship);
            var transform = entities.System<SharedTransformSystem>();
            transform.SetWorldRotation(ship, Angle.FromDegrees(degrees));
            entities.System<MultiDeckDropshipSystem>().Synchronize((ship, assembly));
            var bulkheads = entities.EntityQuery<MohawkRampSegmentComponent>().Where(p => p.Lower && p.Stage == 4).ToArray();
            Assert.That(bulkheads, Has.Length.EqualTo(3));
            foreach (var deployed in new[] { true, false, true })
            {
                entities.System<MohawkSystem>().SetRampDeployed(ship, deployed, true);
                foreach (var bulkhead in bulkheads)
                foreach (var offset in new[] { new Vector2(-0.4f, -0.4f), new Vector2(0.4f, 0.4f), Vector2.Zero })
                {
                    var position = transform.ToMapCoordinates(new EntityCoordinates(bulkhead.Owner, offset));
                    Assert.That(FixturesAt(entities, position, CollisionGroup.MobMask)
                        .Any(f => f.Entity == bulkhead.Owner && f.Fixture.Hard), Is.EqualTo(deployed),
                        "The ramp bulkhead must block ground-level movement over its entire visible tile.");
                }
            }
            entities.DeleteEntity(ship);
        });
        await pair.CleanReturnAsync();
    }

    private static EntityUid LoadShip(IEntityManager entities, string variant)
    {
        entities.System<SharedMapSystem>().CreateMap(out var mapId);
        Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
            new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}.yml"), out var loaded), Is.True);
        return loaded!.Value.Owner;
    }

    private static HashSet<FixtureProxy> WallFixturesAt(IEntityManager entities, EntityUid rider)
        => FixturesAt(entities, entities.System<SharedTransformSystem>().GetMapCoordinates(rider), CollisionGroup.Impassable);

    private static HashSet<FixtureProxy> FixturesAt(IEntityManager entities, MapCoordinates position, CollisionGroup mask)
    {
        var fixtures = new HashSet<FixtureProxy>();
        entities.System<EntityLookupSystem>().GetFixturesIntersecting(position.MapId,
            Box2.CenteredAround(position.Position, new Vector2(0.6f)), fixtures,
            new FixtureQueryArgs(new QueryFilter
            {
                LayerBits = (long) CollisionGroup.MobLayer,
                MaskBits = (long) mask,
                Flags = QueryFlags.Static | QueryFlags.Dynamic,
            }));
        return fixtures;
    }
}
