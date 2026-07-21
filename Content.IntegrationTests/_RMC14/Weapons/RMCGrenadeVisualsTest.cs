using Content.Client.Trigger.Systems;
using Content.IntegrationTests.Fixtures;
using Robust.Client.GameObjects;

namespace Content.IntegrationTests._RMC14.Weapons;

[TestFixture]
public sealed class RMCGrenadeVisualsTest : GameTest
{
    private static readonly string[] Grenades =
    [
        "CMGrenadeHighExplosive",
        "CMGrenadeFrag",
    ];

    [Test]
    public async Task GrenadesHavePrimedVisuals()
    {
        var client = Pair.Client;
        var spriteSystem = client.System<SpriteSystem>();

        await client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var prototype in Grenades)
                {
                    var grenade = client.EntMan.Spawn(prototype);
                    try
                    {
                        Assert.That(client.EntMan.HasComponent<GenericVisualizerComponent>(grenade),
                            Is.True,
                            $"{prototype} has no timer visualizer.");

                        var sprite = client.EntMan.GetComponent<SpriteComponent>(grenade);
                        Assert.That(spriteSystem.LayerMapTryGet(
                                (grenade, sprite),
                                TriggerVisualLayers.Base,
                                out var layerId,
                                false),
                            Is.True,
                            $"{prototype} has no trigger visual layer.");

                        if (!spriteSystem.TryGetLayer((grenade, sprite), layerId, out var layer, false))
                        {
                            Assert.Fail($"{prototype} has no sprite layer for its trigger visuals.");
                            continue;
                        }

                        Assert.That(layer.ActualRsi.TryGetState("primed", out _),
                            Is.True,
                            $"{prototype} has no primed animation in {layer.ActualRsi.Path}.");
                    }
                    finally
                    {
                        client.EntMan.DeleteEntity(grenade);
                    }
                }
            });
        });
    }
}
