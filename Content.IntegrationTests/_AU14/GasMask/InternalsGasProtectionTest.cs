using Content.Shared.CMU14.ChemicalIrritants;
using Content.Shared.CMU14.GasMask;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.GasMask;

[TestFixture]
public sealed class InternalsGasProtectionTest
{
    [Test]
    public async Task InternalsBlockTearGasLikeAFilter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var gasMask = entities.System<SharedGasMaskSystem>();
            var irritants = entities.System<SharedChemicalIrritantSystem>();
            var internalsSystem = entities.System<SharedInternalsSystem>();

            var victim = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var mask = entities.SpawnEntity("ClothingMaskBreath", MapCoordinates.Nullspace);
            var tank = entities.SpawnEntity("OxygenTankFilled", MapCoordinates.Nullspace);
            var internals = entities.EnsureComponent<InternalsComponent>(victim);

            Assert.That(gasMask.IsBreathingInternals(victim), Is.False, "Wearing nothing should not count as internals.");

            internalsSystem.ConnectBreathTool((victim, internals), mask);
            Assert.That(internalsSystem.TryConnectTank((victim, internals), tank), Is.True);
            Assert.That(gasMask.IsBreathingInternals(victim), Is.True);

            irritants.ApplyIrritantDirect(victim, 50, new ChemicalIrritantProfile());
            Assert.That(entities.HasComponent<ChemicalIrritantComponent>(victim), Is.False,
                "Tear gas reached a victim breathing from internals.");

            entities.GetComponent<GasTankComponent>(tank).Air.Clear();
            Assert.That(gasMask.IsBreathingInternals(victim), Is.False, "An empty tank should not protect.");

            irritants.ApplyIrritantDirect(victim, 50, new ChemicalIrritantProfile());
            Assert.That(entities.HasComponent<ChemicalIrritantComponent>(victim), Is.True,
                "Tear gas should reach a victim whose tank is empty.");

            entities.DeleteEntity(victim);
            entities.DeleteEntity(mask);
            entities.DeleteEntity(tank);
        });

        await pair.CleanReturnAsync();
    }
}
