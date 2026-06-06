using System.Numerics;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Construction.Events;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class XenoHiveInstantBuildTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: CMXenoDrone
  id: RMCTestXenoDroneBuildResinWall
  components:
  - type: XenoConstruction
    buildChoice: WallXenoResin
  - type: XenoPlasma
    plasma: 0
    maxPlasma: 0
";

    [Test]
    public async Task InstantBuildPoolIsPerHive()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var construction = entMan.System<SharedXenoConstructionSystem>();
            var hiveOne = entMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var hiveTwo = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(construction.GetHiveInstantBuildsRemaining(hiveOne), Is.EqualTo(200));
                    Assert.That(construction.GetHiveInstantBuildsRemaining(hiveTwo), Is.EqualTo(200));
                });

                Assert.That(construction.TryUseHiveInstantBuild(hiveOne), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(construction.GetHiveInstantBuildsRemaining(hiveOne), Is.EqualTo(199));
                    Assert.That(construction.GetHiveInstantBuildsRemaining(hiveTwo), Is.EqualTo(200));
                });
            }
            finally
            {
                entMan.DeleteEntity(hiveOne);
                entMan.DeleteEntity(hiveTwo);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InstantBuildCounterMirrorsHivePoolForSecreteActions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hiveSystem = entMan.System<SharedXenoHiveSystem>();
            var construction = entMan.System<SharedXenoConstructionSystem>();
            var hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var droneOne = entMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(1, 0)));
            var droneTwo = entMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(2, 0)));

            try
            {
                hiveSystem.SetHive(droneOne, hive);
                hiveSystem.SetHive(droneTwo, hive);

                construction.RefreshHiveInstantBuildActionCounters(hive);

                var counterOne = GetSecreteActionCounter(entMan, droneOne);
                var counterTwo = GetSecreteActionCounter(entMan, droneTwo);

                Assert.Multiple(() =>
                {
                    Assert.That(counterOne.Visible, Is.True);
                    Assert.That(counterOne.BuildsLeft, Is.EqualTo(200));
                    Assert.That(counterTwo.Visible, Is.True);
                    Assert.That(counterTwo.BuildsLeft, Is.EqualTo(200));
                });

                Assert.That(construction.TryUseHiveInstantBuild(hive), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(counterOne.BuildsLeft, Is.EqualTo(199));
                    Assert.That(counterTwo.BuildsLeft, Is.EqualTo(199));
                });
            }
            finally
            {
                entMan.DeleteEntity(hive);
                entMan.DeleteEntity(droneOne);
                entMan.DeleteEntity(droneTwo);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SecreteStructureUsesHiveInstantBuildWithoutPlasmaOrDoAfter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var tileManager = server.ResolveDependency<ITileDefinitionManager>();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hiveSystem = entMan.System<SharedXenoHiveSystem>();
            var construction = entMan.System<SharedXenoConstructionSystem>();
            var mapSystem = entMan.System<SharedMapSystem>();
            var hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var drone = entMan.SpawnEntity("RMCTestXenoDroneBuildResinWall", map.GridCoords);
            var target = map.GridCoords.Offset(new Vector2(1, 0));
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, target, new Tile(tileManager["Plating"].TileId));
            var weeds = entMan.SpawnEntity("XenoWeeds", target);
            var action = SpawnAction(entMan);

            try
            {
                hiveSystem.SetHive(drone, hive);
                hiveSystem.SetSameHive(drone, weeds);

                RaiseSecreteStructure(entMan, drone, target, action);

                Assert.Multiple(() =>
                {
                    Assert.That(CountPrototype(entMan, "WallXenoResin"), Is.EqualTo(1));
                    Assert.That(construction.GetHiveInstantBuildsRemaining(hive), Is.EqualTo(199));
                    Assert.That(HasActiveConstructionDoAfter(entMan, drone), Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(hive);
                entMan.DeleteEntity(drone);
                entMan.DeleteEntity(weeds);
                entMan.DeleteEntity(action.Owner);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FailedSecreteStructureDoesNotConsumeHiveInstantBuild()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var tileManager = server.ResolveDependency<ITileDefinitionManager>();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hiveSystem = entMan.System<SharedXenoHiveSystem>();
            var construction = entMan.System<SharedXenoConstructionSystem>();
            var mapSystem = entMan.System<SharedMapSystem>();
            var hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var drone = entMan.SpawnEntity("RMCTestXenoDroneBuildResinWall", map.GridCoords);
            var target = map.GridCoords.Offset(new Vector2(1, 0));
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, target, new Tile(tileManager["Plating"].TileId));
            var action = SpawnAction(entMan);

            try
            {
                hiveSystem.SetHive(drone, hive);

                RaiseSecreteStructure(entMan, drone, target, action);

                Assert.Multiple(() =>
                {
                    Assert.That(CountPrototype(entMan, "WallXenoResin"), Is.Zero);
                    Assert.That(construction.GetHiveInstantBuildsRemaining(hive), Is.EqualTo(200));
                });
            }
            finally
            {
                entMan.DeleteEntity(hive);
                entMan.DeleteEntity(drone);
                entMan.DeleteEntity(action.Owner);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static Entity<ActionComponent> SpawnAction(IEntityManager entMan)
    {
        var action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
        return (action, entMan.EnsureComponent<ActionComponent>(action));
    }

    private static void RaiseSecreteStructure(
        IEntityManager entMan,
        EntityUid xeno,
        EntityCoordinates target,
        Entity<ActionComponent> action)
    {
        var ev = new XenoSecreteStructureActionEvent
        {
            Performer = xeno,
            Action = action,
            Target = target,
        };

        entMan.EventBus.RaiseLocalEvent(xeno, ev);
    }

    private static XenoHiveInstantBuildActionComponent GetSecreteActionCounter(IEntityManager entMan, EntityUid xeno)
    {
        var actions = entMan.System<SharedActionsSystem>();
        foreach (var action in actions.GetActions(xeno))
        {
            if (entMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID != "ActionXenoSecreteStructure")
                continue;

            Assert.That(entMan.TryGetComponent(action, out XenoHiveInstantBuildActionComponent counter), Is.True);
            return counter;
        }

        Assert.Fail("Expected xeno to have ActionXenoSecreteStructure");
        throw new InvalidOperationException();
    }

    private static bool HasActiveConstructionDoAfter(IEntityManager entMan, EntityUid xeno)
    {
        if (!entMan.TryGetComponent(xeno, out DoAfterComponent doAfter))
            return false;

        foreach (var active in doAfter.DoAfters.Values)
        {
            if (active.Args.Event is XenoSecreteStructureDoAfterEvent)
                return true;
        }

        return false;
    }

    private static int CountPrototype(IEntityManager entMan, string prototypeId)
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var metadata))
        {
            if (metadata.EntityPrototype?.ID == prototypeId)
                count++;
        }

        return count;
    }
}
