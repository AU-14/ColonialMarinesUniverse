using System.Reflection;
using Content.Client.GameTicking.Managers;
using Content.Client.Gameplay;
using Content.Client.Options.UI;
using Content.Client.UserInterface.Systems.EscapeMenu;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Client.UserInterface;

namespace Content.IntegrationTests.Tests.UserInterface;

[TestFixture]
public sealed class EscapeMenuMergeRegressionTest : GameTest
{
    [Test]
    public async Task GameplayLifecycleBalancesWindowTickerCvarAndMergedControls()
    {
        await Client.WaitAssertion(() =>
        {
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            var controller = ui.GetUIController<EscapeUIController>();
            var ticker = Client.System<ClientGameTicker>();
            var state = new GameplayState();
            var originalSeeOwnNotes = Client.CfgMan.GetCVar(CCVars.SeeOwnNotes);
            var baselineTickerHandlers = HandlerCount(ticker, "RoundStatusUpdated");
            var entered = false;

            Assert.That(GetPrivate<EscapeMenu?>(controller, "_escapeWindow"), Is.Null);
            try
            {
                controller.OnStateEntered(state);
                entered = true;
                var window = GetPrivate<EscapeMenu>(controller, "_escapeWindow");
                Assert.Multiple(() =>
                {
                    Assert.That(window, Is.Not.Null);
                    Assert.That(window.Disposed, Is.False);
                    Assert.That(HandlerCount(ticker, "RoundStatusUpdated"),
                        Is.EqualTo(baselineTickerHandlers + 1));
                    Assert.That(GetPrivate<ClientGameTicker>(controller, "_gameTicker"), Is.SameAs(ticker));
                    Assert.That(window.RoundStatusPanel, Is.Not.Null);
                    Assert.That(window.RulesButton, Is.Not.Null);
                    Assert.That(window.GuidebookButton, Is.Not.Null);
                    Assert.That(window.WikiButton, Is.Not.Null);
                    Assert.That(window.ChangelogButton, Is.Not.Null);
                    Assert.That(window.FeedbackButton, Is.Not.Null);
                    Assert.That(window.AdminRemarksButton, Is.Not.Null);
                    Assert.That(window.CreditsButton, Is.Not.Null);
                    Assert.That(window.PatronPerksButton, Is.Not.Null);
                    Assert.That(window.RoadmapButton, Is.Not.Null);
                    Assert.That(window.OptionsButton, Is.Not.Null);
                    Assert.That(window.DisconnectButton, Is.Not.Null);
                    Assert.That(window.QuitButton, Is.Not.Null);
                });

                Assert.That(window.AdminRemarksButton.Disabled, Is.EqualTo(!originalSeeOwnNotes));
                Client.CfgMan.SetCVar(CCVars.SeeOwnNotes, !originalSeeOwnNotes);
                Assert.Multiple(() =>
                {
                    Assert.That(window.AdminRemarksButton.Disabled, Is.EqualTo(originalSeeOwnNotes));
                    Assert.That(window.AdminRemarksButton.ToolTip,
                        Is.EqualTo(originalSeeOwnNotes ? Loc.GetString("ui-escape-remarks-button-disabled") : null));
                });

                InvokePrivate(ticker, "RoundStatus", new TickerRoundStatusEvent(
                    "Escape Colony",
                    "Escape Ship",
                    314,
                    27,
                    "Escape Extended",
                    TimeSpan.FromMinutes(12),
                    TimeSpan.FromMinutes(3),
                    true));
                Assert.Multiple(() =>
                {
                    Assert.That(window.MapValue.Text, Is.EqualTo("Escape Colony"));
                    Assert.That(window.ShipMapValue.Text, Is.EqualTo("Escape Ship"));
                    Assert.That(window.RoundValue.Text, Is.EqualTo("314"));
                    Assert.That(window.PlayersValue.Text, Is.EqualTo("27"));
                    Assert.That(window.GamemodeValue.Text, Is.EqualTo("Escape Extended"));
                    Assert.That(window.RoundTimeValue.Text, Is.EqualTo("00:03"));
                });

                controller.OnStateExited(state);
                entered = false;
                Assert.Multiple(() =>
                {
                    Assert.That(window.Disposed, Is.True,
                        "one state exit must dispose the single state-owned menu window");
                    Assert.That(GetPrivate<EscapeMenu?>(controller, "_escapeWindow"), Is.Null);
                    Assert.That(GetPrivate<ClientGameTicker?>(controller, "_gameTicker"), Is.Null);
                    Assert.That(HandlerCount(ticker, "RoundStatusUpdated"), Is.EqualTo(baselineTickerHandlers));
                });

                Client.CfgMan.SetCVar(CCVars.SeeOwnNotes, originalSeeOwnNotes);
            }
            finally
            {
                if (entered)
                    controller.OnStateExited(state);
                Client.CfgMan.SetCVar(CCVars.SeeOwnNotes, originalSeeOwnNotes);
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

    private static void InvokePrivate(object instance, string method, object argument)
    {
        instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(instance, [argument]);
    }
}
