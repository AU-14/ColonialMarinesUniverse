using System.Linq;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests;

/// <summary>
/// Tests to see if any entity prototypes specify solution fill level sprites that don't exist.
/// </summary>
[TestFixture]
public sealed class FillLevelSpriteTest
{
    private static readonly string[] HandStateNames = ["left", "right"];

    [Test]
    public async Task FillLevelSpritesExist()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var protoMan = client.ResolveDependency<IPrototypeManager>();
        var componentFactory = client.ResolveDependency<IComponentFactory>();
        var resourceCache = client.ResolveDependency<IResourceCache>();

        await client.WaitAssertion(() =>
        {
            var protos = protoMan.EnumeratePrototypes<EntityPrototype>()
                .Where(p => !p.Abstract)
                .Where(p => !pair.IsTestPrototype(p))
                .Where(p => p.TryComp<SolutionContainerVisualsComponent>(out _, componentFactory))
                .OrderBy(p => p.ID)
                .ToList();

            foreach (var proto in protos)
            {
                Assert.That(proto.TryComp<SolutionContainerVisualsComponent>(out var visuals, componentFactory));
                Assert.That(proto.TryComp<SpriteComponent>(out var sprite, componentFactory));

                var rsi = sprite.BaseRSI;
                var hasFillLayer = sprite.LayerMapTryGet(SolutionContainerLayers.Fill, out _, false);
                if (visuals.FillSprite is SpriteSpecifier.Rsi fillSprite)
                {
                    var rsiPath = SpriteSpecifierSerializer.TextureRoot / fillSprite.RsiPath;
                    Assert.That(resourceCache.TryGetResource<RSIResource>(rsiPath, out var resource), Is.True,
                        $"{proto.ID} fillSprite RSI {rsiPath} should load");
                    rsi = resource!.RSI;
                }

                var inhandRsi = rsi;
                if (proto.TryComp<ItemComponent>(out var item, componentFactory) && item.RsiPath != null)
                {
                    var rsiPath = SpriteSpecifierSerializer.TextureRoot / item.RsiPath;
                    Assert.That(resourceCache.TryGetResource<RSIResource>(rsiPath, out var resource), Is.True,
                        $"{proto.ID} item in-hand RSI {rsiPath} should load");
                    inhandRsi = resource!.RSI;
                }

                // Test base sprite fills
                if (hasFillLayer && !string.IsNullOrEmpty(visuals.FillBaseName))
                {
                    for (var i = 1; i <= visuals.MaxFillLevels; i++)
                    {
                        var state = $"{visuals.FillBaseName}{i}";
                        Assert.That(rsi.TryGetState(state, out _), @$"{proto.ID} has SolutionContainerVisualsComponent with
                            MaxFillLevels = {visuals.MaxFillLevels}, but {rsi.Path} doesn't have state {state}!");
                    }
                }

                // Test inhand sprite fills
                if (!string.IsNullOrEmpty(visuals.InHandsFillBaseName))
                {
                    for (var i = 1; i <= visuals.InHandsMaxFillLevels; i++)
                    {
                        foreach (var handname in HandStateNames)
                        {
                            var state = $"inhand-{handname}{visuals.InHandsFillBaseName}{i}";
                            Assert.That(inhandRsi.TryGetState(state, out _), @$"{proto.ID} has SolutionContainerVisualsComponent with
                                InHandsMaxFillLevels = {visuals.InHandsMaxFillLevels}, but {inhandRsi.Path} doesn't have state {state}!");
                        }

                    }
                }
            }
        });

        await pair.CleanReturnAsync();
    }
}
