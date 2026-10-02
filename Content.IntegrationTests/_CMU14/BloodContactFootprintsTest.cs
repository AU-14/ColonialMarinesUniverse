using System.Numerics;
using Content.Server.Decals;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.FootPrint;
using Content.Shared.Gravity;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StepTrigger.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests._CMU14;

[TestFixture]
public sealed class BloodContactFootprintsTest
{
    [TestCase("Blood", false)]
    [TestCase("Blood", true)]
    [TestCase("Water", false)]
    public async Task FloorContactStainsWithoutRequiringSlipperyPuddle(string reagent, bool airborne)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        EntityUid human = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = server.System<SharedMapSystem>();
            var floor = map.Tile.Tile;
            for (var x = 0; x < 6; x++)
                maps.SetTile(map.Grid, new Vector2i(x, 0), floor);
            entities.EnsureComponent<GravityComponent>(map.Grid).Enabled = true;
            human = entities.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            server.System<SharedPhysicsSystem>().SetBodyStatus(human,
                entities.GetComponent<PhysicsComponent>(human), airborne ? BodyStatus.InAir : BodyStatus.OnGround);
            Assert.That(server.System<PuddleSystem>().TrySpillAt(new EntityCoordinates(map.Grid, 2.5f, 0.5f),
                new Solution(reagent, reagent == "Water" ? 2 : 20), out var puddle), Is.True);
            Assert.That(entities.GetComponent<StepTriggerComponent>(puddle).Active, Is.False,
                "This regression requires a puddle whose slip trigger is disabled.");
        });
        await pair.RunTicksSync(2);
        await server.WaitPost(() => server.System<SharedTransformSystem>()
            .SetLocalPosition(human, new Vector2(2.5f, 0.5f)));
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var prints = entities.GetComponent<FootPrintsComponent>(human);
            if (airborne || reagent == "Water")
            {
                Assert.That(prints.PrintsColor.A, Is.Zero, "Clean water and airborne contact must not stain feet.");
                return;
            }

            Assert.That(prints.PrintsColor.A, Is.GreaterThan(0), "Blood contact must stain feet even without slipping.");
            var transforms = server.System<SharedTransformSystem>();
            transforms.SetLocalPosition(human, new Vector2(3.5f, 0.5f));
            var decals = server.System<DecalSystem>();
            Assert.That(decals.GetAllDecals(map.Grid).Any(entry =>
                entry.Decal.Id == prints.LeftBareDecal || entry.Decal.Id == prints.RightBareDecal), Is.True);

            server.System<MobStateSystem>().ChangeMobState(human, MobState.Critical);
            transforms.SetLocalPosition(human, new Vector2(4.5f, 0.5f));
            Assert.That(decals.GetAllDecals(map.Grid).Any(entry => prints.DraggingDecals.Contains(entry.Decal.Id)), Is.True,
                "Dragging a stained body must leave a visible blood trail.");
        });
        await pair.CleanReturnAsync();
    }
}
