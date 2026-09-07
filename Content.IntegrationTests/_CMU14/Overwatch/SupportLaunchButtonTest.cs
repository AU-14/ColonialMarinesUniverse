using System.Reflection;
using Content.Client._RMC14.Overwatch;
using Content.Client._RMC14.SupplyDrop;
using Content.Client.UserInterface.Controls;
using Content.IntegrationTests.Fixtures;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Overwatch;

[TestFixture, NonParallelizable]
public sealed class SupportLaunchButtonTest : GameTest
{
    [TestCase("SupplyDrop")]
    [TestCase("OverwatchSupplyDrop")]
    [TestCase("OrbitalBombardment")]
    public async Task ReadyLaunchCanBeConfirmedAcrossFrames(string target)
    {
        Control view = null;
        ConfirmButton button = null;
        var launches = 0;

        await Client.WaitAssertion(() =>
        {
            if (target == "SupplyDrop")
            {
                var window = new SupplyDropWindow();
                view = window;
                button = window.LaunchButton;
            }
            else
            {
                var monitor = new OverwatchSquadView { HasCrate = true, HasOrbital = true };
                view = monitor;
                button = target == "OverwatchSupplyDrop" ? monitor.LaunchButton : monitor.OrbitalFireButton;
            }

            button.OnPressed += _ => launches++;
            button.Arrange(new UIBox2(0, 0, 200, 40));
            Frame(view);
            Assert.That(button.Disabled, Is.False);
        });

        try
        {
            await Click(button);
            await Client.WaitAssertion(() =>
            {
                Assert.That(button.IsConfirming, Is.True);
                Assert.That(launches, Is.Zero);
                Frame(view);
                Assert.That(button.IsConfirming, Is.True,
                    "Refreshing ready status must preserve the pending launch confirmation.");
                Assert.That(button.Disabled, Is.True,
                    "The confirmation button must retain its debounce delay.");
            });

            await Pair.RunTicksSync(40);
            await Client.WaitAssertion(() =>
            {
                Frame(view);
                Frame(button);
                Assert.That(button.IsConfirming, Is.True);
                Assert.That(button.Disabled, Is.False);
            });

            await Click(button);
            await Client.WaitAssertion(() =>
            {
                Assert.That(launches, Is.EqualTo(1), "The confirmed click must send one launch request.");
                Assert.That(button.IsConfirming, Is.False);

                button.IsConfirming = true;
                SetCooldown(view, target, CGameTiming.CurTime + TimeSpan.FromSeconds(10));
                Frame(view);
                Assert.That(button.IsConfirming, Is.False,
                    "A launch cooldown received from the server must cancel a pending confirmation.");
                Assert.That(button.Disabled, Is.True);

                SetCooldown(view, target, CGameTiming.CurTime);
                Frame(view);
                Assert.That(button.Disabled, Is.False, "Launch must be enabled when its cooldown expires.");
                Assert.That(launches, Is.EqualTo(1));
            });
        }
        finally
        {
            await Client.WaitPost(() => view.Dispose());
        }
    }

    private async Task Click(Control control)
    {
        var position = new ScreenCoordinates(control.GlobalPixelPosition + control.PixelSize / 2, default);
        foreach (var state in new[] { BoundKeyState.Down, BoundKeyState.Up })
        {
            var args = new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, state, position, default, default, default);
            await Client.DoGuiEvent(control, args);
        }
    }

    private static void Frame(Control control)
    {
        control.GetType().GetMethod("FrameUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(control, new object[] { new FrameEventArgs(1f / 60f) });
    }

    private static void SetCooldown(Control view, string target, TimeSpan deadline)
    {
        if (view is SupplyDropWindow window)
            window.NextUpdateAt = deadline;
        else if (view is OverwatchSquadView monitor)
        {
            if (target == "OverwatchSupplyDrop")
                monitor.NextLaunchAt = deadline;
            else
                monitor.NextOrbitalAt = deadline;
        }
    }
}
