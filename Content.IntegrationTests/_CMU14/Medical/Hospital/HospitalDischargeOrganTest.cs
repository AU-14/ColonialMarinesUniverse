using Content.Server.CMU14.Hospital;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Hospital;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Liver;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.IntegrationTests.CMU14.Medical.Hospital;

[TestFixture]
public sealed class HospitalDischargeOrganTest
{
    [Test]
    public async Task RestoringHeartHealthDoesNotClearDischargeUntilCirculationRestarts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var hospital = entities.System<HospitalEmergencySystem>();
            var patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            try
            {
                var admission = entities.EnsureComponent<HospitalPatientComponent>(patient);
                hospital.CaptureAdmissionAnatomy((patient, admission));
                Assert.That(hospital.AssessDischarge(patient).Cleared, Is.True);
                Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<HeartComponent>(patient, out var heart), Is.True);

                // Repairing heart tissue does not restart a stopped heart. Exercise
                // those two separate owner operations before any collapse-time tick.
                var injury = new OrganDamagedEvent(patient, heart,
                    new DamageSpecifier { DamageDict = { ["Blunt"] = 100 } }, OrganDamageSource.Direct);
                entities.EventBus.RaiseLocalEvent(heart, ref injury, broadcast: true);
                entities.System<SharedOrganHealthSystem>().HealOrgan(heart, patient, 100);
                var health = entities.GetComponent<OrganHealthComponent>(heart);
                var heartbeat = entities.GetComponent<HeartComponent>(heart);
                Assert.That(health.Current, Is.EqualTo(health.Max));
                Assert.That(health.Stage, Is.EqualTo(OrganDamageStage.Healthy));
                Assert.That(heartbeat.Stopped, Is.True);
                Assert.That(entities.GetComponent<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Alive));
                var stopped = hospital.AssessDischarge(patient);
                Assert.That(stopped.Cleared, Is.False);
                Assert.That(stopped.MissingAnatomy, Is.False);
                Assert.That(stopped.IncompatibleOrgan, Is.False);
                Assert.That(stopped.MissedInjuries, Is.EqualTo(1), "The stopped heart is one unresolved organ condition.");

                entities.System<SharedHeartSystem>().TryRestartHeart(heart);
                Assert.That(heartbeat.Stopped, Is.False);
                Assert.That(hospital.AssessDischarge(patient).Cleared, Is.True);
            }
            finally
            {
                entities.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SameCategoryReplacementMustSupplyTheAdmissionOrganCapabilities(bool hasHealth)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var hospital = entities.System<HospitalEmergencySystem>();
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            var body = entities.System<SharedBodySystem>();
            var patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var detached = new List<EntityUid>();
            try
            {
                var admission = entities.EnsureComponent<HospitalPatientComponent>(patient);
                hospital.CaptureAdmissionAnatomy((patient, admission));
                Assert.That(index.TryGetOrgan<LiverComponent>(patient, out var original), Is.True);
                Assert.That(index.TryGetOrganPart(original, out var part), Is.True);
                var slot = index.GetOrganSlots(part).Single(candidate => candidate.Organ == original).SlotId;
                var category = entities.GetComponent<OrganComponent>(original).Category;
                Assert.That(body.RemoveOrgan(original), Is.True);
                detached.Add(original);

                var donor = entities.SpawnEntity("OrganHumanLiver", map.GridCoords);
                detached.Add(donor);
                Assert.That(entities.GetComponent<OrganComponent>(donor).Category, Is.EqualTo(category));
                Assert.That(entities.HasComponent<LiverComponent>(donor), Is.False);
                Assert.That(entities.HasComponent<OrganHealthComponent>(donor), Is.False);
                if (hasHealth)
                    entities.EnsureComponent<OrganHealthComponent>(donor);
                Assert.That(body.InsertOrgan(part, donor, slot), Is.True,
                    "The ordinary anatomy contract allows this same-category donor; discharge must check its function.");
                Assert.That(index.TryGetOrganInSlot(part, slot, out var occupying), Is.True);
                Assert.That(occupying, Is.EqualTo(donor));

                var incompatible = hospital.AssessDischarge(patient);
                Assert.That(incompatible.MissingAnatomy, Is.False);
                Assert.That(incompatible.IncompatibleOrgan, Is.True);
                Assert.That(incompatible.EligibleForReward, Is.False);
                Assert.That(incompatible.Cleared, Is.False);
                hospital.CaptureAdmissionAnatomy((patient, admission));
                Assert.That(hospital.AssessDischarge(patient).IncompatibleOrgan, Is.True,
                    "Reassessment cannot replace the admission requirements with the incompatible donor.");

                Assert.That(body.RemoveOrgan(donor), Is.True);
                var replacement = entities.SpawnEntity("CMUOrganHumanLiver", map.GridCoords);
                detached.Add(replacement);
                Assert.That(replacement, Is.Not.EqualTo(original));
                Assert.That(body.InsertOrgan(part, replacement, slot), Is.True);
                Assert.That(hospital.AssessDischarge(patient).Cleared, Is.True,
                    "A compatible replacement satisfies admission even though it is a different organ entity.");
            }
            finally
            {
                entities.DeleteEntity(patient);
                foreach (var organ in detached)
                {
                    if (entities.EntityExists(organ))
                        entities.DeleteEntity(organ);
                }
            }
        });
        await pair.CleanReturnAsync();
    }
}
