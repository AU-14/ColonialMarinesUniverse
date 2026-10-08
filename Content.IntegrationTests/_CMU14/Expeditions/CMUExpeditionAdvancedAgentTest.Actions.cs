using System.Numerics;
using Content.Server.CMU14.Expeditions;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.CMU14.Expeditions;
using Content.Shared._RMC14.Explosion;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Radio.Components;
using Content.Shared.Storage;
using Content.Shared.Throwing;
using Content.Shared.Trigger.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Expeditions;

public sealed partial class CMUExpeditionAdvancedAgentTest
{
    [Test]
    public async Task ReloadIsInterruptibleConsumesActualMagazinesAndRunsOut()
    {
        EntityUid map = default, guard = default, rifle = default;
        await Server.WaitAssertion(() =>
        {
            EntityUid enemy;
            (map, guard, enemy) = Arena("CMUExpeditionScavenger");
            SEntMan.DeleteEntity(enemy);
            Assert.That(Server.System<GunSystem>().TryGetGun(guard, out var gun), Is.True);
            rifle = gun.Owner;
            Empty();
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).State, Is.EqualTo(CMUExpeditionAgentState.Reloading));
            Hurt(guard, 3);
        });
        await Pair.RunSeconds(0.3f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Ammo(rifle), Is.Zero, "Damage cancels preparation without conjuring rounds into the weapon.");
            Assert.That(Stored(guard).Count(item => SEntMan.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID == "RMCMagazineRifleMAR40"), Is.EqualTo(2));
        });
        await Pair.RunSeconds(13);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Ammo(rifle), Is.EqualTo(40));
            Assert.That(SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).Reloads, Is.EqualTo(1));
            Empty();
        });
        await Pair.RunSeconds(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Ammo(rifle), Is.EqualTo(40));
            Assert.That(SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).Reloads, Is.EqualTo(2));
            Empty();
        });
        await Pair.RunSeconds(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Ammo(rifle), Is.Zero);
            Assert.That(SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).Reloads, Is.EqualTo(2), "Two physical spare magazines are the entire reserve.");
            SEntMan.DeleteEntity(map);
        });
        void Empty()
        {
            Assert.That(Server.System<ItemSlotsSystem>().TryEject(rifle, "gun_magazine", guard, out var magazine), Is.True);
            SEntMan.DeleteEntity(magazine!.Value);
        }
    }

    [Test]
    public async Task GrenadeRechecksFriendsBeforePrimingAndThrowsAFinitePhysicalItem()
    {
        EntityUid map = default, guard = default, enemy = default, second = default, grenade = default, ally = default;
        EntityCoordinates contact = default;
        await Server.WaitAssertion(() =>
        {
            (map, guard, enemy) = Arena("CMUExpeditionScavenger");
            contact = SEntMan.GetComponent<TransformComponent>(enemy).Coordinates;
            second = SEntMan.SpawnEntity("CMMobHuman", contact.Offset(new Vector2(0, -1)));
            SEntMan.AddComponent<GodmodeComponent>(second);
            Server.System<NpcFactionSystem>().AddFaction(second, "GOVFOR");
            grenade = Stored(guard).Single(item => SEntMan.TryGetComponent<CMUExpeditionGrenadeComponent>(item, out var kind) && !kind.Smoke);
        });
        await Pair.RunSeconds(0.3f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).State, Is.EqualTo(CMUExpeditionAgentState.Throwing));
            ally = SEntMan.SpawnEntity("CMMobHuman", SEntMan.GetComponent<TransformComponent>(enemy).Coordinates.Offset(new Vector2(0, -2)));
            Server.System<NpcFactionSystem>().AddFaction(ally, "CMUExpeditionHostile");
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<ActiveTimerTriggerComponent>(grenade), Is.False, "A buddy entering the blast area during preparation cancels the throw before priming.");
            Assert.That(SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).GrenadesThrown, Is.Zero);
            SEntMan.DeleteEntity(ally);
            SEntMan.DeleteEntity(enemy);
            SEntMan.DeleteEntity(second);
        });
        await Pair.RunSeconds(15);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).GrenadesThrown, Is.Zero,
                "An interrupted opening decision must not become rapid grenade retries.");
        });
        await Pair.RunSeconds(20);
        await Server.WaitAssertion(() =>
        {
            foreach (var offset in new[] { Vector2.Zero, new Vector2(0, -1) })
            {
                var hostile = SEntMan.SpawnEntity("CMMobHuman", contact.Offset(offset));
                SEntMan.AddComponent<GodmodeComponent>(hostile);
                Server.System<NpcFactionSystem>().AddFaction(hostile, "GOVFOR");
            }
        });
        var thrown = false;
        for (var i = 0; i < 75 && !thrown; i++)
        {
            await Pair.RunSeconds(0.2f);
            await Server.WaitAssertion(() => thrown = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).GrenadesThrown > 0);
        }
        await Server.WaitAssertion(() =>
        {
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            Assert.That(thrown, Is.True, $"state={agent.State}, goal={agent.Goal}, action={agent.Action}, failures={agent.FailedPlans}, threats={agent.VisibleThreats.Count}, held={agent.ActionItem}, pos={SEntMan.GetComponent<TransformComponent>(guard).Coordinates}");
            Assert.That(SEntMan.HasComponent<ActiveTimerTriggerComponent>(grenade), Is.True);
            Assert.That(SEntMan.HasComponent<ThrownItemComponent>(grenade), Is.True, "Use native throwing physics, not a spawned explosion at the target.");
            Assert.That(Stored(guard), Does.Not.Contain(grenade));
        });
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<SharedTransformSystem>().InRange(
                SEntMan.GetComponent<TransformComponent>(guard).Coordinates, SEntMan.GetComponent<TransformComponent>(grenade).Coordinates, 2), Is.False);
            SEntMan.DeleteEntity(map);
        });
    }

    [Test]
    public async Task SquadLimitsOpeningGrenadesAndDoesNotChainThrows()
    {
        EntityUid map = default;
        var guards = new List<EntityUid>();
        await Server.WaitAssertion(() =>
        {
            var arena = Arena("CMUExpeditionScavenger");
            map = arena.Map;
            guards.Add(arena.Guard);
            var origin = SEntMan.GetComponent<TransformComponent>(arena.Guard).Coordinates;
            guards.Add(SEntMan.SpawnEntity("CMUExpeditionScavenger", origin.Offset(new Vector2(0, -2))));
            guards.Add(SEntMan.SpawnEntity("CMUExpeditionScavenger", origin.Offset(new Vector2(0, -4))));
            foreach (var guard in guards)
            {
                SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).Squad = 7;
                SEntMan.AddComponent<GodmodeComponent>(guard);
                SEntMan.RemoveComponent<StunOnExplosionReceivedComponent>(guard);
            }
            // Keep an enduring contact for the cooldown observation. Native explosion stun
            // still uses legacy status IDs that the generic godmode handler cannot resolve.
            SEntMan.RemoveComponent<StunOnExplosionReceivedComponent>(arena.Enemy);
            var second = SEntMan.SpawnEntity("CMMobHuman",
                SEntMan.GetComponent<TransformComponent>(arena.Enemy).Coordinates.Offset(new Vector2(0, -1)));
            SEntMan.AddComponent<GodmodeComponent>(second);
            SEntMan.RemoveComponent<StunOnExplosionReceivedComponent>(second);
            Server.System<NpcFactionSystem>().AddFaction(second, "GOVFOR");
        });
        for (var sample = 0; sample < 150; sample++)
        {
            await Pair.RunSeconds(0.2f);
            await Server.WaitAssertion(() =>
            {
                var agents = guards.Select(g => SEntMan.GetComponent<CMUExpeditionAgentComponent>(g)).ToArray();
                Assert.That(agents.Count(a => a.Action == CMUTacticalAction.ThrowGrenade), Is.LessThanOrEqualTo(2));
                Assert.That(agents.Sum(a => a.GrenadesThrown), Is.LessThanOrEqualTo(2),
                    "A third equipped guard must respect the squad cooldown after the opening pair throws.");
            });
        }
        await Server.WaitAssertion(() =>
        {
            Assert.That(guards.Sum(g => SEntMan.GetComponent<CMUExpeditionAgentComponent>(g).GrenadesThrown), Is.EqualTo(2),
                "Exercise the two-throw allowance with actual grenades, not only a no-throw safety case.");
            SEntMan.DeleteEntity(map);
        });
    }

    [Test]
    public async Task SquadRescuerPhysicallyPullsACriticalBuddyToSafety()
    {
        EntityUid map = default, guard = default, casualty = default;
        EntityCoordinates initial = default;
        await Server.WaitAssertion(() =>
        {
            EntityUid enemy;
            (map, guard, enemy) = Arena("CMUExpeditionScavenger");
            SEntMan.DeleteEntity(enemy);
            initial = SEntMan.GetComponent<TransformComponent>(guard).Coordinates.Offset(new Vector2(3, -1));
            casualty = SEntMan.SpawnEntity("CMUExpeditionScavengerCautious", initial);
            SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).Squad = 7;
            SEntMan.GetComponent<CMUExpeditionAgentComponent>(casualty).Squad = 7;
            Hurt(casualty, 205);
            Server.System<MobStateSystem>().ChangeMobState(casualty, MobState.Critical);
        });
        var pulled = false;
        var rescued = false;
        for (var i = 0; i < 100 && !rescued; i++)
        {
            await Pair.RunSeconds(0.15f);
            await Server.WaitAssertion(() =>
            {
                pulled |= SEntMan.GetComponent<PullableComponent>(casualty).Puller == guard;
                rescued = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).Rescues > 0;
            });
        }
        await Server.WaitAssertion(() =>
        {
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            Assert.That(pulled, Is.True, $"The rescue must establish a native pull joint: {agent.State}/{agent.Action}, failures={agent.FailedPlans}.");
            Assert.That(rescued, Is.True, $"Complete the drag: {agent.State}/{agent.Action}, failures={agent.FailedPlans}.");
            Assert.That(Server.System<SharedTransformSystem>().InRange(SEntMan.GetComponent<TransformComponent>(casualty).Coordinates, initial, 1), Is.False);
            Assert.That(SEntMan.GetComponent<PullableComponent>(casualty).Puller, Is.Null);
            SEntMan.DeleteEntity(map);
        });
    }

    [Test]
    public async Task RadioReportsCarrySnapshotsAndRequireAWorkingSquadHeadset()
    {
        EntityUid map = default, sender = default, receiver = default, enemy = default, headset = default;
        EntityCoordinates report = default;
        await Server.WaitAssertion(() =>
        {
            (map, sender, enemy) = Arena("CMUExpeditionScavenger");
            receiver = SEntMan.SpawnEntity("CMUExpeditionScavengerCautious", SEntMan.GetComponent<TransformComponent>(sender).Coordinates.Offset(new Vector2(0, 2)));
            SEntMan.GetComponent<CMUExpeditionAgentComponent>(sender).Squad = 12;
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(receiver);
            agent.Squad = 12;
            agent.Home = SEntMan.GetComponent<TransformComponent>(receiver).Coordinates;
            agent.NextThink = SGameTiming.CurTime + TimeSpan.FromMinutes(1);
            report = SEntMan.GetComponent<TransformComponent>(enemy).Coordinates;
            Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(receiver, "ears", out var item), Is.True);
            headset = item!.Value;
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(receiver);
            Assert.That(agent.ReportsReceived, Is.GreaterThan(0));
            Server.System<SharedTransformSystem>().SetCoordinates(enemy, report.Offset(new Vector2(20, 0)));
            agent.NextThink = SGameTiming.CurTime;
            Server.System<CMUExpeditionAgentSystem>().Update(0);
            Assert.That(agent.ContactFromRadio, Is.True);
            Assert.That(agent.LastSeen, Is.EqualTo(report), "Receiving a report must not query the unseen target's current transform.");
            SEntMan.GetComponent<HeadsetComponent>(headset).Enabled = false;
            agent.NextThink = SGameTiming.CurTime + TimeSpan.FromMinutes(1);
            agent.ReportsReceived = 0;
            Server.System<SharedTransformSystem>().SetCoordinates(enemy, report);
        });
        await Pair.RunSeconds(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUExpeditionAgentComponent>(receiver).ReportsReceived, Is.Zero);
            SEntMan.DeleteEntity(map);
        });
    }

    private List<EntityUid> Stored(EntityUid guard)
    {
        Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(guard, "back", out var bag), Is.True);
        return SEntMan.GetComponent<StorageComponent>(bag!.Value).Container.ContainedEntities.ToList();
    }
}
