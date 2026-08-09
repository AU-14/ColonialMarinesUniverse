using Content.IntegrationTests.Pair;
using Content.Server._CMU14.Medical.Core;
using Content.Server.Body.Systems;
using Content.Shared.Atmos.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Medical;

[TestFixture]
public sealed class CMUAnesthesiaInternalsTest
{
    [Test]
    public async Task WorkingAnestheticInternalsInduceSleepAndClearAnesthesiaOnDisconnect()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid patient = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var mask = entities.SpawnEntity("ClothingMaskBreath", MapCoordinates.Nullspace);
            var tank = entities.SpawnEntity("CMAnestheticTankFilled", MapCoordinates.Nullspace);
            var internals = entities.System<InternalsSystem>();
            var internalsComponent = entities.GetComponent<InternalsComponent>(patient);

            internals.ConnectBreathTool((patient, internalsComponent), mask);
            Assert.That(internals.TryConnectTank((patient, internalsComponent), tank), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(internals.AreInternalsWorking(patient), Is.True);
                Assert.That(entities.HasComponent<CMUAnesthesiaStateComponent>(patient), Is.True,
                    "Working anesthetic internals did not start anesthesia induction.");
            });
        });

        await pair.RunSeconds(6.1f);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(entities.HasComponent<SleepingComponent>(patient), Is.True,
                "Anesthetic internals did not put the patient to sleep after induction.");

            var internals = entities.System<InternalsSystem>();
            var internalsComponent = entities.GetComponent<InternalsComponent>(patient);
            internals.DisconnectTank((patient, internalsComponent), forced: true);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(entities.HasComponent<CMUAnesthesiaStateComponent>(patient), Is.False);
            Assert.That(
                entities.System<StatusEffectsSystem>().HasStatusEffect(patient, "StatusEffectCMUAnesthesia"),
                Is.False,
                "The CMU anesthesia effect remained after internals disconnected.");
        });

        await pair.CleanReturnAsync();
    }
}
