using Content.Client.CMU14.ZLevels.Core;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Client.GameObjects;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUGhostDrawDepthTest : GameTest
{
    [Test]
    public async Task AirborneObserverStaysAboveDoors()
    {
        EntityUid observer = default;

        await Pair.Client.WaitAssertion(() =>
        {
            observer = Pair.Client.EntMan.Spawn("MobObserver");
            var zPhysics = Pair.Client.EntMan.GetComponent<CMUZPhysicsComponent>(observer);
            Pair.Client.System<CMUClientZLevelsSystem>().SetZLocalPosition((observer, zPhysics), 0.5f);
        });

        await Pair.RunTicksSync(1);

        try
        {
            await Pair.Client.WaitAssertion(() =>
            {
                var sprite = Pair.Client.EntMan.GetComponent<SpriteComponent>(observer);
                Assert.Multiple(() =>
                {
                    Assert.That(sprite.DrawDepth, Is.EqualTo((int) DrawDepth.Ghosts));
                    Assert.That(sprite.DrawDepth, Is.GreaterThan((int) DrawDepth.Doors));
                });
            });
        }
        finally
        {
            await Pair.Client.WaitAssertion(() => Pair.Client.EntMan.DeleteEntity(observer));
        }
    }
}
