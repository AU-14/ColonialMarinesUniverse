using Content.IntegrationTests.Fixtures;
using Content.Server.Body.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Atmos;

[TestFixture]
public sealed class EmptyInternalsBreathingTest : GameTest
{
    [Test]
    public async Task InternalsOnAnEmptyTankMakeTheWearerBreathe()
    {
        var map = await Pair.CreateTestMap();
        EntityUid victim = default;
        EntityUid tank = default;

        await Server.WaitAssertion(() =>
        {
            var internalsSystem = Server.System<SharedInternalsSystem>();
            victim = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var mask = SEntMan.SpawnEntity("ClothingMaskBreath", map.GridCoords);
            tank = SEntMan.SpawnEntity("OxygenTankFilled", map.GridCoords);
            var internals = SEntMan.EnsureComponent<InternalsComponent>(victim);
            internalsSystem.ConnectBreathTool((victim, internals), mask);
            Assert.That(internalsSystem.TryConnectTank((victim, internals), tank), Is.True);
        });

        await Pair.RunSeconds(1.5f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<RespiratorComponent>(victim), Is.False,
                "a full tank on a map without breathing must not add a respirator");
            SEntMan.GetComponent<GasTankComponent>(tank).Air.Clear();
        });

        await Pair.RunSeconds(1.5f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<RespiratorComponent>(victim), Is.True,
                "breathing through internals from an empty tank must make the wearer breathe, and so suffocate");
        });
    }
}
