using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Ordnance;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.OrbitalCannon;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.ZLevels.Ordnance;

[TestFixture]
public sealed class CMUOrbitalExplosionZLevelTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestOrbitalImpact

        - type: entity
          id: CMUTestOrbitalExplosion
          components:
          - type: OrbitalCannonExplosion
            multiZ: true
            steps:
            - explosionEffect: CMUTestOrbitalImpact
              checkProtectionPer: true

        - type: entity
          id: CMUTestOrbitalAllowedArea
          components:
          - type: Area
            OB: true

        - type: entity
          id: CMUTestOrbitalProtectedArea
          components:
          - type: Area
            OB: false
        """;

    [Test]
    public async Task ImpactStepsCheckEachGridAndScuttleBypassesProtection()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var transform = Server.System<SharedTransformSystem>();
            var zLevels = Server.System<CMUZLevelsSystem>();
            var orbital = Server.System<OrbitalCannonSystem>();
            var areas = Server.System<AreaSystem>();
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            var position = new Vector2(8.5f, 11.5f);
            var mapsByDepth = new Dictionary<EntityUid, int>();
            var mapIds = new MapId[3];
            var network = zLevels.CreateZNetwork();
            EntityCoordinates source = default;

            try
            {
                for (var depth = 0; depth < mapIds.Length; depth++)
                {
                    var map = maps.CreateMap(out mapIds[depth], runMapInit: true);
                    var grid = maps.CreateGridEntity(mapIds[depth]);
                    var tile = new Vector2i(8, 11);
                    maps.SetTile(grid, tile, new Tile(tiles["Plating"].TileId));
                    var areaGrid = SEntMan.AddComponent<AreaGridComponent>(grid);
                    areas.ReplaceArea(areaGrid, tile,
                        depth == 1 ? "CMUTestOrbitalProtectedArea" : "CMUTestOrbitalAllowedArea");
                    if (depth < 2)
                        mapsByDepth.Add(map, depth);
                    if (depth == 0)
                        source = new EntityCoordinates(grid, position);
                }
                Assert.That(zLevels.TryAddMapsIntoZNetwork(network, mapsByDepth), Is.True);

                List<MapCoordinates> Find(string prototype)
                {
                    var found = new List<MapCoordinates>();
                    var query = SEntMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
                    while (query.MoveNext(out var uid, out var metadata, out _))
                    {
                        if (metadata.EntityPrototype?.ID == prototype)
                            found.Add(transform.GetMapCoordinates(uid));
                    }
                    return found;
                }

                orbital.SpawnExplosion("CMUTestOrbitalExplosion", source);
                orbital.Update(0);
                Assert.That(Find("CMUTestOrbitalImpact"), Is.EqualTo(new[] { new MapCoordinates(position, mapIds[0]) }),
                    "A protected level must not suppress an allowed level's impact or receive its own impact.");
                Assert.That(Find("RMCLaserDropshipTarget"), Is.EquivalentTo(new[]
                {
                    new MapCoordinates(position, mapIds[0]),
                    new MapCoordinates(position, mapIds[1]),
                }), "The same target is marked on connected levels, never on unrelated maps.");

                orbital.SpawnExplosion("CMUTestOrbitalExplosion", source, CMUTopDownOrdnanceKind.Scuttle);
                orbital.Update(0);
                Assert.That(Find("CMUTestOrbitalImpact"), Is.EquivalentTo(new[]
                {
                    new MapCoordinates(position, mapIds[0]),
                    new MapCoordinates(position, mapIds[0]),
                    new MapCoordinates(position, mapIds[1]),
                }), "Scuttling must still affect the protected level while keeping the earlier impact.");
            }
            finally
            {
                SEntMan.DeleteEntity(network);
                foreach (var mapId in mapIds)
                    maps.DeleteMap(mapId);
            }
        });
    }
}
