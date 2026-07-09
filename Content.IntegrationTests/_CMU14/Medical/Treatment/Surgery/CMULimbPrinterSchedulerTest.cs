using Content.Server._CMU14.Medical.Treatment.Surgery;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared.Interaction.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Medical.Treatment.Surgery;

[TestFixture]
public sealed class CMULimbPrinterSchedulerTest
{
    [Test]
    public async Task RestartingWorkReplacesTheVisualExpiry()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid printer = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var system = entMan.System<CMULimbPrinterSystem>();
            printer = entMan.SpawnEntity("CMULimbPrinter", MapCoordinates.Nullspace);
            var component = entMan.GetComponent<CMULimbPrinterComponent>(printer);
            system.StartWorking((printer, component), TimeSpan.FromSeconds(0.1));
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.05f));

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var system = entMan.System<CMULimbPrinterSystem>();
            var component = entMan.GetComponent<CMULimbPrinterComponent>(printer);
            system.StartWorking((printer, component), TimeSpan.FromSeconds(0.3));
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.15f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var appearance = entMan.System<SharedAppearanceSystem>();
            Assert.That(appearance.TryGetData(
                printer,
                CMULimbPrinterVisuals.Working,
                out bool working), Is.True);
            Assert.That(working, Is.True);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.25f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var appearance = entMan.System<SharedAppearanceSystem>();
            var component = entMan.GetComponent<CMULimbPrinterComponent>(printer);
            Assert.That(appearance.TryGetData(
                printer,
                CMULimbPrinterVisuals.Working,
                out bool working), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(working, Is.False);
                Assert.That(component.WorkingUntil, Is.EqualTo(TimeSpan.Zero));
            });
            entMan.DeleteEntity(printer);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClosingOneViewerKeepsPeriodicRefreshForRemainingViewer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        EntityUid printer = default;
        EntityUid firstViewer = default;
        EntityUid secondViewer = default;
        var expectedWorkingUntil = TimeSpan.Zero;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var ui = entMan.System<UserInterfaceSystem>();
            printer = entMan.SpawnEntity("CMULimbPrinter", map.GridCoords);
            firstViewer = entMan.SpawnEntity(null, map.GridCoords);
            secondViewer = entMan.SpawnEntity(null, map.GridCoords);
            entMan.AddComponent<ComplexInteractionComponent>(firstViewer);
            entMan.AddComponent<ComplexInteractionComponent>(secondViewer);

            ui.OpenUi(printer, CMULimbPrinterUIKey.Key, firstViewer);
            ui.OpenUi(printer, CMULimbPrinterUIKey.Key, secondViewer);
            Assert.Multiple(() =>
            {
                Assert.That(ui.IsUiOpen(printer, CMULimbPrinterUIKey.Key, firstViewer), Is.True);
                Assert.That(ui.IsUiOpen(printer, CMULimbPrinterUIKey.Key, secondViewer), Is.True);
            });

            ui.CloseUi(printer, CMULimbPrinterUIKey.Key, firstViewer);
            Assert.That(ui.IsUiOpen(printer, CMULimbPrinterUIKey.Key), Is.True);

            var component = entMan.GetComponent<CMULimbPrinterComponent>(printer);
            entMan.System<CMULimbPrinterSystem>().StartWorking((printer, component), TimeSpan.FromSeconds(3));
            expectedWorkingUntil = component.WorkingUntil;
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1.1f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ui = entMan.System<UserInterfaceSystem>();
            Assert.That(
                ui.TryGetUiState<CMULimbPrinterBuiState>(printer, CMULimbPrinterUIKey.Key, out var state),
                Is.True);
            Assert.That(state!.WorkingUntil, Is.EqualTo(expectedWorkingUntil));

            ui.CloseUi(printer, CMULimbPrinterUIKey.Key, secondViewer);
            Assert.That(ui.IsUiOpen(printer, CMULimbPrinterUIKey.Key), Is.False);
            entMan.DeleteEntity(firstViewer);
            entMan.DeleteEntity(secondViewer);
            entMan.DeleteEntity(printer);
        });

        await pair.CleanReturnAsync();
    }
}
