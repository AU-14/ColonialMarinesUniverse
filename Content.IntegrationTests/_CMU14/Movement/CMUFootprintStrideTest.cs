using Content.IntegrationTests.Fixtures;
using Content.Server.Decals;
using Content.Shared.FootPrint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.IntegrationTests.CMU14.Movement;

[TestFixture]
public sealed class CMUFootprintStrideTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUFootprintStrideMover
          components:
          - type: FootPrints
          - type: Damageable
          - type: MobThresholds
            thresholds:
              0: Alive
        """;

    [TestCase(false)]
    [TestCase(true)]
    public async Task ShortMovementAndRotationRetainStateUntilTheWalkingOrDraggingStride(bool critical)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = 0; x < 4; x++)
                maps.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
            var uid = SEntMan.SpawnEntity("CMUFootprintStrideMover", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            var prints = SEntMan.GetComponent<FootPrintsComponent>(uid);
            SEntMan.EnsureComponent<MobStateComponent>(uid);
            SEntMan.System<MobStateSystem>().ChangeMobState(uid, critical ? MobState.Critical : MobState.Alive);
            var transforms = SEntMan.System<SharedTransformSystem>();
            var decals = SEntMan.System<DecalSystem>();
            var origin = new Vector2(0.5f);
            prints.StepPos = origin;
            prints.StepSize = 2;
            prints.DragSize = 1;
            prints.PrintsColor = Color.Red;
            var stride = critical ? prints.DragSize : prints.StepSize;
            var rightStep = prints.RightStep;

            try
            {
                for (var i = 1; i <= 8; i++)
                    transforms.SetLocalPosition(uid, origin + new Vector2(stride * i / 16, 0));
                transforms.SetLocalRotation(uid, Angle.FromDegrees(90));
                transforms.SetLocalPosition(uid, origin + new Vector2(stride, 0));
                Assert.Multiple(() =>
                {
                    Assert.That(decals.GetAllDecals(map.Grid), Is.Empty);
                    Assert.That(prints.StepPos, Is.EqualTo(origin));
                    Assert.That(prints.RightStep, Is.EqualTo(rightStep));
                    Assert.That(prints.PrintsColor, Is.EqualTo(Color.Red));
                });

                transforms.SetLocalPosition(uid, origin + new Vector2(stride + 0.125f, 0));
                var step = decals.GetAllDecals(map.Grid).Single();
                Assert.That(prints.StepPos, Is.EqualTo(origin + new Vector2(stride + 0.125f, 0)));
                Assert.That(prints.RightStep, Is.Not.EqualTo(rightStep));
                Assert.That(prints.PrintsColor.A, Is.LessThan(1));
                if (critical)
                    Assert.That(prints.DraggingDecals, Does.Contain(step.Decal.Id));
                else
                    Assert.That(step.Decal.Id, Is.EqualTo(prints.RightStep ? prints.RightBareDecal : prints.LeftBareDecal));
            }
            finally
            {
                SEntMan.DeleteEntity(uid);
            }
        });
    }
}
