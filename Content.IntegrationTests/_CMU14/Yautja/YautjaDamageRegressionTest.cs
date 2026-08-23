using Content.Shared._RMC14.Armor;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaDamageRegressionTest
{
    private const string ArmoredHunter = "CMUTestYautjaMeleeArmoredHunter";

    [TestPrototypes]
    private const string TestPrototypes = $@"
- type: entity
  parent: CMUMobYautja
  id: {ArmoredHunter}
  components:
  - type: CMArmor
    melee: 35
";

    [Test]
    public async Task RavagerMeleeStillDamagesAClanArmoredHunter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var damageable = entMan.System<DamageableSystem>();
            var hunter = entMan.SpawnEntity(ArmoredHunter, map.GridCoords);
            var ravager = entMan.SpawnEntity("CMXenoRavager", map.GridCoords.Offset(new(1, 0)));

            try
            {
                var before = entMan.GetComponent<DamageableComponent>(hunter).TotalDamage;
                var delta = damageable.TryChangeDamage(
                    hunter,
                    new DamageSpecifier(prototypes.Index<DamageGroupPrototype>("Brute"), 45),
                    origin: ravager,
                    tool: ravager);

                Assert.Multiple(() =>
                {
                    Assert.That(delta, Is.Not.Null);
                    Assert.That(entMan.GetComponent<DamageableComponent>(hunter).TotalDamage - before,
                        Is.GreaterThan(FixedPoint2.Zero),
                        "A Ravager's ordinary melee attack must not be rounded to zero by hunter armor.");
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(ravager))
                    entMan.DeleteEntity(ravager);
            }
        });

        await pair.CleanReturnAsync();
    }
}
