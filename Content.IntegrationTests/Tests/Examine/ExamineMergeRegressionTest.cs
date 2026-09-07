using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.Examine;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.Xenonids.Eye;
using Content.Shared._RMC14.Xenonids.Watch;
using Content.Shared.Examine;
using Content.Shared.Ghost.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Examine;

[TestFixture]
[TestOf(typeof(ExamineSystemShared))]
public sealed class ExamineMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: ExamineMergeQueenVisionSeed
          components:
          - type: QueenEyeVision
            range: 28
        """;

    [Test]
    public async Task WatchersUseTheirRemoteEntityAndQueenEyeUsesVisionSeeds()
    {
        var firstMap = await Pair.CreateTestMap();
        var secondMap = await Pair.CreateTestMap();
        var entities = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var examine = Server.System<ExamineSystem>();
                var transform = Server.System<SharedTransformSystem>();
                var xenoWatch = Server.System<SharedXenoWatchSystem>();

                var ordinary = Spawn("MobHuman", firstMap.GridCoords, entities);
                var overwatch = Spawn("MobHuman", firstMap.GridCoords, entities);
                var xenoWatcher = Spawn("MobHuman", firstMap.GridCoords, entities);
                var watched = Spawn("MobHuman", secondMap.GridCoords, entities);
                var target = Spawn("MobHuman", secondMap.GridCoords.Offset(new Vector2(1, 0)), entities);
                EnsureExaminer(ordinary);
                EnsureExaminer(overwatch);
                EnsureExaminer(xenoWatcher);

                Assert.That(examine.CanExamine(ordinary, target), Is.False,
                    "an ordinary examiner cannot cross map boundaries");

                var overwatchComp = SEntMan.EnsureComponent<OverwatchWatchingComponent>(overwatch);
                SetField(overwatchComp, "Watching", (EntityUid?) watched);
                SEntMan.Dirty(overwatch, overwatchComp);
                Assert.That(examine.CanExamine(overwatch, target), Is.True,
                    "overwatch examination must use the watched entity's map, range, and line of sight");

                xenoWatch.SetWatching(xenoWatcher, watched);
                Assert.That(examine.CanExamine(xenoWatcher, target), Is.True,
                    "xeno watching must use the watched entity rather than the remote viewer");

                transform.SetCoordinates(target, secondMap.GridCoords.Offset(new Vector2(17, 0)));
                Assert.Multiple(() =>
                {
                    Assert.That(examine.CanExamine(overwatch, target), Is.False);
                    Assert.That(examine.CanExamine(xenoWatcher, target), Is.False);
                });

                var queen = Spawn("MobHuman", firstMap.GridCoords, entities);
                var queenTarget = Spawn("MobHuman", firstMap.GridCoords.Offset(new Vector2(20, 0)), entities);
                var visionSeed = Spawn("ExamineMergeQueenVisionSeed", firstMap.GridCoords.Offset(new Vector2(20, 0)), entities);
                EnsureExaminer(queen);
                var queenAction = SEntMan.EnsureComponent<QueenEyeActionComponent>(queen);
                SetField(queenAction, "Eye", (EntityUid?) visionSeed);
                SEntMan.Dirty(queen, queenAction);

                Assert.Multiple(() =>
                {
                    Assert.That(Server.System<QueenEyeSystem>().CanSeeTarget((queen, queenAction), queenTarget), Is.True,
                        "the synthetic vision seed must make the distant tile visible to QueenEyeSystem");
                    Assert.That(examine.CanExamine(queen, queenTarget), Is.True,
                        "QueenEye CanSeeTarget replaces the ordinary sixteen-tile examine range");
                });

                SetField(queenAction, "Eye", null);
                SEntMan.Dirty(queen, queenAction);
                Assert.That(examine.CanExamine(queen, queenTarget), Is.False,
                    "without an active eye the same distant target falls back to ordinary examine range");
            });
        }
        finally
        {
            await Delete(entities.ToArray());
        }
    }

    [Test]
    public async Task DetailsAndRaycastsRespectRangeStateEndpointsAndQueryIsolation()
    {
        var map = await Pair.CreateTestMap();
        var entities = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var examine = Server.System<ExamineSystem>();
                var transform = Server.System<SharedTransformSystem>();
                var mobState = Server.System<MobStateSystem>();
                var examiner = Spawn("MobHuman", map.GridCoords, entities);
                var target = Spawn("MobHuman", map.GridCoords.Offset(new Vector2(8, 0)), entities);
                EnsureExaminer(examiner);

                Assert.That(examine.IsInDetailsRange(examiner, target), Is.True,
                    "the detailed range includes its exact eight-tile boundary");
                transform.SetCoordinates(target, map.GridCoords.Offset(new Vector2(8.1f, 0)));
                Assert.That(examine.IsInDetailsRange(examiner, target), Is.False);
                transform.SetCoordinates(target, map.GridCoords.Offset(new Vector2(1, 0)));
                mobState.ChangeMobState(examiner, MobState.Critical);
                Assert.That(examine.IsInDetailsRange(examiner, target), Is.False,
                    "incapacitation suppresses details even at close range");
                SEntMan.EnsureComponent<GhostComponent>(examiner);
                Assert.That(examine.IsInDetailsRange(examiner, target), Is.True,
                    "GhostComponent bypasses range and incapacitation checks");

                var wall = Spawn("WallSolid", map.GridCoords.Offset(new Vector2(2, 0)), entities);
                var origin = transform.GetMapCoordinates(examiner);
                var blocked = new MapCoordinates(origin.Position + new Vector2(4, 0), origin.MapId);
                var clear = new MapCoordinates(origin.Position + new Vector2(0, 4), origin.MapId);
                var endpoint = transform.GetMapCoordinates(wall);

                Assert.Multiple(() =>
                {
                    Assert.That(examine.InRangeUnOccluded(origin, blocked, 8, predicate: null), Is.False);
                    Assert.That(examine.InRangeUnOccluded(origin, clear, 8, predicate: null), Is.True,
                        "the reusable raycast results must not retain the previous query's wall hit");
                    Assert.That(examine.InRangeUnOccluded(origin, blocked, 8, entity => entity == wall), Is.True,
                        "the predicate ignores only its selected blocker");
                    Assert.That(examine.InRangeUnOccluded(origin, endpoint, 8, predicate: null), Is.True,
                        "an endpoint contained by an occluder remains examinable");
                });
            });
        }
        finally
        {
            await Delete(entities.ToArray());
        }
    }

    [Test]
    public async Task BaseXenoFixedDescriptionUsesTheMarineWhitelist()
    {
        var map = await Pair.CreateTestMap();
        var entities = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var examine = Server.System<ExamineSystem>();
                var xeno = Spawn("CMXenoDrone", map.GridCoords, entities);
                var marine = Spawn("CMMobHuman", map.GridCoords, entities);
                var ordinary = Spawn("MobHuman", map.GridCoords, entities);
                var unknown = Loc.GetString("rmc-xeno-unknown-description");
                var native = SEntMan.GetComponent<MetaDataComponent>(xeno).EntityDescription;
                var marineText = examine.GetExamineText(xeno, marine).ToMarkup();
                var ordinaryText = examine.GetExamineText(xeno, ordinary).ToMarkup();

                Assert.Multiple(() =>
                {
                    Assert.That(marineText, Does.Contain(unknown));
                    Assert.That(marineText, Does.Not.Contain(native));
                    Assert.That(ordinaryText, Does.Contain(native));
                    Assert.That(ordinaryText, Does.Not.Contain(unknown));
                });
            });
        }
        finally
        {
            await Delete(entities.ToArray());
        }
    }

    private EntityUid Spawn(string prototype, EntityCoordinates coordinates, ICollection<EntityUid> entities)
    {
        var uid = SEntMan.SpawnEntity(prototype, coordinates);
        entities.Add(uid);
        return uid;
    }

    private void EnsureExaminer(EntityUid uid)
    {
        SEntMan.EnsureComponent<ExaminerComponent>(uid);
    }

    private static void SetField<T>(T component, string name, object? value)
    {
        typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.Public)!.SetValue(component, value);
    }

    private async Task Delete(params EntityUid[] entities)
    {
        await Server.WaitPost(() =>
        {
            foreach (var uid in entities)
            {
                if (SEntMan.EntityExists(uid))
                    SEntMan.DeleteEntity(uid);
            }
        });
    }
}
