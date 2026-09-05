#pragma warning disable RA0002 // Fixtures inspect committed medical ledgers and configure an unfinished procedure.
using Content.Server.CMU14.Hospital;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Hospital;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Eyes;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Liver;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Shrapnel;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared.CMU14.Medical.Treatment.Surgery.Traits;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Medical.Hospital;

[TestFixture]
public sealed class HospitalBillingConditionTest
{
    [TestCase(1)]
    [TestCase(8)]
    public async Task WoundRowsAggregateAndLocalHpAreOneRegionalCondition(int hits)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var patient = CreatePatient(entities);
            try
            {
                var arm = Part(entities, patient, BodyPartType.Arm, BodyPartSymmetry.Left);
                for (var i = 0; i < hits; i++)
                    Hit(entities, patient, arm, new() { DamageDict = { ["Slash"] = 5, ["Piercing"] = 1 } });
                var health = entities.GetComponent<BodyPartHealthComponent>(arm);
                Assert.That(health.Current, Is.LessThan(health.Max));
                Assert.That(health.BodyDamage.GetTotal(), Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(entities.System<CMUWoundLedgerSystem>().GetEntries(entities.GetComponent<BodyPartWoundComponent>(arm)), Is.Not.Empty);
                AssertDebt(entities, patient, 1);

                entities.System<SharedCMUWoundsSystem>().ClearAllWounds(arm);
                AssertDebt(entities, patient, 1, "Removing a wound projection cannot multiply or erase outstanding regional debt.");
                entities.System<SharedBodyPartHealthSystem>().HealPartDamage(patient, arm, "Brute", 100);
                Assert.That(entities.System<HospitalEmergencySystem>().AssessDischarge(patient).Cleared, Is.True);
            }
            finally
            {
                entities.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OrdinaryDressingSharesItsWoundButActualContaminationIsIndependent()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var patient = CreatePatient(entities);
            try
            {
                var arm = Part(entities, patient, BodyPartType.Arm, BodyPartSymmetry.Left);
                Hit(entities, patient, arm, new() { DamageDict = { ["Slash"] = 8 } });
                var entries = entities.System<CMUWoundLedgerSystem>()
                    .GetEntries(entities.GetComponent<BodyPartWoundComponent>(arm));
                Assert.That(entries.Any(entry => (entry.Cleanup & WoundCleanupFlags.DirtyDressing) != 0), Is.True);
                AssertDebt(entities, patient, 1, "Routine dressing does not add a second charge to its wound.");
                var traits = entities.System<SharedCMUSurgicalTraitSystem>();
                traits.EnsureTrait(arm, CMUSurgicalTrait.ContaminatedWound);
                AssertDebt(entities, patient, 2, "An actual contamination complication needs independent treatment.");
                traits.RemoveTrait(arm, CMUSurgicalTrait.ContaminatedWound);
                AssertDebt(entities, patient, 1);
            }
            finally
            {
                entities.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MixedGroupsSeparateSitesAndResidualSystemicDamageRemainAdditive()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var patient = CreatePatient(entities);
            try
            {
                var left = Part(entities, patient, BodyPartType.Arm, BodyPartSymmetry.Left);
                var right = Part(entities, patient, BodyPartType.Arm, BodyPartSymmetry.Right);
                Hit(entities, patient, left, new() { DamageDict = { ["Slash"] = 8, ["Blunt"] = 4, ["Heat"] = 8 } });
                AssertDebt(entities, patient, 2, "Brute types share a condition; the same site's burn is independent.");
                Hit(entities, patient, right, new() { DamageDict = { ["Slash"] = 8 } });
                AssertDebt(entities, patient, 3);
                var damage = entities.System<DamageableSystem>();
                damage.ApplyBodyDamageProjection(patient, new() { DamageDict = { ["Slash"] = 3 } });
                AssertDebt(entities, patient, 4, "Only exact attributed damage is a duplicate; same-group unlocalized residual is still debt.");
                damage.TryChangeDamage(patient, new DamageSpecifier { DamageDict = { ["Poison"] = 5, ["Asphyxiation"] = 7 } }, ignoreResistances: true);
                AssertDebt(entities, patient, 6, "Regional attribution cannot spend unrelated systemic debt.");
                damage.TryChangeDamage(patient, new DamageSpecifier { DamageDict = { ["Poison"] = -5, ["Asphyxiation"] = -7 } }, ignoreResistances: true);
                AssertDebt(entities, patient, 4);
            }
            finally
            {
                entities.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AnatomyOnlyTraumaRemainsOneConditionAfterItsWoundRowsAreCleared()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var patient = CreatePatient(entities);
            try
            {
                var arm = Part(entities, patient, BodyPartType.Arm, BodyPartSymmetry.Left);
                var parts = entities.System<SharedBodyPartHealthSystem>();
                Assert.That(parts.TryApplyPartDamage(patient, arm,
                    new() { DamageDict = { ["Slash"] = 9 } }, impact: DamageImpact.SnaggingContact), Is.True);
                Assert.That(entities.System<DamageableSystem>().GetTotalDamage(patient), Is.EqualTo(FixedPoint2.Zero));
                AssertDebt(entities, patient, 1);
                entities.System<SharedCMUWoundsSystem>().ClearAllWounds(arm);
                AssertDebt(entities, patient, 1, "Unattributed structural injury remains clinically and financially visible.");
                parts.SetCurrent(arm, entities.GetComponent<BodyPartHealthComponent>(arm).Max);
                Assert.That(entities.System<HospitalEmergencySystem>().AssessDischarge(patient).Cleared, Is.True);
            }
            finally
            {
                entities.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(FractureSeverity.Hairline)]
    [TestCase(FractureSeverity.Compound)]
    public async Task BoneIntegrityFractureAndDerivedBleedingShareOneCondition(FractureSeverity severity)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var patient = CreatePatient(entities);
            try
            {
                var arm = Part(entities, patient, BodyPartType.Arm, BodyPartSymmetry.Left);
                var bones = entities.System<SharedBoneSystem>();
                Assert.That(bones.SeedFracture(arm, severity), Is.True);
                // Trauma may also generate contamination; resolve that independent
                // complication through its owner before isolating the fracture.
                entities.System<SharedCMUSurgicalTraitSystem>().RemoveTrait(arm, CMUSurgicalTrait.ContaminatedWound);
                AssertDebt(entities, patient, 1);
                var wounds = entities.System<SharedCMUWoundsSystem>();
                wounds.SeedInternalBleed(arm, "hospital shuttle internal trauma", 5);
                AssertDebt(entities, patient, 2, "Independent vascular injury is not explained away by a coexisting fracture.");
                wounds.ClearInternalBleed(arm);
                AssertDebt(entities, patient, 1);
                entities.System<SharedFractureSystem>().SetSeverity((arm, entities.GetComponent<FractureComponent>(arm)), FractureSeverity.None);
                AssertDebt(entities, patient, 1, "Clearing the marker leaves the same structural bone injury.");
                bones.RestoreIntegrity(arm, entities.GetComponent<BoneComponent>(arm).IntegrityMax);
                Assert.That(entities.System<HospitalEmergencySystem>().AssessDischarge(patient).Cleared, Is.True);
            }
            finally
            {
                entities.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShrapnelTraitAndCleanupRowAreOneForeignBodyCondition()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var patient = CreatePatient(entities);
            try
            {
                var arm = Part(entities, patient, BodyPartType.Arm, BodyPartSymmetry.Left);
                var shrapnel = entities.System<SharedCMUShrapnelSystem>();
                Assert.That(shrapnel.AddShrapnel(arm, 3, 4), Is.True);
                Assert.That(entities.HasComponent<CMUEmbeddedForeignBodyComponent>(arm), Is.True);
                Assert.That(entities.System<CMUWoundLedgerSystem>().GetEntries(entities.GetComponent<BodyPartWoundComponent>(arm)), Is.Not.Empty);
                AssertDebt(entities, patient, 1);
                Hit(entities, patient, arm, new() { DamageDict = { ["Slash"] = 8 } });
                AssertDebt(entities, patient, 2, "The laceration and retained object need independent treatment.");
                Assert.That(shrapnel.TryClearShrapnel(arm), Is.True);
                AssertDebt(entities, patient, 1);
            }
            finally
            {
                entities.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MissingAdmissionSubtreesCountOnceAndDifferentAmputationsRemainAdditive()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var patient = CreatePatient(entities, entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords));
            var carriers = new List<EntityUid>();
            try
            {
                var hospital = entities.System<HospitalEmergencySystem>();
                var admission = entities.GetComponent<HospitalPatientComponent>(patient);
                Assert.That(admission.AdmissionParents[new(BodyPartType.Hand, BodyPartSymmetry.Left)],
                    Is.EqualTo(new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left)));
                foreach (var side in new[] { BodyPartSymmetry.Left, BodyPartSymmetry.Right })
                {
                    var arm = Part(entities, patient, BodyPartType.Arm, side);
                    var carrier = entities.System<DetachableOrganSystem>().Detach(arm);
                    Assert.That(carrier, Is.Not.Null);
                    carriers.Add(carrier!.Value);
                    Assert.That(entities.System<CMUMedicalBodyIndexSystem>()
                        .TryGetBodyPart(patient, new(BodyPartType.Hand, side), out _), Is.False);
                    var assessment = hospital.AssessDischarge(patient);
                    Assert.That(assessment.MissedInjuries, Is.EqualTo(carriers.Count));
                    Assert.That(assessment.MissingAnatomy, Is.True);
                    Assert.That(assessment.EligibleForReward, Is.False);
                    hospital.CaptureAdmissionAnatomy((patient, admission));
                    Assert.That(hospital.AssessDischarge(patient).MissedInjuries, Is.EqualTo(carriers.Count));
                    var surgery = entities.EnsureComponent<CMUSurgeryInProgressComponent>(patient);
                    surgery.Part = Part(entities, patient, BodyPartType.Torso, BodyPartSymmetry.None);
                    surgery.TargetPartType = BodyPartType.Arm;
                    surgery.TargetSymmetry = side;
                    Assert.That(hospital.AssessDischarge(patient).MissedInjuries, Is.EqualTo(carriers.Count),
                        "Preparing the missing limb's socket is not an additional injury fee.");
                    Assert.That(hospital.AssessDischarge(patient).TreatmentPending, Is.True);
                    entities.RemoveComponent<CMUSurgeryInProgressComponent>(patient);
                }
            }
            finally
            {
                entities.DeleteEntity(patient);
                foreach (var carrier in carriers)
                    entities.DeleteEntity(carrier);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OpenTreatmentDoesNotMultiplyItsInjuryButStillBlocksClearanceAfterRepair()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var patient = CreatePatient(entities);
            try
            {
                var arm = Part(entities, patient, BodyPartType.Arm, BodyPartSymmetry.Left);
                Hit(entities, patient, arm, new() { DamageDict = { ["Slash"] = 8 } });
                entities.EnsureComponent<CMIncisionOpenComponent>(arm);
                entities.EnsureComponent<CMUSurgeryInProgressComponent>(patient).Part = arm;
                AssertDebt(entities, patient, 1);
                entities.System<SharedCMUWoundsSystem>().ClearAllWounds(arm);
                entities.System<SharedBodyPartHealthSystem>().HealPartDamage(patient, arm, "Brute", 100);
                AssertDebt(entities, patient, 1, "The clean but unclosed treatment site still requires closure.");
                entities.RemoveComponent<CMIncisionOpenComponent>(arm);
                entities.RemoveComponent<CMUSurgeryInProgressComponent>(patient);
                Assert.That(entities.System<HospitalEmergencySystem>().AssessDischarge(patient).Cleared, Is.True);
                entities.EnsureComponent<CMUSurgeryInProgressComponent>(patient).Part = EntityUid.Invalid;
                var stale = entities.System<HospitalEmergencySystem>().AssessDischarge(patient);
                Assert.That(stale.TreatmentPending, Is.True);
                Assert.That(stale.Cleared, Is.False, "An invalid procedure anchor must never silently clear discharge.");
            }
            finally
            {
                entities.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HemostasisDoesNotIntroduceANewFeeForBloodVolumeThatWasAlreadyLow()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var patient = CreatePatient(entities);
            try
            {
                var arm = Part(entities, patient, BodyPartType.Arm, BodyPartSymmetry.Left);
                Hit(entities, patient, arm, new() { DamageDict = { ["Slash"] = 8 } });
                var blood = entities.System<BloodstreamSystem>();
                blood.TryRegulateBloodLevel(patient, 10000, referenceFactor: 0.3f);
                AssertDebt(entities, patient, 2);
                var wounds = entities.System<SharedCMUWoundsSystem>();
                Assert.That(wounds.TryTreatWound(arm, out _), Is.True);
                AssertDebt(entities, patient, 2, "Successful wound closure cannot reveal a previously hidden transfusion fee.");
                wounds.ClearAllWounds(arm);
                entities.System<SharedBodyPartHealthSystem>().HealPartDamage(patient, arm, "Brute", 100);
                AssertDebt(entities, patient, 1);
                blood.TryRegulateBloodLevel(patient, 10000, referenceFactor: 1);
                Assert.That(entities.System<HospitalEmergencySystem>().AssessDischarge(patient).Cleared, Is.True);
            }
            finally
            {
                entities.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InjuredOrganAndItsDerivedBleedingAreOneOrganCondition()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var patient = CreatePatient(entities);
            try
            {
                Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<EyesComponent>(patient, out var eyes), Is.True);
                var health = entities.GetComponent<OrganHealthComponent>(eyes);
                var injury = new OrganDamagedEvent(patient, eyes,
                    new() { DamageDict = { ["Blunt"] = health.Current - health.StageThresholds[OrganDamageStage.Dead] } },
                    OrganDamageSource.Direct);
                entities.EventBus.RaiseLocalEvent(eyes, ref injury, broadcast: true);
                Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrganPart(eyes, out var head), Is.True);
                Assert.That(entities.HasComponent<InternalBleedingComponent>(head), Is.True);
                AssertDebt(entities, patient, 1);
                entities.System<SharedOrganHealthSystem>().HealOrgan(eyes, patient, 100);
                Assert.That(entities.System<HospitalEmergencySystem>().AssessDischarge(patient).Cleared, Is.True);
            }
            finally
            {
                entities.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task IncompatibleAndDamagedReplacementIsOneUnresolvedSlot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var coordinates = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
            var patient = CreatePatient(entities, coordinates);
            EntityUid original = default;
            try
            {
                var index = entities.System<CMUMedicalBodyIndexSystem>();
                Assert.That(index.TryGetOrgan<LiverComponent>(patient, out original), Is.True);
                Assert.That(index.TryGetOrganPart(original, out var part), Is.True);
                var slot = index.GetOrganSlots(part).Single(entry => entry.Organ == original).SlotId;
                var body = entities.System<SharedBodySystem>();
                Assert.That(body.RemoveOrgan(original), Is.True);
                var donor = entities.SpawnEntity("OrganHumanLiver", coordinates);
                var health = entities.EnsureComponent<OrganHealthComponent>(donor);
                Assert.That(body.InsertOrgan(part, donor, slot), Is.True);
                var injury = new OrganDamagedEvent(patient, donor,
                    new() { DamageDict = { ["Blunt"] = health.Current - health.StageThresholds[OrganDamageStage.Bruised] } },
                    OrganDamageSource.Direct);
                entities.EventBus.RaiseLocalEvent(donor, ref injury, broadcast: true);
                AssertDebt(entities, patient, 1);
                Assert.That(entities.System<HospitalEmergencySystem>().AssessDischarge(patient).IncompatibleOrgan, Is.True);
                entities.System<SharedOrganHealthSystem>().HealOrgan(donor, patient, 100);
                AssertDebt(entities, patient, 1, "Tissue healing cannot satisfy the absent liver capability.");
            }
            finally
            {
                entities.DeleteEntity(patient);
                if (entities.EntityExists(original))
                    entities.DeleteEntity(original);
            }
        });
        await pair.CleanReturnAsync();
    }

    private static EntityUid CreatePatient(IEntityManager entities, MapCoordinates? coordinates = null)
    {
        entities.System<HospitalBillingTargetSystem>();
        var patient = entities.SpawnEntity("CMMobHuman", coordinates ?? MapCoordinates.Nullspace);
        entities.AddComponent<HospitalBillingTargetComponent>(patient);
        var admission = entities.EnsureComponent<HospitalPatientComponent>(patient);
        entities.System<HospitalEmergencySystem>().CaptureAdmissionAnatomy((patient, admission));
        Assert.That(entities.System<HospitalEmergencySystem>().AssessDischarge(patient).Cleared, Is.True);
        return patient;
    }

    private static EntityUid Part(IEntityManager entities, EntityUid patient, BodyPartType type, BodyPartSymmetry symmetry)
    {
        Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(patient, new(type, symmetry), out var part), Is.True);
        return part;
    }

    private static void Hit(IEntityManager entities, EntityUid patient, EntityUid part, DamageSpecifier damage)
    {
        entities.GetComponent<HospitalBillingTargetComponent>(patient).Part = part;
        Assert.That(entities.System<DamageableSystem>().TryChangeDamage(patient, damage,
            ignoreResistances: true, impact: DamageImpact.SnaggingContact), Is.Not.Null);
    }

    private static void AssertDebt(IEntityManager entities, EntityUid patient, int conditions, string? message = null)
    {
        var assessment = entities.System<HospitalEmergencySystem>().AssessDischarge(patient);
        Assert.That(assessment.MissedInjuries, Is.EqualTo(conditions), message);
        Assert.That(assessment.Cleared, Is.False);
    }
}

[RegisterComponent]
public sealed partial class HospitalBillingTargetComponent : Component
{
    public EntityUid Part;
}

public sealed partial class HospitalBillingTargetSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HospitalBillingTargetComponent, HitLocationResolveEvent>(OnResolve);
    }

    private void OnResolve(Entity<HospitalBillingTargetComponent> ent, ref HitLocationResolveEvent args)
    {
        args.ResolvedPartEntity = ent.Comp.Part;
        args.ResolvedPart = Comp<BodyPartComponent>(ent.Comp.Part).PartType;
        args.Handled = true;
    }
}
