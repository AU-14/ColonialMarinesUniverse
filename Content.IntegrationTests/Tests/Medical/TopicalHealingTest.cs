#nullable enable
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Medical.Healing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Medical;

[TestFixture]
[TestOf(typeof(HealingSystem))]
public sealed class TopicalHealingTest
{
    private const string SupportedDamage = "TopicalHealingTestSupported";
    private const string UnsupportedDamage = "TopicalHealingTestUnsupported";
    private const string TargetPrototype = "TopicalHealingTestTarget";
    private const string TopicalPrototype = "TopicalHealingTestItem";

    [TestPrototypes]
    private const string Prototypes = @"
- type: damageType
  id: TopicalHealingTestSupported
  name: damage-type-blunt

- type: damageType
  id: TopicalHealingTestUnsupported
  name: damage-type-blunt

- type: damageContainer
  id: TopicalHealingTestContainer
  supportedTypes:
  - TopicalHealingTestSupported

- type: entity
  id: TopicalHealingTestTarget
  components:
  - type: Damageable
    damageContainer: TopicalHealingTestContainer

- type: entity
  id: TopicalHealingTestItem
  components:
  - type: Healing
    damage:
      types:
        TopicalHealingTestUnsupported: -1
";

    [Test]
    public async Task UnsupportedDamageTypeDoesNotStartHealing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var target = entMan.SpawnEntity(TargetPrototype, MapCoordinates.Nullspace);
            var topical = entMan.SpawnEntity(TopicalPrototype, MapCoordinates.Nullspace);
            var damage = entMan.System<DamageableSystem>().GetAllDamage(target);

            Assert.That(damage.DamageDict.ContainsKey(SupportedDamage), Is.True);
            Assert.That(damage.DamageDict.ContainsKey(UnsupportedDamage), Is.False);

            var useEvent = new UseInHandEvent(target);
            entMan.EventBus.RaiseLocalEvent(topical, useEvent);

            Assert.That(useEvent.Handled, Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
