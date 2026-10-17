using Content.Shared.Alert;
using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Components;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using ServerMoverController = Content.Server.Physics.Controllers.MoverController;

namespace Content.IntegrationTests.Tests.Movement;

public sealed class BuckleMovementTest : MovementTest
{
    [Test]
    public async Task BuckledPlayerCanRotateCameraInBothDirectionsFromNonCardinalAngle()
    {
        await SpawnTarget("Chair");

        var buckle = Comp<BuckleComponent>(Player);
#pragma warning disable RA0002
        buckle.Delay = TimeSpan.Zero;
#pragma warning restore RA0002

        await Interact();
        Assert.That(buckle.Buckled, Is.True);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.HasComponent<ActiveInputMoverComponent>(SPlayer), Is.False));

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<ServerMoverController>().SetCameraRotation(
                SPlayer,
                Angle.FromDegrees(70),
                immediate: true),
                Is.True);
        });
        await Pair.RunUntilSynced();

        await PressKey(EngineKeyFunctions.CameraRotateLeft);
        await AssertCameraSettlesAt(160);

        await PressKey(EngineKeyFunctions.CameraRotateRight);
        await AssertCameraSettlesAt(70);

        async Task AssertCameraSettlesAt(double expectedDegrees)
        {
            await Pair.RunTicksSync(60);

            await Server.WaitAssertion(() =>
            {
                var mover = SEntMan.GetComponent<InputMoverComponent>(SPlayer);
                Assert.Multiple(() =>
                {
                    Assert.That(mover.TargetRelativeRotation.Degrees, Is.EqualTo(expectedDegrees).Within(0.01),
                        "numpad camera rotation must not be discarded while the player is buckled");
                    Assert.That(mover.RelativeRotation.Degrees, Is.EqualTo(expectedDegrees).Within(1),
                        "the server must finish camera interpolation for a buckled player");
                    Assert.That(SEntMan.HasComponent<ActiveInputMoverComponent>(SPlayer), Is.False,
                        "an immobile mover must become inactive again after camera interpolation finishes");
                });
            });
            await Client.WaitAssertion(() =>
            {
                var mover = CEntMan.GetComponent<InputMoverComponent>(CPlayer);
                Assert.Multiple(() =>
                {
                    Assert.That(mover.TargetRelativeRotation.Degrees, Is.EqualTo(expectedDegrees).Within(0.01));
                    Assert.That(mover.RelativeRotation.Degrees, Is.EqualTo(expectedDegrees).Within(1),
                        "the displayed camera must finish the numpad rotation while the player is buckled");
                });
            });
        }
    }

    // Check that interacting with a chair straps you to it and prevents movement.
    [Test]
    public async Task ChairTest()
    {
        await SpawnTarget("Chair");

        var cAlert = Client.System<AlertsSystem>();
        var sAlert = Server.System<AlertsSystem>();
        var buckle = Comp<BuckleComponent>(Player);
        var strap = Comp<StrapComponent>(Target);

#pragma warning disable RA0002
        buckle.Delay = TimeSpan.Zero;
#pragma warning restore RA0002

        // Initially not buckled to the chair, and standing off to the side
        Assert.That(Delta(), Is.InRange(0.9f, 1.1f));
        Assert.That(buckle.Buckled, Is.False);
        Assert.That(buckle.BuckledTo, Is.Null);
        Assert.That(strap.BuckledEntities, Is.Empty);
        if (strap.BuckledAlertType != null) //RMC14
        {
            Assert.That(cAlert.IsShowingAlert(CPlayer, strap.BuckledAlertType.Value), Is.False);
            Assert.That(sAlert.IsShowingAlert(SPlayer, strap.BuckledAlertType.Value), Is.False);
        }

        // Interact results in being buckled to the chair
        await Interact();
        Assert.That(Delta(), Is.InRange(-0.01f, 0.01f));
        Assert.That(buckle.Buckled, Is.True);
        Assert.That(buckle.BuckledTo, Is.EqualTo(STarget));
        Assert.That(strap.BuckledEntities, Is.EquivalentTo(new[] { SPlayer }));
        if (strap.BuckledAlertType != null)// RMC14
        {
            Assert.That(cAlert.IsShowingAlert(CPlayer, strap.BuckledAlertType.Value), Is.True);
            Assert.That(sAlert.IsShowingAlert(SPlayer, strap.BuckledAlertType.Value), Is.True);
        }

        // Attempting to walk away does nothing
        await Move(DirectionFlag.East, 1);
        Assert.That(Delta(), Is.InRange(-0.01f, 0.01f));
        Assert.That(buckle.Buckled, Is.True);
        Assert.That(buckle.BuckledTo, Is.EqualTo(STarget));
        Assert.That(strap.BuckledEntities, Is.EquivalentTo(new[] { SPlayer }));
        if (strap.BuckledAlertType != null) //RMC14
        {
            Assert.That(cAlert.IsShowingAlert(CPlayer, strap.BuckledAlertType.Value), Is.True);
            Assert.That(sAlert.IsShowingAlert(SPlayer, strap.BuckledAlertType.Value), Is.True);
        }

        // Interacting again will unbuckle the player
        await Interact();
        Assert.That(Delta(), Is.InRange(-0.5f, 0.5f));
        Assert.That(buckle.Buckled, Is.False);
        Assert.That(buckle.BuckledTo, Is.Null);
        Assert.That(strap.BuckledEntities, Is.Empty);
        if (strap.BuckledAlertType != null) //RMC14
        {
            Assert.That(cAlert.IsShowingAlert(CPlayer, strap.BuckledAlertType.Value), Is.False);
            Assert.That(sAlert.IsShowingAlert(SPlayer, strap.BuckledAlertType.Value), Is.False);
        }

        // And now they can move away
        await Move(DirectionFlag.SouthEast, 1);
        Assert.That(Delta(), Is.LessThan(-1));
    }
}
