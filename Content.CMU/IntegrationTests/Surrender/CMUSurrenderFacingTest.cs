using Content.Client.CMU14.Surrender;
using Robust.Client.Graphics;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Chat.Systems;
using Content.Server.CMU14.Surrender;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Surrender;

[TestFixture]
public sealed class CMUSurrenderFacingTest : GameTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task TurningUsesAllFourPosesAndLoweringRestoresDirectionControl(bool fixedFacing)
    {
        var map = await Pair.CreateTestMap();
        var attached = ServerSession!.AttachedEntity;
        EntityUid human = default;
        try
        {
            await Server.WaitPost(() =>
            {
                human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                Server.PlayerMan.SetAttachedEntity(ServerSession, human);
            });
            await Pair.RunUntilSynced();
            var client = Pair.ToClientUid(human);
            var originalOverride = false;
            var originalDirection = Direction.South;
            await Client.WaitPost(() =>
            {
                var sprite = CEntMan.GetComponent<SpriteComponent>(client);
                if (fixedFacing)
                {
                    sprite.EnableDirectionOverride = true;
                    sprite.DirectionOverride = Direction.East;
                }
                originalOverride = sprite.EnableDirectionOverride;
                originalDirection = sprite.DirectionOverride;
            });
            await Server.WaitAssertion(() => Assert.That(Server.System<ChatSystem>()
                .TryEmoteWithChat(human, CMUSurrenderSystem.SurrenderEmote), Is.True));
            await Pair.RunUntilSynced();
            foreach (var direction in new[] { Direction.South, Direction.East, Direction.North, Direction.West })
            {
                await Server.WaitPost(() => Server.System<SharedTransformSystem>()
                    .SetLocalRotation(human, direction.ToAngle()));
                await Pair.RunUntilSynced();
                await Client.WaitAssertion(() =>
                {
                    Client.System<CMUSurrenderVisualsSystem>().FrameUpdate(1f / 60);
                    var sprite = CEntMan.GetComponent<SpriteComponent>(client);
                    Assert.That(sprite.EnableDirectionOverride, Is.True);
                    Assert.That(sprite.DirectionOverride,
                        Is.EqualTo(fixedFacing ? Direction.East : direction));
                });
            }
            await Client.WaitAssertion(() =>
            {
                var eye = Client.ResolveDependency<IEyeManager>().CurrentEye;
                var rotation = eye.Rotation;
                var sprite = CEntMan.GetComponent<SpriteComponent>(client);
                var layers = sprite.AllLayers.ToArray();
                try
                {
                    // The character last faced west. A quarter-turn camera must
                    // select its south frame without allocating another pose.
                    eye.Rotation = rotation + Angle.FromDegrees(90);
                    Client.System<CMUSurrenderVisualsSystem>().FrameUpdate(0);
                    Assert.That(sprite.DirectionOverride,
                        Is.EqualTo(fixedFacing ? Direction.East : Direction.South));
                    Assert.That(sprite.AllLayers, Is.EqualTo(layers),
                        "Turning the camera must reuse the existing pose layers.");
                }
                finally { eye.Rotation = rotation; }
            });
            await Server.WaitAssertion(() => Assert.That(Server.System<ChatSystem>()
                .TryEmoteWithChat(human, CMUSurrenderSystem.SurrenderEmote), Is.True));
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                var sprite = CEntMan.GetComponent<SpriteComponent>(client);
                Assert.That(sprite.EnableDirectionOverride, Is.EqualTo(originalOverride));
                Assert.That(sprite.DirectionOverride, Is.EqualTo(originalDirection));
                Assert.That(CEntMan.HasComponent<CMUSurrenderFacingComponent>(client), Is.False);
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, attached));
            if (human != default) await Pair.DeleteEntityTreeLeafFirst(human);
        }
    }
}
