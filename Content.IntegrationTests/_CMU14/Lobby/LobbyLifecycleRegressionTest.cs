using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using Content.Client.Gameplay;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Allegiance;
using Content.Shared.CMU14.Allegiance;
using Content.Shared.CCVar;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.IntegrationTests.CMU14.Lobby;

[TestFixture]
public sealed class LobbyLifecycleRegressionTest : GameTest
{
    private static readonly string[] CharacterSetupCvars =
    [
        CCVars.SeeOwnNotes.Name,
        CCVars.CrtUiEnabled.Name,
        CCVars.CrtUiColor.Name,
        CCVars.GameMaxCharacterSlots.Name,
    ];

    public override PoolSettings PoolSettings => new() { InLobby = true };

    [Test]
    public async Task FreshLobbyResetsAllegianceAndBalancesCharacterSetupSubscriptions()
    {
        var userId = Client.User!.Value;
        var serverNet = Server.ResolveDependency<IServerNetManager>();
        Dictionary<Type, ProcessMessage> callbacks = null!;
        ProcessMessage originalCallback = null!;
        var messageCount = 0;

        await Server.WaitAssertion(() =>
        {
            callbacks = GetMessageCallbacks(serverNet);
            originalCallback = callbacks[typeof(MsgIgnoreAllegiance)];
            callbacks[typeof(MsgIgnoreAllegiance)] = message =>
            {
                Interlocked.Increment(ref messageCount);
                originalCallback(message);
            };
        });

        try
        {
            var allegiance = SEntMan.System<AllegianceSystem>();
            var clientNet = Client.ResolveDependency<IClientNetManager>();
            var stateManager = Client.ResolveDependency<IStateManager>();
            var configuration = Client.ResolveDependency<IConfigurationManager>();
            LobbyState initialLobby = null!;

            await Client.WaitAssertion(() =>
            {
                initialLobby = (LobbyState) stateManager.CurrentState;
                initialLobby.IgnoreAllegiance = true;
                initialLobby.Lobby!.CharacterPreview.IgnoreAllegianceToggle.Pressed = true;
                clientNet.ClientSendMessage(new MsgIgnoreAllegiance { IgnoreAllegiance = true });
                AssertCharacterSetupSubscriptions(configuration, 1);
            });
            await PoolManager.WaitUntil(Server, () => allegiance.IsIgnoringAllegiance(userId), maxTicks: 60);
            Interlocked.Exchange(ref messageCount, 0);

            await Client.WaitAssertion(() =>
            {
                initialLobby.SwitchState(LobbyGui.LobbyGuiState.CharacterSetup);
                initialLobby.SwitchState(LobbyGui.LobbyGuiState.Default);
                Client.ResolveDependency<IUserInterfaceManager>()
                    .GetUIController<LobbyUIController>()
                    .ReloadCharacterSetup();

                Assert.That(stateManager.CurrentState, Is.SameAs(initialLobby));
                Assert.That(initialLobby.IgnoreAllegiance, Is.True);
                Assert.That(initialLobby.Lobby!.CharacterPreview.IgnoreAllegianceToggle.Pressed, Is.True);
                AssertCharacterSetupSubscriptions(configuration, 1);
            });
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(allegiance.IsIgnoringAllegiance(userId), Is.True);
                Assert.That(Volatile.Read(ref messageCount), Is.Zero);
            });

            await Client.WaitPost(() => stateManager.RequestStateChange<GameplayState>());
            await Pair.ReallyBeIdle(2);
            await Client.WaitAssertion(() =>
            {
                Assert.That(stateManager.CurrentState, Is.TypeOf<GameplayState>());
                AssertCharacterSetupSubscriptions(configuration, 0);
            });
            await Server.WaitAssertion(() =>
            {
                Assert.That(allegiance.IsIgnoringAllegiance(userId), Is.True,
                    "Leaving the lobby must not race spawn processing by clearing the choice.");
                Assert.That(Volatile.Read(ref messageCount), Is.Zero,
                    "Lobby exit alone must not send an allegiance reset.");
            });

            await Client.WaitPost(() => stateManager.RequestStateChange<LobbyState>());
            await Pair.ReallyBeIdle(2);
            await Client.WaitAssertion(() =>
            {
                var freshLobby = (LobbyState) stateManager.CurrentState;
                Assert.Multiple(() =>
                {
                    Assert.That(freshLobby, Is.Not.SameAs(initialLobby));
                    Assert.That(freshLobby.IgnoreAllegiance, Is.False);
                    Assert.That(freshLobby.Lobby!.CharacterPreview.IgnoreAllegianceToggle.Pressed, Is.False);
                });

                var controller = Client.ResolveDependency<IUserInterfaceManager>()
                    .GetUIController<LobbyUIController>();
                controller.ReloadCharacterSetup();
                controller.ReloadCharacterSetup();
                AssertCharacterSetupSubscriptions(configuration, 1);
            });
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(allegiance.IsIgnoringAllegiance(userId), Is.False);
                Assert.That(Volatile.Read(ref messageCount), Is.EqualTo(1),
                    "A fresh lobby state must send exactly one authoritative false reset.");
            });

            await Client.WaitPost(() => stateManager.RequestStateChange<GameplayState>());
            await Pair.ReallyBeIdle(2);
            await Client.WaitAssertion(() => AssertCharacterSetupSubscriptions(configuration, 0));
            await Server.WaitAssertion(() => Assert.That(Volatile.Read(ref messageCount), Is.EqualTo(1)));
        }
        finally
        {
            await Server.WaitAssertion(() => callbacks[typeof(MsgIgnoreAllegiance)] = originalCallback);
        }
    }

    private static Dictionary<Type, ProcessMessage> GetMessageCallbacks(IServerNetManager netManager)
    {
        var field = netManager.GetType().GetField("_callbacks", BindingFlags.Instance | BindingFlags.NonPublic);
        return (Dictionary<Type, ProcessMessage>) field!.GetValue(netManager)!;
    }

    private static void AssertCharacterSetupSubscriptions(IConfigurationManager configuration, int expected)
    {
        var method = FindInstanceMethod(configuration.GetType(), "GetSubs");
        foreach (var cvar in CharacterSetupCvars)
        {
            var subscriptions = (IEnumerable) method.Invoke(configuration, [cvar])!;
            var count = subscriptions.Cast<Delegate>().Count(subscription => subscription.Target is CharacterSetupGui);
            Assert.That(count, Is.EqualTo(expected), $"Unexpected CharacterSetupGui subscription count for {cvar}.");
        }
    }

    private static MethodInfo FindInstanceMethod(Type type, string name)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (current.GetMethod(name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) is { } method)
            {
                return method;
            }
        }

        throw new MissingMethodException(type.FullName, name);
    }
}
