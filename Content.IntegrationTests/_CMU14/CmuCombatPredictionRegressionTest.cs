#nullable enable annotations
#pragma warning disable RA0002 // Regression setup deliberately inspects simulation state.

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Projectiles;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Projectiles.Penetration;
using Content.Shared._RMC14.Xenonids.Charge;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared._RMC14.Xenonids.Lunge;
using Content.Shared._RMC14.Xenonids.Projectile;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Threats.Mobs.Ape;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Robust.Client.GameStates;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14;

[TestFixture]
public sealed class CmuCombatPredictionRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: damageContainer
  id: CmuCombatPredictionDamage
  supportedTypes: [ Blunt ]

- type: entity
  id: CmuCombatPredictionTarget
  components:
  - type: Damageable
  - type: Injurable
    damageContainer: CmuCombatPredictionDamage
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      fix:
        shape: !type:PhysShapeCircle
          radius: 0.25
        layer: [ BulletImpassable ]
        hard: true
  - type: CmuCombatPredictionProbe

- type: entity
  id: CmuCombatPredictionWall
  parent: CmuCombatPredictionTarget
  components:
  - type: Fixtures
    fixtures:
      fix:
        shape: !type:PhysShapeCircle
          radius: 0.25
        layer: [ BulletImpassable, Impassable ]
        hard: true

- type: entity
  id: CmuCombatPredictionBreakableWall
  parent: CmuCombatPredictionWall
  components:
  - type: Destructible
    thresholds:
    - trigger: !type:DamageTrigger
        damage: 10
      behaviors:
      - !type:DoActsBehavior
        acts: [ Destruction ]

- type: entity
  id: CmuCombatPredictionDecorativeFixture
  parent: CmuCombatPredictionTarget
  components:
  - type: Fixtures
    fixtures:
      fix:
        shape: !type:PhysShapeCircle
          radius: 0.25
        layer: [ BulletImpassable ]
        hard: true
      decoration:
        shape: !type:PhysShapeCircle
          radius: 0.1
        layer: [ Impassable ]
        hard: true

- type: entity
  id: CmuCombatPredictionProjectile
  components:
  - type: Projectile
    damage:
      types:
        Blunt: 10
    ignoreResistances: true
    deleteOnCollide: false
  - type: RMCPenetratingProjectile
    range: 100
  - type: Physics
    bodyType: Dynamic
  - type: Fixtures
    fixtures:
      projectile:
        shape: !type:PhysShapeCircle
          radius: 0.1
        mask: [ BulletImpassable ]
        hard: false
