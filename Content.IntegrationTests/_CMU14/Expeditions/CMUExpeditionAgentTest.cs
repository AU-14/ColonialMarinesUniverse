using System.Numerics;
using Content.IntegrationTests.Fixtures;
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
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Expeditions;

[TestFixture]
public sealed class CMUExpeditionAgentTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true };

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
            Server.System<NpcFactionSystem>().AddFaction(enemy, "GOVFOR");
            ally = SEntMan.SpawnEntity("CMMobHuman", origin);
            Server.System<NpcFactionSystem>().AddFaction(ally, "CMUExpeditionHostile");
            // Isolate the trigger discipline test from the separate cover movement test.
            SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).NextReposition = SGameTiming.CurTime + TimeSpan.FromMinutes(1);
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
        var ammoBefore = 0;
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
            Server.System<NpcFactionSystem>().AddFaction(enemy, "GOVFOR");
            enemyRifle = SEntMan.SpawnEntity("WeaponRifleMAR40", origin.Offset(new Vector2(5, 0)));
            Assert.That(Server.System<Content.Shared.Hands.EntitySystems.SharedHandsSystem>().TryPickupAnyHand(enemy, enemyRifle), Is.True);
            // The initial lane is open, while the far side of this fragment offers real shelter.
            for (var y = 1; y <= 3; y++)
            {
                var coordinates = origin.Offset(new Vector2(-2, y));
                SEntMan.SpawnEntity("CMUExpeditionHull", coordinates);
                expedition.Plan.Props[expedition.Plan.Index((int) coordinates.X, (int) coordinates.Y)] = CMUExpeditionProp.Hull;
            }
            Assert.That(Server.System<GunSystem>().TryGetGun(guard, out var gun), Is.True);
            rifle = gun.Owner;
            ammoBefore = Ammo();
        });

        var states = new HashSet<CMUExpeditionAgentState>();
        var volley = 0;
        var volleys = new List<int>();
        var shelteredAfterFiring = false;
        var suppressionTested = false;
        for (var sample = 0; sample < 100; sample++)
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
                var ammo = Ammo();
                if (ammo < ammoBefore)
                {
                    volley += ammoBefore - ammo;
                    Assert.That(Server.System<SharedInteractionSystem>().InRangeUnobstructed(guard, enemy, 18,
                        CollisionGroup.Impassable | CollisionGroup.InteractImpassable, predicate: e => e == guard || e == enemy), Is.True,
                        "Actual volleys must leave a clear firing position, not strike the shelter.");
                }
                ammoBefore = ammo;
                if (!suppressionTested && agent.State == CMUExpeditionAgentState.Engage)
                {
                    var aim = SEntMan.GetComponent<TransformComponent>(guard).Coordinates.Offset(new Vector2(0, 1.2f));
                    Assert.That(Server.System<GunSystem>().AttemptShoot(enemy,
                        (enemyRifle, SEntMan.GetComponent<Content.Shared.Weapons.Ranged.Components.GunComponent>(enemyRifle)), aim), Is.True);
                    Assert.That(agent.SuppressedUntil, Is.GreaterThan(SGameTiming.CurTime),
                        "A perceived hostile near miss must create pressure without requiring a damage event.");
                    suppressionTested = true;
                }
                if (agent.State == CMUExpeditionAgentState.Withdraw && volley > 0)
                {
                    volleys.Add(volley);
                    volley = 0;
                }
                if (agent.State == CMUExpeditionAgentState.Recover && volleys.Count > 0)
                {
                    Assert.That(agent.CoverAnchor, Is.Not.Null);
                    Assert.That(Server.System<SharedTransformSystem>().InRange(
                        SEntMan.GetComponent<TransformComponent>(guard).Coordinates, agent.CoverAnchor!.Value, 0.3f), Is.True);
                    shelteredAfterFiring |= !Server.System<SharedInteractionSystem>().InRangeUnobstructed(guard, enemy, 18,
                        CollisionGroup.Impassable | CollisionGroup.InteractImpassable, predicate: e => e == guard || e == enemy);
                }
            });
        }
        await Server.WaitAssertion(() =>
        {
            Assert.That(states, Does.Contain(CMUExpeditionAgentState.Peeking));
            Assert.That(states, Does.Contain(CMUExpeditionAgentState.Withdraw));
            Assert.That(volleys.Count, Is.GreaterThanOrEqualTo(2), "The guard must complete repeated attacks.");
            Assert.That(volleys, Has.All.InRange(1, 3));
            Assert.That(shelteredAfterFiring, Is.True, "Returning to cover must physically break enemy line of sight.");
            Assert.That(suppressionTested, Is.True);
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
    public async Task SquadStaggersPeeksAndBothGuardsKeepAttacking()
    {
        EntityUid map = default;
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
            var enemy = SEntMan.SpawnEntity("CMMobHuman", origin.Offset(new Vector2(5, 0)));
            SEntMan.AddComponent<GodmodeComponent>(enemy);
            Server.System<NpcFactionSystem>().AddFaction(enemy, "GOVFOR");
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
                }
                Assert.That(attackers, Is.LessThanOrEqualTo(1), "A pair must stagger its exposure, not pop out together.");
            });
        }
        await Server.WaitAssertion(() =>
        {
            for (var index = 0; index < 2; index++)
                Assert.That(Ammo(rifles[index]), Is.LessThan(initial[index]), $"Guard {index} must receive an attack turn.");
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guards[0]);
            var failed = SEntMan.GetComponent<TransformComponent>(guards[0]).Coordinates.Offset(new Vector2(-3, 0));
            agent.CoverDestination = failed;
            agent.State = CMUExpeditionAgentState.Reposition;
            agent.MoveUntil = SGameTiming.CurTime - TimeSpan.FromSeconds(1);
            agent.NextThink = SGameTiming.CurTime;
            Server.System<CMUExpeditionAgentSystem>().Update(0);
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
            Server.System<NpcFactionSystem>().AddFaction(enemy, "GOVFOR");
            wall = SEntMan.SpawnEntity("CMUExpeditionHull", origin.Offset(new Vector2(0, 1)));
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
        var cornerTrace = new List<string>();
        for (var sample = 0; sample < 60; sample++)
        {
            await Pair.RunSeconds(0.1f);
            await Server.WaitAssertion(() =>
            {
                if (sample % 10 == 0)
                {
                    var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
                    cornerTrace.Add($"{agent.State}: {SEntMan.GetComponent<TransformComponent>(guard).Coordinates}, dest={agent.CoverDestination}, failed={agent.FailedPosition}");
                }
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
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(enemy).Float(), Is.GreaterThan(0));
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
            Server.System<NpcFactionSystem>().AddFaction(enemy, "GOVFOR");
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
            enemy = SEntMan.SpawnEntity("CMMobHuman", origin.Offset(new Vector2(7, 0)));
            SEntMan.AddComponent<GodmodeComponent>(enemy);
            Server.System<NpcFactionSystem>().AddFaction(enemy, "GOVFOR");
            var ally = SEntMan.SpawnEntity("CMMobHuman", origin.Offset(new Vector2(-7, 2)));
            Server.System<NpcFactionSystem>().AddFaction(ally, "CMUExpeditionHostile");
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
            shelter = SEntMan.GetComponent<NPCSteeringComponent>(guard).Coordinates;
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
