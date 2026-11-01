using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Expeditions;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._RMC14.Weapons.Ranged.Chamber;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage.Components;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.NPC.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Trigger.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Expeditions;

[TestFixture]
public sealed class CMUAssaultReadinessTest : GameTest
{
    private static readonly ProtoId<Content.Shared.NPC.Prototypes.NpcFactionPrototype> EnemyFaction = "GOVFOR";

    [TestCase(true)]
    [TestCase(false)]
    public async Task PreparedSmokeMustScreenTheThreatenedApproach(bool screensApproach)
    {
        var map = await Pair.CreateTestMap();
        EntityUid grenade = default;
        await Server.WaitAssertion(() =>
        {
            Floor(map.Grid, map.Tile.Tile);
            var start = new EntityCoordinates(map.Grid, 2.5f, 4.5f);
            var guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", start);
            grenade = SEntMan.SpawnEntity("CMUExpeditionSmokeGrenade", start);
            Assert.That(Server.System<GunSystem>().TryGetGun(guard, out var gun), Is.True);
            Server.System<SharedWieldableSystem>().TryUnwield(gun.Owner, guard);
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            // Continue a prepared throw through real hand use and its native do-after.
            // The side-room landing is throwable but screens none of the observed approach.
            agent.State = CMUExpeditionAgentState.Throwing;
            agent.Action = CMUTacticalAction.ThrowGrenade;
            agent.ActionItem = grenade;
            agent.ActionStarted = SGameTiming.CurTime;
            agent.ActionUntil = SGameTiming.CurTime + TimeSpan.FromSeconds(10);
            agent.SmokeGrenade = true;
            agent.GrenadeTarget = screensApproach ? start.Offset(new Vector2(4, 0)) : start.Offset(new Vector2(0, 3));
            agent.GrenadeObservedContact = start.Offset(new Vector2(10, 0));
            agent.GrenadeContactUntil = SGameTiming.CurTime + TimeSpan.FromSeconds(6);
        });
        await Pair.RunSeconds(1.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<ActiveTimerTriggerComponent>(grenade), Is.EqualTo(screensApproach),
                "Only a screen that intersects the threatened approach should be thrown and primed.");
            if (screensApproach)
                Assert.That(Server.System<SharedContainerSystem>().IsEntityOrParentInContainer(grenade), Is.False);
        });
    }

    [Test]
    public async Task StationaryGunnerDeploysBipodAndSustainsSafeCoveringFire()
    {
        var map = await Pair.CreateTestMap();
        EntityUid guard = default, weapon = default;
        await Server.WaitAssertion(() =>
        {
            Floor(map.Grid, map.Tile.Tile);
            guard = SEntMan.SpawnEntity("CMUExpeditionScavengerMachinegunner", new EntityCoordinates(map.Grid, 2.5f, 4.5f));
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            agent.HoldPosition = true;
            agent.Squad = 42;
            agent.DarkSightRange = 20;
            agent.NextGrenade = TimeSpan.MaxValue;
            Assert.That(Server.System<GunSystem>().TryGetGun(guard, out var gun), Is.True);
            weapon = gun.Owner;
        });
        await Pair.RunSeconds(4);
        await Server.WaitAssertion(() =>
        {
            var holder = SEntMan.GetComponent<AttachableHolderComponent>(weapon);
            Assert.That(holder.Slots.Keys.Any(slot =>
                Server.System<AttachableHolderSystem>().TryGetAttachable((weapon, holder), slot, out var attachment) &&
                SEntMan.TryGetComponent<AttachableToggleableComponent>(attachment, out var toggle) && toggle.Active &&
                toggle.InstantToggle == AttachableInstantToggleConditions.Brace), Is.True,
                "A stationary gunner should complete native bipod deployment before contact.");
            var enemy = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 12.5f, 4.5f));
            SEntMan.AddComponent<GodmodeComponent>(enemy);
            Server.System<NpcFactionSystem>().AddFaction(enemy, EnemyFaction);
        });
        var sustained = false;
        for (var i = 0; i < 40 && !sustained; i++)
        {
            await Pair.RunSeconds(0.2f);
            await Server.WaitAssertion(() =>
            {
                var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
                sustained = agent.SustainedFire && agent.ShotsFired > 8;
            });
        }
        await Server.WaitAssertion(() =>
        {
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            Assert.That(sustained, Is.True, $"{agent.State}, {agent.LastFireCheck}, volley={agent.FireControlVolley}, shots={agent.ShotsFired}");
            var friendly = SEntMan.SpawnEntity("CMUExpeditionScavenger", new EntityCoordinates(map.Grid, 7.5f, 4.5f));
            SEntMan.AddComponent<GodmodeComponent>(friendly);
            SEntMan.GetComponent<CMUExpeditionAgentComponent>(friendly).NextThink = TimeSpan.MaxValue;
        });
        await Pair.RunTicksSync(2);
        var remaining = 0;
        await Server.WaitAssertion(() => remaining = Ammo(weapon));
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() => Assert.That(Ammo(weapon), Is.EqualTo(remaining),
            "A long suppression volley must still stop for a friendly entering the lane."));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task EngagesAcrossAlmayerGlassDoorBeforeCrossingIt(bool open)
    {
        var map = await Pair.CreateTestMap();
        EntityUid guard = default, rifle = default, door = default;
        await Server.WaitAssertion(() =>
        {
            Floor(map.Grid, map.Tile.Tile);
            for (var y = 0; y < 10; y++)
                if (y != 4)
                    SEntMan.SpawnEntity("CMUExpeditionHull", new EntityCoordinates(map.Grid, 8.5f, y + 0.5f));
            door = SEntMan.SpawnEntity("CMDoubleDoorAlmayerGlass", new EntityCoordinates(map.Grid, 8.5f, 4.5f));
            if (open)
                Server.System<SharedDoorSystem>().StartOpening(door);
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<DoorComponent>(door).State, Is.EqualTo(open ? DoorState.Open : DoorState.Closed));
            guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", new EntityCoordinates(map.Grid, 6.5f, 4.5f));
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            agent.DarkSightRange = 20;
            agent.NextGrenade = TimeSpan.MaxValue;
            Assert.That(Server.System<GunSystem>().TryGetGun(guard, out var gun), Is.True);
            rifle = gun.Owner;
            for (var i = 0; i < 2; i++)
            {
                var enemy = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 12.5f, 4.5f + i));
                SEntMan.AddComponent<GodmodeComponent>(enemy);
                Server.System<NpcFactionSystem>().AddFaction(enemy, EnemyFaction);
            }
            Assert.That(Server.System<CMUExpeditionAgentSystem>().OrderAssault(guard,
                new EntityCoordinates(map.Grid, 14.5f, 4.5f)), Is.True);
        });
        var fired = false;
        for (var i = 0; i < 15 && !fired; i++)
        {
            await Pair.RunSeconds(0.1f);
            await Server.WaitAssertion(() =>
            {
                fired = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).LastFiredWeapon == rifle;
                Assert.That(SEntMan.GetComponent<TransformComponent>(guard).Coordinates.X, Is.LessThan(8.5f),
                    "A visible contact must receive fire before the guard walks through its doorway.");
            });
        }
        await Server.WaitAssertion(() => Assert.That(fired, Is.True,
            SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).LastFireCheck));
    }

    [Test]
    public async Task SecuredAssaultKeepsCoveringWhileAnotherMemberAdvances()
    {
        var map = await Pair.CreateTestMap();
        var guards = new List<EntityUid>();
        var guns = new List<EntityUid>();
        await Server.WaitAssertion(() =>
        {
            Floor(map.Grid, map.Tile.Tile);
            for (var i = 0; i < 2; i++)
            {
                var guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", new EntityCoordinates(map.Grid, 2.5f, 2.5f + i * 3));
                guards.Add(guard);
                var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
                agent.Squad = 42;
                agent.DarkSightRange = 20;
                Assert.That(Server.System<GunSystem>().TryGetGun(guard, out var gun), Is.True);
                guns.Add(gun.Owner);
                Assert.That(Server.System<CMUExpeditionAgentSystem>().OrderAssault(guard,
                    new EntityCoordinates(map.Grid, 5.5f, 2.5f + i * 3)), Is.True);
            }
        });
        await Pair.RunSeconds(5);
        var ammunition = new List<int>();
        await Server.WaitAssertion(() =>
        {
            foreach (var guard in guards)
            {
                Assert.That(SEntMan.GetComponent<TransformComponent>(guard).Coordinates.X, Is.InRange(5, 6));
                // Exclude optional cover relocation: this exercises the assault's own bounds.
                SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).NextReposition = TimeSpan.MaxValue;
            }
            ammunition.AddRange(guns.Select(Ammo));
            var enemy = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 16.5f, 4));
            SEntMan.AddComponent<GodmodeComponent>(enemy);
            Server.System<NpcFactionSystem>().AddFaction(enemy, EnemyFaction);
        });
        var coveredAdvance = false;
        for (var i = 0; i < 50 && !coveredAdvance; i++)
        {
            await Pair.RunSeconds(0.2f);
            await Server.WaitAssertion(() =>
            {
                for (var member = 0; member < 2; member++)
                    coveredAdvance |= SEntMan.GetComponent<TransformComponent>(guards[member]).Coordinates.X > 7 &&
                        SEntMan.GetComponent<TransformComponent>(guards[1 - member]).Coordinates.X < 6.5f &&
                        Ammo(guns[1 - member]) < ammunition[1 - member];
            });
        }
        await Server.WaitAssertion(() => Assert.That(coveredAdvance, Is.True,
            string.Join('\n', guards.Select(guard =>
            {
                var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
                return $"{SEntMan.GetComponent<TransformComponent>(guard).Coordinates}: {agent.AssaultDecision}, {agent.Duty}, {agent.LastFireCheck}";
            }))));
    }

    [Test]
    public async Task ReloadsLastChamberedRoundBeforeAdvancing()
    {
        var map = await Pair.CreateTestMap();
        EntityUid guard = default, rifle = default;
        EntityCoordinates start = default;
        await Server.WaitAssertion(() =>
        {
            Floor(map.Grid, map.Tile.Tile);
            start = new EntityCoordinates(map.Grid, 2.5f, 2.5f);
            guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", start);
            SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).NextThink = TimeSpan.MaxValue;
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<GunSystem>().TryGetGun(guard, out var gun), Is.True);
            rifle = gun.Owner;
            Assert.That(Server.System<ItemSlotsSystem>().TryEject(rifle, "gun_magazine", guard, out var magazine), Is.True);
            SEntMan.DeleteEntity(magazine!.Value);
            var chamber = SEntMan.GetComponent<RMCGunChamberComponent>(rifle);
            Assert.That(Server.System<SharedContainerSystem>().TryGetContainer(rifle, chamber.ContainerId, out var rounds), Is.True);
            Assert.That(rounds!.ContainedEntities.Count, Is.EqualTo(1), "The remaining live round reproduces the nearly empty push.");
            Assert.That(Server.System<CMUExpeditionAgentSystem>().OrderAssault(guard, start.Offset(new Vector2(7, 0))), Is.True);
            SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).NextThink = SGameTiming.CurTime;
        });
        var reloaded = false;
        for (var i = 0; i < 40 && !reloaded; i++)
        {
            await Pair.RunSeconds(0.2f);
            await Server.WaitAssertion(() =>
            {
                var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
                reloaded = agent.Reloads > 0;
                if (!reloaded)
                    Assert.That(Server.System<SharedTransformSystem>().InRange(start,
                    SEntMan.GetComponent<TransformComponent>(guard).Coordinates, 0.5f), Is.True,
                    $"Prepare ammunition before advancing: {agent.State}, {agent.Action}, {agent.AssaultDecision}, " +
                    $"{agent.WeaponDecision}, reloads={agent.Reloads}, ammo={Ammo(rifle)}.");
            });
        }
        await Server.WaitAssertion(() => Assert.That(Ammo(rifle), Is.GreaterThan(10), "Reload actual reserve ammunition."));
        await Pair.RunSeconds(5);
        await Server.WaitAssertion(() => Assert.That(Server.System<SharedTransformSystem>().InRange(start,
            SEntMan.GetComponent<TransformComponent>(guard).Coordinates, 2), Is.False, "Resume the assault after reloading."));
    }

    [Test]
    public async Task RetrievesLostPrimaryWhileHoldingSidearmDuringLull()
    {
        var map = await Pair.CreateTestMap();
        EntityUid guard = default, rifle = default;
        await Server.WaitAssertion(() =>
        {
            Floor(map.Grid, map.Tile.Tile);
            var start = new EntityCoordinates(map.Grid, 2.5f, 2.5f);
            guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", start);
            Assert.That(Server.System<GunSystem>().TryGetGun(guard, out var gun), Is.True);
            rifle = gun.Owner;
            SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).DarkSightRange = 20;
            Server.System<SharedWieldableSystem>().TryUnwield(rifle, guard);
            Assert.That(Server.System<SharedHandsSystem>().TryDrop(guard, rifle), Is.True);
            Server.System<SharedTransformSystem>().SetCoordinates(rifle, start.Offset(new Vector2(4, 0)));
            var pistol = SEntMan.SpawnEntity("CMUExpeditionSidearm", start);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(guard, pistol), Is.True);
        });
        await Pair.RunSeconds(10);
        await Server.WaitAssertion(() =>
        {
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            Assert.That(Server.System<SharedContainerSystem>().IsEntityOrParentInContainer(rifle), Is.True,
                $"Recover the owned rifle four tiles away instead of forgetting it after drawing a pistol: {agent.WeaponRecoveryDecision}.");
            Assert.That(Server.System<GunSystem>().TryGetGun(guard, out var gun), Is.True);
            Assert.That(gun.Owner, Is.EqualTo(rifle), "Re-equip the recovered primary.");
        });
    }

    private void Floor(EntityUid grid, Tile tile)
    {
        var tiles = new List<(Vector2i, Tile)>();
        for (var x = 0; x < 18; x++)
        for (var y = 0; y < 10; y++)
            tiles.Add((new Vector2i(x, y), tile));
        Server.System<SharedMapSystem>().SetTiles(grid, SEntMan.GetComponent<MapGridComponent>(grid), tiles);
    }

    private int Ammo(EntityUid weapon)
    {
        var ammo = new GetAmmoCountEvent();
        SEntMan.EventBus.RaiseLocalEvent(weapon, ref ammo);
        return ammo.Count;
    }
}
