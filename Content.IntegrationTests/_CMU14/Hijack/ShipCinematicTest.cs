using System.Reflection;
using Content.Client.CMU14.Hijack;
using Content.Client.RoundEnd;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Hijack;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Hijack;

[TestFixture]
public sealed class ShipCinematicTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };
    [SidedDependency(Side.Client)] private readonly IOverlayManager _overlays = null!;
    [SidedDependency(Side.Client)] private readonly IUserInterfaceManager _ui = null!;

    private static readonly FieldInfo WindowField = typeof(RoundEndSummaryUIController)
        .GetField("_window", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private RoundEndSummaryWindow? Window =>
        (RoundEndSummaryWindow?) WindowField.GetValue(_ui.GetUIController<RoundEndSummaryUIController>());

    [Test]
    public async Task SummaryWaitsForTheEntireExplosionAndRestartClearsTheOverlay()
    {
        await Client.WaitAssertion(() =>
        {
            CEntMan.EventBus.RaiseEvent(EventSource.Network, new CMUShipCinematicEvent(CMUShipCinematicStage.Ship));
            Assert.That(_overlays.HasOverlay<ShipDestructionOverlay>(), Is.True);
            CEntMan.EventBus.RaiseEvent(EventSource.Network, new CMUShipCinematicEvent(CMUShipCinematicStage.Detonation));
            CEntMan.EventBus.RaiseEvent(EventSource.Network, new CMUShipCinematicEvent(CMUShipCinematicStage.Destroyed));
            CEntMan.EventBus.RaiseEvent(EventSource.Network,
                new RoundEndMessageEvent("Test mode", "Ship destroyed", TimeSpan.FromHours(1), 4501, 0, [], null,
                    RoundEndSummaryStats.Empty));
            Assert.That(Window, Is.Null, "Round end at 27.5 seconds must not hide the explosion.");
        });
        await RunSeconds(7);
        await Client.WaitAssertion(() =>
        {
            Client.System<ShipHijackSystem>().FrameUpdate(0);
            Assert.That(_overlays.GetOverlay<ShipDestructionOverlay>().Finished, Is.False);
            Assert.That(Window, Is.Null);
        });
        await RunSeconds(1);
        await Client.WaitAssertion(() =>
        {
            Client.System<ShipHijackSystem>().FrameUpdate(0);
            Assert.That(_overlays.GetOverlay<ShipDestructionOverlay>().Finished, Is.True);
            Assert.That(Window?.RoundId, Is.EqualTo(4501));
            Window!.Close();
            WindowField.SetValue(_ui.GetUIController<RoundEndSummaryUIController>(), null);
            CEntMan.EventBus.RaiseEvent(EventSource.Network, new RoundRestartCleanupEvent());
            Assert.That(_overlays.HasOverlay<ShipDestructionOverlay>(), Is.False);
        });
    }
}
