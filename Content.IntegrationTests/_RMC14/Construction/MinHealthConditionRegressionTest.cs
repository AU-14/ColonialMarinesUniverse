using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Construction.Conditions;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;

namespace Content.IntegrationTests._RMC14.Construction;

[TestFixture]
[TestOf(typeof(MinHealth))]
public sealed class MinHealthConditionRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: RMCMinHealthFinite
          components:
          - type: Damageable
          - type: Injurable
            damageContainer: Inorganic
          - type: Destructible
            thresholds:
            - trigger:
                !type:DamageTrigger
                damage: 100
              behaviors:
              - !type:DoActsBehavior
                acts: [Breakage]

        - type: entity
          id: RMCMinHealthNoFiniteThreshold
          components:
          - type: Damageable
          - type: Injurable
            damageContainer: Inorganic
          - type: Destructible
            thresholds:
            - trigger:
                !type:DamageTrigger
                damage: 100

        - type: entity
          id: RMCMinHealthInvalidThreshold
          components:
          - type: Damageable
          - type: Injurable
            damageContainer: Inorganic
          - type: Destructible
            thresholds:
            - trigger:
                !type:DamageTrigger
                damage: 0
              behaviors:
              - !type:DoActsBehavior
                acts: [Breakage]
        """;

    [Test]
    public async Task ProportionalBoundaryPreservesInclusiveAndExclusiveSemantics()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var entity = SEntMan.SpawnEntity("RMCMinHealthFinite", map.GridCoords);
            var damageable = SEntMan.GetComponent<DamageableComponent>(entity);
            var damage = SEntMan.System<DamageableSystem>();
            var blunt = SProtoMan.Index<DamageTypePrototype>("Blunt");
            var condition = new MinHealth
            {
                Threshold = FixedPoint2.New(0.78),
                ByProportion = true,
            };

            SetDamage(21);
            Assert.That(condition.Condition(entity, SEntMan), Is.True,
                "79/100 health must satisfy the 0.78 minimum");

            SetDamage(22);
            Assert.That(condition.Condition(entity, SEntMan), Is.True,
                "the default inclusive comparison must accept exactly 78/100 health");

            condition.IncludeEquals = false;
            Assert.That(condition.Condition(entity, SEntMan), Is.False,
                "the exclusive comparison must reject exactly 78/100 health");

            condition.IncludeEquals = true;
            SetDamage(23);
            Assert.That(condition.Condition(entity, SEntMan), Is.False,
                "77/100 health must not satisfy the 0.78 minimum");

            void SetDamage(int amount)
            {
                damage.SetDamage((entity, damageable),
                    new DamageSpecifier(blunt, FixedPoint2.New(amount)));
            }
        });
    }

    [Test]
    public async Task MissingOrInvalidDestructionThresholdCannotSatisfyCondition()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var condition = new MinHealth
            {
                Threshold = FixedPoint2.New(0.78),
                ByProportion = true,
            };
            var noFiniteThreshold = SEntMan.SpawnEntity("RMCMinHealthNoFiniteThreshold", map.GridCoords);
            var invalidThreshold = SEntMan.SpawnEntity("RMCMinHealthInvalidThreshold", map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(condition.Condition(noFiniteThreshold, SEntMan), Is.False);
                Assert.That(condition.Condition(invalidThreshold, SEntMan), Is.False);
            });
        });
    }
}
