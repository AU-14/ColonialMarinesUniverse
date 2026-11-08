using System.Numerics;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Construction.BuildMark;
using Content.Shared._RMC14.Xenonids.Construction.Events;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.Actions.Components;
using Content.Shared.Eye;
using Content.Shared.GameTicking;
using Robust.Server.GameStates;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU.Xenomorphs;

[TestFixture]
public sealed class CMUXenoBuildMarksTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: CMXenoDrone
  id: CMUTestBuildMarkDrone
  components:
  - type: XenoConstruction
    buildChoice: WallXenoResin
  - type: XenoPlasma
    plasma: 0
    maxPlasma: 0
    plasmaRegenOnWeeds: 0
    plasmaRegenOffWeeds: 0

- type: entity
  parent: CMU14XenoNeomorph
  id: CMUTestBuildMarkNeomorph
  components:
  - type: XenoConstruction
    buildChoice: CMU14XenoMyceliumWall
  - type: XenoPlasma
    plasma: 0
    maxPlasma: 0
    plasmaRegenOnWeeds: 0
    plasmaRegenOffWeeds: 0

- type: entity
  parent: CMUTestBuildMarkNeomorph
  id: CMUTestBuildMarkNeomorphPlasma
  components:
  - type: XenoPlasma
    plasma: 1000
    maxPlasma: 1000

- type: entity
  id: CMUTestBuildMarkArea
  components:
  - type: Area
";

    [TestCase("CMUTestBuildMarkDrone", "CMXenoHive", "XenoWeeds", "WallXenoResin", true)]
    [TestCase("CMUTestBuildMarkNeomorph", "CMUPathogenHive", "CMU14XenoMyceliumWeeds", "CMU14XenoMyceliumWall", true)]
    [TestCase("CMUTestBuildMarkNeomorphPlasma", "CMUPathogenHive", "CMU14XenoMyceliumWeeds", "CMU14XenoMyceliumWall", false)]
    public async Task OutOfRangeMarkBuildsOnApproach(string builderId, string hiveId, string weedsId, string structureId, bool instant)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var tiles = server.ResolveDependency<ITileDefinitionManager>();
        EntityUid builder = default;
        EntityUid mark = default;
        EntityUid hive = default;
        var target = map.GridCoords.Offset(new Vector2(4, 0));

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var construction = entities.System<SharedXenoConstructionSystem>();
            var hives = entities.System<SharedXenoHiveSystem>();
            var mapSystem = entities.System<SharedMapSystem>();
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, target, new Tile(tiles["Plating"].TileId));
            var areaGrid = entities.EnsureComponent<AreaGridComponent>(map.Grid.Owner);
            entities.System<AreaSystem>().ReplaceArea(areaGrid, mapSystem.CoordinatesToTile(map.Grid.Owner, map.Grid.Comp, target), "CMUTestBuildMarkArea");
            hive = entities.SpawnEntity(hiveId, map.GridCoords);
            // Pooled servers may already be past the normal instant-build window.
            entities.GetComponent<HiveComponent>(hive).InstantBuildDuration =
                entities.System<SharedGameTicker>().RoundDuration() + TimeSpan.FromMinutes(1);
            if (!instant)
            {
                while (construction.TryUseHiveInstantBuild(hive))
                {
                }
            }
            builder = entities.SpawnEntity(builderId, map.GridCoords);
            hives.SetHive(builder, hive);
            var weeds = entities.SpawnEntity(weedsId, target);
            hives.SetSameHive(builder, weeds);
            var actionUid = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var action = entities.EnsureComponent<ActionComponent>(actionUid);
            var request = new XenoSecreteStructureActionEvent
            {
                Performer = builder,
                Action = (actionUid, action),
                Target = target,
            };
            entities.EventBus.RaiseLocalEvent(builder, request);

            var marks = entities.System<XenoBuildMarkSystem>();
            Assert.That(marks.TryGetOwnMark(builder, target, out var placed), Is.True);
            mark = placed.Owner;
            Assert.That(construction.GetHiveInstantBuildsRemaining(hive), Is.EqualTo(instant ? 200 : 0));

            var ownerPvs = new ExpandPvsEvent(null!, (int) VisibilityFlags.Normal);
            entities.EventBus.RaiseLocalEvent(builder, ref ownerPvs);
            Assert.That(ownerPvs.Entities, Does.Contain(mark));
            Assert.That(ownerPvs.VisMask & entities.GetComponent<VisibilityComponent>(mark).Layer, Is.Not.Zero);
            var otherPvs = new ExpandPvsEvent(null!, (int) VisibilityFlags.Normal);
            entities.EventBus.RaiseLocalEvent(hive, ref otherPvs);
            Assert.That(otherPvs.VisMask & entities.GetComponent<VisibilityComponent>(mark).Layer, Is.Zero);

            entities.System<SharedTransformSystem>().SetCoordinates(builder, target.Offset(new Vector2(-1, 0)));
        });

        await pair.RunTicksSync(90);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var marks = entities.System<XenoBuildMarkSystem>();
            Assert.That(entities.Deleted(mark), Is.True);
            Assert.That(marks.TryGetOwnMark(builder, target, out _), Is.False);
            Assert.That(entities.System<SharedXenoConstructionSystem>().GetHiveInstantBuildsRemaining(hive), Is.EqualTo(instant ? 199 : 0));
            var query = entities.EntityQueryEnumerator<XenoConstructComponent, MetaDataComponent>();
            var built = 0;
            while (query.MoveNext(out _, out _, out var metadata))
            {
                if (metadata.EntityPrototype?.ID == structureId)
                    built++;
            }
            Assert.That(built, Is.EqualTo(1));
            if (!instant)
            {
                var remainingPlasma = entities.GetComponent<XenoPlasmaComponent>(builder).Plasma;
                Assert.That(remainingPlasma.Float(), Is.LessThan(1000));
            }
        });

        await pair.CleanReturnAsync();
    }
}
