using System.Numerics;
using Content.Client.CMU14.Fighter;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Fighter;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Fighter;

[TestFixture]
public sealed class FighterCrashPresentationTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task FlybyNoseFacesTravelBeforeAndDuringCrash()
    {
        await Client.WaitAssertion(() =>
        {
            var map = CEntMan.System<SharedMapSystem>().CreateMap(out _);
            var transform = CEntMan.System<SharedTransformSystem>();
            var uid = CEntMan.SpawnEntity("CMUFighterFlyby", new EntityCoordinates(map, Vector2.Zero));
            var flyby = CEntMan.GetComponent<FighterFlybyComponent>(uid);
            var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
            try
            {
                foreach (var crashing in new[] { false, true })
                foreach (var degrees in new[] { 0, 45, 90, 180, 270 })
                {
                    var heading = Angle.FromDegrees(degrees);
                    transform.SetWorldRotation(uid, -heading);
                    flyby.Crashing = crashing;
                    CEntMan.System<FighterGroundVisualSystem>().FrameUpdate(0);
                    // The jet's nose is at the bottom of its source image.
                    var nose = (transform.GetWorldRotation(uid) + sprite.Rotation).RotateVec(-Vector2.UnitY);
                    var travel = FighterFlight.Forward((float) heading.Theta);
                    Assert.That(Vector2.Dot(nose, travel), Is.GreaterThan(.97f),
                        $"Fighter faces away from travel at {degrees} degrees; crashing={crashing}.");
                }
            }
            finally { CEntMan.DeleteEntity(map); }
        });
    }

    [Test]
    public async Task CrashCharsTheCockpitAndMountsWithTheHull()
    {
        await Client.WaitAssertion(() =>
        {
            var map = CEntMan.System<SharedMapSystem>().CreateMap(out _);
            var hull = CEntMan.SpawnEntity("CMUFighterGround", new EntityCoordinates(map, Vector2.Zero));
            var ground = CEntMan.GetComponent<FighterGroundComponent>(hull);
            var cabin = CEntMan.SpawnEntity(null, new EntityCoordinates(map, Vector2.Zero));
            var aircraft = CEntMan.AddComponent<FighterAircraftComponent>(cabin);
            ground.Aircraft = cabin;
            ground.Canopy = CEntMan.SpawnEntity("CMUFighterCanopy", new EntityCoordinates(hull, Vector2.Zero));
            ground.FrontSeat = CEntMan.SpawnEntity("CMUFighterPilotSeat", new EntityCoordinates(hull, Vector2.Zero));
            ground.RearSeat = CEntMan.SpawnEntity("CMUFighterObserverSeat", new EntityCoordinates(hull, Vector2.Zero));
            var mount = CEntMan.SpawnEntity("CMUFighterHardpoint", new EntityCoordinates(hull, Vector2.Zero));
            var visuals = CEntMan.System<FighterGroundVisualSystem>();
            try
            {
                ground.State = FighterGroundState.Crashed;
                visuals.FrameUpdate(0);
                var hullColor = CEntMan.GetComponent<SpriteComponent>(hull).Color;
                Assert.That(Math.Max(hullColor.R, Math.Max(hullColor.G, hullColor.B)), Is.InRange(.5f, .7f),
                    "Scorching must preserve readable airframe details instead of turning the wreck almost black.");
                foreach (var part in new[] { ground.Canopy.Value, ground.FrontSeat.Value, ground.RearSeat.Value, mount })
                    Assert.That(CEntMan.GetComponent<SpriteComponent>(part).Color, Is.EqualTo(hullColor),
                        "Cockpit and weapon attachments must not remain bright over the blackened wreck.");

                ground.State = FighterGroundState.Grounded;
                visuals.FrameUpdate(0);
                Assert.That(CEntMan.GetComponent<SpriteComponent>(ground.Canopy.Value).Color, Is.EqualTo(Color.White),
                    "An intact cockpit must retain its normal colors.");
            }
            finally { CEntMan.DeleteEntity(map); }
        });
    }

}
