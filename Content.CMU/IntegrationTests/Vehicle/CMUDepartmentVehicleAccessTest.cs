using Content.IntegrationTests.Fixtures;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.Vehicle;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;

using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Vehicle;

[TestFixture]
public sealed class CMUDepartmentVehicleAccessTest : GameTest
{
    [Test]
    public async Task ChangingHeldCredentialChangesDrivingPermissionWithoutLoggingMovement()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            // A chair exercises the operator and movement permission path without engine, wheel or fuel interlocks.
            var vehicle = SEntMan.SpawnEntity("ChairOfficeLight", map.GridCoords);
            SEntMan.EnsureComponent<ColonyDepartmentVehicleComponent>(vehicle);
            var reader = SEntMan.EnsureComponent<AccessReaderComponent>(vehicle);
            Server.System<AccessReaderSystem>().TrySetAccesses((vehicle, reader),
                new List<ProtoId<AccessLevelPrototype>> { "CMAccessColonyEngineering" });
            var driver = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var card = SEntMan.SpawnEntity("CMIDCardStandardDogtag", map.GridCoords);
            Server.System<SharedAccessSystem>().TrySetTags(card, ["CMAccessColonyEngineering"]);
            var chassis = SEntMan.GetComponent<VehicleComponent>(vehicle);
            Assert.That(Server.System<VehicleSystem>().TrySetOperator((vehicle, chassis), driver), Is.True);
            bool CanRun()
            {
                var ev = new VehicleCanRunEvent((vehicle, chassis));
                SEntMan.EventBus.RaiseLocalEvent(vehicle, ref ev);
                return ev.CanRun;
            }

            Assert.That(CanRun(), Is.False);
            var hands = Server.System<SharedHandsSystem>();
            Assert.That(hands.TryPickupAnyHand(driver, card), Is.True);
            for (var step = 0; step < 60; step++)
                Assert.That(CanRun(), Is.True);
            Assert.That(reader.AccessLog, Is.Empty);
            Assert.That(hands.TryDrop(driver, card), Is.True);
            Assert.That(CanRun(), Is.False);
        });
    }
}
