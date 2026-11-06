using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Charges.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Lathe;
using Content.Shared.Research.Prototypes;
using Content.Shared._RMC14.Requisitions.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// A jeep crate opens into the jeep on its wheels with the spare in the hatch, the colony's crates come in a
/// showroom paint and only as cargo and medical jeeps, the autolathe prints the vehicle spray painter,
/// the painter on the wheels or a seat repaints only them, and space cleaner takes a jeep's dirt,
/// blood and crayon off.
/// </summary>
[TestFixture]
public sealed class CMUJeepKitAndGrimeTest : GameTest
{
    private async Task<TestMapData> Floor()
    {
        var map = await Pair.CreateTestMap();
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
        });
        return map;
    }

    [Test]
    public async Task CrateOpensIntoAJeepOnItsWheelsWithTheSpareInTheHatch()
    {
        var map = await Floor();
        EntityUid crate = default;
        EntityUid user = default;
        await Server.WaitPost(() =>
        {
            crate = SEntMan.SpawnEntity("CMUCrateJeepCargo", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            user = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 2.5f));
        });
        await RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            var comp = SEntMan.GetComponent<CMUVehicleCrateComponent>(crate);
            Assert.That(SEntMan.System<CMUVehicleCrateSystem>().TryStartOpening((crate, comp), user), Is.True);
        });
        await RunSeconds(4f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(crate), Is.True, "The crate should give way to the jeep.");
            var jeep = SEntMan.EntityQuery<CMUOpenVehicleComponent>(true).Select(c => c.Owner).Single();
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(jeep).EntityPrototype?.ID, Is.EqualTo("CMUVehicleJeepCargo"));

            var containers = SEntMan.System<SharedContainerSystem>();
            Assert.That(containers.GetContainer(jeep, "wheel-1").ContainedEntities, Has.Count.EqualTo(1), "The jeep comes on its wheels.");
            var hatch = containers.GetContainer(jeep, CMUOpenVehicleSystem.SpareSlot).ContainedEntities;
            Assert.That(hatch, Has.Count.EqualTo(1), "The wheel set rides in the rear hatch.");
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(hatch[0]).EntityPrototype?.ID, Is.EqualTo("CMUJeepWheel"));
            Assert.That(SEntMan.GetComponent<CMUVehicleIgnitionComponent>(jeep).KeySlot.HasItem, Is.True, "The key comes in.");
            Assert.That(SEntMan.EntityQuery<CMUVehicleCrateRemainsComponent>(true).Count(), Is.EqualTo(2), "The floor and the falling walls stand where the crate was.");
        });
    }

}
