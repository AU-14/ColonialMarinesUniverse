using System.Numerics;
using System.Collections.Generic;
using Content.Shared._RMC14.Actions;
using Content.Server.Body.Systems;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared._RMC14.Shields;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Reaper;
using Content.Shared.Alert;
using Content.Shared.Actions.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Traits.Assorted;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class XenoReaperTest
{
    private static readonly EntProtoId ReaperRedGasPrototype = "XenoReaperRedGas";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: CMXenoReaper
  id: RMCTestXenoReaperStocked
  components:
  - type: XenoReaper
    fleshResin: 1000

- type: entity
  parent: CMXenoReaper
  id: RMCTestXenoReaperFlesh300
  components:
  - type: XenoReaper
    fleshResin: 300

- type: entity
  parent: CMXenoReaper
  id: RMCTestXenoReaperFlesh301
  components:
  - type: XenoReaper
    fleshResin: 301

- type: entity
  parent: CMXenoReaper
  id: RMCTestXenoReaperFlesh40
  components:
  - type: XenoReaper
    fleshResin: 40
";

    [Test]
    public async Task ReaperIsCarrierStrainWithDroneConstruction()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var reaper = entMan.SpawnEntity("CMXenoReaper", map.GridCoords);

            try
            {
                var xeno = entMan.GetComponent<XenoComponent>(reaper);
                var devolve = entMan.GetComponent<XenoDevolveComponent>(reaper);
                Assert.That(entMan.TryGetComponent<XenoConstructionComponent>(reaper, out var construction), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(xeno.Role.Id, Is.EqualTo("CMXenoCarrier"));
                    Assert.That(devolve.DevolvesTo, Is.EqualTo(new[] { "CMXenoCarrier" }));
                    Assert.That(construction!.BuildDelay, Is.EqualTo(TimeSpan.FromSeconds(2)));
                    Assert.That(construction.CanBuild, Does.Contain("WallXenoResin"));
                    Assert.That(construction.CanBuild, Does.Contain("WallXenoMembrane"));
                    Assert.That(construction.CanBuild, Does.Contain("DoorXenoResin"));
                    Assert.That(construction.CanBuild, Does.Contain("XenoStickyResin"));
                    Assert.That(construction.CanBuild, Does.Contain("XenoFastResin"));
                    Assert.That(construction.CanBuild, Does.Not.Contain("WallXenoResinThick"));
                    Assert.That(construction.CanBuild, Does.Not.Contain("HiveAcidPillarXeno"));
                    Assert.That(construction.CanOrderConstruction, Does.Contain("HiveCoreXenoConstructionNode"));
                    Assert.That(construction.CanUpgrade, Is.False);
                    Assert.That(entMan.HasComponent<XenoEggRetrieverComponent>(reaper), Is.False);
                    Assert.That(xeno.ActionIds, Does.Contain("ActionXenoReaperRedGas"));
                });
            }
            finally
            {
                entMan.DeleteEntity(reaper);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReaperEvolvesFromCarrierNotHivelord()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var carrier = entMan.SpawnEntity("CMXenoCarrier", map.GridCoords);
            var hivelord = entMan.SpawnEntity("CMXenoHivelord", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                var carrierEvolution = entMan.GetComponent<XenoEvolutionComponent>(carrier);
                var hivelordEvolution = entMan.GetComponent<XenoEvolutionComponent>(hivelord);

                Assert.Multiple(() =>
                {
                    Assert.That(carrierEvolution.EvolvesTo, Does.Contain("CMXenoReaper"));
                    Assert.That(hivelordEvolution.EvolvesTo, Does.Not.Contain("CMXenoReaper"));
                });
            }
            finally
            {
                entMan.DeleteEntity(carrier);
                entMan.DeleteEntity(hivelord);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FleshHarvestRejectsDeadMarineThatIsNotPermaDead()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mob = entMan.System<MobStateSystem>();
            var reaper = entMan.SpawnEntity("CMXenoReaper", map.GridCoords);
            var marine = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var action = SpawnAction(entMan);

            try
            {
                mob.ChangeMobState(marine, MobState.Dead);
                var ev = RaiseFleshHarvest(entMan, reaper, marine, action);

                Assert.That(ev.Handled, Is.False);
            }
            finally
            {
                entMan.DeleteEntity(reaper);
                entMan.DeleteEntity(marine);
                entMan.DeleteEntity(action.Owner);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FleshHarvestRemovesPermaDeadMarineLimbs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid reaper = default;
        EntityUid marine = default;
        Entity<ActionComponent> action = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mob = entMan.System<MobStateSystem>();
            reaper = entMan.SpawnEntity("CMXenoReaper", map.GridCoords);
            marine = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            action = SpawnAction(entMan);

            mob.ChangeMobState(marine, MobState.Dead);
            entMan.EnsureComponent<UnrevivableComponent>(marine);

            var before = CountLimbs(entMan, marine);
            Assert.That(before, Is.GreaterThan(0));

            var ev = RaiseFleshHarvest(entMan, reaper, marine, action);
            Assert.That(ev.Handled, Is.True);
        });

        await server.WaitRunTicks(300);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(CountLimbs(entMan, marine), Is.Zero);

            entMan.DeleteEntity(reaper);
            entMan.DeleteEntity(marine);
            entMan.DeleteEntity(action.Owner);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FleshBloomActionHasSevenTileRange()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var action = entMan.SpawnEntity("ActionXenoFleshBloom", MapCoordinates.Nullspace);

            try
            {
                var target = entMan.GetComponent<TargetActionComponent>(action);
                var range = entMan.GetComponent<ActionInRangeUnobstructedComponent>(action);

                Assert.Multiple(() =>
                {
                    Assert.That(target.Range, Is.EqualTo(7));
                    Assert.That(range.Range, Is.EqualTo(7));
                });
            }
            finally
            {
                entMan.DeleteEntity(action);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FleshBloomUsesDelayedThreeByThreeToxinBloom()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid reaper = default;
        var targets = new List<EntityUid>();
        Entity<ActionComponent> action = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var center = map.GridCoords.Offset(new Vector2(2, 0));
            reaper = entMan.SpawnEntity("RMCTestXenoReaperStocked", map.GridCoords);
            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    targets.Add(entMan.SpawnEntity("CMMobHuman", center.Offset(new Vector2(x, y))));
                }
            }

            action = SpawnAction(entMan);

            RaiseFleshBloom(entMan, reaper, center, action);

            Assert.That(CountBloomEntities(entMan), Is.Zero);
        });

        await server.WaitRunTicks(90);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var blooms = GetBlooms(entMan);

            Assert.That(blooms, Has.Count.EqualTo(9));
            Assert.Multiple(() =>
            {
                Assert.That(blooms, Has.All.Matches<Entity<XenoFleshBloomComponent>>(bloom => bloom.Comp.Range <= 0.5f));
                Assert.That(CountTelegraphs(entMan), Is.EqualTo(9));
            });

            foreach (var target in targets)
            {
                var damage = entMan.GetComponent<DamageableComponent>(target).Damage.DamageDict;
                Assert.Multiple(() =>
                {
                    Assert.That(damage.TryGetValue("Poison", out var poison) && poison > 0, Is.True);
                    Assert.That(!damage.TryGetValue("Cellular", out var cellular) || cellular <= 0, Is.True);
                });
            }

            entMan.DeleteEntity(reaper);
            foreach (var target in targets)
            {
                entMan.DeleteEntity(target);
            }

            entMan.DeleteEntity(action.Owner);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RaptureSpawnsHitEffect()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var reaper = entMan.SpawnEntity("CMXenoReaper", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var action = SpawnAction(entMan);

            try
            {
                var ev = RaiseRapture(entMan, reaper, target, action);

                Assert.Multiple(() =>
                {
                    Assert.That(ev.Handled, Is.True);
                    Assert.That(CountPrototype(entMan, "RMCEffectExtraSlash"), Is.EqualTo(1));
                });
            }
            finally
            {
                entMan.DeleteEntity(reaper);
                entMan.DeleteEntity(target);
                entMan.DeleteEntity(action.Owner);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CarrionMantleAppliesKingShield()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var reaper = entMan.SpawnEntity("RMCTestXenoReaperStocked", map.GridCoords);
            var action = SpawnAction(entMan);

            try
            {
                RaiseCarrionMantle(entMan, reaper, reaper, action);

                Assert.That(entMan.TryGetComponent<XenoShieldComponent>(reaper, out var shield), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(shield!.Active, Is.True);
                    Assert.That(shield.Shield, Is.EqualTo(XenoShieldSystem.ShieldType.King));
                    Assert.That(entMan.HasComponent<KingShieldComponent>(reaper), Is.True);
                });
            }
            finally
            {
                entMan.DeleteEntity(reaper);
                entMan.DeleteEntity(action.Owner);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PassiveFleshDrainOnlyRemovesOneAboveThreeHundred()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid atThreshold = default;
        EntityUid overThreshold = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            atThreshold = entMan.SpawnEntity("RMCTestXenoReaperFlesh300", map.GridCoords);
            overThreshold = entMan.SpawnEntity("RMCTestXenoReaperFlesh301", map.GridCoords.Offset(new Vector2(1, 0)));
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<XenoReaperComponent>(atThreshold).FleshResin, Is.EqualTo(300));
                Assert.That(entMan.GetComponent<XenoReaperComponent>(overThreshold).FleshResin, Is.EqualTo(300));
            });

            entMan.DeleteEntity(atThreshold);
            entMan.DeleteEntity(overThreshold);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReaperShowsStoredFleshAlert()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var reaper = entMan.SpawnEntity("RMCTestXenoReaperStocked", map.GridCoords);

            try
            {
                var alerts = entMan.GetComponent<AlertsComponent>(reaper).Alerts.Values;

                Assert.That(alerts, Has.Some.Matches<AlertState>(alert =>
                    alert.Type == "XenoFlesh" &&
                    alert.DynamicMessage == "1000 / 1000"));
            }
            finally
            {
                entMan.DeleteEntity(reaper);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RedGasIsTranslucentAndDoesNotOccludeVision()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var prototypes = client.ResolveDependency<IPrototypeManager>();
        var factory = client.ResolveDependency<IComponentFactory>();

        await client.WaitAssertion(() =>
        {
            Assert.That(prototypes.TryIndex<EntityPrototype>(ReaperRedGasPrototype, out var proto), Is.True);
            Assert.That(proto!.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(proto.TryGetComponent<OccluderComponent>(out _, factory), Is.False);
                Assert.That(sprite!.Color.A, Is.LessThan(1f));
                Assert.That(sprite.Color.A, Is.GreaterThan(0.5f));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RedGasWaitsForEachMarchStep()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid reaper = default;
        Entity<ActionComponent> action = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            reaper = entMan.SpawnEntity("RMCTestXenoReaperStocked", map.GridCoords);
            action = SpawnAction(entMan);

            RaiseRedGas(entMan, reaper, map.GridCoords.Offset(new Vector2(6, 0)), action);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(CountPrototype(server.EntMan, "XenoReaperRedGas"), Is.Zero);
        });

        await server.WaitRunTicks(10);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            Assert.That(CountPrototype(entMan, "XenoReaperRedGas"), Is.GreaterThan(0));

            entMan.DeleteEntity(reaper);
            entMan.DeleteEntity(action.Owner);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RedGasMarchesFromReaperAndWidensOverDistance()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid reaper = default;
        Entity<ActionComponent> action = default;
        var origin = map.GridCoords.Position;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            reaper = entMan.SpawnEntity("RMCTestXenoReaperStocked", map.GridCoords);
            action = SpawnAction(entMan);

            RaiseRedGas(entMan, reaper, map.GridCoords.Offset(new Vector2(6, 0)), action);
        });

        await server.WaitRunTicks(80);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var positions = GetPrototypePositions(entMan, "XenoReaperRedGas");

            Assert.Multiple(() =>
            {
                Assert.That(positions, Has.Count.EqualTo(15));
                Assert.That(CountAtStep(positions, origin, 0), Is.EqualTo(1));
                Assert.That(CountAtStep(positions, origin, 3), Is.EqualTo(2));
                Assert.That(CountAtStep(positions, origin, 6), Is.EqualTo(3));
            });

            entMan.DeleteEntity(reaper);
            entMan.DeleteEntity(action.Owner);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RedGasConsumesFleshPerTileAndStopsWhenEmpty()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid reaper = default;
        Entity<ActionComponent> action = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            reaper = entMan.SpawnEntity("RMCTestXenoReaperFlesh40", map.GridCoords);
            action = SpawnAction(entMan);

            RaiseRedGas(entMan, reaper, map.GridCoords.Offset(new Vector2(6, 0)), action);
        });

        await server.WaitRunTicks(80);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            Assert.Multiple(() =>
            {
                Assert.That(CountPrototype(entMan, "XenoReaperRedGas"), Is.EqualTo(4));
                Assert.That(entMan.GetComponent<XenoReaperComponent>(reaper).FleshResin, Is.Zero);
            });

            entMan.DeleteEntity(reaper);
            entMan.DeleteEntity(action.Owner);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RedGasPoisonsEnemiesButIgnoresSameHiveXenos()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hiveEnt = default;
        EntityUid reaper = default;
        EntityUid marine = default;
        EntityUid friendly = default;
        Entity<ActionComponent> action = default;
        FixedPoint2 friendlyBefore = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hive = entMan.System<SharedXenoHiveSystem>();

            hiveEnt = entMan.SpawnEntity("CMXenoHive", map.GridCoords);
            reaper = entMan.SpawnEntity("RMCTestXenoReaperStocked", map.GridCoords);
            marine = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            friendly = entMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(2, 0)));
            action = SpawnAction(entMan);

            hive.SetHive(reaper, hiveEnt);
            hive.SetSameHive(reaper, friendly);
            friendlyBefore = TotalDamage(entMan, friendly);

            RaiseRedGas(entMan, reaper, map.GridCoords.Offset(new Vector2(2, 0)), action);
        });

        await server.WaitRunTicks(80);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            Assert.Multiple(() =>
            {
                Assert.That(PoisonDamage(entMan, marine), Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(TotalDamage(entMan, friendly), Is.EqualTo(friendlyBefore));
            });

            entMan.DeleteEntity(hiveEnt);
            entMan.DeleteEntity(reaper);
            entMan.DeleteEntity(marine);
            entMan.DeleteEntity(friendly);
            entMan.DeleteEntity(action.Owner);
        });

        await pair.CleanReturnAsync();
    }

    private static Entity<ActionComponent> SpawnAction(IEntityManager entMan)
    {
        var action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
        return (action, entMan.EnsureComponent<ActionComponent>(action));
    }

    private static XenoFleshHarvestActionEvent RaiseFleshHarvest(
        IEntityManager entMan,
        EntityUid reaper,
        EntityUid target,
        Entity<ActionComponent> action)
    {
        var ev = new XenoFleshHarvestActionEvent
        {
            Performer = reaper,
            Action = action,
            Target = target,
        };

        entMan.EventBus.RaiseLocalEvent(reaper, ev);
        return ev;
    }

    private static void RaiseFleshBloom(
        IEntityManager entMan,
        EntityUid reaper,
        EntityCoordinates target,
        Entity<ActionComponent> action)
    {
        var ev = new XenoFleshBloomActionEvent
        {
            Performer = reaper,
            Action = action,
            Target = target,
        };

        entMan.EventBus.RaiseLocalEvent(reaper, ev);
    }

    private static XenoRaptureActionEvent RaiseRapture(
        IEntityManager entMan,
        EntityUid reaper,
        EntityUid target,
        Entity<ActionComponent> action)
    {
        var ev = new XenoRaptureActionEvent
        {
            Performer = reaper,
            Action = action,
            Target = target,
        };

        entMan.EventBus.RaiseLocalEvent(reaper, ev);
        return ev;
    }

    private static void RaiseCarrionMantle(
        IEntityManager entMan,
        EntityUid reaper,
        EntityUid target,
        Entity<ActionComponent> action)
    {
        var ev = new XenoCarrionMantleActionEvent
        {
            Performer = reaper,
            Action = action,
            Target = target,
        };

        entMan.EventBus.RaiseLocalEvent(reaper, ev);
    }

    private static void RaiseRedGas(
        IEntityManager entMan,
        EntityUid reaper,
        EntityCoordinates target,
        Entity<ActionComponent> action)
    {
        var ev = new XenoReaperRedGasActionEvent
        {
            Performer = reaper,
            Action = action,
            Target = target,
        };

        entMan.EventBus.RaiseLocalEvent(reaper, ev);
    }

    private static int CountLimbs(IEntityManager entMan, EntityUid body)
    {
        var bodySystem = entMan.System<BodySystem>();
        var bodyComp = entMan.GetComponent<BodyComponent>(body);
        var count = 0;

        foreach (var type in new[] { BodyPartType.Arm, BodyPartType.Hand, BodyPartType.Leg, BodyPartType.Foot })
        {
            foreach (var _ in bodySystem.GetBodyChildrenOfType(body, type, bodyComp))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountBloomEntities(IEntityManager entMan)
    {
        return GetBlooms(entMan).Count;
    }

    private static List<Entity<XenoFleshBloomComponent>> GetBlooms(IEntityManager entMan)
    {
        var result = new List<Entity<XenoFleshBloomComponent>>();
        var query = entMan.EntityQueryEnumerator<XenoFleshBloomComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            result.Add((uid, comp));
        }

        return result;
    }

    private static int CountTelegraphs(IEntityManager entMan)
    {
        return CountPrototype(entMan, "RMCEffectReaperFleshBloomTelegraph");
    }

    private static List<Vector2> GetPrototypePositions(IEntityManager entMan, string prototypeId)
    {
        var result = new List<Vector2>();
        var query = entMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out _, out var metadata, out var transform))
        {
            if (metadata.EntityPrototype?.ID == prototypeId)
                result.Add(transform.Coordinates.Position);
        }

        return result;
    }

    private static int CountAtStep(List<Vector2> positions, Vector2 origin, int step)
    {
        var count = 0;
        foreach (var position in positions)
        {
            if ((int) MathF.Floor(position.X - origin.X) == step)
                count++;
        }

        return count;
    }

    private static FixedPoint2 PoisonDamage(IEntityManager entMan, EntityUid uid)
    {
        var damage = entMan.GetComponent<DamageableComponent>(uid).Damage.DamageDict;
        return damage.GetValueOrDefault("Poison");
    }

    private static FixedPoint2 TotalDamage(IEntityManager entMan, EntityUid uid)
    {
        var damage = entMan.GetComponent<DamageableComponent>(uid).Damage.DamageDict;
        var total = FixedPoint2.Zero;
        foreach (var amount in damage.Values)
        {
            total += amount;
        }

        return total;
    }

    private static int CountPrototype(IEntityManager entMan, string prototypeId)
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var metadata))
        {
            if (metadata.EntityPrototype?.ID == prototypeId)
                count++;
        }

        return count;
    }
}
