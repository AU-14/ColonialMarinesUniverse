using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Utility;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Mortar;
using Content.Shared._RMC14.Shields;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Heatshield;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;

namespace Content.IntegrationTests.Tests.Damageable;

/// <summary>
/// Major part of why we need this test is to check that entities marked as 'Injurable' have 'Damageable' sister component,
/// because they work in pairs within current damage system. Please be careful when modifying test for special cases
/// of having 'Damageable' without proper 'Injurable'. In future updates, when there will be entities that
/// have damage model not relying on our simple 'Injurable' implementation, test must be improved to validate
/// that there at least one way of handling damage attached to entity.
/// </summary>
[TestFixture]
[TestOf(typeof(InjurableComponent))]
[TestOf(typeof(DamageableComponent))]
[TestOf(typeof(DamageableSystem))]
public sealed class DamageAllPrototypesTest : GameTest
{
    [SidedDependency(Side.Server)] private readonly DamageableSystem _damageableSystem = default!;

    [Test]
    [TestOf(typeof(DamageableSystem))]
    [Description("Ensures all Entity Prototypes with damageable can be damaged.")]
    public async Task TestInjurableComponentOwnersCanTakeDamage()
    {
        var map = await Pair.CreateTestMap();
        var failures = new List<string>();

        try
        {
            foreach (var injurable in GameDataScrounger.EntitiesWithComponent("Injurable"))
            {
                var entity = await SpawnAtPosition(injurable, map.GridCoords);

                try
                {
                    // RemoveComponents may opt a derived prototype out of the damage model at map init.
                    if (!SEntMan.HasComponent<InjurableComponent>(entity))
                        continue;

                    // Intentionally cannot take damage, ignore it.
                    if (SEntMan.HasComponent<GodmodeComponent>(entity))
                        continue;

                    // These temporary structures remove Damageable until replaced by their vulnerable form.
                    if (SEntMan.HasComponent<InvincibleHiveStructureComponent>(entity))
                    {
                        Assert.That(SEntMan.HasComponent<DamageableComponent>(entity), Is.False);
                        continue;
                    }

                    // Carried eggs intentionally reject damage; planted eggs must be damageable.
                    await Server.WaitPost(() =>
                    {
                        if (SEntMan.TryGetComponent<XenoEggComponent>(entity, out var egg))
                        {
                            egg.State = XenoEggState.Grown;
                            SEntMan.Dirty(entity, egg);
                        }

                        // Mortars are invulnerable while packed in their portable item form.
                        if (SEntMan.TryGetComponent<MortarComponent>(entity, out var mortar))
                        {
                            typeof(MortarComponent).GetField(nameof(MortarComponent.Deployed))!.SetValue(mortar, true);
                            SEntMan.Dirty(entity, mortar);
                        }

                        if (SEntMan.TryGetComponent<RMCLandmineComponent>(entity, out var mine))
                        {
                            mine.Armed = true;
                            SEntMan.Dirty(entity, mine);
                        }

                        // Test the underlying damage model after any initial shield is depleted.
                        if (SEntMan.TryGetComponent<XenoShieldComponent>(entity, out var shield))
                            SEntMan.System<XenoShieldSystem>().RemoveShield(entity, shield.Shield);
                    });

                    var canBeDamaged = false;

                    foreach (var type in SProtoMan.EnumeratePrototypes<DamageTypePrototype>())
                    {
                        if (!_damageableSystem.CanBeDamagedBy(entity, type))
                            continue;

                        canBeDamaged = true;

                        await Server.WaitAssertion(() =>
                        {
                            var damage = new DamageSpecifier(type, FixedPoint2.Epsilon);
                            var previousDamage = _damageableSystem.GetTotalDamage(entity);
                            _damageableSystem.ChangeDamage(entity, damage, ignoreResistances: true);
                            var expectedDamage = FixedPoint2.Epsilon;
                            // Heatshield's innate heat protection applies after conventional resistances.
                            if (type.ID == "Heat" && SEntMan.TryGetComponent<XenoHeatshieldComponent>(entity, out var heatshield))
                                expectedDamage *= heatshield.FireDamageMultiplier;
                            var actualDamage = _damageableSystem.GetTotalDamage(entity) - previousDamage;
                            if (actualDamage != expectedDamage)
                                failures.Add($"{injurable}: expected {expectedDamage} {type.ID} damage, got {actualDamage}.");

                            _damageableSystem.ClearAllDamage(entity);
                        });
                    }

                    // Ensure that this entity can actually be damaged.
                    Assert.That(canBeDamaged, Is.True, $"{injurable} cannot be damaged by any damage type.");
                }
                finally
                {
                    await Server.WaitPost(() => SEntMan.DeleteEntity(entity));
                }
            }

            Assert.That(failures, Is.Empty);
        }
        finally
        {
            await Server.WaitPost(() => SEntMan.DeleteEntity(map.MapUid));
        }
    }
}
