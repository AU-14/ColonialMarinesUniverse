using System.Collections.Generic;
using Content.Shared.CMU14.Chemistry.Effects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Movement.Components;
using Content.Shared.CMU14.Medical.Injuries.Pain.Penalties;
using Content.Shared.Administration.Systems;

namespace Content.IntegrationTests.CMU14.Medical.Core;

[TestFixture]
public sealed class ChemicalStatusAuthorityTest
{
    [Test]
    public async Task RejuvenationRetiresEveryChemicalPropertyAndItsSourceHistory()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            ApplyAll(entities.System<ChemicalPropertyStatusSystem>(), patient);
            AssertAll(entities, patient, true);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(1));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<RejuvenateSystem>().PerformRejuvenate(patient);
            AssertAll(entities, patient, false);
            entities.System<ChemicalPropertyStatusSystem>().ApplyNerveStimulation(patient, 1, "fresh");
            Assert.That(entities.GetComponent<ChemicalNerveStimulationComponent>(patient).Strength, Is.EqualTo(1));
        });
        await pair.RunTicksSync(pair.SecondsToTicks(1.25f));
        await pair.Server.WaitAssertion(() =>
            Assert.That(pair.Server.EntMan.HasComponent<ChemicalNerveStimulationComponent>(patient), Is.True,
                "The retired dose's deadline removed a fresh source."));
        await pair.RunTicksSync(pair.SecondsToTicks(1));
        await pair.Server.WaitAssertion(() =>
        {
            AssertAll(pair.Server.EntMan, patient, false);
            pair.Server.EntMan.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovingStimulantsReconcilesCachedMovementAndActionSpeed()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        await pair.Server.WaitPost(() => patient = pair.Server.EntMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace));
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var movement = entities.GetComponent<MovementSpeedModifierComponent>(patient);
            var medicalSpeed = entities.System<SharedCMUMedicalSpeedSystem>();
            var chemicals = entities.System<ChemicalPropertyStatusSystem>();
#pragma warning disable RA0002 // Inspect public movement projection after each owner mutation.
            var initialSpeed = movement.CurrentWalkSpeed;
            var initialAction = medicalSpeed.ComputeActionSpeedMultiplier(patient);
            chemicals.ApplyNerveStimulation(patient, 2);
            chemicals.ApplyMuscleStimulation(patient, 2);
            Assert.That(movement.CurrentWalkSpeed, Is.GreaterThan(initialSpeed));
            Assert.That(medicalSpeed.ComputeActionSpeedMultiplier(patient), Is.LessThan(initialAction));
            entities.RemoveComponent<ChemicalNerveStimulationComponent>(patient);
            entities.RemoveComponent<ChemicalMuscleStimulationComponent>(patient);
            Assert.That(movement.CurrentWalkSpeed, Is.EqualTo(initialSpeed).Within(0.0001f));
#pragma warning restore RA0002
            Assert.That(medicalSpeed.ComputeActionSpeedMultiplier(patient), Is.EqualTo(initialAction).Within(0.0001f));
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task IndependentAntiparasiticExpiryRefreshesIncubationImmediately()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid target = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            target = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
#pragma warning disable RA0002 // Infection is fixture state; exercise medicine through its public API.
            entities.EnsureComponent<VictimInfectedComponent>(target).BurstAt =
                pair.Server.ResolveDependency<IGameTiming>().CurTime + TimeSpan.FromMinutes(8);
            entities.System<ChemicalPropertyStatusSystem>().ApplyAntiparasitic(target, 2, 0, "strong");
            Assert.That(entities.GetComponent<VictimInfectedComponent>(target).IncubationMultiplier, Is.Zero);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(1));
        await pair.Server.WaitPost(() =>
            pair.Server.EntMan.System<ChemicalPropertyStatusSystem>().ApplyAntiparasitic(target, 1, 0, "weak"));
        await pair.RunTicksSync(pair.SecondsToTicks(1.25f));
        await pair.Server.WaitAssertion(() =>
            Assert.That(pair.Server.EntMan.GetComponent<VictimInfectedComponent>(target).IncubationMultiplier, Is.EqualTo(0.5f)));
        await pair.RunTicksSync(pair.SecondsToTicks(1));
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(pair.Server.EntMan.GetComponent<VictimInfectedComponent>(target).IncubationMultiplier, Is.EqualTo(1));
#pragma warning restore RA0002
            pair.Server.EntMan.DeleteEntity(target);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReplicatedSourcesSurviveClientUpdatesUntilAuthoritativeExpiry()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var player = pair.Player!;
        var original = player.AttachedEntity;
        EntityUid target = default;
        NetEntity networkTarget = default;
        try
        {
            await pair.Server.WaitPost(() =>
            {
                var entities = pair.Server.EntMan;
                target = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                networkTarget = entities.GetNetEntity(target);
                pair.Server.PlayerMan.SetAttachedEntity(player, target);
                ApplyAll(entities.System<ChemicalPropertyStatusSystem>(), target);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(0.75f));
            await pair.Client.WaitAssertion(() =>
            {
                var entities = pair.Client.EntMan;
                var clientTarget = entities.GetEntity(networkTarget);
                AssertAll(entities, clientTarget, true);
                // The public application boundary also rejects client-side sources.
                entities.System<ChemicalPropertyStatusSystem>().ApplyNerveStimulation(clientTarget, 10);
                Assert.That(entities.GetComponent<ChemicalNerveStimulationComponent>(clientTarget).Strength,
                    Is.EqualTo(2));
            });
            await pair.RunTicksSync(pair.SecondsToTicks(1.75f));
            await pair.Server.WaitAssertion(() => AssertAll(pair.Server.EntMan, target, false));
            await pair.Client.WaitAssertion(() =>
                AssertAll(pair.Client.EntMan, pair.Client.EntMan.GetEntity(networkTarget), false));
        }
        finally
        {
            await pair.Server.WaitPost(() => pair.Server.PlayerMan.SetAttachedEntity(player, original));
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PausedSourcesKeepTheirLifetimesAndFallbackToTheRemainingStrength()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid target = default;
        await pair.Server.WaitPost(() =>
        {
            target = pair.Server.EntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            pair.Server.EntMan.System<ChemicalPropertyStatusSystem>().ApplyNerveStimulation(target, 3, "strong");
        });
        await pair.RunTicksSync(pair.SecondsToTicks(1));
        await pair.Server.WaitPost(() =>
        {
            pair.Server.EntMan.System<ChemicalPropertyStatusSystem>().ApplyNerveStimulation(target, 1, "weak");
            pair.Server.EntMan.System<MetaDataSystem>().SetEntityPaused(target, true);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(3));
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(pair.Server.EntMan.GetComponent<ChemicalNerveStimulationComponent>(target).Strength, Is.EqualTo(3));
            pair.Server.EntMan.System<MetaDataSystem>().SetEntityPaused(target, false);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(1.25f));
        await pair.Server.WaitAssertion(() =>
            Assert.That(pair.Server.EntMan.GetComponent<ChemicalNerveStimulationComponent>(target).Strength, Is.EqualTo(1)));
        await pair.RunTicksSync(pair.SecondsToTicks(1));
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(pair.Server.EntMan.HasComponent<ChemicalNerveStimulationComponent>(target), Is.False);
            pair.Server.EntMan.DeleteEntity(target);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(50)]
    [TestCase(200)]
    [TestCase(500)]
    public async Task StableMedicationDoesNotDirtyEverySimulationFrame(int population)
    {
        await using var pair = await PoolManager.GetServerClient();
        var targets = new List<(EntityUid Entity, GameTick LastDirty)>(population);
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            var status = entities.System<ChemicalPropertyStatusSystem>();
            for (var i = 0; i < population; i++)
            {
                var target = entities.SpawnEntity(null, MapCoordinates.Nullspace);
                ApplyAll(status, target);
                targets.Add((target, entities.GetComponent<ChemicalNerveStimulationComponent>(target).LastModifiedTick));
            }
        });
        await pair.RunTicksSync(pair.SecondsToTicks(1));
        await pair.Server.WaitAssertion(() =>
        {
            foreach (var (target, lastDirty) in targets)
            {
                Assert.That(pair.Server.EntMan.GetComponent<ChemicalNerveStimulationComponent>(target).LastModifiedTick,
                    Is.EqualTo(lastDirty));
                pair.Server.EntMan.DeleteEntity(target);
            }
        });
        await pair.RunTicksSync(pair.SecondsToTicks(2));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ApplicationMidPauseStartsItsDurationAtResume()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid target = default;
        await pair.Server.WaitPost(() =>
        {
            target = pair.Server.EntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            pair.Server.EntMan.System<MetaDataSystem>().SetEntityPaused(target, true);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(5));
        await pair.Server.WaitPost(() => ApplyAll(pair.Server.EntMan.System<ChemicalPropertyStatusSystem>(), target));
        await pair.RunTicksSync(pair.SecondsToTicks(2));
        await pair.Server.WaitPost(() => pair.Server.EntMan.System<MetaDataSystem>().SetEntityPaused(target, false));
        await pair.RunTicksSync(pair.SecondsToTicks(1));
        await pair.Server.WaitAssertion(() => AssertAll(pair.Server.EntMan, target, true));
        await pair.RunTicksSync(pair.SecondsToTicks(1.5f));
        await pair.Server.WaitAssertion(() =>
        {
            AssertAll(pair.Server.EntMan, target, false);
            pair.Server.EntMan.DeleteEntity(target);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovedStatusCannotResurrectAnOldSourceOnReapplication()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var target = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var status = entities.System<ChemicalPropertyStatusSystem>();
            status.ApplyNerveStimulation(target, 5, "old");
            entities.RemoveComponent<ChemicalNerveStimulationComponent>(target);
            status.ApplyNerveStimulation(target, 1, "new");
            Assert.That(entities.GetComponent<ChemicalNerveStimulationComponent>(target).Strength, Is.EqualTo(1));
            entities.DeleteEntity(target);
        });
        await pair.CleanReturnAsync();
    }

    private static void ApplyAll(ChemicalPropertyStatusSystem status, EntityUid target)
    {
        status.ApplyNerveStimulation(target, 2);
        status.ApplyMuscleStimulation(target, 2);
        status.ApplyCardiacPacing(target, 2);
        status.ApplyHyperdensity(target);
        status.ApplyNeuroshield(target);
        status.ApplyNeurocryogenic(target);
        status.ApplyAntiparasitic(target, 2, 1);
        status.ApplyFluxing(target, 1);
        status.ApplyPainSensitivity(target, 2);
        status.ApplyAddictionTreatment(target, 2, 1);
    }

    private static void AssertAll(IEntityManager entities, EntityUid target, bool present)
    {
        Assert.Multiple(() =>
        {
            Assert.That(entities.HasComponent<ChemicalNerveStimulationComponent>(target), Is.EqualTo(present));
            Assert.That(entities.HasComponent<ChemicalMuscleStimulationComponent>(target), Is.EqualTo(present));
            Assert.That(entities.HasComponent<ChemicalCardiacPacingComponent>(target), Is.EqualTo(present));
            Assert.That(entities.HasComponent<ChemicalHyperdensityComponent>(target), Is.EqualTo(present));
            Assert.That(entities.HasComponent<ChemicalNeuroshieldComponent>(target), Is.EqualTo(present));
            Assert.That(entities.HasComponent<ChemicalNeurocryogenicComponent>(target), Is.EqualTo(present));
            Assert.That(entities.HasComponent<ChemicalAntiparasiticComponent>(target), Is.EqualTo(present));
            Assert.That(entities.HasComponent<ChemicalFluxingComponent>(target), Is.EqualTo(present));
            Assert.That(entities.HasComponent<ChemicalPainSensitivityComponent>(target), Is.EqualTo(present));
            Assert.That(entities.HasComponent<ChemicalAddictionTreatmentComponent>(target), Is.EqualTo(present));
        });
    }
}
