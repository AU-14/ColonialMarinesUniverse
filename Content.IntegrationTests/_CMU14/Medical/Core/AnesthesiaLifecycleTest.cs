using Content.Server.CMU14.Medical.Core;
using Content.Shared.Administration.Systems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Drowsiness;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Medical.Core;

[TestFixture]
public sealed class AnesthesiaLifecycleTest
{
    [Test]
    public async Task PausingAndReconnectingCannotCompleteAnOldInduction()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default, mask = default, tank = default;
        await pair.Server.WaitAssertion(() => (patient, mask, tank) = Connect(pair.Server.EntMan));
        await pair.RunTicksSync(pair.SecondsToTicks(2));
        await pair.Server.WaitPost(() => pair.Server.EntMan.System<MetaDataSystem>().SetEntityPaused(patient, true));
        await pair.RunTicksSync(pair.SecondsToTicks(7));
        await pair.Server.WaitAssertion(() =>
        {
            AssertSleep(pair.Server.EntMan, patient, false);
            pair.Server.EntMan.System<MetaDataSystem>().SetEntityPaused(patient, false);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(3));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            AssertSleep(entities, patient, false);
            var internals = entities.System<SharedInternalsSystem>();
            internals.DisconnectTank((patient, entities.GetComponent<InternalsComponent>(patient)), forced: true);
            Assert.That(entities.HasComponent<CMUAnesthesiaStateComponent>(patient), Is.False);
            Assert.That(internals.TryConnectTank((patient, entities.GetComponent<InternalsComponent>(patient)), tank), Is.True);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(2));
        await pair.Server.WaitAssertion(() => AssertSleep(pair.Server.EntMan, patient, false));
        await pair.RunTicksSync(pair.SecondsToTicks(4.2f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            AssertSleep(entities, patient, true);
            entities.System<SharedInternalsSystem>().DisconnectTank((patient, entities.GetComponent<InternalsComponent>(patient)), forced: true);
            AssertSleep(entities, patient, false);
            Delete(entities, patient, mask, tank);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ConsumedGasIsRecheckedDuringInductionAndWhileAsleep(bool induced)
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default, mask = default, tank = default;
        await pair.Server.WaitAssertion(() => (patient, mask, tank) = Connect(pair.Server.EntMan));
        await pair.RunTicksSync(pair.SecondsToTicks(induced ? 6.2f : 5.2f));
        await pair.Server.WaitAssertion(() =>
        {
            AssertSleep(pair.Server.EntMan, patient, induced);
            pair.Server.EntMan.GetComponent<GasTankComponent>(tank).Air.SetMoles(Gas.NitrousOxide, 0);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(1.2f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.HasComponent<CMUAnesthesiaStateComponent>(patient), Is.False);
            AssertSleep(entities, patient, false);
            Delete(entities, patient, mask, tank);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DisconnectPreservesAnotherSedativeAndItsDrowsinessDeadline()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default, mask = default, tank = default, drowsiness = default, forcedSleep = default;
        TimeSpan? otherDeadline = null;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            (patient, mask, tank) = Connect(entities);
            var status = entities.System<StatusEffectsSystem>();
            Assert.That(status.TrySetStatusEffectDuration(patient, "StatusEffectDrowsiness", out var effect, TimeSpan.FromSeconds(40)), Is.True);
            drowsiness = effect!.Value;
            otherDeadline = entities.GetComponent<StatusEffectComponent>(drowsiness).EndEffectTime;
            // Keep this test deterministic; the second forced-sleep source is applied explicitly below.
            entities.GetComponent<DrowsinessStatusEffectComponent>(drowsiness).CausesSleep = false;
        });
        await pair.RunTicksSync(pair.SecondsToTicks(6.2f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var status = entities.System<StatusEffectsSystem>();
            AssertSleep(entities, patient, true);
            Assert.That(status.TrySetStatusEffectDuration(patient, "StatusEffectForcedSleeping", out var other, TimeSpan.FromSeconds(20)), Is.True);
            forcedSleep = other!.Value;
            entities.System<SharedInternalsSystem>().DisconnectTank((patient, entities.GetComponent<InternalsComponent>(patient)), forced: true);
            Assert.That(status.HasStatusEffect(patient, "StatusEffectCMUAnesthesia"), Is.False);
            Assert.That(status.HasStatusEffect(patient, "StatusEffectCMUAnesthesiaInduction"), Is.False);
            Assert.That(status.TryGetStatusEffect(patient, "StatusEffectDrowsiness", out var retained), Is.True);
            Assert.That(retained, Is.EqualTo(drowsiness));
            Assert.That(entities.GetComponent<StatusEffectComponent>(drowsiness).EndEffectTime, Is.EqualTo(otherDeadline));
            Assert.That(entities.HasComponent<SleepingComponent>(patient), Is.True);
            entities.DeleteEntity(forcedSleep);
            Assert.That(entities.System<SleepingSystem>().TryWaking((patient, null)), Is.True);
            Delete(entities, patient, mask, tank);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ClosedRegulatorCannotInduceOrMaintainAnesthesia(bool induced)
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default, mask = default, tank = default;
        await pair.Server.WaitAssertion(() => (patient, mask, tank) = Connect(pair.Server.EntMan));
        await pair.RunTicksSync(pair.SecondsToTicks(induced ? 6.2f : 2));
        await pair.Server.WaitAssertion(() =>
        {
            AssertSleep(pair.Server.EntMan, patient, induced);
            var command = new GasTankSetPressureMessage { Pressure = 0, Actor = patient };
            pair.Server.EntMan.EventBus.RaiseLocalEvent(tank, command);
            Assert.That(pair.Server.EntMan.GetComponent<GasTankComponent>(tank).ReleasePressure, Is.Zero);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(7));
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(pair.Server.EntMan.HasComponent<CMUAnesthesiaStateComponent>(patient), Is.False);
            AssertSleep(pair.Server.EntMan, patient, false);
            Delete(pair.Server.EntMan, patient, mask, tank);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SleepStartedDuringInductionIsNotOwnedByAnesthesia()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default, mask = default, tank = default;
        await pair.Server.WaitAssertion(() => (patient, mask, tank) = Connect(pair.Server.EntMan));
        await pair.RunTicksSync(pair.SecondsToTicks(2));
        await pair.Server.WaitAssertion(() => Assert.That(pair.Server.EntMan.System<SleepingSystem>().TrySleeping((patient, null)), Is.True));
        await pair.RunTicksSync(pair.SecondsToTicks(4.2f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            AssertSleep(entities, patient, true);
            entities.System<SharedInternalsSystem>().DisconnectTank((patient, entities.GetComponent<InternalsComponent>(patient)), forced: true);
            Assert.That(entities.HasComponent<SleepingComponent>(patient), Is.True);
            Assert.That(entities.System<StatusEffectsSystem>().HasStatusEffect(patient, "StatusEffectCMUAnesthesia"), Is.False);
            Delete(entities, patient, mask, tank);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task RejuvenationRetiresPendingAndAppliedAnesthesia(bool induced)
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default, mask = default, tank = default;
        await pair.Server.WaitAssertion(() => (patient, mask, tank) = Connect(pair.Server.EntMan));
        await pair.RunTicksSync(pair.SecondsToTicks(induced ? 6.2f : 2));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<RejuvenateSystem>().PerformRejuvenate(patient);
            Assert.That(entities.HasComponent<CMUAnesthesiaStateComponent>(patient), Is.False);
            AssertSleep(entities, patient, false);
            entities.System<SharedInternalsSystem>().DisconnectTank((patient, entities.GetComponent<InternalsComponent>(patient)), forced: true);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(7));
        await pair.Server.WaitAssertion(() =>
        {
            AssertSleep(pair.Server.EntMan, patient, false);
            Delete(pair.Server.EntMan, patient, mask, tank);
        });
        await pair.CleanReturnAsync();
    }

    private static (EntityUid Patient, EntityUid Mask, EntityUid Tank) Connect(IEntityManager entities)
    {
        var patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        var mask = entities.SpawnEntity("ClothingMaskBreath", MapCoordinates.Nullspace);
        var tank = entities.SpawnEntity("OxygenTankFilled", MapCoordinates.Nullspace);
        entities.GetComponent<GasTankComponent>(tank).Air.SetMoles(Gas.NitrousOxide, 1);
        var internals = entities.EnsureComponent<InternalsComponent>(patient);
        var system = entities.System<SharedInternalsSystem>();
        system.ConnectBreathTool((patient, internals), mask);
        Assert.That(system.TryConnectTank((patient, internals), tank), Is.True);
        Assert.That(system.AreInternalsWorking(patient), Is.True);
        Assert.That(entities.HasComponent<CMUAnesthesiaStateComponent>(patient), Is.True);
        AssertSleep(entities, patient, false);
        return (patient, mask, tank);
    }

    private static void AssertSleep(IEntityManager entities, EntityUid patient, bool expected)
    {
        Assert.That(entities.System<StatusEffectsSystem>().HasStatusEffect(patient, "StatusEffectCMUAnesthesia"), Is.EqualTo(expected));
        Assert.That(entities.HasComponent<SleepingComponent>(patient), Is.EqualTo(expected));
    }

    private static void Delete(IEntityManager entities, params EntityUid[] entitiesToDelete)
    {
        foreach (var entity in entitiesToDelete)
            entities.DeleteEntity(entity);
    }
}
