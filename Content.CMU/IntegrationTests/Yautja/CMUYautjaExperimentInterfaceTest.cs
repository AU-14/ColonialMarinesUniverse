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
        Popup popup = default!;
        await server.WaitAssertion(() =>
        {
            user = server.EntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            bracer = server.EntMan.SpawnEntity("CMUYautjaBracerBone", map.GridCoords);
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
        });
        // Click the real contacts: opening the BUI alone misses double-parenting in OpenAtMouse.
        foreach (var index in new[] { 0, 0, 1 })
        {
            var contact = window.FindControl<CMUYautjaBracerDiagram>("Diagram").Children.OfType<BaseButton>().ElementAt(index);
            var point = new ScreenCoordinates(contact.GlobalPixelPosition + contact.PixelSize / 2, default);
            foreach (var keyState in new[] { BoundKeyState.Down, BoundKeyState.Up })
                await client.DoGuiEvent(contact, new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick,
                    keyState, point, default, contact.Size / 2, contact.PixelSize / 2));
            await client.WaitAssertion(() =>
            {
                popup = client.ResolveDependency<IUserInterfaceManager>().ModalRoot.Children.OfType<Popup>()
                    .Single(control => control.Visible);
                popup.Close();
                Assert.That(popup.Parent, Is.Null, "Closing a clue must release the modal root before reopening it.");
            });
        }
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
    }
}
