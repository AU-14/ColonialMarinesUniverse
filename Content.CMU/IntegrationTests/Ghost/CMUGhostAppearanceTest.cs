using Content.Client.CMU14.Ghost;
using Content.Shared.CMU14.Ghost;
using Content.Shared.Ghost.Components;
using Robust.Client.GameObjects;

namespace Content.IntegrationTests.CMU14.Ghost;

[TestFixture]
public sealed class CMUGhostAppearanceTest
{
    [Test]
    public async Task DamageAppearanceUpdatesPreserveCopiedBody()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            var sprites = entities.System<SpriteSystem>();
            var appearance = entities.System<AppearanceSystem>();
            var ghost = entities.SpawnEntity("MobObserver", MapCoordinates.Nullspace);
            var body = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            try
            {
                // Humanoid sprites use per-layer RSIs without a shared base RSI.
                var bodySprite = entities.AddComponent<SpriteComponent>(body);
                sprites.AddLayer((body, bodySprite), new SpriteSpecifier.Rsi(
                    new ResPath("Mobs/Ghosts/ghost_human.rsi"), "animated"));

                // Keep the ordinary damage variant until a body is available to copy.
                appearance.SetData(ghost, GhostVisuals.Damage, "ghost_Piercing0");
                appearance.FrameUpdate(0);
                var ghostSprite = entities.GetComponent<SpriteComponent>(ghost);
                Assert.That(ghostSprite["ghostVariant"].RsiState.ToString(), Is.EqualTo("ghost_Piercing0"));

                entities.System<CMUGhostAppearanceSystem>().SetSource(ghost, body);
                entities.System<CMUGhostAppearanceClientSystem>().FrameUpdate(0);

                // A later appearance update must not recreate a default ghost layer on the copied body.
                appearance.SetData(ghost, GhostVisuals.Damage, "ghost_Piercing1");
                appearance.FrameUpdate(0);
                Assert.That(ghostSprite.AllLayers.Select(layer => (layer.Rsi, layer.RsiState)),
                    Is.EqualTo(bodySprite.AllLayers.Select(layer => (layer.Rsi, layer.RsiState))),
                    "Damage appearance updates must preserve the body's copied sprite layers.");
            }
            finally
            {
                entities.DeleteEntity(ghost);
                entities.DeleteEntity(body);
            }
        });
        await pair.CleanReturnAsync();
    }
}
