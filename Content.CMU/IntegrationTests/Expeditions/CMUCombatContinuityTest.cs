using System.Numerics;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests.Helpers;
using Content.Server.CMU14.Expeditions;
using Content.Server.Fluids.EntitySystems;
using Content.Server.NPC.Systems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.CMU14.Hearing;
using Content.Shared.Damage.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Expeditions;

[TestFixture]
public sealed class CMUCombatContinuityTest : GameTest
{
    private static readonly ProtoId<NpcFactionPrototype> EnemyFaction = "GOVFOR";
    private static readonly ProtoId<NpcFactionPrototype> FriendlyFaction = "CMUExpeditionHostile";

    [Test]
    public async Task SmokeThrowCommitsToWithdrawalWithoutCoveringFire()
    {
        var map = await Pair.CreateTestMap();
        EntityUid guard = default;
        var start = new EntityCoordinates(map.Grid, 7.5f, 6.5f);
        await Server.WaitAssertion(() =>
        {
            Floor(map.Grid, map.Tile.Tile);
            guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", start);
            var buddy = SEntMan.SpawnEntity("CMUExpeditionScavenger", start.Offset(new Vector2(0, 2)));
            var partner = SEntMan.GetComponent<CMUExpeditionAgentComponent>(buddy);
            partner.Squad = 42;
            partner.NextThink = TimeSpan.MaxValue;
            var grenade = SEntMan.SpawnEntity("CMUExpeditionSmokeGrenade", start);
            Assert.That(Server.System<GunSystem>().TryGetGun(guard, out var gun), Is.True);
            Server.System<SharedWieldableSystem>().TryUnwield(gun.Owner, guard);
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            agent.Squad = 42;
            agent.HoldPosition = true;
            agent.State = CMUExpeditionAgentState.Throwing;
            agent.Action = CMUTacticalAction.ThrowGrenade;
            agent.ActionItem = grenade;
            agent.ActionStarted = SGameTiming.CurTime;
            agent.ActionUntil = SGameTiming.CurTime + TimeSpan.FromSeconds(10);
            agent.SmokeGrenade = true;
            agent.GrenadeTarget = start.Offset(new Vector2(4, 0));
            agent.GrenadeObservedContact = start.Offset(new Vector2(9, 0));
            agent.GrenadeContactUntil = SGameTiming.CurTime + TimeSpan.FromSeconds(6);
            agent.CoverDestination = start.Offset(new Vector2(-6, 0));
            agent.MoveUntil = SGameTiming.CurTime + TimeSpan.FromSeconds(10);
            Server.System<NPCSteeringSystem>().Register(guard, agent.CoverDestination.Value);
        });
        await Pair.RunSeconds(0.6f);
        await Server.WaitAssertion(() =>
        {
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            Assert.That(agent.ActionDoAfter, Is.Not.Null, "The grenade is still being prepared through its native do-after.");
            Assert.That(SEntMan.GetComponent<TransformComponent>(guard).Coordinates.X, Is.LessThan(start.X - 0.2f),
                "Grenade preparation must not stop an already accepted withdrawal leg.");
        });
        await Pair.RunSeconds(3.4f);
        await Server.WaitAssertion(() =>
        {
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            Assert.That(agent.SmokesThrown, Is.EqualTo(1), "Exercise the completed native smoke throw.");
            Assert.That(SEntMan.GetComponent<TransformComponent>(guard).Coordinates.X, Is.LessThan(start.X - 2),
                $"Follow the screen with a withdrawal even without a covering shooter: {agent.State}, {agent.DecisionReason}");
        });
    }

