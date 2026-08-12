using Content.Server._RMC14.Vehicle;
using Content.Shared._RMC14.Vehicle.Supply;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14.Vehicle;

[TestFixture]
[TestOf(typeof(VehicleSupplySystem))]
public sealed class RMCVehicleSupplyTest
{
    private const string VehicleId = "RMCVehicleSupplyTestVehicle";
    private const string VehicleKey = "rmcvehiclesupplytestvehicle";

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: RMCVehicleSupplyTestVehicle
          name: test vehicle

        - type: entity
          id: RMCVehicleSupplyTestLift
          components:
          - type: VehicleSupplyLift

        - type: entity
          id: RMCVehicleSupplyTestConsole
          components:
          - type: VehicleSupplyConsole
            vehicles:
            - vehicle: RMCVehicleSupplyTestVehicle
        """;

    [Test]
    public async Task ConsoleBackfillsLiftWhenLiftInitializedFirst()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid lift = default;
        EntityUid console = default;

        await server.WaitPost(() =>
        {
            lift = server.EntMan.SpawnEntity("RMCVehicleSupplyTestLift", map.GridCoords);
        });
        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<VehicleSupplyLiftComponent>(lift).Stored, Is.Empty,
                "The lift must finish initialization before the console exists.");
        });

        await server.WaitPost(() =>
        {
            console = server.EntMan.SpawnEntity("RMCVehicleSupplyTestConsole", map.GridCoords);
        });
        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var consoleComponent = server.EntMan.GetComponent<VehicleSupplyConsoleComponent>(console);
            Assert.That(consoleComponent.Ui.Available, Is.Empty);

            var ev = new BeforeActivatableUIOpenEvent(console);
            server.EntMan.EventBus.RaiseLocalEvent(console, ev);

            var liftComponent = server.EntMan.GetComponent<VehicleSupplyLiftComponent>(lift);
            Assert.That(liftComponent.Stored.TryGetValue(VehicleKey, out var stored), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(stored, Is.EqualTo(1));
                Assert.That(consoleComponent.Ui.Available, Has.Count.EqualTo(1));
                Assert.That(consoleComponent.Ui.Available[0].Id, Is.EqualTo(VehicleId));
                Assert.That(consoleComponent.Ui.Available[0].Count, Is.EqualTo(1));
            });
        });

        await pair.CleanReturnAsync();
    }
}
