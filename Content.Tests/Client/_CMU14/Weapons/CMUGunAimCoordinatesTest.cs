using System.Numerics;
using Content.Client.Weapons.Ranged.Systems;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.UnitTesting;

namespace Content.Tests.Client.CMU14.Weapons;

[TestFixture]
public sealed class CMUGunAimCoordinatesTest : RobustUnitTest
{
    public override UnitTestProject Project => UnitTestProject.Client;

    [TestCase(false, 0)]
    [TestCase(true, 0)]
    [TestCase(true, 90)]
    [TestCase(true, 180)]
    [TestCase(true, 270)]
    public void TurningShooterDoesNotRedirectQueuedAim(bool onGrid, int gridDegrees)
    {
        var entities = IoCManager.Resolve<IEntityManager>();
        var maps = entities.System<SharedMapSystem>();
        var transform = entities.System<SharedTransformSystem>();
        var traversal = entities.System<SharedGridTraversalSystem>();
        var traversalEnabled = traversal.Enabled;
        // This coordinate-only fixture has no floor tiles to keep entities on its grid.
        traversal.Enabled = false;
        var map = maps.CreateMap(out _);
        try
        {
            var parent = onGrid ? maps.CreateGridEntity(map).Owner : map;
            if (onGrid)
            {
                transform.SetLocalPosition(parent, new Vector2(100, -40));
                transform.SetLocalRotation(parent, Angle.FromDegrees(gridDegrees));
            }
            var shooter = entities.SpawnEntity(null, new EntityCoordinates(parent, new Vector2(3, 2)));
            var targetOnGrid = new EntityCoordinates(parent, new Vector2(3, 8));
            var target = transform.ToMapCoordinates(targetOnGrid);

            var aim = GunSystem.GetAimCoordinates(transform, shooter, target);
            transform.SetWorldRotation(shooter, transform.GetWorldRotation(shooter) + Angle.FromDegrees(90));

            Assert.That(Vector2.Distance(transform.ToMapCoordinates(aim).Position, target.Position),
                Is.LessThan(0.001f), "A facing update must not rotate the requested shot away from the cursor.");

            if (!onGrid)
                return;

            transform.SetLocalPosition(parent, new Vector2(150, 70));
            transform.SetLocalRotation(parent, Angle.FromDegrees(gridDegrees + 90));
            Assert.That(Vector2.Distance(transform.ToMapCoordinates(aim).Position,
                    transform.ToMapCoordinates(targetOnGrid).Position),
                Is.LessThan(0.001f), "Aim must still follow dropship translation and rotation.");
        }
        finally
        {
            entities.DeleteEntity(map);
            traversal.Enabled = traversalEnabled;
        }
    }
}
