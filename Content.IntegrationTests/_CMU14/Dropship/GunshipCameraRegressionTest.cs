using System.Reflection;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.CMU14.Dropship.TacticalLand;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class GunshipCameraRegressionTest : InteractionTest
{
    [Test]
    public async Task FlightCameraPreservesDestinationPreviewAndRestoresPanCoverage()
    {
        await SpawnTarget("Chair");
        await Server.WaitAssertion(() =>
        {
            var seat = STarget!.Value;
            var controls = SEntMan.AddComponent<GunshipPilotSeatComponent>(seat);
            controls.Pilot = SPlayer;
            controls.Eye = SEntMan.SpawnEntity(null, SEntMan.GetCoordinates(PlayerCoords));
            var hud = SEntMan.AddComponent<GunshipPilotHudComponent>(SPlayer);
            hud.Dropship = MapData.Grid.Owner;
            var eye = SEntMan.GetComponent<EyeComponent>(SPlayer);
            var eyes = Server.System<SharedEyeSystem>();
            var preview = SEntMan.SpawnEntity(null, SEntMan.GetCoordinates(PlayerCoords));
            SEntMan.AddComponent<DropshipPilotEyeComponent>(preview);
            eyes.SetTarget(SPlayer, preview);
            eyes.SetPvsScale(SPlayer, 2.25f);

            var update = typeof(DropshipTacticalLandSystem).GetMethod(
                "UpdateGunshipCameraMode", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var system = Server.System<DropshipTacticalLandSystem>();
            var args = new object[] { new Entity<GunshipPilotSeatComponent>(seat, controls), MapData.Grid.Owner };
            update.Invoke(system, args);
            Assert.Multiple(() =>
            {
                Assert.That(eye.Target, Is.EqualTo(preview));
                Assert.That(eye.PvsScale, Is.EqualTo(2.25f));
            });

            eyes.SetTarget(SPlayer, null);
            update.Invoke(system, args);
            // A 24-tile pan plus the 2.25x viewport must fit in the
            // server's loading area, which remains centered on the pilot.
            Assert.That(eye.PvsScale, Is.GreaterThanOrEqualTo(2.25f + 24f / 10f));
            controls.PilotPanning = false;
            update.Invoke(system, args);
            Assert.That(eye.PvsScale, Is.EqualTo(1.5f));

            SEntMan.DeleteEntity(preview);
            SEntMan.DeleteEntity(controls.Eye.Value);
            controls.Eye = null;
            controls.Pilot = null;
            SEntMan.RemoveComponent<GunshipPilotHudComponent>(SPlayer);
        });
    }
}