""";

    [Test]
    public async Task LungeInterceptorConsumesImpactBeforeSynchronousLanding()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var warrior = SEntMan.SpawnEntity("CMXenoWarrior", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            var selected = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(5.5f, 0.5f)));
            var interceptor = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2.5f, 0.5f)));
            var transform = Server.System<SharedTransformSystem>();
            var selectedPosition = transform.GetMapCoordinates(selected);
            var interceptorPosition = transform.GetMapCoordinates(interceptor);
            StartLunge(warrior, selected);
            var active = SEntMan.GetComponent<XenoActiveLungeComponent>(warrior);
            var thrown = SEntMan.GetComponent<ThrownItemComponent>(warrior);

            // LandComponent raises LandEvent synchronously inside the real throw-hit handler.
            Server.System<ThrownItemSystem>().ThrowCollideInteraction(thrown, warrior, interceptor);

            Assert.Multiple(() =>
            {
                Assert.That(active.HitResolved, Is.True);
                Assert.That(SEntMan.HasComponent<XenoLungeStunnedComponent>(selected), Is.False);
                Assert.That(SEntMan.HasComponent<XenoLungeStunnedComponent>(interceptor), Is.True);
                Assert.That(SEntMan.GetComponent<PullerComponent>(warrior).Pulling, Is.EqualTo(interceptor));
                Assert.That(transform.GetMapCoordinates(selected), Is.EqualTo(selectedPosition));
                Assert.That(transform.GetMapCoordinates(interceptor), Is.EqualTo(interceptorPosition),
                    "An interceptor must never teleport to the originally selected target.");
            });

            Server.System<ThrownItemSystem>().ThrowCollideInteraction(thrown, warrior, selected);
            Assert.That(SEntMan.HasComponent<XenoLungeStunnedComponent>(selected), Is.False,
                "A second collision must not resolve a consumed lunge.");
        });
    }

    [Test]
    public async Task MissedLungeLandingDoesNotHitOrTeleportSelectedTarget()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var warrior = SEntMan.SpawnEntity("CMXenoWarrior", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            var target = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(4.5f, 0.5f)));
            StartLunge(warrior, target);
            var transform = Server.System<SharedTransformSystem>();
            transform.SetCoordinates(target, map.GridCoords.Offset(new Vector2(10.5f, 5.5f)));
            var targetPosition = transform.GetMapCoordinates(target);
            var active = SEntMan.GetComponent<XenoActiveLungeComponent>(warrior);

            Server.System<ThrownItemSystem>().LandComponent(warrior,
                SEntMan.GetComponent<ThrownItemComponent>(warrior),
                SEntMan.GetComponent<PhysicsComponent>(warrior), false);

            Assert.Multiple(() =>
            {
                Assert.That(active.Running, Is.False);
                Assert.That(SEntMan.HasComponent<XenoLungeStunnedComponent>(target), Is.False);
                Assert.That(SEntMan.GetComponent<PullerComponent>(warrior).Pulling, Is.Null);
                Assert.That(transform.GetMapCoordinates(target), Is.EqualTo(targetPosition));
            });
        });
    }

    [Test]
    public async Task LungeDirectionUsesMapSpaceOnTranslatedRotatedGrid()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var transform = Server.System<SharedTransformSystem>();
            transform.SetLocalPosition(map.Grid.Owner, new Vector2(100, 50));
            transform.SetLocalRotation(map.Grid.Owner, Angle.FromDegrees(90));
            var warrior = SEntMan.SpawnEntity("CMXenoWarrior", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            var target = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(4.5f, 0.5f)));
            StartLunge(warrior, target);
            var active = SEntMan.GetComponent<XenoActiveLungeComponent>(warrior);
            var expected = Vector2.Normalize(transform.GetMapCoordinates(target).Position - active.Origin.Position) * active.Range;
            Assert.That(Vector2.Distance(active.Charge, expected), Is.LessThan(0.001f));
            Assert.That(active.TargetCoordinates, Is.EqualTo(transform.GetMapCoordinates(target)));
        });
    }

    [Test]
    public async Task ChargeCooldownPairDoesNotDiscardOtherHitOrSkipReset()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            _ = Server.System<CmuCombatPredictionProbeSystem>();
            var charger = SEntMan.SpawnEntity("CmuCombatPredictionProjectile", map.GridCoords);
            SEntMan.RemoveComponent<ProjectileComponent>(charger);
            SEntMan.RemoveComponent<RMCPenetratingProjectileComponent>(charger);
            SEntMan.EnsureComponent<XenoToggleChargingComponent>(charger);
            var active = SEntMan.EnsureComponent<ActiveXenoToggleChargingComponent>(charger);
            active.Steps = 3;
            active.Stage = 2;
            var recent = SEntMan.SpawnEntity("CmuCombatPredictionTarget", map.GridCoords.Offset(new Vector2(1, 0)));
            var other = SEntMan.SpawnEntity("CmuCombatPredictionTarget", map.GridCoords.Offset(new Vector2(2, 0)));
            SEntMan.EnsureComponent<XenoToggleChargingRecentlyHitComponent>(recent).LastHitAt = SGameTiming.CurTime;
            Server.System<SharedPhysicsSystem>().SetBodyStatus(charger,
                SEntMan.GetComponent<PhysicsComponent>(charger), BodyStatus.InAir);
            RaiseCollision(charger, recent);
            RaiseCollision(charger, other);

            Server.System<XenoChargeSystem>().Update(0);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<CmuCombatPredictionProbeComponent>(recent).ChargeHits, Is.Zero);
                Assert.That(SEntMan.GetComponent<CmuCombatPredictionProbeComponent>(other).ChargeHits, Is.EqualTo(1));
                Assert.That(active.Stage, Is.Zero, "The final airborne reset must run even when a queued pair is on cooldown.");
            });
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.PlaytestProjectileDamageModifier), 1f)]
    public async Task PenetrationPhysicalAndReportedHitsLoseDamageAndRangeOnce(bool reported)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var projectile = SEntMan.SpawnEntity("CmuCombatPredictionProjectile", map.GridCoords);
            var first = SEntMan.SpawnEntity("CmuCombatPredictionTarget", map.GridCoords);
            var second = SEntMan.SpawnEntity("CmuCombatPredictionTarget", map.GridCoords);
            var component = SEntMan.GetComponent<ProjectileComponent>(projectile);
            var penetrating = SEntMan.GetComponent<RMCPenetratingProjectileComponent>(projectile);
            if (reported)
                Hit(projectile, first, true);
            else
                RaiseCollision(projectile, first);
            Assert.That(Damage(first), Is.EqualTo((FixedPoint2) 10));
            Assert.That(component.Damage.DamageDict["Blunt"], Is.EqualTo((FixedPoint2) 8));
            Assert.That(penetrating.Range, Is.EqualTo(97));

            Hit(projectile, first, true);
            RaiseCollision(projectile, first);
            Assert.That(penetrating.Range, Is.EqualTo(97), "Late reports and physical contacts must not consume a hit twice.");
            Assert.That(Damage(first), Is.EqualTo((FixedPoint2) 10));

            Hit(projectile, second, true);
            Assert.That(Damage(second), Is.EqualTo((FixedPoint2) 8), "The next victim must receive reduced damage.");
            Assert.That(penetrating.Range, Is.EqualTo(94));
        });
    }

    [TestCase("CmuCombatPredictionWall", 91, 4)]
    [TestCase("CmuCombatPredictionBreakableWall", 91, 4)]
    [TestCase("CmuCombatPredictionDecorativeFixture", 97, 8)]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.PlaytestProjectileDamageModifier), 1f)]
    public async Task PenetrationMaterialIsCapturedBeforeDamageAndRequiresCollidingFixture(string prototype, int range, int damage)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var projectile = SEntMan.SpawnEntity("CmuCombatPredictionProjectile", map.GridCoords);
            var target = SEntMan.SpawnEntity(prototype, map.GridCoords);
            Hit(projectile, target, true);
            Assert.That(SEntMan.GetComponent<RMCPenetratingProjectileComponent>(projectile).Range, Is.EqualTo(range));
            // FixedPoint2 truncates float products to hundredths; 10 * (1 - .2 * 3)
            // can be 3.99. Keep the material/range oracle exact and allow one damage quantum.
            Assert.That(SEntMan.GetComponent<ProjectileComponent>(projectile).Damage.DamageDict["Blunt"].Float(),
                Is.InRange(damage - 0.01001f, damage));
        });
    }

    [Test]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.PlaytestProjectileDamageModifier), 1f)]
    public async Task PenetrationRangeExhaustionStopsTheNextHit()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var projectile = SEntMan.SpawnEntity("CmuCombatPredictionProjectile", map.GridCoords);
            var wall = SEntMan.SpawnEntity("CmuCombatPredictionWall", map.GridCoords);
            var next = SEntMan.SpawnEntity("CmuCombatPredictionTarget", map.GridCoords);
            SEntMan.GetComponent<RMCPenetratingProjectileComponent>(projectile).Range = 8;
            Hit(projectile, wall, true);
            Assert.That(SEntMan.GetComponent<ProjectileComponent>(projectile).ProjectileSpent, Is.True);
            Hit(projectile, next, true);
            Assert.That(Damage(next), Is.EqualTo(FixedPoint2.Zero));
        });
    }

    [Test]
    [EnsureCVar(Side.Server, typeof(RMCCVars), nameof(RMCCVars.RMCGunPrediction), true)]
    public async Task XenoReplayRebuildsShotIdsWithoutSpawningDuplicateProjectiles()
    {
        var map = await Pair.CreateTestMap();
        NetEntity netShooter = default;
        IComponentState? baseline = null;
        await Server.WaitAssertion(() =>
        {
            var shooter = SEntMan.SpawnEntity(null, map.GridCoords);
            var component = SEntMan.EnsureComponent<XenoProjectileShooterComponent>(shooter);
            var getState = new ComponentGetState(null, GameTick.Zero);
            SEntMan.EventBus.RaiseComponentEvent(shooter, component, ref getState);
            baseline = getState.State;
            netShooter = SEntMan.GetNetEntity(shooter);
            Server.PlayerMan.SetAttachedEntity(Pair.Player!, shooter);
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var shooter = CEntMan.GetEntity(netShooter);
            var component = CEntMan.GetComponent<XenoProjectileShooterComponent>(shooter);
            var target = CEntMan.GetComponent<TransformComponent>(shooter).Coordinates.Offset(new Vector2(5, 0));
            var projectiles = Client.System<XenoProjectileSystem>();
            Assert.That(projectiles.TryShoot(shooter, target, 0, "CmuCombatPredictionProjectile", null, 3, Angle.Zero, 10), Is.True);
            var firstEntities = component.Shot.ToArray();
            Assert.That(firstEntities.Select(e => CEntMan.GetComponent<XenoProjectileShotComponent>(e).Id), Is.EqualTo(new[] { 0, 1, 2 }));

            // Apply the unacknowledged server state, just as reconciliation does, then replay its action.
            using (CGameTiming.StartStateApplicationArea())
            {
                var restore = new ComponentHandleState(baseline, null);
                CEntMan.EventBus.RaiseComponentEvent(shooter, component, ref restore);
            }
            using (CGameTiming.StartPastPredictionArea())
            {
                Assert.That(projectiles.TryShoot(shooter, target, 0, "CmuCombatPredictionProjectile", null, 3, Angle.Zero, 10), Is.True);
                Assert.That(component.NextId, Is.EqualTo(3));
                Assert.That(component.Shot, Is.Empty, "Replaying allocations must not spawn another presentation volley.");
            }

            Assert.That(projectiles.TryShoot(shooter, target, 0, "CmuCombatPredictionProjectile", null, 1, Angle.Zero, 10), Is.True);
            Assert.That(component.Shot, Has.Count.EqualTo(1));
            Assert.That(CEntMan.GetComponent<XenoProjectileShotComponent>(component.Shot[0]).Id, Is.EqualTo(3),
                "A second unacknowledged shot must not reuse the first volley's identity.");
            foreach (var entity in firstEntities.Concat(component.Shot).ToArray())
                CEntMan.DeleteEntity(entity);
        });
    }

    [TestCase("lunge")]
    [TestCase("leap")]
    [TestCase("ape")]
    public async Task PredictedAbilityHitSendsOnceAndReplayReconstructsWithoutResending(string ability)
    {
        var map = await Pair.CreateTestMap();
        NetEntity netAttacker = default;
        NetEntity netTarget = default;
        await Server.WaitAssertion(() =>
        {
            Server.System<CmuCombatPredictionProbeSystem>().NetworkHits = 0;
            var attacker = SEntMan.SpawnEntity("CMXenoWarrior", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            var target = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2.5f, 0.5f)));
            netAttacker = SEntMan.GetNetEntity(attacker);
            netTarget = SEntMan.GetNetEntity(target);
            Server.PlayerMan.SetAttachedEntity(Pair.Player!, attacker);
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var attacker = CEntMan.GetEntity(netAttacker);
            var target = CEntMan.GetEntity(netTarget);
            Assert.That(CGameTiming.InPrediction, Is.True, "Exercise predictive dispatch, which already sends one network event.");
            ResolveClientAbilityHit(attacker, target, ability);

            // Recreate the pre-hit component state that rollback restores. Replay must
            // rebuild the same hit effects but must not emit another transport message.
            using (CGameTiming.StartStateApplicationArea())
            {
                CEntMan.RemoveComponent<XenoActiveLungeComponent>(attacker);
                CEntMan.RemoveComponent<XenoLeapingComponent>(attacker);
                CEntMan.RemoveComponent<ApeLeapingComponent>(attacker);
                CEntMan.RemoveComponent<LeapIncapacitatedComponent>(target);
            }
            using (CGameTiming.StartPastPredictionArea())
                ResolveClientAbilityHit(attacker, target, ability);
        });

        await Pair.RunTicksSync(10);
        await Server.WaitAssertion(() => Assert.That(Server.System<CmuCombatPredictionProbeSystem>().NetworkHits, Is.EqualTo(1),
            "A first predicted impact sends once; collision replay sends nothing."));
    }

    [Test]
    public async Task SavedLungeHitEventReplaysRestoredStateWithoutCollisionOrResend()
    {
        var map = await Pair.CreateTestMap();
        NetEntity netAttacker = default;
        NetEntity netTarget = default;
        IComponentState? baseline = null;
        await Server.WaitAssertion(() =>
        {
            Server.System<CmuCombatPredictionProbeSystem>().NetworkHits = 0;
            var attacker = SEntMan.SpawnEntity("CMXenoWarrior", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            var target = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2.5f, 0.5f)));
            var active = SEntMan.EnsureComponent<XenoActiveLungeComponent>(attacker);
            active.Target = target;
            active.StunTime = TimeSpan.FromSeconds(1);
            var getState = new ComponentGetState(null, GameTick.Zero);
            SEntMan.EventBus.RaiseComponentEvent(attacker, active, ref getState);
            baseline = getState.State;
            netAttacker = SEntMan.GetNetEntity(attacker);
            netTarget = SEntMan.GetNetEntity(target);
            Server.PlayerMan.SetAttachedEntity(Pair.Player!, attacker);
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var attacker = CEntMan.GetEntity(netAttacker);
            var target = CEntMan.GetEntity(netTarget);
            Assert.That(CGameTiming.InPrediction, Is.True);
            ResolveClientAbilityHit(attacker, target, "lunge");

            Assert.That(CEntMan.GetComponent<PullerComponent>(attacker).Pulling, Is.EqualTo(target));
            // Tear down the predicted joint as well as its pull fields before restoring
            // the snapshot. TryStopPull during ApplyingState intentionally preserves
            // joints for their own state handler, which this focused restore does not run.
            Assert.That(Client.System<PullingSystem>().TryStopPull(target,
                CEntMan.GetComponent<PullableComponent>(target)), Is.True);

            XenoActiveLungeComponent restored;
            using (CGameTiming.StartStateApplicationArea())
            {
                CEntMan.RemoveComponent<XenoActiveLungeComponent>(attacker);
                restored = CEntMan.EnsureComponent<XenoActiveLungeComponent>(attacker);
                var restore = new ComponentHandleState(baseline, null);
                CEntMan.EventBus.RaiseComponentEvent(attacker, restored, ref restore);
            }
            Assert.That(restored.HitResolved, Is.False);
            Assert.That(CEntMan.GetComponent<PullerComponent>(attacker).Pulling, Is.Null);

            // PredictTicks re-dispatches the engine's saved session event under its
            // past-prediction guard. Ending at the first tick skips TickUpdate/physics,
            // so no new collision callback can accidentally make this assertion pass.
            var predictedTick = CGameTiming.CurTick;
            try
            {
                CGameTiming.CurTick = predictedTick - 1;
                ((ClientGameStateManager) Client.ResolveDependency<IClientGameStateManager>()).PredictTicks(predictedTick);
            }
            finally
            {
                CGameTiming.CurTick = predictedTick;
            }

            Assert.Multiple(() =>
            {
                Assert.That(restored.HitResolved, Is.True, "The saved hit event must consume the restored lunge on the client.");
                Assert.That(CEntMan.GetComponent<PullerComponent>(attacker).Pulling, Is.EqualTo(target),
                    "Saved-event replay must reconstruct pulling without another physical collision.");
            });
        });

        await Pair.RunTicksSync(10);
        await Server.WaitAssertion(() => Assert.That(Server.System<CmuCombatPredictionProbeSystem>().NetworkHits, Is.EqualTo(1),
            "Re-dispatching the saved event must not send it back to the server."));
    }

    private void ResolveClientAbilityHit(EntityUid attacker, EntityUid target, string ability)
    {
        if (ability == "lunge")
        {
            var active = CEntMan.EnsureComponent<XenoActiveLungeComponent>(attacker);
            active.Target = target;
            active.StunTime = TimeSpan.FromSeconds(1);
            var hit = new ThrowDoHitEvent(attacker, target, new ThrownItemComponent());
            CEntMan.EventBus.RaiseLocalEvent(attacker, ref hit);
            Assert.That(active.HitResolved, Is.True, "The local lunge must resolve on first prediction and replay.");
            return;
        }

        if (ability == "leap")
        {
            var active = CEntMan.EnsureComponent<XenoLeapingComponent>(attacker);
            active.ParalyzeTime = TimeSpan.FromSeconds(1);
            RaiseCollision(attacker, target, CEntMan);
            Assert.That(active.KnockedDown, Is.True, "The local leap must resolve on first prediction and replay.");
        }
        else
        {
            var active = CEntMan.EnsureComponent<ApeLeapingComponent>(attacker);
            active.ParalyzeTime = TimeSpan.FromSeconds(1);
            RaiseCollision(attacker, target, CEntMan);
            Assert.That(active.KnockedDown, Is.True, "The local Ape leap must resolve on first prediction and replay.");
        }
        Assert.That(CEntMan.HasComponent<LeapIncapacitatedComponent>(target), Is.True);
    }

    private void StartLunge(EntityUid warrior, EntityUid target)
    {
        var action = new XenoLungeActionEvent
        {
            Entity = target,
            Target = SEntMan.GetComponent<TransformComponent>(target).Coordinates,
        };
        SEntMan.EventBus.RaiseLocalEvent(warrior, action);
        Assert.That(action.Handled, Is.True);
        Assert.That(SEntMan.HasComponent<XenoActiveLungeComponent>(warrior), Is.True);
    }

    private void Hit(EntityUid projectile, EntityUid target, bool reported)
    {
        Server.System<ProjectileSystem>().ProjectileCollide(
            (projectile, SEntMan.GetComponent<ProjectileComponent>(projectile), SEntMan.GetComponent<PhysicsComponent>(projectile)),
            target, reported);
    }

    private FixedPoint2 Damage(EntityUid target)
    {
        return SEntMan.GetComponent<DamageableComponent>(target).Damage.DamageDict.GetValueOrDefault("Blunt");
    }

    // Contact generation has an engine-internal constructor. Inject only its immutable
    // boundary event so tests exercise the real subscriptions with a deterministic order.
    private void RaiseCollision(EntityUid projectile, EntityUid target, IEntityManager? entities = null)
    {
        entities ??= SEntMan;
        var ourFixture = entities.GetComponent<FixturesComponent>(projectile).Fixtures.First();
        var theirFixture = entities.GetComponent<FixturesComponent>(target).Fixtures.First();
        var constructor = typeof(StartCollideEvent).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
        var collision = (StartCollideEvent) constructor.Invoke(new object[]
        {
            projectile, target, ourFixture.Key, theirFixture.Key,
            ourFixture.Value,
            theirFixture.Value,
            entities.GetComponent<PhysicsComponent>(projectile),
            entities.GetComponent<PhysicsComponent>(target),
            Activator.CreateInstance(constructor.GetParameters()[8].ParameterType)!, 0, Vector2.UnitX,
        });
        entities.EventBus.RaiseLocalEvent(projectile, ref collision);
    }
}

[RegisterComponent]
public sealed partial class CmuCombatPredictionProbeComponent : Component
{
    public int ChargeHits;
}

public sealed class CmuCombatPredictionProbeSystem : EntitySystem
{
    public int NetworkHits;

    public override void Initialize()
    {
        SubscribeLocalEvent<CmuCombatPredictionProbeComponent, XenoToggleChargingCollideEvent>(OnCharge);
        SubscribeAllEvent<XenoLungePredictedHitEvent>((_, _) => NetworkHits++);
        SubscribeAllEvent<XenoLeapPredictedHitEvent>((_, _) => NetworkHits++);
        SubscribeAllEvent<ApeLeapPredictedHitEvent>((_, _) => NetworkHits++);
    }

    private static void OnCharge(Entity<CmuCombatPredictionProbeComponent> target, ref XenoToggleChargingCollideEvent args)
    {
        target.Comp.ChargeHits++;
        args.Handled = true;
    }
}

#pragma warning restore RA0002
