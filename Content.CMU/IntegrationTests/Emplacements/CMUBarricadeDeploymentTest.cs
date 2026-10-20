#pragma warning disable RA0002 // Inspect prototype clearance settings without changing live mounts.
using Content.Shared._RMC14.Emplacements;
using Content.Shared._RMC14.Map;
using Content.Shared.Physics;
using Content.Shared.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Emplacements;

[TestFixture]
public sealed class CMUBarricadeDeploymentTest
{
    [TestCase("CMBarricadeMetal")]
    [TestCase("RMCBarricadePlasteel")]
    [TestCase("CMBarricadeSandbag")]
    public async Task BarricadesAllowWeaponsButWallsStillBlock(string barricadePrototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;
        EntityUid barricade = default;
        EntityUid wall = default;

        try
        {
            await server.WaitPost(() => barricade = entities.SpawnEntity(barricadePrototype, map.GridCoords));
            await pair.RunTicksSync(2);
            await server.WaitAssertion(() =>
            {
                var rmcMap = server.System<RMCMapSystem>();
                Assert.That(rmcMap.CanDeployWeaponOn(map.GridCoords), Is.True, "Sentries can deploy against barricades.");
                Assert.That(rmcMap.CanDeployWeaponOn(map.GridCoords, CollisionGroup.MidImpassable, checkStructures: false),
                    Is.True, "Weapon mounts can deploy against barricades.");
                wall = entities.SpawnEntity("CMWallMetal", map.GridCoords);
            });
            await pair.RunTicksSync(2);
            await server.WaitAssertion(() =>
            {
                var rmcMap = server.System<RMCMapSystem>();
                Assert.That(rmcMap.CanDeployWeaponOn(map.GridCoords), Is.False, "A barricade must not hide another obstruction.");
                Assert.That(rmcMap.CanDeployWeaponOn(map.GridCoords, CollisionGroup.MidImpassable, checkStructures: false), Is.False);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                if (entities.EntityExists(barricade))
                    entities.DeleteEntity(barricade);
                if (entities.EntityExists(wall))
                    entities.DeleteEntity(wall);
            });
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NoWeaponMountRequiresBarricadeClearance()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var factory = pair.Server.EntMan.ComponentFactory;
            foreach (var prototype in pair.Server.ResolveDependency<IPrototypeManager>().EnumeratePrototypes<EntityPrototype>())
            {
                if (prototype.TryComp<WeaponMountComponent>(out var mount, factory))
                    Assert.That(mount.BarricadeExclusionAreaSize, Is.Zero, prototype.ID);
            }
        });
        await pair.CleanReturnAsync();
    }
}
