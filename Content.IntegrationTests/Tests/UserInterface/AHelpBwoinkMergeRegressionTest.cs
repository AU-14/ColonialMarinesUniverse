using System.Reflection;
using Content.Client.Administration.Managers;
using Content.Client.Administration.Systems;
using Content.Client.Administration.UI.Bwoink;
using Content.Client._RMC14.Mentor;
using Content.Client.UserInterface.Systems.Bwoink;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Administration;
using Content.Shared.Input;
using Robust.Client.Input;
using Robust.Client.UserInterface;

namespace Content.IntegrationTests.Tests.UserInterface;

[TestFixture]
public sealed class AHelpBwoinkMergeRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [Test]
    public async Task OpenAHelpInputRoutesThroughStaffChooserAndUnloadsCleanly()
    {
        await Client.WaitAssertion(() =>
        {
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            var input = Client.ResolveDependency<IInputManager>();
            var controller = ui.GetUIController<AHelpUIController>();
            var staff = ui.GetUIController<StaffHelpUIController>();
            var system = Client.System<BwoinkSystem>();
            var baselineHandlers = HandlerCount(system, "OnBwoinkTextMessageRecieved");
            var originalHelper = controller.UIHelper;
            var unloaded = false;

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivate<BwoinkSystem?>(controller, "_bwoinkSystem"), Is.SameAs(system));
                Assert.That(input.GetInputCommand(ContentKeyFunctions.OpenAHelp), Is.Not.Null);
                Assert.That(GetPrivate<StaffHelpWindow?>(staff, "_staffHelpWindow"), Is.Null);
            });

            try
            {
                controller.OnSystemUnloaded(system);
                unloaded = true;
                Assert.Multiple(() =>
                {
                    Assert.That(input.GetInputCommand(ContentKeyFunctions.OpenAHelp), Is.Null,
                        "unloading must clear the direct OpenAHelp registration");
                    Assert.That(HandlerCount(system, "OnBwoinkTextMessageRecieved"),
                        Is.EqualTo(baselineHandlers - 1));
                });

                controller.OnSystemLoaded(system);
                unloaded = false;
                var command = input.GetInputCommand(ContentKeyFunctions.OpenAHelp);
                Assert.Multiple(() =>
                {
                    Assert.That(command, Is.Not.Null);
                    Assert.That(HandlerCount(system, "OnBwoinkTextMessageRecieved"), Is.EqualTo(baselineHandlers));
                });

                command!.Enabled(null);
                Assert.That(GetPrivate<StaffHelpWindow?>(staff, "_staffHelpWindow"), Is.Not.Null,
                    "OpenAHelp must open the StaffHelp chooser, not the legacy AHelp window directly");
                Assert.That(controller.UIHelper, Is.SameAs(originalHelper));

                command.Enabled(null);
                Assert.That(GetPrivate<StaffHelpWindow?>(staff, "_staffHelpWindow"), Is.Null);
            }
            finally
            {
                if (unloaded)
                    controller.OnSystemLoaded(system);
                if (GetPrivate<StaffHelpWindow?>(staff, "_staffHelpWindow") is not null)
                    staff.ToggleWindow();
            }
        });
    }

    [Test]
    public async Task AdminPopoutDisposalCanRecreateAndReopenTheControl()
    {
        await Server.WaitPost(() =>
            Server.ResolveDependency<Content.Server.Administration.Managers.IAdminManager>()
                .PromoteHost(ServerSession!));

        for (var i = 0; i < 10; i++)
        {
            await RunTicksSync(1);
            var ready = false;
            await Client.WaitAssertion(() =>
                ready = Client.ResolveDependency<IClientAdminManager>().HasFlag(AdminFlags.Adminhelp));
            if (ready)
                break;
        }

        await Client.WaitAssertion(() =>
        {
            var admin = Client.ResolveDependency<IClientAdminManager>();
            var controller = Client.ResolveDependency<IUserInterfaceManager>()
                .GetUIController<AHelpUIController>();
            AdminAHelpUIHandler? helper = null;

            try
            {
                Assert.That(admin.HasFlag(AdminFlags.Adminhelp), Is.True);
                controller.Open();
                helper = controller.UIHelper as AdminAHelpUIHandler;
                Assert.Multiple(() =>
                {
                    Assert.That(helper, Is.Not.Null);
                    Assert.That(helper!.Window, Is.Not.Null);
                    Assert.That(helper.Window!.IsOpen, Is.True);
                    Assert.That(helper.Control, Is.Not.Null);
                });

                var firstControl = helper!.Control!;
                controller.PopOut();
                Assert.Multiple(() =>
                {
                    Assert.That(helper.Window, Is.Null);
                    Assert.That(helper.ClydeWindow, Is.Not.Null);
                    Assert.That(helper.ClydeWindow!.IsDisposed, Is.False);
                    Assert.That(firstControl.Disposed, Is.False);
                });

                helper.Close();
                Assert.Multiple(() =>
                {
                    Assert.That(helper.ClydeWindow!.IsDisposed, Is.True);
                    Assert.That(firstControl.Disposed, Is.True);
                    Assert.That(helper.IsOpen, Is.False);
                });

                controller.ToggleWindow();
                Assert.Multiple(() =>
                {
                    Assert.That(helper.Control, Is.Not.Null.And.Not.SameAs(firstControl));
                    Assert.That(helper.Control!.Disposed, Is.False);
                    Assert.That(helper.Window, Is.Not.Null);
                    Assert.That(helper.Window!.IsOpen, Is.True,
                        "closing a popped-out admin window must not retain a detached disposed control");
                });
            }
            finally
            {
                if (helper?.ClydeWindow is { IsDisposed: false })
                    helper.Close();
                helper?.Dispose();
                controller.UIHelper = null;
            }
        });
    }

    private static int HandlerCount(object instance, string eventName)
    {
        return (instance.GetType().GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance) as MulticastDelegate)?.GetInvocationList().Length ?? 0;
    }

    private static T GetPrivate<T>(object instance, string field)
    {
        return (T) instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance)!;
    }
}
