using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// A crate pulled to the cargo jeep goes aboard over the lowered tailgate when the jeep is clicked
/// with the pulling hand, and a wrench then fastens it down. Once loosened again, grabbing it takes it
/// off the bed.
/// </summary>
[TestFixture]
public sealed class CMUJeepCargoLoadTest : GameTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task PulledCrateLoadsOnClickAndWrenchSecuresIt(bool clickRearPart)
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
        EntityUid crate = default;
        EntityUid user = default;

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

            // The jeep faces south, so its cargo bed is to the north. The user stands right behind the
            // bed: in reach of the jeep's body (rear edge 0.85 away), not of its centre (1.9 away),
            // where the bodiless part masks sit.
            jeep = SEntMan.SpawnEntity("CMUVehicleJeepCargo", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            crate = SEntMan.SpawnEntity("RMCCrateGreen", new EntityCoordinates(map.Grid, 1.6f, 3.1f));
            user = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 2.4f));
        });
        await RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.System<PullingSystem>().TryStartPull(user, crate), Is.True);
        });
        await RunTicksSync(5);

        EntityUid target = default;
        await Server.WaitPost(() =>
        {
            target = jeep;
            if (clickRearPart)
            {
                target = SEntMan.GetComponent<CMUJeepComponent>(jeep).PartEntities
                    .First(part => SEntMan.GetComponent<CMUVehiclePartComponent>(part).Part == CMUJeepSystem.Tailgate);
            }

            SEntMan.System<SharedInteractionSystem>()
                .UserInteraction(user, SEntMan.GetComponent<TransformComponent>(target).Coordinates, target);
        });
        await RunSeconds(3);

        // The tailgate is up: nothing goes aboard until it is lowered.
        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUVehicleCargoComponent>(jeep).Crate, Is.Null,
                "A crate should not load over a shut tailgate.");
            SEntMan.System<CMUJeepSystem>()
                .SetPanel((jeep, SEntMan.GetComponent<CMUJeepComponent>(jeep)), CMUJeepSystem.Tailgate, true);
            SEntMan.System<SharedInteractionSystem>()
                .UserInteraction(user, SEntMan.GetComponent<TransformComponent>(target).Coordinates, target);
        });
        await RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            var cargo = SEntMan.GetComponent<CMUVehicleCargoComponent>(jeep);
            Assert.That(cargo.Crate, Is.EqualTo(crate), "Clicking the jeep with the pulling hand should load the crate.");
            Assert.That(cargo.Secured, Is.False);
            Assert.That(SEntMan.GetComponent<TransformComponent>(crate).ParentUid, Is.EqualTo(jeep));
        });

        await Server.WaitPost(() =>
        {
            var wrench = SEntMan.SpawnEntity("CMWrench", SEntMan.GetComponent<TransformComponent>(user).Coordinates);
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(user, wrench), Is.True);
            SEntMan.System<SharedInteractionSystem>()
                .UserInteraction(user, SEntMan.GetComponent<TransformComponent>(jeep).Coordinates, jeep);
        });
        await RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUVehicleCargoComponent>(jeep).Secured, Is.True,
                "A wrench on the jeep should fasten the loaded crate down.");
        });

        // Wrenched down, it can't be grabbed off.
        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.System<PullingSystem>().TryStartPull(user, crate), Is.False);
            Assert.That(SEntMan.GetComponent<CMUVehicleCargoComponent>(jeep).Crate, Is.EqualTo(crate));
            SEntMan.System<SharedInteractionSystem>()
                .UserInteraction(user, SEntMan.GetComponent<TransformComponent>(jeep).Coordinates, jeep);
        });
        await RunSeconds(3);

        // Loosened, a grab sets it down behind the jeep and pulls it.
        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUVehicleCargoComponent>(jeep).Secured, Is.False,
                "A second wrench should loosen the crate.");
            Assert.That(SEntMan.System<PullingSystem>().TryStartPull(user, crate), Is.True,
                "Grabbing a loose crate should take it off the bed.");
        });
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUVehicleCargoComponent>(jeep).Crate, Is.Null);
            Assert.That(SEntMan.GetComponent<TransformComponent>(crate).ParentUid, Is.Not.EqualTo(jeep));
            Assert.That(SEntMan.GetComponent<PullableComponent>(crate).Puller, Is.EqualTo(user));
        });
    }
}
