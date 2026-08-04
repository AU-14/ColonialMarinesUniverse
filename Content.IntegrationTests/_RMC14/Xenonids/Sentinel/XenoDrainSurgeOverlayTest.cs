using Content.Client._RMC14.Xenonids.Sentinel;
using Content.IntegrationTests.Fixtures;
using Robust.Client.ResourceManagement;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._RMC14.Xenonids.Sentinel;

[TestFixture]
public sealed class XenoDrainSurgeOverlayTest : GameTest
{
    private static readonly ResPath EffectsRsi = new("/Textures/_RMC14/Effects/effects.rsi");
    private static readonly string[] RequiredStates = ["drip", "x"];

    [Test]
    public async Task ParticleStatesExist()
    {
        var client = Pair.Client;

        await client.WaitAssertion(() =>
        {
            var resourceCache = client.ResolveDependency<IResourceCache>();
            var rsi = resourceCache.GetResource<RSIResource>(EffectsRsi).RSI;

            Assert.Multiple(() =>
            {
                foreach (var state in RequiredStates)
                {
                    Assert.That(rsi.TryGetState(state, out _),
                        $"{nameof(XenoDrainSurgeOverlay)} requires state {state} in {EffectsRsi}.");
                }
            });
        });
    }
}
