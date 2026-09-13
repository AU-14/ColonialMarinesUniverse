using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Moq;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.IntegrationTests.CMU14.Maps;

[TestFixture]
public sealed class BosenmoriSpriteTest : GameTest
{
    [TestCase("CMUBosenmoriExact14c4d784a867e0fd")]
    [TestCase("CMUBosenmoriExact4ec8fd43f3d673be")]
    [TestCase("CMUBosenmoriExactb41fdf7c43fe3355")]
    [TestCase("CMUBosenmoriExactcee913ec175d586b")]
    public async Task ImportedTablesRenderWithoutCornerLayers(string prototype)
    {
        await Client.WaitAssertion(() =>
        {
            var uid = CEntMan.Spawn(prototype);
            try
            {
                var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
                var sprites = Client.System<SpriteSystem>();
                var handle = new Mock<DrawingHandleWorld>(MockBehavior.Loose, sprite.BaseRSI["source"].Frame0);

                // Exercise the real renderer: missing smoothing states fall back to a one-direction
                // RSI, whose rotated corner layers previously caused IndexOutOfRangeException.
                for (var quarter = 0; quarter < 4; quarter++)
                {
                    var rotation = Angle.FromDegrees(quarter * 90);
                    Assert.DoesNotThrow(() => sprites.RenderSprite(
                        (uid, sprite), handle.Object, rotation, Angle.Zero, Vector2.Zero));
                }

                Assert.That(sprite.AllLayers.Count(), Is.EqualTo(1),
                    "Imported tables must retain their baked source sprite without smoothing corners.");
                Assert.That(sprite[0].RsiState.ToString(), Is.EqualTo("source"));
            }
            finally
            {
                CEntMan.DeleteEntity(uid);
            }
        });
    }
}
