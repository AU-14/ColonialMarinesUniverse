using System.Numerics;
using Content.Shared.CMU14.DroneOperator;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.DroneOperator;

[TestFixture]
public sealed class CMUDroneTransferShakeTest
{
    [TestCase("CMUCombatDrone", false)]
    [TestCase("CMUFlamerDrone", false)]
    [TestCase("CMUCombatDrone", true)]
    [TestCase("CMUFlamerDrone", true)]
    public async Task RepeatedControlTransfersRestoreSpriteOffset(string prototype, bool interrupt)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        NetEntity droneNet = default;
        await pair.Server.WaitPost(() =>
        {
            var drone = pair.Server.EntMan.SpawnEntity(prototype, map.GridCoords);
            droneNet = pair.Server.EntMan.GetNetEntity(drone);
            pair.Server.PlayerMan.SetAttachedEntity(pair.Player!, drone);
        });
        await pair.RunUntilSynced();

        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            var drone = entities.GetEntity(droneNet);
            var sprite = entities.GetComponent<SpriteComponent>(drone);
            var animations = entities.System<AnimationPlayerSystem>();
            var originalOffset = new Vector2(0.23f, -0.17f);
            var originalPosition = entities.GetComponent<TransformComponent>(drone).LocalPosition;
            entities.System<SpriteSystem>().SetOffset((drone, sprite), originalOffset);

            for (var i = 0; i < 5; i++)
            {
                Shake();
                animations.FrameUpdate(0.025f);
                Assert.That(Vector2.Distance(sprite.Offset, originalOffset), Is.GreaterThan(0.01f),
                    "The transfer effect must actually shake the sprite.");
                if (interrupt)
                    Shake();

                // Finish at a normal frame boundary just beyond the advertised 0.1 second duration.
                for (var frame = 0; frame < 7; frame++)
                    animations.FrameUpdate(1f / 60f);

                Assert.That(animations.HasRunningAnimation(drone, "cmu-drone-transfer-shake"), Is.False);
                Assert.That(Vector2.Distance(sprite.Offset, originalOffset), Is.LessThan(0.0001f),
                    "Transfers must return to the original center instead of accumulating sprite drift.");
                Assert.That(entities.GetComponent<TransformComponent>(drone).LocalPosition,
                    Is.EqualTo(originalPosition));
            }

            void Shake()
            {
                entities.EventBus.RaiseEvent(EventSource.Network, new CMUDroneAndroidShakeEvent(droneNet, 0.1f));
            }
        });

        await pair.CleanReturnAsync();
    }
}
