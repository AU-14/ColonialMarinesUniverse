using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Expeditions;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Damage.Components;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Physics;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Expeditions;

[TestFixture, NonParallelizable]
public sealed class CMUExpeditionScenarioCombatTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true, Connected = true };

    [TestCase("Trees", "CMUExpeditionWoodland", CMUExpeditionLandform.RiverValley)]
    [TestCase("MULE", "CMUExpeditionMountain", CMUExpeditionLandform.Highlands)]
    [TestCase("Mountain", "CMUExpeditionMountain", CMUExpeditionLandform.Highlands)]
    [TestCase("Swamp", "CMUExpeditionSwamp", CMUExpeditionLandform.RiverValley)]
    public async Task InfantryFightsAMovingPlayerInGeneratedTerrain(string scene, string profile, CMUExpeditionLandform landform)
    {
        EntityUid map = default, guard = default, player = default, rifle = default;
        EntityCoordinates initial = default;
        Direction movement = Direction.East;
        var ammoBefore = 0;
        var maximumTravel = 0f;
        var acquired = false;
        var trace = new List<string>();
        var previousAttached = ServerSession!.AttachedEntity;
        await Server.WaitAssertion(() =>
        {
            var generator = Server.System<CMUExpeditionSystem>();
            Assert.That(generator.TryGenerate(profile, 42, landform, CMUExpeditionStory.CrashRecovery, out map, out var error), Is.True, error);
            var expedition = SEntMan.GetComponent<CMUExpeditionMapComponent>(map);
            for (var i = 0; i < 900 && !expedition.Ready; i++)
                generator.Update(0);
            Assert.That(expedition.Ready, Is.True);
            var pair = FindEncounter(map, expedition.Plan, scene);
            Assert.That(pair, Is.Not.Null, $"Seed 42 must contain a real {scene} combat lane, without replacing its terrain.");
            var (from, to, direction) = pair!.Value;
            movement = direction;
            initial = to;
            guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", from);
            player = SEntMan.SpawnEntity("CMMobHuman", to);
            SEntMan.AddComponent<GodmodeComponent>(player);
            Server.System<NpcFactionSystem>().AddFaction(player, "GOVFOR");
            Server.PlayerMan.SetAttachedEntity(ServerSession, player);
            Assert.That(Server.System<GunSystem>().TryGetGun(guard, out var gun), Is.True);
            rifle = gun.Owner;
            ammoBefore = Ammo(rifle);
            trace.Add($"{scene}: guard={from}, player={to}, movement={movement}");
        });
        for (var sample = 0; sample < 100; sample++)
        {
            await Server.WaitAssertion(() =>
            {
                var mover = SEntMan.EnsureComponent<InputMoverComponent>(player);
                var controller = Server.System<SharedMoverController>();
                if (sample % 4 == 0)
                    controller.SetVelocityDirection((player, mover), movement, 0, true);
                if (sample % 4 == 2)
                    controller.SetVelocityDirection((player, mover), movement, 0, false);
                // Reverse every two samples; physics and collision, not teleporting, move the player.
                var opposite = movement == Direction.East ? Direction.West : Direction.South;
                controller.SetVelocityDirection((player, mover), opposite, 0, sample % 4 >= 2);
            });
            await Pair.RunSeconds(0.15f);
            await Server.WaitAssertion(() =>
            {
                var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
                acquired |= agent.Target == player;
                var position = SEntMan.GetComponent<TransformComponent>(guard).Coordinates;
                var plan = SEntMan.GetComponent<CMUExpeditionMapComponent>(map).Plan;
                var cell = plan.Index((int) position.X, (int) position.Y);
                Assert.That(plan.Terrain[cell], Is.Not.AnyOf(CMUExpeditionTerrain.Water, CMUExpeditionTerrain.Cliff), "Do not route a rifleman into the bank or mountain.");
                Assert.That(agent.LastSearchCells, Is.LessThanOrEqualTo(256));
                Assert.That(agent.LastRouteCells, Is.LessThanOrEqualTo(256));
                if (agent.Action != null)
                    Assert.That(SGameTiming.CurTime - agent.ActionStarted, Is.LessThan(TimeSpan.FromSeconds(11)), "Long travel must time out and replan.");
                maximumTravel = Math.Max(maximumTravel, Vector2.Distance(initial.Position, SEntMan.GetComponent<TransformComponent>(player).Coordinates.Position));
                if (sample % 20 == 0)
                    trace.Add($"{agent.State}/{agent.Action}, guard={position}, ammo={Ammo(rifle)}, fire={agent.LastFireCheck}, anchor={agent.CoverAnchor}, peek={agent.PeekPosition}, search={agent.LastSearchMilliseconds:F2}ms, route={agent.LastRouteMilliseconds:F2}ms");
            });
        }
        await Server.WaitAssertion(() =>
        {
            Assert.That(maximumTravel, Is.GreaterThan(0.3f), "The connected player body must actually move through native input and physics.");
            Assert.That(acquired, Is.True, string.Join(';', trace));
            Assert.That(Ammo(rifle), Is.LessThan(ammoBefore), $"The guard must find a usable shot against the moving player: {string.Join(';', trace)}");
            TestContext.Progress.WriteLine(string.Join(Environment.NewLine, trace));
            Server.PlayerMan.SetAttachedEntity(ServerSession, previousAttached);
            SEntMan.DeleteEntity(map);
        });
    }

    private (EntityCoordinates Guard, EntityCoordinates Player, Direction Movement)? FindEncounter(EntityUid map, CMUExpeditionPlan plan, string scene)
    {
        var interaction = Server.System<SharedInteractionSystem>();
        var transform = Server.System<SharedTransformSystem>();
        var offsets = new[] { (7, 0), (-7, 0), (0, 7), (0, -7), (5, 3), (-5, -3), (3, 5), (-3, -5), (5, 0), (0, 5) };
        bool Open(int x, int y) => x > 1 && y > 1 && x < plan.Size - 2 && y < plan.Size - 2 &&
            plan.Props[plan.Index(x, y)] == CMUExpeditionProp.None &&
            plan.Terrain[plan.Index(x, y)] is not (CMUExpeditionTerrain.Water or CMUExpeditionTerrain.Cliff) &&
            !plan.FirePockets.Any(f => Math.Abs(f.X - x) <= 3 && Math.Abs(f.Y - y) <= 3);
        foreach (var cell in Enumerable.Range(0, plan.Size * plan.Size).OrderBy(i =>
                     Math.Abs(i % plan.Size - plan.Objective.X) + Math.Abs(i / plan.Size - plan.Objective.Y)))
        {
            var x = cell % plan.Size;
            var y = cell / plan.Size;
            if (!Open(x, y))
                continue;
            var trees = 0;
            var feature = scene == "MULE" && plan.WreckFloors.ContainsKey(cell);
            for (var dx = -3; dx <= 3; dx++)
            for (var dy = -3; dy <= 3; dy++)
            {
                if (x + dx < 0 || y + dy < 0 || x + dx >= plan.Size || y + dy >= plan.Size)
                    continue;
                var nearby = plan.Index(x + dx, y + dy);
                if (plan.Props[nearby] == CMUExpeditionProp.Tree) trees++;
                feature |= scene == "Mountain" && plan.Terrain[nearby] == CMUExpeditionTerrain.Cliff;
                feature |= scene == "Swamp" && plan.Terrain[nearby] == CMUExpeditionTerrain.Water;
            }
            feature |= scene == "Trees" && trees >= 6;
            if (!feature)
                continue;
            foreach (var (dx, dy) in offsets)
            {
                var px = x + dx;
                var py = y + dy;
                if (!Open(px, py) || scene == "MULE" && !plan.WreckFloors.ContainsKey(plan.Index(px, py)))
                    continue;
                var direction = Open(px - 1, py) && Open(px + 1, py) ? Direction.East : Direction.North;
                if (direction == Direction.North && (!Open(px, py - 1) || !Open(px, py + 1)))
                    continue;
                var from = new EntityCoordinates(map, new Vector2(x + 0.5f, y + 0.5f));
                var to = new EntityCoordinates(map, new Vector2(px + 0.5f, py + 0.5f));
                if (interaction.InRangeUnobstructed(transform.ToMapCoordinates(from), transform.ToMapCoordinates(to), 12,
                        CollisionGroup.Impassable | CollisionGroup.InteractImpassable | CollisionGroup.BulletImpassable))
                    return (from, to, direction);
            }
        }
        return null;
    }

    private int Ammo(EntityUid rifle)
    {
        var ammo = new GetAmmoCountEvent();
        SEntMan.EventBus.RaiseLocalEvent(rifle, ref ammo);
        return ammo.Count;
    }
}
