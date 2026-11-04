#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.Destructible;
using Content.Server._RMC14.Barricade;
using Content.Server._RMC14.Sentry.Laptop;
using Content.Shared._RMC14.Barricade;
using Content.Shared._RMC14.Barricade.Components;
using Content.Shared._RMC14.Random;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.FixedPoint;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
[TestOf(typeof(BarbedSystem))]
[TestOf(typeof(DirectionalAttackBlockSystem))]
[TestOf(typeof(SentryLaptopSystem))]
public sealed class DamageTriggerConsumerMigrationTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: DamageTriggerConsumerBarricade
          components:
          - type: Damageable
            damage:
              types:
                Blunt: 1
          - type: Injurable
            damageContainer: Inorganic
          - type: Physics
            bodyType: Static
          - type: Destructible
            thresholds:
            - trigger:
                !type:DamageTrigger
                damage: 450
          - type: DirectionalAttackBlocker
          - type: Barbed
            thornsDamage:
              types:
                Slash: 1
            maxHealthIncrease: 50

        - type: entity
          id: DamageTriggerConsumerSentry
          components:
          - type: Destructible
            thresholds:
            - trigger:
                !type:DamageTrigger
                damage: 200
            - trigger:
                !type:DamageTrigger
                damage: 275
        """;

    [Test]
    public async Task FixedPointThresholdConsumersPreserveBarricadeAndSentrySemantics()
    {
        var map = await Pair.CreateTestMap();
        EntityUid barricade = default;
        EntityUid attacker = default;

        await Server.WaitAssertion(() =>
        {
            var transform = SEntMan.System<SharedTransformSystem>();
            var directional = SEntMan.System<DirectionalAttackBlockSystem>();

            barricade = SEntMan.SpawnEntity("DamageTriggerConsumerBarricade", map.GridCoords);
            attacker = SEntMan.SpawnEntity(null, map.GridCoords.Offset(Vector2.UnitX));

            var destructible = SEntMan.GetComponent<DestructibleComponent>(barricade);
            var trigger = (DamageTrigger) destructible.Thresholds.Single().Trigger!;
            var blocker = SEntMan.GetComponent<DirectionalAttackBlockerComponent>(barricade);
            var barbed = SEntMan.GetComponent<BarbedComponent>(barricade);

            Assert.Multiple(() =>
            {
                Assert.That(trigger.Damage, Is.EqualTo(FixedPoint2.New(450)));
                Assert.That(blocker.MaxHealth, Is.EqualTo(450),
                    "MapInit must initialize directional blocking from the fixed-point threshold.");
            });

            barbed.IsBarbed = true;
            var stateChanged = new BarbedStateChangedEvent();
            SEntMan.EventBus.RaiseLocalEvent(barricade, ref stateChanged);
            Assert.That(trigger.Damage, Is.EqualTo(FixedPoint2.New(500)),
                "adding barbed wire must increase the maximum-health threshold by exactly 50");

            barbed.IsBarbed = false;
            stateChanged = new BarbedStateChangedEvent();
            SEntMan.EventBus.RaiseLocalEvent(barricade, ref stateChanged);
            Assert.That(trigger.Damage, Is.EqualTo(FixedPoint2.New(450)),
                "removing barbed wire must restore the original threshold without fixed-point drift");

            Assert.That(transform.AnchorEntity(barricade), Is.True);
            var foundFacingPosition = false;
            foreach (var offset in new[] { Vector2.UnitX, Vector2.UnitY, -Vector2.UnitX, -Vector2.UnitY })
            {
                transform.SetCoordinates(attacker, map.GridCoords.Offset(offset));
                if (!directional.IsFacingTarget(barricade, attacker))
                    continue;

                foundFacingPosition = true;
                break;
            }

            Assert.That(foundFacingPosition, Is.True, "the attack probe must be placed inside the blocking cone");

            var sentry = SEntMan.SpawnEntity("DamageTriggerConsumerSentry", map.GridCoords);
            var getMaxHealth = typeof(SentryLaptopSystem).GetMethod(
                "GetSentryMaxHealth",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var maxHealth = (float) getMaxHealth.Invoke(SEntMan.System<SentryLaptopSystem>(), [sentry])!;
            Assert.That(maxHealth, Is.EqualTo(275f),
                "the laptop must expose the highest fixed-point damage threshold as sentry maximum health");
        });

        var sampledThresholdBoundary = false;
        for (var i = 0; i < 256 && !sampledThresholdBoundary; i++)
        {
            await Server.WaitAssertion(() =>
            {
                var attackerId = SEntMan.GetNetEntity(attacker).Id;
                var seed = ((long) SGameTiming.CurTick.Value << 32) | (uint) attackerId;
                var roll = new Xoroshiro64S(seed).NextFloat(0, 1);
                if (roll is <= 0.3f or >= 0.7f)
                    return;

                var damageable = SEntMan.GetComponent<DamageableComponent>(barricade);
                var damage = SEntMan.System<DamageableSystem>();
                var directional = SEntMan.System<DirectionalAttackBlockSystem>();

                damage.SetAllDamage((barricade, damageable), 135);
                Assert.That(directional.IsAttackBlocked(attacker, barricade), Is.True,
                    $"135 accumulated damage leaves a 0.7 block chance, above deterministic roll {roll}");

                damage.SetAllDamage((barricade, damageable), 315);
                Assert.That(directional.IsAttackBlocked(attacker, barricade), Is.False,
                    $"315 accumulated damage reaches the 0.3 minimum block chance, below deterministic roll {roll}");
                sampledThresholdBoundary = true;
            });

            if (!sampledThresholdBoundary)
                await Pair.RunTicksSync(1);
        }

        Assert.That(sampledThresholdBoundary, Is.True,
            "a deterministic roll between the accumulated-damage block thresholds was not observed");
    }
}

#pragma warning restore RA0002
