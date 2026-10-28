using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory.VirtualItem;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// A military jeep comes with a searchlight by the driver's windshield post, a colony one without.
/// Taken in an empty hand it lights up and swings where it is aimed, within the gun mount's arc, and
/// letting go of it puts it out.
/// A screwdriver takes it off, and it goes on the passenger's side with a screwdriver in the other hand.
/// </summary>
[TestFixture]
public sealed class CMUJeepSearchlightTest : GameTest
{
    private const string LeftSlot = "vehicle-searchlight-left";
    private const string RightSlot = "vehicle-searchlight-right";

    private async Task<(TestMapData Map, EntityUid Jeep)> SpawnJeep()
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
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

            jeep = SEntMan.SpawnEntity("CMUVehicleJeepCargo", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
        });
        await RunTicksSync(5);
        return (map, jeep);
    }

    private EntityUid PartEntity(EntityUid jeep, string part)
    {
        return SEntMan.GetComponent<CMUOpenVehicleComponent>(jeep).PartEntities
            .First(p => SEntMan.GetComponent<CMUVehiclePartComponent>(p).Part == part);
    }

    private IReadOnlyList<EntityUid> Fitted(EntityUid jeep, string slot)
    {
        return SEntMan.System<SharedContainerSystem>().GetContainer(jeep, slot).ContainedEntities;
    }

    private bool Lit(EntityUid jeep)
    {
        var beam = SEntMan.GetComponent<CMUVehicleSearchlightComponent>(jeep).Beam;
        return beam != null && SEntMan.System<SharedPointLightSystem>().TryGetLight(beam.Value, out var light) && light.Enabled;
    }

    private void Click(EntityUid user, EntityUid part, EntityUid? used = null)
    {
        var at = SEntMan.GetComponent<TransformComponent>(part).Coordinates;
        if (used is { } item)
            SEntMan.EventBus.RaiseLocalEvent(part, new InteractUsingEvent(user, item, part, at));
        else
            SEntMan.EventBus.RaiseLocalEvent(part, new InteractHandEvent(user, part));
    }

    [Test]
    public async Task OnlyMilitaryJeepsComeWithOne()
    {
        var (map, jeep) = await SpawnJeep();
        EntityUid colony = default;
        await Server.WaitPost(() =>
        {
            var crate = SEntMan.SpawnEntity("CMUCrateJeepCargoColony", new EntityCoordinates(map.Grid, 0.5f, -3f));
            colony = SEntMan.System<CMUVehicleCrateSystem>().Open((crate, SEntMan.GetComponent<CMUVehicleCrateComponent>(crate)));
        });
        await RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            var fitted = Fitted(jeep, LeftSlot).Select(e => SEntMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID);
            Assert.That(fitted, Is.EqualTo(new[] { "CMUJeepSearchlight" }), "A military jeep has its searchlight on the driver's side.");
            Assert.That(Fitted(jeep, RightSlot), Is.Empty, "The passenger's mount starts empty.");
            Assert.That(Fitted(colony, LeftSlot), Is.Empty, "A colony jeep comes without one.");
            Assert.That(Lit(jeep), Is.False, "It stays dark until someone takes hold of it.");
        });
    }

    [Test]
    public async Task TakenInHandItLightsAndFollowsItsAim()
    {
        var (map, jeep) = await SpawnJeep();
        EntityUid user = default;
        await Server.WaitPost(() =>
        {
            user = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 1.5f, 0.5f));
            Click(user, PartEntity(jeep, "searchlight_left"));
        });
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var searchlight = SEntMan.GetComponent<CMUVehicleSearchlightComponent>(jeep);
            Assert.That(searchlight.Operator, Is.EqualTo(user));
            Assert.That(Lit(jeep), Is.True, "Taken in hand, it lights up.");
            var held = SEntMan.System<SharedHandsSystem>().EnumerateHeld(user)
                .Select(e => SEntMan.GetComponentOrNull<VirtualItemComponent>(e)?.BlockingEntity)
                .ToList();
            Assert.That(held, Does.Contain(Fitted(jeep, LeftSlot).Single()), "The hand holds the searchlight.");
        });

        // The jeep faces south (0); within its arc the beam swings round to the aim, past it it stops
        // at the edge of the arc.
        foreach (var (aim, expected) in new[] { (30d, 30d), (-120d, -45d) })
        {
            await Server.WaitPost(() =>
                SEntMan.System<CMUVehicleSearchlightSystem>().Aim((jeep, SEntMan.GetComponent<CMUVehicleSearchlightComponent>(jeep)),
                    Angle.FromDegrees(aim)));
            await RunSeconds(1f);
            await Server.WaitAssertion(() =>
            {
                var beam = SEntMan.GetComponent<CMUVehicleSearchlightComponent>(jeep).Beam!.Value;
                var pointing = SEntMan.System<SharedTransformSystem>().GetWorldRotation(beam);
                Assert.That(Angle.ShortestDistance(pointing, Angle.FromDegrees(expected)).Degrees, Is.EqualTo(0).Within(0.5),
                    $"Aimed at {aim} degrees, it points at {expected}.");
            });
        }

        await Server.WaitPost(() =>
        {
            var hands = SEntMan.System<SharedHandsSystem>();
            var hold = hands.EnumerateHeld(user).First(SEntMan.HasComponent<VirtualItemComponent>);
            Assert.That(hands.TryDrop(user, hold), Is.True);
        });
        await RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUVehicleSearchlightComponent>(jeep).Operator, Is.Null);
            Assert.That(Lit(jeep), Is.False, "Let go of, it goes dark.");
            Assert.That(SEntMan.HasComponent<CMUVehicleSearchlightOperatorComponent>(user), Is.False);
        });
    }

    [Test]
    public async Task ItMovesAcrossWithAScrewdriver()
    {
        var (map, jeep) = await SpawnJeep();
        EntityUid user = default;
        EntityUid screwdriver = default;
        EntityUid light = default;
        await Server.WaitPost(() =>
        {
            user = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, -1f));
            screwdriver = SEntMan.SpawnEntity("CMScrewdriver", new EntityCoordinates(map.Grid, 0.5f, -1f));
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(user, screwdriver), Is.True);
            light = Fitted(jeep, LeftSlot).Single();
            Click(user, PartEntity(jeep, "searchlight_left"), screwdriver);
        });
        await RunSeconds(3f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Fitted(jeep, LeftSlot), Is.Empty, "The screwdriver takes it off.");
            Assert.That(SEntMan.System<SharedHandsSystem>().IsHolding(user, light), Is.True, "It comes into the free hand.");
        });

        // Without the screwdriver it won't go on.
        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.System<SharedHandsSystem>().TryDrop(user, screwdriver), Is.True);
            Click(user, PartEntity(jeep, "searchlight_right"), light);
        });
        await RunSeconds(3f);
        await Server.WaitAssertion(() => Assert.That(Fitted(jeep, RightSlot), Is.Empty, "It needs a screwdriver to fit."));

        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(user, screwdriver), Is.True);
            Click(user, PartEntity(jeep, "searchlight_right"), light);
        });
        await RunSeconds(3f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Fitted(jeep, RightSlot), Does.Contain(light), "With a screwdriver it goes on the passenger's side.");
            Assert.That(SEntMan.GetComponent<CMUVehicleLampsComponent>(jeep).Broken, Does.Not.Contain("searchlight_right"));
        });

        // Worked from its new side, it lights up there.
        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.System<SharedHandsSystem>().TryDrop(user, screwdriver), Is.True);
            Click(user, PartEntity(jeep, "searchlight_right"));
        });
        await RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Lit(jeep), Is.True);
            Assert.That(SEntMan.GetComponent<CMUVehicleSearchlightComponent>(jeep).Lamp, Is.EqualTo("searchlight_right"));
        });
    }
}
