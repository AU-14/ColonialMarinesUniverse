using System.Linq;
using Content.Client.CMU14.Yautja;
using Content.Server.CMU14.Yautja;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Yautja;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class CMUYautjaExperimentInterfaceTest
{
    [Test]
    public async Task ResearchMenusCannotAlterTheDeviceAfterItIsPickedUp()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        EntityUid user = default, bracer = default;
        NetEntity bracerNet = default;
        CMUYautjaExperimentBui ui = default!;
        CMUYautjaExperimentWindow window = default!;
        string firstClue = default!;
        await server.WaitAssertion(() =>
        {
            user = server.EntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            bracer = server.EntMan.SpawnEntity("CMUYautjaBracerBone", map.GridCoords);
            // Switch from a short clue to a long one before opening the procedure picker.
            server.EntMan.GetComponent<CMUYautjaExperimentComponent>(bracer).RequiredExperiments =
                ["CMUYautjaBio", "CMUYautjaRadar", "CMUYautjaRoot", "CMUYautjaStatic", "CMUYautjaVeil"];
            bracerNet = server.EntMan.GetNetEntity(bracer);
            server.EntMan.System<SkillsSystem>().SetSkill(user, "RMCSkillResearch", 1);
            server.PlayerMan.SetAttachedEntity(pair.Player!, user);
        });
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() => Assert.That(server.EntMan.System<CMUYautjaExperimentSystem>()
            .SelectExperiment(bracer, user, "CMUYautjaDiagnostic"), Is.True));
        await pair.RunTicksSync(10);
        await client.WaitAssertion(() =>
        {
            Assert.That(client.EntMan.System<SharedUserInterfaceSystem>().TryGetOpenUi<CMUYautjaExperimentBui>(
                client.EntMan.GetEntity(bracerNet), CMUYautjaExperimentUiKey.Key, out var opened), Is.True);
            ui = opened!;
            window = client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children
                .OfType<CMUYautjaExperimentWindow>().Single();
            firstClue = window.FindControl<RichTextLabel>("Clue").Text!;
        });
        // Repeated contacts update the same inspection panel without attaching modal popups.
        foreach (var index in new[] { 0, 0, 1 })
        {
            var contact = window.FindControl<CMUYautjaBracerDiagram>("Diagram").Children.OfType<BaseButton>().ElementAt(index);
            await Click(contact);
            await client.WaitAssertion(() =>
            {
                var clue = window.FindControl<RichTextLabel>("Clue").Text;
                Assert.That(clue, index == 0 ? Is.EqualTo(firstClue) : Is.Not.EqualTo(firstClue));
                Assert.That(client.ResolveDependency<IUserInterfaceManager>().ModalRoot.Children.OfType<Popup>()
                    .Any(control => control.Visible), Is.False, "Inspecting contacts must leave world interaction available.");
            });
        }
        await Click(window.FindControl<Button>("ProcedureButton"));
        await pair.RunTicksSync(2);
        var firstProcedure = window.FindControl<BoxContainer>("DrawerContent").Children.OfType<Button>().First();
        await Click(firstProcedure);
        await pair.RunTicksSync(2);
        await client.WaitAssertion(() => Assert.That(window.FindControl<BoxContainer>("Drawer").Visible, Is.False,
            "Choosing a visible procedure must close the drawer."));
        await client.WaitPost(() => ui.SendMessage(new CMUYautjaExperimentMessage(CMUYautjaExperimentCommand.Select, "CMUYautjaTranslator")));
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<CMUYautjaExperimentComponent>(bracer).Selected.Id,
                Is.EqualTo("CMUYautjaTranslator"));
            var solutions = server.EntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(bracer, CMUYautjaExperimentSystem.SampleWell, out var well, out _), Is.True);
            solutions.TryAddReagent(well!.Value, "Water", 5);
        });
        await client.WaitPost(() => ui.SendMessage(new CMUYautjaExperimentMessage(CMUYautjaExperimentCommand.Purge)));
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() =>
        {
            var solutions = server.EntMan.System<SharedSolutionContainerSystem>();
            solutions.TryGetSolution(bracer, CMUYautjaExperimentSystem.SampleWell, out var well, out var solution);
            Assert.That(solution!.Volume, Is.EqualTo(FixedPoint2.Zero), "The floor-bound menu must purge the real sample well.");
            solutions.TryAddReagent(well!.Value, "Water", 5);
            Assert.That(server.EntMan.System<SharedHandsSystem>().TryPickupAnyHand(user, bracer), Is.True);
        });
        await pair.RunTicksSync(5);
        await client.WaitAssertion(() =>
            Assert.That(window.IsOpen, Is.False, "Picking up the bracer must safely close floor inspection."));
        await client.WaitPost(() =>
        {
            // A stale menu must not bypass the same floor restriction as physical tools.
            ui.SendMessage(new CMUYautjaExperimentMessage(CMUYautjaExperimentCommand.Select, "CMUYautjaDiagnostic"));
            ui.SendMessage(new CMUYautjaExperimentMessage(CMUYautjaExperimentCommand.Purge));
        });
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<CMUYautjaExperimentComponent>(bracer).Selected.Id,
                Is.EqualTo("CMUYautjaTranslator"));
            server.EntMan.System<SharedSolutionContainerSystem>().TryGetSolution(bracer,
                CMUYautjaExperimentSystem.SampleWell, out _, out var solution);
            Assert.That(solution!.Volume, Is.EqualTo((FixedPoint2) 5), "Held bracers must retain their samples.");
        });
        await pair.CleanReturnAsync();

        async Task Click(BaseButton button)
        {
            ScreenCoordinates point = default;
            Control hit = default!;
            await client.WaitAssertion(() =>
            {
                var manager = client.ResolveDependency<IUserInterfaceManager>();
                point = new ScreenCoordinates(button.GlobalPixelPosition + button.PixelSize / 2,
                    button.Root!.Window!.Id);
                hit = manager.MouseGetControl(point)!;
                Assert.That(hit, Is.SameAs(button),
                    $"{button.Name}: the rendered button must be reachable through its clipping ancestors.");
            });
            foreach (var keyState in new[] { BoundKeyState.Down, BoundKeyState.Up })
                await client.DoGuiEvent(hit, new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick,
                    keyState, point, true, hit.Size / 2, hit.PixelSize / 2));
        }
    }
}
