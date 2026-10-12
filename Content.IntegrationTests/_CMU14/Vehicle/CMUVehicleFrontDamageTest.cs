using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Vehicle;
using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Tools.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// Head on crashes crumple the jeep's front, more with each, and side crashes don't; a welder used on
/// the front beats it straight a pass at a time, and one used elsewhere on the jeep leaves it be.
/// </summary>
[TestFixture]
public sealed class CMUVehicleFrontDamageTest : GameTest
{
    private async Task<(EntityUid Jeep, EntityUid User, EntityUid Welder)> SpawnJeep()
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
        EntityUid user = default;
        EntityUid welder = default;
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -4; x <= 4; x++)
            {
                for (var y = -4; y <= 4; y++)
                {
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                }
            }

            // Facing south: the front is towards -y.
            jeep = SEntMan.SpawnEntity("CMUVehicleJeepCargo", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            user = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, -1.2f));
            welder = SEntMan.SpawnEntity("CMWelder", new EntityCoordinates(map.Grid, 0.5f, -1.2f));
        });
        await RunTicksSync(5);
        return (jeep, user, welder);
    }

    private void Crash(EntityUid jeep, bool frontal)
    {
        var ev = new CMUVehicleCrashedEvent(30f, frontal, 1f);
        SEntMan.EventBus.RaiseLocalEvent(jeep, ref ev);
    }

    private float Damage(EntityUid jeep)
    {
        return SEntMan.GetComponent<CMUVehicleFrontDamageComponent>(jeep).Damage;
    }

    /// <summary>
    /// A point on the jeep that its crayon map puts on the front, or behind the middle for the back.
    /// </summary>
    private EntityCoordinates Surface(EntityUid jeep, bool front)
    {
        var crayon = SEntMan.System<SharedCMUVehicleCrayonSystem>();
        var comp = SEntMan.GetComponent<CMUVehicleCrayonComponent>(jeep);
        var xform = SEntMan.GetComponent<TransformComponent>(jeep);
        for (var y = front ? -0.3f : 0.3f; MathF.Abs(y) < 1.6f; y += front ? -0.05f : 0.05f)
        {
            var click = xform.Coordinates.Offset(new Vector2(0f, y));
            if (crayon.TryGetClickedSurface((jeep, comp), click, out _, out var voxel, out _, out _) &&
                (front ? voxel.X >= 22f : voxel.X <= -10f))
            {
                return click;
            }
        }

        Assert.Fail($"No {(front ? "front" : "back")} surface on the jeep's crayon map.");
        return default;
    }

    [Test]
    public async Task HeadOnCrashesCrumpleTheFrontAndAWelderStraightensIt()
    {
        var (jeep, user, welder) = await SpawnJeep();
        var fuelBefore = 0f;
        await Server.WaitPost(() =>
        {
            var front = SEntMan.System<CMUVehicleFrontDamageSystem>();
            Crash(jeep, false);
            Assert.That(Damage(jeep), Is.Zero, "A side or rear crash leaves the front alone.");

            Crash(jeep, true);
            Assert.That(Damage(jeep), Is.EqualTo(30f).Within(0.01f));
            Assert.That(front.GetLevel(jeep), Is.EqualTo(2));
            Crash(jeep, true);
            Crash(jeep, true);
            Assert.That(Damage(jeep), Is.EqualTo(SEntMan.GetComponent<CMUVehicleFrontDamageComponent>(jeep).MaxDamage));
            Assert.That(front.GetLevel(jeep), Is.EqualTo(3));

            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(user, welder), Is.True);
            Assert.That(SEntMan.System<ItemToggleSystem>().TryActivate(welder, user), Is.True, "The welder should light.");
            fuelBefore = SEntMan.System<SharedToolSystem>().GetWelderFuelAndCapacity(welder).fuel.Float();

            // A welder on the back works the hull, not the front.
            var back = new InteractUsingEvent(user, welder, jeep, Surface(jeep, false));
            SEntMan.EventBus.RaiseLocalEvent(jeep, back);
        });
        await RunSeconds(4f);
        await Server.WaitAssertion(() =>
            Assert.That(Damage(jeep), Is.EqualTo(60f).Within(0.01f), "Welding the back shouldn't touch the front."));

        await Server.WaitPost(() =>
        {
            var ev = new InteractUsingEvent(user, welder, jeep, Surface(jeep, true));
            SEntMan.EventBus.RaiseLocalEvent(jeep, ev);
            Assert.That(ev.Handled, Is.True);
        });
        await RunSeconds(3.5f);
        await Server.WaitAssertion(() =>
            Assert.That(Damage(jeep), Is.EqualTo(45f).Within(0.01f), "One pass takes some of it out."));

        await RunSeconds(10f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Damage(jeep), Is.Zero, "The passes repeat until the front is straight.");
            Assert.That(SEntMan.System<SharedToolSystem>().GetWelderFuelAndCapacity(welder).fuel.Float(),
                Is.LessThan(fuelBefore), "Welding uses fuel.");
        });
    }
}
