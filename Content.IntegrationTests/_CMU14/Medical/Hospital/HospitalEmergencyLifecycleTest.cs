using System;
using Content.Server.CMU14.Hospital;
using Content.Server.Shuttles.Events;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Hospital;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Shuttles.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Medical.Hospital;

[TestFixture]
public sealed class HospitalEmergencyLifecycleTest
{
    [TestCase(HospitalEmergencyStatus.ManualUnloading, true)]
    [TestCase(HospitalEmergencyStatus.ManualUnloading, false)]
    [TestCase(HospitalEmergencyStatus.PickupBoarding, true)]
    [TestCase(HospitalEmergencyStatus.PickupBoarding, false)]
    public async Task ReleaseMessageRetainsPatientsUntilDepartureSucceeds(HospitalEmergencyStatus status, bool cooldown)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var computer = entities.SpawnEntity("AU14HospitalEmergencyComputer", map.GridCoords);
            var patient = entities.SpawnEntity("AU14HospitalPatient", map.GridCoords);
            var destination = entities.SpawnEntity("CMDropshipDestinationThirdPartyReturn", map.GridCoords);
            var comp = entities.GetComponent<HospitalEmergencyComputerComponent>(computer);
            var patientComp = entities.EnsureComponent<HospitalPatientComponent>(patient);
            patientComp.SourceComputer = computer;
            comp.Patients.Add(patient);
            comp.ActiveShuttle = map.Grid.Owner;
            comp.ReturnDestination = destination;
            comp.Status = status;
            if (cooldown)
                entities.EnsureComponent<FTLComponent>(map.Grid.Owner);

            // Exercise the production BUI message handler. No navigation console is installed;
            // both a normal cooldown and an unavailable console must retain the transport.
            entities.EventBus.RaiseLocalEvent(computer, new HospitalEmergencyReleaseShuttleMsg());

            Assert.Multiple(() =>
            {
                Assert.That(comp.Status, Is.EqualTo(HospitalEmergencyStatus.WaitingForDeparture));
                Assert.That(comp.LastPayout, Is.Zero);
                Assert.That(comp.Patients, Does.Contain(patient));
                Assert.That(comp.ActiveShuttle, Is.EqualTo(map.Grid.Owner));
                Assert.That(entities.IsQueuedForDeletion(patient), Is.False);
                Assert.That(entities.IsQueuedForDeletion(map.Grid.Owner), Is.False);
                Assert.That(comp.TransportFailure, Is.Not.Empty);
            });

            comp.ActiveShuttle = null;
            entities.DeleteEntity(computer);
            entities.DeleteEntity(patient);
            entities.DeleteEntity(destination);
            entities.RemoveComponent<FTLComponent>(map.Grid.Owner);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnexpectedFlightCompletionDoesNotDeleteTheShuttleOrPay()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var computer = entities.SpawnEntity("AU14HospitalEmergencyComputer", map.GridCoords);
            var destination = entities.SpawnEntity("CMDropshipDestinationThirdPartyReturn", map.GridCoords);
            var otherDestination = entities.SpawnEntity("CMDropshipDestination", map.GridCoords);
            var comp = entities.GetComponent<HospitalEmergencyComputerComponent>(computer);
            comp.ActiveShuttle = map.Grid.Owner;
            comp.ReturnDestination = destination;
            comp.ExpectedDestination = destination;
            comp.ShuttlePurpose = HospitalShuttlePurpose.PickupReturning;
            comp.Status = HospitalEmergencyStatus.ShuttleDeparting;
            entities.EnsureComponent<DropshipComponent>(map.Grid.Owner);
            server.System<SharedDropshipSystem>().SetDropshipDestination(map.Grid.Owner, otherDestination);

            var completed = new FTLCompletedEvent(map.Grid.Owner, map.MapUid);
            entities.EventBus.RaiseEvent(EventSource.Local, ref completed);

            Assert.Multiple(() =>
            {
                Assert.That(comp.ActiveShuttle, Is.EqualTo(map.Grid.Owner));
                Assert.That(comp.Status, Is.EqualTo(HospitalEmergencyStatus.ShuttleDeparting));
                Assert.That(comp.LastPayout, Is.Zero);
                Assert.That(entities.IsQueuedForDeletion(map.Grid.Owner), Is.False);
            });

            comp.ActiveShuttle = null;
            entities.DeleteEntity(computer);
            entities.DeleteEntity(destination);
            entities.DeleteEntity(otherDestination);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeletedTreatmentManifestCompletesWithoutAnAdministratorReset()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var computer = entities.SpawnEntity("AU14HospitalEmergencyComputer", map.GridCoords);
            var patient = entities.SpawnEntity("AU14HospitalPatient", map.GridCoords);
            var comp = entities.GetComponent<HospitalEmergencyComputerComponent>(computer);
            comp.Patients.Add(patient);
            comp.Status = HospitalEmergencyStatus.Treating;
            entities.DeleteEntity(patient);

            server.System<HospitalEmergencySystem>().Update(0f);

            Assert.Multiple(() =>
            {
                Assert.That(comp.Status, Is.EqualTo(HospitalEmergencyStatus.RewardReady));
                Assert.That(comp.Patients, Is.Empty);
                Assert.That(comp.LastPayout, Is.Zero);
                Assert.That(comp.NextIncidentAt, Is.GreaterThan(server.ResolveDependency<IGameTiming>().CurTime));
            });
            entities.DeleteEntity(computer);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DischargeUsesAdmissionAnatomyAndUnclosedTreatmentSites()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var hospital = server.System<HospitalEmergencySystem>();
            var index = server.System<CMUMedicalBodyIndexSystem>();
            var patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            EntityUid? carrier = null;
            try
            {
                var admission = entities.EnsureComponent<HospitalPatientComponent>(patient);
                hospital.CaptureAdmissionAnatomy((patient, admission));
                Assert.That(hospital.AssessDischarge(patient).Cleared, Is.True);

                Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Arm, BodyPartSymmetry.Left), out var arm), Is.True);
                entities.EnsureComponent<CMIncisionOpenComponent>(arm);
                Assert.That(hospital.AssessDischarge(patient).Cleared, Is.False);
                entities.RemoveComponent<CMIncisionOpenComponent>(arm);

                server.System<SharedBoneSystem>().RestoreIntegrity((arm, null), 75);
                Assert.That(hospital.AssessDischarge(patient).Cleared, Is.False);
                server.System<SharedBoneSystem>().RestoreIntegrity((arm, null), 100);

                carrier = server.System<DetachableOrganSystem>().Detach(arm);
                Assert.That(carrier, Is.Not.Null);
                var afterRemoval = hospital.AssessDischarge(patient);
                Assert.Multiple(() =>
                {
                    Assert.That(afterRemoval.MissingAnatomy, Is.True);
                    Assert.That(afterRemoval.EligibleForReward, Is.False);
                    Assert.That(afterRemoval.Cleared, Is.False);
                });
                hospital.CaptureAdmissionAnatomy((patient, admission));
                Assert.That(hospital.AssessDischarge(patient).MissingAnatomy, Is.True,
                    "Reassessment must not replace the original anatomy with the amputated state.");
            }
            finally
            {
                entities.DeleteEntity(patient);
                if (carrier is { } detached && entities.EntityExists(detached))
                    entities.DeleteEntity(detached);
            }
        });

        await pair.CleanReturnAsync();
    }
}
