using System.Reflection;
using Content.Client.Launcher;
using Content.IntegrationTests.Fixtures;
using Robust.Client.UserInterface;
using Robust.Shared.IoC;
using Robust.Shared.Network;

namespace Content.IntegrationTests.Tests.ClientSession;

[TestFixture]
public sealed class LauncherConnectingMergeRegressionTest : GameTest
{
    [Test]
    public async Task ShutdownDisposesControlAndUnsubscribesNetworkFailureHandler()
    {
        await Client.WaitAssertion(() =>
        {
            var net = Client.Resolve<IClientNetManager>();
            var baseline = SubscriberCount(net, "ConnectFailed");
            var state = new LauncherConnecting();
            IoCManager.InjectDependencies(state);

            InvokeLifecycle(state, "Startup");
            var control = GetPrivate<Control>(state, "_control");
            Assert.Multiple(() =>
            {
                Assert.That(control.Disposed, Is.False);
                Assert.That(control.Parent, Is.Not.Null,
                    "startup must attach the connecting UI to the state root");
                Assert.That(state.CurrentPage, Is.EqualTo(LauncherConnecting.Page.Connecting));
                Assert.That(SubscriberCount(net, "ConnectFailed"), Is.EqualTo(baseline + 1));
                Assert.That(state.Redial(), Is.False,
                    "the ordinary integration launch state has no ss14 address and must fail redial cleanly");
            });

            InvokeLifecycle(state, "Shutdown");
            Assert.Multiple(() =>
            {
                Assert.That(control.Disposed, Is.True);
                Assert.That(control.Parent, Is.Null);
                Assert.That(SubscriberCount(net, "ConnectFailed"), Is.EqualTo(baseline),
                    "shutdown must remove the exact handler it installed before disposing the state");
            });
        });
    }

    private static void InvokeLifecycle(LauncherConnecting state, string method)
    {
        typeof(LauncherConnecting)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(state, null);
    }

    private static T GetPrivate<T>(LauncherConnecting state, string field)
    {
        return (T) typeof(LauncherConnecting)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(state)!;
    }

    private static int SubscriberCount(IClientNetManager net, string eventName)
    {
        return ((Delegate?) net.GetType()
            .GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
            .GetValue(net))?.GetInvocationList().Length ?? 0;
    }
}
