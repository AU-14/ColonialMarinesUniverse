#pragma warning disable RA0002 // Inspect committed medical state in public-interaction regressions.
using System.Collections.Generic;
using Content.Server.CMU14.Medical.Diagnostics;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Lungs.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Pain.Penalties;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Medical.Anatomy;

[TestFixture]
public sealed class LungCapacityConsistencyTest
{
    [Test]
    public async Task DonorChangesReconcileCapacityWithoutHidingLocalLungInjury()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            var body = entities.System<SharedBodySystem>();
            var status = entities.System<StatusEffectsSystem>();
            Assert.That(index.TryGetOrgan<LungsComponent>(patient, out var original), Is.True);
            Assert.That(index.TryGetOrganPart(original, out var torso), Is.True);
            var (donor, site) = AddSecondLung(entities, patient);
            try
            {
                DamageToStage(entities, patient, donor, OrganDamageStage.Failing);
                AssertCapacity(entities, patient, 1f, OrganDamageStage.Healthy, false);
                Assert.That(entities.HasComponent<InternalBleedingComponent>(site), Is.True,
                    "A healthy respiratory reserve must not hide local bleeding from the failed donor.");
                Assert.That(entities.System<SharedCMUMedicalSpeedSystem>().ComputeMovementMultiplier(patient), Is.EqualTo(1f));
                Assert.That(entities.System<CMUStethoscopeSystem>().ReadStethoscope(patient, patient),
                    Does.Contain("clear"));

                Assert.That(body.RemoveOrgan(donor), Is.True);
                AssertCapacity(entities, patient, 1f, OrganDamageStage.Healthy, false);
                Assert.That(entities.HasComponent<InternalBleedingComponent>(site), Is.False);
                Assert.That(body.InsertOrgan(site, donor, "lungs"), Is.True);
                Assert.That(body.RemoveOrgan(original), Is.True);
                AssertCapacity(entities, patient, 0.3f, OrganDamageStage.Failing, true);
                Assert.That(entities.System<SharedCMUMedicalSpeedSystem>().ComputeMovementMultiplier(patient), Is.EqualTo(0.85f));
                Assert.That(entities.System<CMUStethoscopeSystem>().ReadStethoscope(patient, patient),
                    Does.Contain("faint"));

                entities.System<SharedOrganHealthSystem>().HealOrgan(donor, patient, 100);
                AssertCapacity(entities, patient, 1f, OrganDamageStage.Healthy, false);
                DamageToStage(entities, patient, donor, OrganDamageStage.Damaged);
                AssertCapacity(entities, patient, 0.6f, OrganDamageStage.Damaged, true);
                Assert.That(status.TryGetStatusEffect(patient, "StatusEffectCMUPulmonaryEdema", out var oldEdema), Is.True);

                // Two committed changes in one tick must retire the old status source
                // and leave a fresh one, instead of renewing an entity queued for deletion.
                Assert.That(body.InsertOrgan(torso, original, "lungs"), Is.True);
                AssertCapacity(entities, patient, 1f, OrganDamageStage.Healthy, false);
                Assert.That(body.RemoveOrgan(original), Is.True);
                AssertCapacity(entities, patient, 0.6f, OrganDamageStage.Damaged, true);
                Assert.That(status.TryGetStatusEffect(patient, "StatusEffectCMUPulmonaryEdema", out var replacement), Is.True);
                Assert.That(replacement, Is.Not.EqualTo(oldEdema));

                Assert.That(body.RemoveOrgan(donor), Is.True);
                AssertMissing(entities, patient);
                Assert.That(body.InsertOrgan(site, donor, "lungs"), Is.True);
                AssertCapacity(entities, patient, 0.6f, OrganDamageStage.Damaged, true);
            }
            finally
            {
                entities.DeleteEntity(patient);
                if (entities.EntityExists(original))
                    entities.DeleteEntity(original);
                if (entities.EntityExists(donor))
                    entities.DeleteEntity(donor);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SameTickHealthyDonorReplacementKeepsTheNewEdemaSourceAfterDeletionFlush()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        EntityUid donor = default;
        EntityUid? expectedEdema = null;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<LungsComponent>(patient, out var first), Is.True);
            DamageToStage(entities, patient, first, OrganDamageStage.Damaged);
            var status = entities.System<StatusEffectsSystem>();
            Assert.That(status.TryGetStatusEffect(patient, "StatusEffectCMUPulmonaryEdema", out var previous), Is.True);
            donor = AddSecondLung(entities, patient).Organ;
            AssertCapacity(entities, patient, 1f, OrganDamageStage.Healthy, false);
            Assert.That(entities.System<SharedBodySystem>().RemoveOrgan(donor), Is.True);
            AssertCapacity(entities, patient, 0.6f, OrganDamageStage.Damaged, true);
            Assert.That(status.TryGetStatusEffect(patient, "StatusEffectCMUPulmonaryEdema", out expectedEdema), Is.True);
            Assert.That(expectedEdema, Is.Not.EqualTo(previous));
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            AssertCapacity(entities, patient, 0.6f, OrganDamageStage.Damaged, true);
            Assert.That(entities.System<StatusEffectsSystem>().TryGetStatusEffect(patient,
                "StatusEffectCMUPulmonaryEdema", out var actual), Is.True);
            Assert.That(actual, Is.EqualTo(expectedEdema));
            entities.DeleteEntity(patient);
            entities.DeleteEntity(donor);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MultipleFailingLungsApplyOneBodyRateAndHealthyReserveAppliesNone()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid single = default;
        EntityUid multiple = default;
        EntityUid reserve = default;
        EntityUid missing = default;
        EntityUid removedLung = default;
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<CMULungDamageProbeSystem>();
            single = SpawnMeasuredPatient(entities);
            multiple = SpawnMeasuredPatient(entities);
            reserve = SpawnMeasuredPatient(entities);
            missing = SpawnMeasuredPatient(entities);
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            Assert.That(index.TryGetOrgan<LungsComponent>(single, out var singleLung), Is.True);
            Assert.That(index.TryGetOrgan<LungsComponent>(multiple, out var multipleLung), Is.True);
            Assert.That(index.TryGetOrgan<LungsComponent>(missing, out removedLung), Is.True);
            Assert.That(entities.System<SharedBodySystem>().RemoveOrgan(removedLung), Is.True);
            DamageToStage(entities, single, singleLung, OrganDamageStage.Failing);
            DamageToStage(entities, multiple, multipleLung, OrganDamageStage.Failing);
            DamageToStage(entities, multiple, AddSecondLung(entities, multiple).Organ, OrganDamageStage.Failing);
            DamageToStage(entities, reserve, AddSecondLung(entities, reserve).Organ, OrganDamageStage.Failing);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(3.3f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var one = entities.GetComponent<CMULungDamageProbeComponent>(single).Applied;
            var two = entities.GetComponent<CMULungDamageProbeComponent>(multiple).Applied;
            Assert.That(one, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(one, Is.All.EqualTo(FixedPoint2.New(2)), "Preserve the single failing lung's actual damage rate.");
            Assert.That(two, Is.EqualTo(one), "Additional failed lungs must not multiply functional asphyxiation.");
            Assert.That(entities.GetComponent<CMULungDamageProbeComponent>(reserve).Applied, Is.Empty,
                "A healthy attached lung supplies the body's respiratory capacity.");
            var absent = entities.GetComponent<CMULungDamageProbeComponent>(missing).Applied;
            Assert.That(absent, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(absent, Is.All.EqualTo(FixedPoint2.New(5)), "Missing-lung pressure keeps its existing body rate.");
            entities.DeleteEntity(single);
            entities.DeleteEntity(multiple);
            entities.DeleteEntity(reserve);
            entities.DeleteEntity(missing);
            entities.DeleteEntity(removedLung);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task MultipleLungFailureFreezesDuringStasisOrPatientPause(bool pause)
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<CMULungDamageProbeSystem>();
            patient = SpawnMeasuredPatient(entities);
            Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<LungsComponent>(patient, out var first), Is.True);
            DamageToStage(entities, patient, first, OrganDamageStage.Damaged);
            DamageToStage(entities, patient, AddSecondLung(entities, patient).Organ, OrganDamageStage.Damaged);
            if (pause)
                entities.System<MetaDataSystem>().SetEntityPaused(patient, true);
            else
                entities.EnsureComponent<CMInStasisComponent>(patient);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(3.3f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.GetComponent<CMULungDamageProbeComponent>(patient).Applied, Is.Empty);
            if (pause)
                entities.System<MetaDataSystem>().SetEntityPaused(patient, false);
            else
                entities.RemoveComponent<CMInStasisComponent>(patient);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(3.3f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var applied = entities.GetComponent<CMULungDamageProbeComponent>(patient).Applied;
            Assert.That(applied, Has.Count.InRange(2, 4));
            Assert.That(applied, Is.All.EqualTo(FixedPoint2.New(0.5)), "Resume one body rate without charging frozen time.");
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NativeLungRecoveryUpdatesBestCapacityWhileOtherLungRemainsInjured()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        EntityUid recovering = default;
        EntityUid injuredSite = default;
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<LungsComponent>(patient, out recovering), Is.True);
            DamageToStage(entities, patient, recovering, OrganDamageStage.Bruised);
            var second = AddSecondLung(entities, patient);
            injuredSite = second.Site;
            DamageToStage(entities, patient, second.Organ, OrganDamageStage.Failing);
            AssertCapacity(entities, patient, 0.85f, OrganDamageStage.Bruised, false);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(11));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.GetComponent<OrganHealthComponent>(recovering).Stage, Is.EqualTo(OrganDamageStage.Healthy));
            AssertCapacity(entities, patient, 1f, OrganDamageStage.Healthy, false);
            Assert.That(entities.HasComponent<InternalBleedingComponent>(injuredSite), Is.True);
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeletingAttachedLungsReconcilesTheSurvivingPatientAndReplicatedAnatomy()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var player = pair.Player!;
        var originalPlayer = player.AttachedEntity;
        EntityUid patient = default;
        EntityUid first = default;
        EntityUid second = default;
        NetEntity patientNet = default;
        NetEntity firstNet = default;
        NetEntity secondNet = default;
        try
        {
            await pair.Server.WaitPost(() =>
            {
                var entities = pair.Server.EntMan;
                patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                pair.Server.PlayerMan.SetAttachedEntity(player, patient);
                Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<LungsComponent>(patient, out first), Is.True);
                second = AddSecondLung(entities, patient).Organ;
                DamageToStage(entities, patient, first, OrganDamageStage.Failing);
                patientNet = entities.GetNetEntity(patient);
                firstNet = entities.GetNetEntity(first);
                secondNet = entities.GetNetEntity(second);
            });
            await pair.RunUntilSynced();
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(pair.Client.EntMan.TryGetEntity(firstNet, out _), Is.True);
                Assert.That(pair.Client.EntMan.TryGetEntity(secondNet, out _), Is.True);
                AssertProjection(pair.Client.EntMan, pair.Client.EntMan.GetEntity(patientNet), 1f, OrganDamageStage.Healthy);
            });
            await pair.Server.WaitAssertion(() =>
            {
                pair.Server.EntMan.DeleteEntity(second);
                AssertCapacity(pair.Server.EntMan, patient, 0.3f, OrganDamageStage.Failing, true);
            });
            await pair.RunUntilSynced();
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(pair.Client.EntMan.TryGetEntity(secondNet, out _), Is.False);
                AssertProjection(pair.Client.EntMan, pair.Client.EntMan.GetEntity(patientNet), 0.3f, OrganDamageStage.Failing);
            });
            await pair.Server.WaitAssertion(() =>
            {
                pair.Server.EntMan.DeleteEntity(first);
                AssertMissing(pair.Server.EntMan, patient);
            });
            await pair.RunUntilSynced();
            await pair.Client.WaitAssertion(() =>
            {
                var entities = pair.Client.EntMan;
                Assert.That(entities.TryGetEntity(firstNet, out _), Is.False);
                Assert.That(entities.System<SharedLungsSystem>().TryGetRespiratoryCapacity(entities.GetEntity(patientNet), out _), Is.False);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                pair.Server.PlayerMan.SetAttachedEntity(player, originalPlayer);
                if (pair.Server.EntMan.EntityExists(patient))
                    pair.Server.EntMan.DeleteEntity(patient);
            });
        }
        await pair.RunUntilSynced();
        await pair.CleanReturnAsync();
    }

    private static EntityUid SpawnMeasuredPatient(IEntityManager entities)
    {
        var patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        entities.AddComponent<CMULungDamageProbeComponent>(patient);
        return patient;
    }

    private static (EntityUid Organ, EntityUid Site) AddSecondLung(IEntityManager entities, EntityUid patient)
    {
        // Human anatomy has one native lungs slot. A second canonical slot on an
        // attached part exercises supported multi-organ topology without fake relations.
        var index = entities.System<CMUMedicalBodyIndexSystem>();
        var body = entities.System<SharedBodySystem>();
        Assert.That(index.TryGetBodyPart(patient,
            new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left), out var site), Is.True);
        Assert.That(body.TryCreateOrganSlot(site, "lungs", out _), Is.True);
        var donor = entities.SpawnEntity("CMUOrganHumanLungs", MapCoordinates.Nullspace);
        Assert.That(body.InsertOrgan(site, donor, "lungs"), Is.True);
        return (donor, site);
    }

    private static void DamageToStage(IEntityManager entities, EntityUid patient, EntityUid organ, OrganDamageStage stage)
    {
        var health = entities.GetComponent<OrganHealthComponent>(organ);
        var damage = health.Current - health.StageThresholds[stage];
        Assert.That(damage, Is.GreaterThan(FixedPoint2.Zero));
        var ev = new OrganDamagedEvent(patient, organ,
            new DamageSpecifier { DamageDict = { ["Blunt"] = damage } }, OrganDamageSource.Direct);
        entities.EventBus.RaiseLocalEvent(organ, ref ev, broadcast: true);
        Assert.That(health.Stage, Is.EqualTo(stage));
    }

    private static void AssertProjection(IEntityManager entities, EntityUid patient, float efficiency, OrganDamageStage stage)
    {
        Assert.That(entities.System<SharedLungsSystem>().TryGetRespiratoryCapacity(patient, out var capacity), Is.True);
        Assert.That(capacity.Efficiency, Is.EqualTo(efficiency));
        Assert.That(capacity.Stage, Is.EqualTo(stage));
        var breathing = new LungEfficiencyMultiplyEvent(patient, 1f);
        entities.EventBus.RaiseLocalEvent(patient, ref breathing);
        Assert.That(breathing.Multiplier, Is.EqualTo(efficiency));
    }

    private static void AssertCapacity(IEntityManager entities, EntityUid patient, float efficiency, OrganDamageStage stage, bool edema)
    {
        AssertProjection(entities, patient, efficiency, stage);
        Assert.That(entities.HasComponent<MissingLungsComponent>(patient), Is.False);
        Assert.That(entities.System<StatusEffectsSystem>().HasStatusEffect(patient, "StatusEffectCMUPulmonaryEdema"), Is.EqualTo(edema));
    }

    private static void AssertMissing(IEntityManager entities, EntityUid patient)
    {
        Assert.That(entities.System<SharedLungsSystem>().TryGetRespiratoryCapacity(patient, out _), Is.False);
        Assert.That(entities.HasComponent<MissingLungsComponent>(patient), Is.True);
        Assert.That(entities.System<StatusEffectsSystem>().HasStatusEffect(patient, "StatusEffectCMUPulmonaryEdema"), Is.True);
        var breathing = new LungEfficiencyMultiplyEvent(patient, 1f);
        entities.EventBus.RaiseLocalEvent(patient, ref breathing);
        Assert.That(breathing.Multiplier, Is.Zero);
    }
}

[RegisterComponent]
public sealed partial class CMULungDamageProbeComponent : Component
{
    public readonly List<FixedPoint2> Applied = new();
}

public sealed partial class CMULungDamageProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMULungDamageProbeComponent, DamageDealtEvent>(OnDamage,
            after: new[] { typeof(DamageableSystem) });
    }

    private void OnDamage(Entity<CMULungDamageProbeComponent> ent, ref DamageDealtEvent args)
    {
        if (args.Origin is not { } origin ||
            !(HasComp<LungsComponent>(origin) || origin == ent.Owner && HasComp<MissingLungsComponent>(origin)) ||
            args.AppliedDamage is not { } applied ||
            !applied.DamageDict.TryGetValue("Asphyxiation", out var amount) || amount <= FixedPoint2.Zero)
            return;
        ent.Comp.Applied.Add(amount);
    }
}
