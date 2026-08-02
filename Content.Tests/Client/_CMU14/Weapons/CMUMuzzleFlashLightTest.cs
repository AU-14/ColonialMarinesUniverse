using Content.Client.Weapons.Ranged.Systems;
using NUnit.Framework;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.UnitTesting;

namespace Content.Tests.Client._CMU14.Weapons;

[TestFixture]
public sealed class CMUMuzzleFlashLightTest : RobustUnitTest
{
    public override UnitTestProject Project => UnitTestProject.Client;

    [Test]
    public void MuzzleFlashDoesNotConsumeShadowCastingLightCapacity()
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();
        var lights = entityManager.System<PointLightSystem>();
        var uid = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
        var light = lights.EnsureLight(uid);

        GunSystem.ConfigureMuzzleFlashLight(uid, light, lights);

        Assert.That(light.CastShadows, Is.False,
            "Temporary muzzle flashes must not displace persistent lights from the shadow-light budget.");
    }
}
