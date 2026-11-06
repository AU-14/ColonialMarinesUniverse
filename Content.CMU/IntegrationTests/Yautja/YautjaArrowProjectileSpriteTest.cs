using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaArrowProjectileSpriteTest
{
    private static readonly string[] Projectiles =
    {
        "CMUYautjaArrowProjectile",
        "CMUYautjaExplosiveArrowProjectile",
        "CMUYautjaEmpArrowProjectile",
        "CMUYautjaSnareArrowProjectile",
    };

    // BaseBullet brings a "bullet" layer; a child that only sets sprite + state keeps
    // that layer pointed at the bow RSI, which has no such state, so the shot drew as an error sprite.
    [Test]
    public async Task FiredArrowsOnlyDrawStatesTheirRsiHas()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            var sprites = client.System<SpriteSystem>();
            Assert.Multiple(() =>
            {
                foreach (var id in Projectiles)
                {
                    var uid = client.EntMan.Spawn(id);
                    var sprite = client.EntMan.GetComponent<SpriteComponent>(uid);
                    for (var i = 0; sprites.TryGetLayer((uid, sprite), i, out var layer, false); i++)
                    {
                        if (!layer.Visible || layer.ActualRsi is not { } rsi)
                            continue;

                        Assert.That(rsi.TryGetState(layer.State, out _), Is.True,
                            $"{id} layer {i} wants state '{layer.State}' that {rsi.Path} does not have");
                    }

                    client.EntMan.DeleteEntity(uid);
                }
            });
        });

        await pair.CleanReturnAsync();
    }
}
