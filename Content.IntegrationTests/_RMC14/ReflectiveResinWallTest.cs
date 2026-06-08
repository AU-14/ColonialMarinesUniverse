using Content.Shared._RMC14.Projectiles.Reflect;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Designer;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class ReflectiveResinWallTest
{
    private static readonly EntProtoId ReflectiveWallPrototype = "WallXenoResinReflective";
    private static readonly EntProtoId UnstableReflectiveWallPrototype = "WallXenoResinReflectiveUnstable";

    [Test]
    public async Task ReflectiveResinWallPrototypeHasExpectedComponents()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var components = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            Assert.That(prototypes.TryIndex<EntityPrototype>(ReflectiveWallPrototype, out var wall), Is.True);
            Assert.That(wall!.TryGetComponent<RMCReflectiveComponent>(out var reflective, components), Is.True);
            Assert.That(wall.TryGetComponent<XenoSecretionLimitedComponent>(out var limited, components), Is.True);
            Assert.That(wall.TryGetComponent<XenoConstructionPlasmaCostComponent>(out var plasmaCost, components), Is.True);
            Assert.That(wall.TryGetComponent<DamageableComponent>(out var damageable, components), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(reflective!.Chance, Is.EqualTo(0.75f));
                Assert.That(reflective.Angle.Degrees, Is.EqualTo(60).Within(0.001f));
                Assert.That(reflective.Range, Is.EqualTo(10));
                Assert.That(reflective.Accuracy, Is.EqualTo(35));
                Assert.That(reflective.ReflectionMultiplier, Is.EqualTo(0.5f));
                Assert.That(limited!.Id, Is.EqualTo("WallXenoResinReflective"));
                Assert.That(limited.Amount, Is.EqualTo(5));
                Assert.That(plasmaCost!.Plasma, Is.EqualTo(FixedPoint2.New(145)));
                Assert.That(plasmaCost.ScalingCost, Is.True);
                Assert.That(damageable!.DamageModifierSetId, Is.EqualTo("ReflectiveResin"));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReflectiveResinWallIsBuildableByHivelordQueenAndDesignerSurge()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var components = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            Assert.That(prototypes.TryIndex<EntityPrototype>(UnstableReflectiveWallPrototype, out var unstableWall), Is.True);
            Assert.That(unstableWall!.TryGetComponent<RMCReflectiveComponent>(out _, components), Is.True);
            Assert.That(unstableWall.TryGetComponent<TimedDespawnComponent>(out var despawn, components), Is.True);
            Assert.That(despawn!.Lifetime, Is.EqualTo(13f));
        });

        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hivelord = entMan.SpawnEntity("CMXenoHivelord", map.GridCoords);
            var queen = entMan.SpawnEntity("CMXenoQueen", map.GridCoords);
            var designer = entMan.SpawnEntity("CMXenoHivelordDesigner", map.GridCoords);

            try
            {
                var hivelordConstruction = entMan.GetComponent<XenoConstructionComponent>(hivelord);
                var queenConstruction = entMan.GetComponent<XenoConstructionComponent>(queen);
                var designerStrain = entMan.GetComponent<DesignerStrainComponent>(designer);

                Assert.Multiple(() =>
                {
                    Assert.That(hivelordConstruction.CanBuild, Does.Contain("WallXenoResinReflective"));
                    Assert.That(queenConstruction.CanBuild, Does.Contain("WallXenoResinReflective"));
                    Assert.That(designerStrain.GreaterResinSurgeWallPrototype, Is.EqualTo("WallXenoResinReflectiveUnstable"));
                });
            }
            finally
            {
                entMan.DeleteEntity(hivelord);
                entMan.DeleteEntity(queen);
                entMan.DeleteEntity(designer);
            }
        });

        await pair.CleanReturnAsync();
    }
}