    [Test]
    public async Task EngulfedSquadLeavesSmokeWithoutCoveringFire()
    {
        var map = await Pair.CreateTestMap();
        var guards = new List<EntityUid>();
        await Server.WaitAssertion(() =>
        {
            Floor(map.Grid, map.Tile.Tile);
            for (var y = 6; y <= 7; y++)
            {
                var guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", new EntityCoordinates(map.Grid, 7.5f, y + 0.5f));
                guards.Add(guard);
                var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
                agent.Squad = 42;
                agent.HoldPosition = true;
                agent.NextGrenade = TimeSpan.MaxValue;
            }
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            for (var x = 5; x <= 9; x++)
            for (var y = 4; y <= 8; y++)
            {
                var smoke = SEntMan.SpawnEntity("Smoke", new EntityCoordinates(map.Grid, x + 0.5f, y + 0.5f));
                Server.System<SmokeSystem>().StartSmoke(smoke, new Solution(), duration: 15, spreadAmount: 0);
            }
            foreach (var guard in guards)
                SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).IncomingFireUntil = SGameTiming.CurTime + TimeSpan.FromSeconds(10);
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            for (var index = 0; index < guards.Count; index++)
            {
                var position = SEntMan.GetComponent<TransformComponent>(guards[index]).Coordinates;
                Assert.That(Vector2.Distance(position.Position, new Vector2(7.5f, 6.5f + index)), Is.GreaterThan(0.5f),
                    "Every engulfed squadmate must begin moving promptly.");
            }
        });
        await Pair.RunSeconds(2);
        var newClouds = new List<EntityCoordinates>();
        await Server.WaitAssertion(() =>
        {
            foreach (var guard in guards)
            {
                var position = SEntMan.GetComponent<TransformComponent>(guard).Coordinates;
                var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
                Assert.That(position.X < 5 || position.X >= 10 || position.Y < 4 || position.Y >= 9, Is.True,
                    $"Both engulfed squadmates must leave the smoke without covering fire: {position}, " +
                    $"{agent.State}, {agent.DecisionReason}, {agent.IncomingFireDecision}, exit {agent.SmokeEscapeDestination}, " +
                    $"route {agent.Route.Count}, move {agent.MoveUntil}, now {SGameTiming.CurTime}.");
            }
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            foreach (var guard in guards)
            {
                var position = SEntMan.GetComponent<TransformComponent>(guard).Coordinates;
                Assert.That(position.X < 5 || position.X >= 10 || position.Y < 4 || position.Y >= 9, Is.True,
                    "Traffic coordination must not send a withdrawn squadmate back into the cloud.");
                var cloud = new EntityCoordinates(map.Grid, MathF.Floor(position.X) + 0.5f, MathF.Floor(position.Y) + 0.5f);
                newClouds.Add(cloud);
                var smoke = SEntMan.SpawnEntity("Smoke", cloud);
                Server.System<SmokeSystem>().StartSmoke(smoke, new Solution(), duration: 15, spreadAmount: 0);
            }
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            for (var index = 0; index < guards.Count; index++)
            {
                var position = SEntMan.GetComponent<TransformComponent>(guards[index]).Coordinates;
                Assert.That(MathF.Floor(position.X) != MathF.Floor(newClouds[index].X) ||
                    MathF.Floor(position.Y) != MathF.Floor(newClouds[index].Y), Is.True,
                    "Smoke spreading onto a recovery position must restart the escape.");
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task IncomingRoundsThroughSmokeTriggerReturnFireAndEscape(bool hit)
    {
        var map = await Pair.CreateTestMap();
        EntityUid guard = default, weapon = default;
        var start = new EntityCoordinates(map.Grid, 4.5f, 6.5f);
        await Server.WaitAssertion(() =>
        {
            Floor(map.Grid, map.Tile.Tile);
            guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", start);
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            agent.HoldPosition = true;
            agent.DarkSightRange = 30;
            agent.NextGrenade = TimeSpan.MaxValue;
            Assert.That(Server.System<GunSystem>().TryGetGun(guard, out var gun), Is.True);
            weapon = gun.Owner;
            SEntMan.EnsureComponent<TestListenerComponent>(weapon);
        });
        await Pair.RunSeconds(2);
        var before = 0;
        await Server.WaitAssertion(() =>
        {
            for (var y = 0; y < 14; y++)
            {
                var smoke = SEntMan.SpawnEntity("Smoke", new EntityCoordinates(map.Grid, 7.5f, y + 0.5f));
                Server.System<SmokeSystem>().StartSmoke(smoke, new Solution(), duration: 15, spreadAmount: 0);
            }
            var enemy = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 13.5f, 6.5f));
            SEntMan.AddComponent<GodmodeComponent>(enemy);
            Server.System<NpcFactionSystem>().AddFaction(enemy, EnemyFaction);
            before = Ammo(weapon);
            // Feed the native near-pass notification. It exposes the bullet's direction,
            // not a visible target; the shooter immediately changes position behind smoke.
            if (hit)
            {
                var bullet = SEntMan.SpawnEntity("RMCBulletRifle545x39mm", start);
                Server.System<SharedPhysicsSystem>().SetLinearVelocity(bullet, new Vector2(-50, 0));
                Server.System<DamageableSystem>().TryChangeDamage(guard,
                    new DamageSpecifier { DamageDict = { ["Piercing"] = 5 } }, ignoreResistances: true,
                    origin: enemy, tool: bullet, impact: DamageImpact.Projectile);
                SEntMan.DeleteEntity(bullet);
            }
            else
            {
                var shot = new CMUGunFiredEvent(weapon, enemy,
                    SEntMan.GetComponent<TransformComponent>(enemy).Coordinates, start.Offset(new Vector2(-2, 0)));
                SEntMan.EventBus.RaiseEvent(EventSource.Local, ref shot);
            }
            Server.System<SharedTransformSystem>().SetCoordinates(enemy, new EntityCoordinates(map.Grid, 13.5f, 12.5f));
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            var position = SEntMan.GetComponent<TransformComponent>(guard).Coordinates;
            Assert.That(Ammo(weapon), Is.LessThan(before), "Return fire along the observed incoming lane through smoke.");
            Assert.That(Vector2.Distance(position.Position, start.Position), Is.GreaterThan(0.5f),
                "Fire and leave the incoming lane during the same response.");
            Assert.That(agent.Target, Is.Null, "The response must not acquire or follow the hidden shooter.");
            var shots = SEntMan.GetComponent<TestListenerComponent>(weapon).Events.GetOrNew(typeof(GunShotEvent)).Cast<GunShotEvent>().ToArray();
            Assert.That(shots, Is.Not.Empty);
            Assert.That(shots.All(shot => Math.Abs(Server.System<SharedTransformSystem>()
                .ToCoordinates(map.Grid.Owner, Server.System<SharedTransformSystem>().ToMapCoordinates(shot.ToCoordinates)).Y - 6.5f) < 1),
                Is.True, "Aim remains on the observed lane, not the shooter's new hidden position.");
        });
        await Pair.RunSeconds(3);
        var after = 0;
        await Server.WaitAssertion(() =>
        {
            after = Ammo(weapon);
            Assert.That(before - after, Is.InRange(1, 6), "One observation authorizes only a bounded response.");
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() => Assert.That(Ammo(weapon), Is.EqualTo(after), "Expired evidence stops suppression."));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task SuppressionRechecksWallsAndFriendlies(bool wall)
    {
        var map = await Pair.CreateTestMap();
        var start = new EntityCoordinates(map.Grid, 4.5f, 6.5f);
        EntityUid guard = default, weapon = default;
        await Server.WaitAssertion(() =>
        {
            Floor(map.Grid, map.Tile.Tile);
            guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", start);
            SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).NextGrenade = TimeSpan.MaxValue;
            Assert.That(Server.System<GunSystem>().TryGetGun(guard, out var gun), Is.True);
            weapon = gun.Owner;
        });
        await Pair.RunSeconds(2);
        var before = 0;
        await Server.WaitAssertion(() =>
        {
            var enemy = SEntMan.SpawnEntity("CMMobHuman", start.Offset(new Vector2(13, 0)));
            Server.System<NpcFactionSystem>().AddFaction(enemy, EnemyFaction);
            var shot = new CMUGunFiredEvent(weapon, enemy,
                SEntMan.GetComponent<TransformComponent>(enemy).Coordinates, start.Offset(new Vector2(-2, 0)));
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref shot);
            // A new obstruction arrives after observation but before the trigger.
            if (wall)
                SEntMan.SpawnEntity("CMUExpeditionHull", start.Offset(new Vector2(2, 0)));
            else
            {
                var buddy = SEntMan.SpawnEntity("CMMobHuman", start.Offset(new Vector2(9, 0)));
                Server.System<NpcFactionSystem>().AddFaction(buddy, FriendlyFaction);
            }
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            agent.NextThink = TimeSpan.MaxValue;
            before = Ammo(weapon);
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() => Assert.That(Ammo(weapon), Is.EqualTo(before),
            "Suppression still checks physical walls and friendlies beyond the estimated aim point."));
    }

    [Test]
    public async Task CasualtyApproachFiresWhileMovingButTreatmentOwnsTheHands()
    {
        var map = await Pair.CreateTestMap();
        var start = new EntityCoordinates(map.Grid, 4.5f, 3.5f);
        EntityUid guard = default, weapon = default;
        await Server.WaitAssertion(() =>
        {
            Floor(map.Grid, map.Tile.Tile);
            guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", start);
            SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard).NextGrenade = TimeSpan.MaxValue;
            Assert.That(Server.System<GunSystem>().TryGetGun(guard, out var gun), Is.True);
            weapon = gun.Owner;
            SEntMan.EnsureComponent<TestListenerComponent>(weapon);
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            var enemy = SEntMan.SpawnEntity("CMMobHuman", start.Offset(new Vector2(8, 0)));
            SEntMan.AddComponent<GodmodeComponent>(enemy);
            Server.System<NpcFactionSystem>().AddFaction(enemy, EnemyFaction);
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            agent.DarkSightRange = 30;
            agent.Target = enemy;
            agent.LastContact = SGameTiming.CurTime;
            agent.Action = CMUTacticalAction.ApproachCasualty;
            agent.State = CMUExpeditionAgentState.PlanMove;
            // Isolate the trigger executor during an already accepted movement action.
            agent.NextThink = TimeSpan.MaxValue;
            Server.System<NPCSteeringSystem>().Register(guard, start.Offset(new Vector2(0, 8)));
        });
        await Pair.RunSeconds(1.5f);
        var beforeTreatment = 0;
        await Server.WaitAssertion(() =>
        {
            var shots = SEntMan.GetComponent<TestListenerComponent>(weapon).Events.GetOrNew(typeof(GunShotEvent)).Cast<GunShotEvent>();
            Assert.That(shots.Any(shot =>
                Server.System<SharedTransformSystem>().ToCoordinates(map.Grid.Owner,
                    Server.System<SharedTransformSystem>().ToMapCoordinates(shot.FromCoordinates)).Y > start.Y + 0.2f), Is.True,
                "The gun must actually fire after movement starts, without cancelling the approach.");
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            Assert.That(agent.Action, Is.EqualTo(CMUTacticalAction.ApproachCasualty));
            agent.Action = CMUTacticalAction.Treat;
            agent.State = CMUExpeditionAgentState.Healing;
            beforeTreatment = Ammo(weapon);
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() => Assert.That(Ammo(weapon), Is.EqualTo(beforeTreatment),
            "Hands occupied by treatment are not available to fire."));
    }

    [Test]
    public async Task TravelLowersWeaponAndContactRaisesItBeforeMovingFire()
    {
        var map = await Pair.CreateTestMap();
        var start = new EntityCoordinates(map.Grid, 3.5f, 6.5f);
        EntityUid guard = default, weapon = default;
        await Server.WaitAssertion(() =>
        {
            Floor(map.Grid, map.Tile.Tile);
            guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", start);
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            agent.DarkSightRange = 30;
            agent.NextGrenade = TimeSpan.MaxValue;
            Assert.That(Server.System<GunSystem>().TryGetGun(guard, out var gun), Is.True);
            weapon = gun.Owner;
            Assert.That(Server.System<CMUExpeditionAgentSystem>().OrderPosition(guard,
                start.Offset(new Vector2(14, 0)), false), Is.True);
        });
        await Pair.RunSeconds(1);
        var before = 0;
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<TransformComponent>(guard).Coordinates.X, Is.GreaterThan(start.X + 0.5f));
            Assert.That(SEntMan.GetComponent<WieldableComponent>(weapon).Wielded, Is.False,
                "Travel without contact should release the movement penalty.");
            var position = SEntMan.GetComponent<TransformComponent>(guard).Coordinates;
            var enemy = SEntMan.SpawnEntity("CMMobHuman", position.Offset(new Vector2(7, 0)));
            SEntMan.AddComponent<GodmodeComponent>(enemy);
            Server.System<NpcFactionSystem>().AddFaction(enemy, EnemyFaction);
            before = Ammo(weapon);
        });
        await Pair.RunSeconds(1.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<WieldableComponent>(weapon).Wielded, Is.True,
                "Contact raises the rifle while the movement controller is still active.");
            Assert.That(Ammo(weapon), Is.LessThan(before), "Native readiness must lead to a real shot.");
        });
    }

    private void Floor(EntityUid grid, Tile tile)
    {
        var tiles = new List<(Vector2i, Tile)>();
        for (var x = 0; x < 22; x++)
        for (var y = 0; y < 14; y++)
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
