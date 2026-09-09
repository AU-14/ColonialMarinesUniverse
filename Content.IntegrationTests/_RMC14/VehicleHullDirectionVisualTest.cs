using Content.Client._RMC14.Vehicle;
using Content.IntegrationTests.Fixtures;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class VehicleHullDirectionVisualTest : GameTest
{
    [Test]
    public async Task HullDirectionIsReappliedAfterSpriteStateRefresh()
    {
        EntityUid vehicle = default;

        try
        {
            await Pair.Client.WaitAssertion(() =>
            {
                var entities = Pair.Client.EntMan;
                var eye = Pair.Client.ResolveDependency<IEyeManager>();
                var transforms = entities.System<SharedTransformSystem>();
                var directions = entities.System<VehicleExactCardinalDirectionSystem>();

                vehicle = entities.Spawn("VehicleTank");
                entities.RunMapInit(vehicle, entities.GetComponent<MetaDataComponent>(vehicle));
                eye.CurrentEye.Rotation = Angle.Zero;
                transforms.SetWorldRotation(vehicle, Angle.FromDegrees(90));

                directions.FrameUpdate(0f);

                var sprite = entities.GetComponent<SpriteComponent>(vehicle);
                Assert.That(sprite.DirectionOverride, Is.EqualTo(Direction.East));

                // Authoritative sprite state and other visual systems can restore the
                // prototype direction while the chassis transform remains unchanged.
                sprite.DirectionOverride = Direction.South;
                directions.FrameUpdate(0f);

                Assert.That(sprite.DirectionOverride, Is.EqualTo(Direction.East),
                    "an east-moving vehicle must not leave its hull visually facing south");
            });
        }
        finally
        {
            if (vehicle.IsValid())
                await Pair.Client.WaitAssertion(() => Pair.Client.EntMan.DeleteEntity(vehicle));
        }
    }
}
