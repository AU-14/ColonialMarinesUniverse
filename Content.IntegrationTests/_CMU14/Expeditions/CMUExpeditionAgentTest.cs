using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests.Helpers;
using Content.Server.CMU14.Expeditions;
using Content.Server.NPC.Components;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.NPC;
using Content.Shared.NPC.Systems;
using Content.Shared.Physics;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Expeditions;

[TestFixture, NonParallelizable]
public sealed class CMUExpeditionAgentTest : GameTest
{
    private sealed record VolleyShot(bool Moving, TimeSpan End, int Rounds);

    private sealed class ShotListenerSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<TestListenerComponent, GunShotEvent>(OnShot,
                after: new[] { typeof(CMUExpeditionAgentSystem) });
        }

        private void OnShot(Entity<TestListenerComponent> ent, ref GunShotEvent args)
        {
            if (!TryComp<CMUExpeditionAgentComponent>(args.User, out var agent))
                return;
            ent.Comp.Events.GetOrNew(typeof(GunShotEvent)).Add(args);
            // Urgent fire can start the next burst between samples. Record native shots
            // against their actual stationary/mobile burst window at the firing event.
            ent.Comp.Events.GetOrNew(typeof(VolleyShot)).Add(new VolleyShot(agent.MovingFire,
                agent.MovingFire ? agent.MovingBurstEnd : agent.BurstEnd, args.Ammo.Count));
        }
    }

    private static readonly Robust.Shared.Prototypes.ProtoId<Content.Shared.NPC.Prototypes.NpcFactionPrototype> GOVFORPrototype = "GOVFOR";
    private static readonly Robust.Shared.Prototypes.ProtoId<Content.Shared.NPC.Prototypes.NpcFactionPrototype> CMUExpeditionHostilePrototype = "CMUExpeditionHostile";

    // fixed seed, these assert emergent fights and a fresh seed per pair made a different one miss each run
    public override PoolSettings PoolSettings => new() { Dirty = true, ServerSeed = 1414 };

    // learned tactics carry over between rounds on a recycled server, start every case from the baseline
    [SetUp]
    public async Task ResetExpeditionLearning() =>
        await Server.WaitPost(() => Server.System<CMUExpeditionAgentSystem>().ResetLearnedExperience());

    [Test]
    public async Task InfantryReadiesRifleAndHoldsFireForTeammates()
    {
        EntityUid map = default, guard = default, ally = default, rifle = default;
        EntityCoordinates origin = default;
        var initialAmmo = 0;
        await Server.WaitAssertion(() =>
        {
            var generator = Server.System<CMUExpeditionSystem>();
            Assert.That(generator.TryGenerate("CMUExpeditionWoodland", 42, CMUExpeditionLandform.RiverValley,
                CMUExpeditionStory.CrashRecovery, out map, out var error), Is.True, error);
            var expedition = SEntMan.GetComponent<CMUExpeditionMapComponent>(map);
            for (var i = 0; i < 900 && !expedition.Ready; i++)
                generator.Update(0);
            var lz = expedition.Plan.LandingZone;
            origin = new EntityCoordinates(map, new Vector2(lz.X + 0.5f, lz.Y + 0.5f));
            guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", origin.Offset(new Vector2(-5, 0)));
            var enemy = SEntMan.SpawnEntity("CMMobHuman", origin.Offset(new Vector2(5, 0)));
            SEntMan.AddComponent<GodmodeComponent>(enemy);
            Server.System<NpcFactionSystem>().AddFaction(enemy, GOVFORPrototype);
            ally = SEntMan.SpawnEntity("CMMobHuman", origin);
            Server.System<NpcFactionSystem>().AddFaction(ally, CMUExpeditionHostilePrototype);
            // Keep the firing position fixed so lane-clearing movement cannot bypass the teammate.
            SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).LeashRange = 0.25f;
            Assert.That(Server.System<GunSystem>().TryGetGun(guard, out var gun), Is.True);
            rifle = gun.Owner;
            initialAmmo = Ammo();
        });
        await Pair.RunSeconds(0.4f);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<Content.Shared.Wieldable.Components.WieldableComponent>(rifle).Wielded,
            Is.True, "Riflemen must shoulder their weapon rather than use the large unwielded scatter penalty."));
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Ammo(), Is.EqualTo(initialAmmo), "Do not fire through a living teammate.");
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(ally).Float(), Is.Zero);
            Server.System<SharedTransformSystem>().SetCoordinates(ally, origin.Offset(new Vector2(0, 4)));
        });
        await Pair.RunSeconds(0.8f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Ammo(), Is.LessThan(initialAmmo), "Resume promptly when the friendly clears the lane.");
            SEntMan.DeleteEntity(map);
        });

        int Ammo()
        {
            var count = new GetAmmoCountEvent();
            SEntMan.EventBus.RaiseLocalEvent(rifle, ref count);
            return count.Count;
        }
    }

    [Test]
    public async Task InfantryPeeksFiresShortBurstsAndPhysicallyReturnsToShelter()
    {
        EntityUid map = default, guard = default, enemy = default, rifle = default, enemyRifle = default;
        await Server.WaitAssertion(() =>
        {
            var generator = Server.System<CMUExpeditionSystem>();
            Assert.That(generator.TryGenerate("CMUExpeditionWoodland", 42, CMUExpeditionLandform.RiverValley,
                CMUExpeditionStory.CrashRecovery, out map, out var error), Is.True, error);
            var expedition = SEntMan.GetComponent<CMUExpeditionMapComponent>(map);
            for (var i = 0; i < 900 && !expedition.Ready; i++)
                generator.Update(0);
            var lz = expedition.Plan.LandingZone;
            var origin = new EntityCoordinates(map, new Vector2(lz.X + 0.5f, lz.Y + 0.5f));
            guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", origin.Offset(new Vector2(-4, 0)));
            enemy = SEntMan.SpawnEntity("CMMobHuman", origin.Offset(new Vector2(5, 0)));
            SEntMan.AddComponent<GodmodeComponent>(enemy);
            Server.System<NpcFactionSystem>().AddFaction(enemy, GOVFORPrototype);
            enemyRifle = SEntMan.SpawnEntity("WeaponRifleMAR40", origin.Offset(new Vector2(5, 0)));
            Assert.That(Server.System<Content.Shared.Hands.EntitySystems.SharedHandsSystem>().TryPickupAnyHand(enemy, enemyRifle), Is.True);
            // Start from acquired cover to exercise the peek/withdraw executor. An open,
            // productive firing lane no longer makes the planner seek cover every volley.
            for (var y = 1; y <= 3; y++)
            {
                var coordinates = origin.Offset(new Vector2(-2, y));
                SEntMan.SpawnEntity("CMUExpeditionHull", coordinates);
                expedition.Plan.Props[expedition.Plan.Index((int) coordinates.X, (int) coordinates.Y)] = CMUExpeditionProp.Hull;
            }
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            // Utility decisions have separate coverage; sustained suppression here must
            // exercise the cover executor rather than spend the carried grenade.
            agent.PlanningEnabled = false;
            agent.Target = enemy;
            agent.LastSeen = SEntMan.GetComponent<TransformComponent>(enemy).Coordinates;
            agent.LastContact = SGameTiming.CurTime;
            agent.ForgetAt = SGameTiming.CurTime + agent.MemoryDuration;
            agent.CoverAnchor = origin.Offset(new Vector2(-4, 2));
            agent.PeekPosition = origin.Offset(new Vector2(-4, 0));
            agent.State = CMUExpeditionAgentState.Recover;
            Server.System<SharedTransformSystem>().SetCoordinates(guard, agent.CoverAnchor.Value);
            Assert.That(Server.System<GunSystem>().TryGetGun(guard, out var gun), Is.True);
            rifle = gun.Owner;
            SEntMan.EnsureComponent<TestListenerComponent>(rifle);
        });

        var states = new HashSet<CMUExpeditionAgentState>();
        var coveredVolleys = 0;
        var firedSinceShelter = false;
        var suppressionTested = false;
        var nextIncomingShot = TimeSpan.Zero;
        for (var sample = 0; sample < 100 && coveredVolleys < 2; sample++)
        {
            await Pair.RunSeconds(0.15f);
            await Server.WaitAssertion(() =>
            {
                var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
                states.Add(agent.State);
                Assert.That(agent.LastSearchCells, Is.LessThanOrEqualTo(256));
                if (agent.State == CMUExpeditionAgentState.Peeking)
                    Assert.That(SEntMan.GetComponent<Content.Shared.Wieldable.Components.WieldableComponent>(rifle).Wielded,
                        Is.True, "A short step out must keep the rifle shouldered instead of paying another wield delay in the open.");
                var shots = SEntMan.GetComponent<TestListenerComponent>(rifle).Events.GetOrNew(typeof(GunShotEvent));
                foreach (var shot in shots.Cast<GunShotEvent>())
                {
                    firedSinceShelter = true;
                    // The guard can fire while moving and reach shelter before this sample.
                    // Check the shot's recorded origin rather than its later body position.
                    var transform = Server.System<SharedTransformSystem>();
                    Assert.That(Server.System<SharedInteractionSystem>().InRangeUnobstructed(
                        transform.ToMapCoordinates(shot.FromCoordinates), transform.ToMapCoordinates(shot.ToCoordinates), 18,
                        CollisionGroup.Impassable | CollisionGroup.InteractImpassable, predicate: e => e == guard || e == enemy), Is.True,
                        "Actual volleys must leave a clear firing position, not strike the shelter.");
                }
                shots.Clear();
                if (agent.State == CMUExpeditionAgentState.Engage && SGameTiming.CurTime >= nextIncomingShot)
                {
                    var aim = SEntMan.GetComponent<TransformComponent>(guard).Coordinates.Offset(new Vector2(0, 1.2f));
                    Assert.That(Server.System<GunSystem>().AttemptShoot(enemy,
                        (enemyRifle, SEntMan.GetComponent<Content.Shared.Weapons.Ranged.Components.GunComponent>(enemyRifle)), aim), Is.True);
                    Assert.That(agent.SuppressedUntil, Is.GreaterThan(SGameTiming.CurTime),
                        "A perceived hostile near miss must create pressure without requiring a damage event.");
                    suppressionTested = true;
                    nextIncomingShot = SGameTiming.CurTime + TimeSpan.FromSeconds(1);
                }
                if (agent.State == CMUExpeditionAgentState.Recover && agent.CoverAnchor != null && firedSinceShelter)
                {
                    Assert.That(agent.CoverAnchor, Is.Not.Null);
                    Assert.That(Server.System<SharedTransformSystem>().InRange(
                        SEntMan.GetComponent<TransformComponent>(guard).Coordinates, agent.CoverAnchor!.Value, 0.3f), Is.True,
                        $"Recovery must stay at shelter: pos={SEntMan.GetComponent<TransformComponent>(guard).Coordinates}, anchor={agent.CoverAnchor}, destination={agent.CoverDestination}, route={agent.Route.Count}, action={agent.Action}, sample={sample}");
                    Assert.That(Server.System<SharedInteractionSystem>().InRangeUnobstructed(guard, enemy, 100,
                        CollisionGroup.Impassable | CollisionGroup.InteractImpassable, predicate: e => e == guard || e == enemy), Is.False,
                        "Returning to cover must physically break enemy line of sight.");
                    coveredVolleys++;
                    firedSinceShelter = false;
                }
            });
        }
        await Server.WaitAssertion(() =>
        {
            Assert.That(states, Does.Contain(CMUExpeditionAgentState.Peeking));
            Assert.That(states, Does.Contain(CMUExpeditionAgentState.Withdraw));
            var volleys = SEntMan.GetComponent<TestListenerComponent>(rifle).Events[typeof(VolleyShot)]
                .Cast<VolleyShot>().GroupBy(shot => (shot.Moving, shot.End)).ToArray();
            Assert.That(volleys.Count(group => !group.Key.Moving), Is.GreaterThanOrEqualTo(2), "The guard must complete repeated attacks.");
            Assert.That(coveredVolleys, Is.GreaterThanOrEqualTo(2), "Complete repeated attacks with physical returns to shelter.");
            Assert.That(volleys.Select(group => group.Sum(shot => shot.Rounds)), Has.All.InRange(1, 3));
            Assert.That(suppressionTested, Is.True);
            SEntMan.DeleteEntity(map);
        });

    }

    [Test]
    public async Task SquadStaggersPeeksAndBothGuardsKeepAttacking()
    {
        EntityUid map = default, enemy = default, enemyRifle = default;
        var guards = new List<EntityUid>();
        var rifles = new List<EntityUid>();
        var initial = new List<int>();
        await Server.WaitAssertion(() =>
        {
            var generator = Server.System<CMUExpeditionSystem>();
            Assert.That(generator.TryGenerate("CMUExpeditionWoodland", 42, CMUExpeditionLandform.RiverValley,
                CMUExpeditionStory.CrashRecovery, out map, out var error), Is.True, error);
            var expedition = SEntMan.GetComponent<CMUExpeditionMapComponent>(map);
            for (var i = 0; i < 900 && !expedition.Ready; i++)
                generator.Update(0);
            var lz = expedition.Plan.LandingZone;
            var origin = new EntityCoordinates(map, new Vector2(lz.X + 0.5f, lz.Y + 0.5f));
            enemy = SEntMan.SpawnEntity("CMMobHuman", origin.Offset(new Vector2(5, 0)));
            SEntMan.AddComponent<GodmodeComponent>(enemy);
            Server.System<NpcFactionSystem>().AddFaction(enemy, GOVFORPrototype);
            enemyRifle = SEntMan.SpawnEntity("WeaponRifleMAR40", SEntMan.GetComponent<TransformComponent>(enemy).Coordinates);
            Assert.That(Server.System<Content.Shared.Hands.EntitySystems.SharedHandsSystem>().TryPickupAnyHand(enemy, enemyRifle), Is.True);
            foreach (var y in new[] { 1, 2, 3, -2, -3, -4 })
            {
                var coordinates = origin.Offset(new Vector2(-2, y));
                SEntMan.SpawnEntity("CMUExpeditionHull", coordinates);
                expedition.Plan.Props[expedition.Plan.Index((int) coordinates.X, (int) coordinates.Y)] = CMUExpeditionProp.Hull;
            }
            for (var index = 0; index < 2; index++)
            {
                var anchor = origin.Offset(new Vector2(-4, index == 0 ? 2 : -3));
                var guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", anchor);
                guards.Add(guard);
                var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
                // Begin with two already-acquired contacts in known shelters to isolate squad scheduling.
                agent.PlanningEnabled = false;
                agent.Target = enemy;
                agent.LastSeen = SEntMan.GetComponent<TransformComponent>(enemy).Coordinates;
                agent.LastContact = SGameTiming.CurTime;
                agent.ForgetAt = SGameTiming.CurTime + agent.MemoryDuration;
                agent.CoverAnchor = anchor;
                agent.PeekPosition = origin.Offset(new Vector2(-4, index == 0 ? 0 : -1));
                agent.State = CMUExpeditionAgentState.Recover;
                Assert.That(Server.System<GunSystem>().TryGetGun(guard, out var gun), Is.True);
                rifles.Add(gun.Owner);
                initial.Add(Ammo(gun.Owner));
            }
        });
        var nextIncomingShot = TimeSpan.Zero;
        for (var sample = 0; sample < 80; sample++)
        {
            await Pair.RunSeconds(0.15f);
            await Server.WaitAssertion(() =>
            {
                var attackers = 0;
                foreach (var guard in guards)
                {
                    var state = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).State;
                    if (state is CMUExpeditionAgentState.Peeking or CMUExpeditionAgentState.Aim or CMUExpeditionAgentState.Engage)
                        attackers++;
                    // Productive positions persist without pressure. Native incoming fire
                    // makes each attacker return to shelter and yield the next peek.
                    if (state == CMUExpeditionAgentState.Engage && SGameTiming.CurTime >= nextIncomingShot)
                    {
                        var aim = SEntMan.GetComponent<TransformComponent>(guard).Coordinates.Offset(new Vector2(0, 1.2f));
                        Assert.That(Server.System<GunSystem>().AttemptShoot(enemy,
                            (enemyRifle, SEntMan.GetComponent<Content.Shared.Weapons.Ranged.Components.GunComponent>(enemyRifle)), aim), Is.True);
                        nextIncomingShot = SGameTiming.CurTime + TimeSpan.FromSeconds(1);
                    }
                }
                Assert.That(attackers, Is.LessThanOrEqualTo(1), "A pair must stagger its exposure, not pop out together.");
            });
        }
        await Server.WaitAssertion(() =>
        {
            for (var index = 0; index < 2; index++)
                Assert.That(Ammo(rifles[index]), Is.LessThan(initial[index]), $"Guard {index} must receive an attack turn.");
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guards[0]);
            SEntMan.DeleteEntity(enemy);
            var agents = Server.System<CMUExpeditionAgentSystem>();
            agents.ResetOrders(guards[0], agent);
            var failed = SEntMan.GetComponent<TransformComponent>(guards[0]).Coordinates.Offset(new Vector2(-3, 0));
            // A reachable timeout now retries a local detour. Block the destination so
            // this check exercises a real failed move after that recovery is exhausted.
            SEntMan.SpawnEntity("CMUExpeditionHull", failed);
            agent.CoverDestination = failed;
            agent.State = CMUExpeditionAgentState.Reposition;
            agent.MoveUntil = SGameTiming.CurTime - TimeSpan.FromSeconds(1);
            agent.NextThink = SGameTiming.CurTime;
            agents.Update(0);
            Assert.That(agent.FailedPosition, Is.EqualTo(failed), "Timed-out actions must inform the next position search.");
            Assert.That(agent.AvoidPositionUntil, Is.GreaterThan(SGameTiming.CurTime));
            SEntMan.DeleteEntity(map);
        });

        int Ammo(EntityUid gun)
        {
            var count = new GetAmmoCountEvent();
            SEntMan.EventBus.RaiseLocalEvent(gun, ref count);
            return count.Count;
        }
    }

    [Test]
    public async Task InfantryStepsClearOfGrazingWallAndShootsWithoutRemovingIt()
    {
        EntityUid map = default, guard = default, enemy = default, wall = default, rifle = default;
        EntityCoordinates initialPosition = default;
        var initialAmmo = 0;
        await Server.WaitAssertion(() =>
        {
            var generator = Server.System<CMUExpeditionSystem>();
            Assert.That(generator.TryGenerate("CMUExpeditionWoodland", 42, CMUExpeditionLandform.RiverValley,
                CMUExpeditionStory.CrashRecovery, out map, out var error), Is.True, error);
            var expedition = SEntMan.GetComponent<CMUExpeditionMapComponent>(map);
            for (var i = 0; i < 900 && !expedition.Ready; i++)
                generator.Update(0);
            var lz = expedition.Plan.LandingZone;
            var origin = new EntityCoordinates(map, new Vector2(lz.X + 0.5f, lz.Y + 0.5f));
            initialPosition = origin.Offset(new Vector2(-4, 0.3f));
            guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", initialPosition);
            enemy = SEntMan.SpawnEntity("CMMobHuman", origin.Offset(new Vector2(4, 0.3f)));
            Server.System<NpcFactionSystem>().AddFaction(enemy, GOVFORPrototype);
            // Obstruct the muzzle clearance, not scatter beside a distant target.
            wall = SEntMan.SpawnEntity("CMUExpeditionHull", origin.Offset(new Vector2(-3, 1)));
            SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).NextReposition = SGameTiming.CurTime + TimeSpan.FromMinutes(1);
            Assert.That(Server.System<GunSystem>().TryGetGun(guard, out var gun), Is.True);
            rifle = gun.Owner;
            initialAmmo = Ammo();
        });
        await Pair.RunSeconds(0.15f);
        await Server.WaitAssertion(() => Assert.That(Server.System<SharedInteractionSystem>().InRangeUnobstructed(guard, enemy, 18,
            CollisionGroup.Impassable | CollisionGroup.InteractImpassable, predicate: e => e == guard || e == enemy), Is.True,
            "The center ray must be clear while the firing cone grazes the wall."));
        var fired = false;
        var hit = false;
        var cornerTrace = new List<string>();
        // Native scatter depends on the gun entity and simulation tick. Observe bounded repeated
        // volleys until a physical hit rather than require one particular random volley to connect.
        for (var sample = 0; sample < 120 && !hit; sample++)
        {
            await Pair.RunSeconds(0.1f);
            await Server.WaitAssertion(() =>
            {
                if (sample % 10 == 0)
                {
                    var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
                    cornerTrace.Add($"{agent.State}: {SEntMan.GetComponent<TransformComponent>(guard).Coordinates}, dest={agent.CoverDestination}, failed={agent.FailedPosition}, ammo={Ammo()}");
                }
                hit = Server.System<DamageableSystem>().GetTotalDamage(enemy).Float() > 0;
                if (fired || Ammo() == initialAmmo)
                    return;
                fired = true;
                Assert.That(Server.System<SharedTransformSystem>().InRange(
                    SEntMan.GetComponent<TransformComponent>(guard).Coordinates, initialPosition, 0.3f), Is.False,
                    "A blocked firing stance must be corrected before a bullet is fired.");
                Assert.That(SEntMan.GetComponent<Content.Shared.Wieldable.Components.WieldableComponent>(rifle).Wielded, Is.True);
            });
        }
        await Server.WaitAssertion(() =>
        {
            Assert.That(fired, Is.True, $"Find a usable corner angle instead of standing exposed without shooting: {string.Join(';', cornerTrace)}");
            Assert.That(SEntMan.Deleted(wall), Is.False, "The test must succeed with the obstruction still present.");
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(enemy).Float(), Is.GreaterThan(0), string.Join(';', cornerTrace));
            SEntMan.DeleteEntity(map);
        });

        int Ammo()
        {
            var count = new GetAmmoCountEvent();
            SEntMan.EventBus.RaiseLocalEvent(rifle, ref count);
            return count.Count;
        }
    }

    [Test]
    public async Task WoundedInfantryTreatsInShelterInterruptsOnDamageAndExhaustsDressings()
    {
        EntityUid map = default, guard = default, enemy = default, medicine = default;
        await Server.WaitAssertion(() =>
        {
            var generator = Server.System<CMUExpeditionSystem>();
            Assert.That(generator.TryGenerate("CMUExpeditionWoodland", 42, CMUExpeditionLandform.RiverValley,
                CMUExpeditionStory.CrashRecovery, out map, out var error), Is.True, error);
            var expedition = SEntMan.GetComponent<CMUExpeditionMapComponent>(map);
            for (var i = 0; i < 900 && !expedition.Ready; i++)
                generator.Update(0);
            var lz = expedition.Plan.LandingZone;
            var origin = new EntityCoordinates(map, new Vector2(lz.X + 0.5f, lz.Y + 0.5f));
            guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", origin.Offset(new Vector2(-4, 0)));
            enemy = SEntMan.SpawnEntity("CMMobHuman", origin.Offset(new Vector2(5, 0)));
            SEntMan.AddComponent<GodmodeComponent>(enemy);
            Server.System<NpcFactionSystem>().AddFaction(enemy, GOVFORPrototype);
            for (var y = 1; y <= 4; y++)
            {
                var coordinates = origin.Offset(new Vector2(-2, y));
                SEntMan.SpawnEntity("CMUExpeditionHull", coordinates);
                expedition.Plan.Props[expedition.Plan.Index((int) coordinates.X, (int) coordinates.Y)] = CMUExpeditionProp.Hull;
            }
            Assert.That(Server.System<Content.Shared.Inventory.InventorySystem>().TryGetSlotEntity(guard, "pocket1", out var item), Is.True);
            medicine = item!.Value;
            var patient = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            patient.HealDamage = 8;
            patient.EmergencyHealDamage = 8; // Isolate urgent treatment/consumption from elective triage decisions.
            patient.RetreatDamage = 25;
            Assert.That(SEntMan.GetComponent<Content.Shared.Stacks.StackComponent>(medicine).Count, Is.EqualTo(3));
            Hurt(30);
        });
        var healing = false;
        var medicalTrace = new List<string>();
        for (var sample = 0; sample < 60 && !healing; sample++)
        {
            await Pair.RunSeconds(0.15f);
            await Server.WaitAssertion(() =>
            {
                var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
                healing = agent.State == CMUExpeditionAgentState.Healing;
                if (sample % 10 == 0)
                    medicalTrace.Add($"{agent.State}: {SEntMan.GetComponent<TransformComponent>(guard).Coordinates}, dest={agent.CoverDestination}, hands={Server.System<Content.Shared.Hands.EntitySystems.SharedHandsSystem>().GetEmptyHandCount(guard)}, damage={Server.System<DamageableSystem>().GetTotalDamage(guard)}");
            });
        }
        await Server.WaitAssertion(() =>
        {
            Assert.That(healing, Is.True, $"A wounded guard must reach shelter and start treatment: {string.Join(';', medicalTrace)}");
            Assert.That(Server.System<SharedInteractionSystem>().InRangeUnobstructed(guard, enemy, 18,
                CollisionGroup.Impassable | CollisionGroup.InteractImpassable, predicate: e => e == guard || e == enemy), Is.False);
            Hurt(3);
        });
        await Pair.RunSeconds(0.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<Content.Shared.Stacks.StackComponent>(medicine).Count, Is.EqualTo(3),
                "Interrupted treatment must not consume a dressing.");
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(guard).Float(), Is.GreaterThanOrEqualTo(32));
        });
        await Pair.RunSeconds(12);
        float damageAfter = 0;
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(medicine), Is.True, $"Three treatments must exhaust the pack: damage={Server.System<DamageableSystem>().GetTotalDamage(guard)}, state={SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).State}, remaining={(SEntMan.TryGetComponent<Content.Shared.Stacks.StackComponent>(medicine, out var stack) ? stack.Count : -1)}.");
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(guard).Float(), Is.LessThan(8), "Native healing must actually reduce the wounds.");
            Hurt(20);
            damageAfter = Server.System<DamageableSystem>().GetTotalDamage(guard).Float();
        });
        await Pair.RunSeconds(4);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(guard).Float(), Is.GreaterThan(damageAfter - 5),
                "An exhausted pack cannot give the guard unlimited healing.");
            SEntMan.DeleteEntity(map);
        });

        void Hurt(int amount) => Server.System<DamageableSystem>().TryChangeDamage(guard,
            new DamageSpecifier { DamageDict = { ["Blunt"] = amount } }, ignoreResistances: true);
    }

    [Test]
    public async Task InfantryUsesSightRealAmmunitionAndMovementThenStopsWhenIncapacitated()
    {
        EntityUid map = default, guard = default, enemy = default, rifle = default;
        var walls = new List<EntityUid>();
        EntityCoordinates origin = default;
        var initialAmmo = 0;
        await Server.WaitAssertion(() =>
        {
            var generator = Server.System<CMUExpeditionSystem>();
            Assert.That(generator.TryGenerate("CMUExpeditionWoodland", 42, CMUExpeditionLandform.RiverValley,
                CMUExpeditionStory.CrashRecovery, out map, out var error), Is.True, error);
            var expedition = SEntMan.GetComponent<CMUExpeditionMapComponent>(map);
            for (var i = 0; i < 900 && !expedition.Ready; i++)
                generator.Update(0);
            Assert.That(expedition.Ready, Is.True);
            var lz = expedition.Plan.LandingZone;
            origin = new EntityCoordinates(map, new Vector2(lz.X + 0.5f, lz.Y + 0.5f));
            guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", origin.Offset(new Vector2(-7, 0)));
            // Tactical utilities have separate tests; isolate sight, movement and self-treatment.
            SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).PlanningEnabled = false;
            enemy = SEntMan.SpawnEntity("CMMobHuman", origin.Offset(new Vector2(7, 0)));
            // Exercise the ranged-contact investigation delay, not melee-threat memory.
            var enemyWeapon = SEntMan.SpawnEntity("WeaponRifleMAR40", SEntMan.GetComponent<TransformComponent>(enemy).Coordinates);
            Assert.That(Server.System<Content.Shared.Hands.EntitySystems.SharedHandsSystem>().TryPickupAnyHand(enemy, enemyWeapon), Is.True);
            SEntMan.AddComponent<GodmodeComponent>(enemy);
            Server.System<NpcFactionSystem>().AddFaction(enemy, GOVFORPrototype);
            var ally = SEntMan.SpawnEntity("CMMobHuman", origin.Offset(new Vector2(-7, 2)));
            Server.System<NpcFactionSystem>().AddFaction(ally, CMUExpeditionHostilePrototype);
            for (var y = -3; y <= 3; y++)
                walls.Add(SEntMan.SpawnEntity("CMUExpeditionHull", origin.Offset(new Vector2(0, y))));
            Assert.That(Server.System<GunSystem>().TryGetGun(guard, out var gun), Is.True, "Loadout must put a usable rifle in hand.");
            rifle = gun.Owner;
            initialAmmo = Ammo();
            Assert.That(initialAmmo, Is.GreaterThan(0));
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).Target, Is.Null,
                "A nearby hostile behind a wall and a visible ally must not be acquired.");
            Assert.That(Ammo(), Is.EqualTo(initialAmmo));
            foreach (var wall in walls)
                SEntMan.DeleteEntity(wall);
        });
        await Pair.RunSeconds(4);
        EntityCoordinates remembered = default;
        await Server.WaitAssertion(() =>
        {
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            Assert.That(agent.Target, Is.EqualTo(enemy));
            SEntMan.TryGetComponent<NPCSteeringComponent>(guard, out var steering);
            var mover = SEntMan.GetComponent<InputMoverComponent>(guard);
            Assert.That(SEntMan.GetComponent<TransformComponent>(guard).LocalPosition.X, Is.GreaterThan(origin.X - 6),
                $"Actual movement required; state={agent.State}, steering={steering?.Status}, path={steering?.CurrentPath.Count}, canMove={mover.CanMove}, input={mover.CurTickSprintMovement}, active={SEntMan.HasComponent<ActiveNPCComponent>(guard)}");
            Assert.That(Ammo(), Is.LessThan(initialAmmo), "Native gunfire must consume the loaded magazine.");
            remembered = agent.LastSeen!.Value;
            var plan = SEntMan.GetComponent<CMUExpeditionMapComponent>(map).Plan;
            var hiddenCell = Enumerable.Range(0, plan.Terrain.Length).First(i => plan.Paths[i] &&
                plan.Props[i] == CMUExpeditionProp.None && plan.Terrain[i] is not (CMUExpeditionTerrain.Water or CMUExpeditionTerrain.Cliff) &&
                Vector2.Distance(new Vector2(i % plan.Size, i / plan.Size), origin.Position) > 40);
            Server.System<SharedTransformSystem>().SetCoordinates(enemy,
                new EntityCoordinates(map, new Vector2(hiddenCell % plan.Size + 0.5f, hiddenCell / plan.Size + 0.5f)));
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            Assert.That(agent.LastSeen, Is.EqualTo(remembered), "Unseen movement must not update the remembered location.");
            Assert.That(agent.State, Is.EqualTo(CMUExpeditionAgentState.Watch), "Briefly watch the last opening before abandoning a firing position.");
            Assert.That(SEntMan.HasComponent<NPCRangedCombatComponent>(guard), Is.False);
            Assert.That(SEntMan.HasComponent<NPCSteeringComponent>(guard), Is.False);
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).State,
            Is.EqualTo(CMUExpeditionAgentState.Investigate)));
        await Pair.RunSeconds(6);
        EntityCoordinates shelter = default;
        await Server.WaitAssertion(() =>
        {
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            Assert.That(agent.LastSeen, Is.Null);
            Assert.That(agent.Target, Is.Null);
            var transform = Server.System<SharedTransformSystem>();
            transform.SetCoordinates(guard, origin.Offset(new Vector2(-6, 0)));
            transform.SetCoordinates(enemy, origin.Offset(new Vector2(4, 0)));
            agent.Home = origin.Offset(new Vector2(-6, 0));
            // A small known wreck fragment inside the clear LZ gives the tactical test an unambiguous shelter.
            var plan = SEntMan.GetComponent<CMUExpeditionMapComponent>(map).Plan;
            for (var y = 2; y <= 4; y++)
            {
                var location = origin.Offset(new Vector2(-3, y));
                SEntMan.SpawnEntity("CMUExpeditionHull", location);
                plan.Props[plan.Index((int) location.X, (int) location.Y)] = CMUExpeditionProp.Hull;
            }
            agent.RetreatDamage = 4;
            agent.HealDamage = 4;
            agent.EmergencyHealDamage = 4;
            Server.System<DamageableSystem>().TryChangeDamage(guard,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 10 } }, ignoreResistances: true);
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(guard).Float(), Is.GreaterThanOrEqualTo(4));
        });
        await Pair.RunSeconds(0.6f);
        await Server.WaitAssertion(() =>
        {
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            Assert.That(agent.State, Is.EqualTo(CMUExpeditionAgentState.Retreat));
            Assert.That(SEntMan.HasComponent<NPCSteeringComponent>(guard), Is.True);
            Assert.That(agent.CoverDestination, Is.Not.Null);
            // Native steering targets the next danger-route waypoint, not the final shelter.
            shelter = agent.CoverDestination!.Value;
            Assert.That(shelter, Is.Not.EqualTo(agent.Home), "Injury should choose shelter rather than simply return home.");
            var mapPosition = Server.System<SharedTransformSystem>().ToMapCoordinates(shelter);
            Assert.That(Server.System<SharedInteractionSystem>().InRangeUnobstructed(mapPosition, enemy, 18,
                CollisionGroup.Impassable | CollisionGroup.InteractImpassable, predicate: e => e == guard || e == enemy), Is.False);
            Assert.That(SEntMan.HasComponent<NPCRangedCombatComponent>(guard), Is.False);
        });
        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<SharedTransformSystem>().InRange(
                SEntMan.GetComponent<TransformComponent>(guard).Coordinates, shelter, 1.2f), Is.True,
                $"Must reach shelter {shelter}; state={SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).State}, pos={SEntMan.GetComponent<TransformComponent>(guard).Coordinates}, dest={SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).CoverDestination}.");
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            Assert.That(agent.State, Is.AnyOf(CMUExpeditionAgentState.Retreat, CMUExpeditionAgentState.Healing),
                "Losing sight in cover must preserve retreat or allow sheltered treatment.");
            Server.System<MobStateSystem>().ChangeMobState(guard, MobState.Critical);
            Assert.That(agent.State, Is.EqualTo(CMUExpeditionAgentState.Disabled));
            Assert.That(SEntMan.HasComponent<NPCRangedCombatComponent>(guard), Is.False);
            Assert.That(SEntMan.HasComponent<NPCSteeringComponent>(guard), Is.False);
            SEntMan.DeleteEntity(map);
        });

        int Ammo()
        {
            var count = new GetAmmoCountEvent();
            SEntMan.EventBus.RaiseLocalEvent(rifle, ref count);
            return count.Count;
        }
    }
}
