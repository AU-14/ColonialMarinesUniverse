using Content.Client._RMC14.Explosion;
using Robust.Client.Graphics;

namespace Content.IntegrationTests._RMC14.Explosion;

[TestFixture]
public sealed class RMCExplosionShockWaveOverlayTest
{
    [Test]
    public async Task ShockWaveOverlayIsRegisteredAtClientStartup()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var overlays = pair.Client.ResolveDependency<IOverlayManager>();

        await pair.Client.WaitAssertion(() =>
            Assert.That(overlays.HasOverlay<RMCExplosionShockWaveOverlay>(),
                Is.True,
                "RMC shockwave entities are invisible unless their world-space shader overlay is registered."));

        await pair.CleanReturnAsync();
    }
}
