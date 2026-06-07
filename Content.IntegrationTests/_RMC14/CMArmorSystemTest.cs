using Content.Shared._RMC14.Armor;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
[TestOf(typeof(CMArmorSystem))]
public sealed class CMArmorSystemTest
{
    private const string TestArmorEntity = "RMCArmorInvalidOriginDamageable";
    private static readonly ProtoId<DamageTypePrototype> SlashDamageType = "Slash";

    [TestPrototypes]
    private const string Prototypes = $@"
- type: entity
  id: {TestArmorEntity}
  name: {TestArmorEntity}
  components:
  - type: Damageable
    damageContainer: Biological
  - type: CMArmor
";

    [Test]
    public async Task DamageWithInvalidOriginDoesNotResolveOriginTransform()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var sysMan = server.ResolveDependency<IEntitySystemManager>();

        DamageSpecifier result = null;

        await server.WaitPost(() =>
        {
            var target = entMan.SpawnEntity(TestArmorEntity, map.MapCoords);
            var slash = protoMan.Index(SlashDamageType);
            var damageable = sysMan.GetEntitySystem<DamageableSystem>();

            result = damageable.TryChangeDamage(target, new DamageSpecifier(slash, 10), origin: EntityUid.Invalid);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(result, Is.Not.Null);
        });

        await pair.CleanReturnAsync();
    }
}
