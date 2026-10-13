using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Charges.Systems;
using Content.Shared.CMU14.Vehicle.Jeep;

namespace Content.IntegrationTests.CMU14.Vehicle;

[TestFixture]
public sealed class CMUJeepCrayonTest : InteractionTest
{
    [Test]
    public async Task CrayonStampsItsSymbolOnTheBody()
    {
        var jeep = await SpawnTarget("CMUVehicleJeepCargo");
        var crayon = await PlaceInHands("CrayonWhite");

        var before = 0;
        await Server.WaitPost(() => before = Server.System<SharedChargesSystem>().GetCurrentCharges(ToServer(crayon)));

        await Interact();

        await Server.WaitAssertion(() =>
        {
            var comp = SEntMan.GetComponent<CMUVehicleCrayonComponent>(ToServer(jeep));
            Assert.That(comp.Drawings, Has.Count.EqualTo(1));
            Assert.That(Server.System<SharedChargesSystem>().GetCurrentCharges(ToServer(crayon)), Is.EqualTo(before - 1));

            SEntMan.System<CMUVehicleGrimeSystem>().Clean((ToServer(jeep), SEntMan.GetComponent<CMUVehicleGrimeComponent>(ToServer(jeep))), 1f);
            Assert.That(comp.Drawings, Is.Empty, "Cleaning takes the symbol off.");
        });
    }
}
