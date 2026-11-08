using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Shuttles;
using Content.Server.Shuttles.Events;
using Content.Shared._RMC14.OnCollide;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Evacuation;

[TestFixture]
public sealed class CMULegacyDepartureHazardTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task LegacyDepartureStillLeavesALethalHazard()
    {
        EntityUid map = default;
        EntityUid transitMap = default;
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            map = maps.CreateMap(out var mapId, runMapInit: true);
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            var terrain = maps.CreateGridEntity(mapId);
            maps.SetTile(terrain, Vector2i.Zero, floor);

            var shuttle = maps.CreateGridEntity(mapId);
            maps.SetTile(shuttle, Vector2i.Zero, floor);
            SEntMan.AddComponent<RMCSpawnEntityOnFTLStartComponent>(shuttle);
            var before = new BeforeFTLStartedEvent();
            SEntMan.EventBus.RaiseLocalEvent(shuttle, ref before);

            // Capture the old footprint through the real departure event, then vacate it.
            transitMap = maps.CreateMap(out var transitMapId, runMapInit: true);
            SEntMan.System<SharedTransformSystem>().SetMapCoordinates(shuttle, new MapCoordinates(Vector2.Zero, transitMapId));
            var departed = new FTLStartedEvent(shuttle, new EntityCoordinates(map, Vector2.Zero),
                map, Matrix3x2.Identity, Angle.Zero);
            SEntMan.EventBus.RaiseLocalEvent(shuttle, ref departed);
        });

        await Server.WaitAssertion(() =>
        {
            var hazards = 0;
            var query = SEntMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent, DamageOnCollideComponent>();
            while (query.MoveNext(out _, out var metadata, out var transform, out _))
            {
                if (!metadata.Deleted && metadata.EntityPrototype?.ID == "FloorDeathEntity" && transform.MapUid == map)
                    hazards++;
            }

            Assert.That(hazards, Is.EqualTo(1),
                "legacy maps which have not opted into playable space must retain their departure hazard");
        });
        await Pair.DeleteEntityTreeLeafFirst(map);
        await Pair.DeleteEntityTreeLeafFirst(transitMap);
    }
}
