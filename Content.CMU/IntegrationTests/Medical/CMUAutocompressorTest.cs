// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.CPR;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.Medical.Autocompressor;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Robust.Shared.GameObjects;
using Robust.Shared.Containers;

namespace Content.IntegrationTests.CMU14.Medical;

[TestFixture]
public sealed class CMUAutocompressorTest : GameTest
{
    [Test]
    public Task FittingRequiresTrainingAndAnUnconsciousPatientWithAnEmptySlot()
        => RunCase(scene =>
        {
            var skills = Server.System<SkillsSystem>();
            var inventory = Server.System<InventorySystem>();
            var state = Server.System<MobStateSystem>();
            skills.SetSkill(scene.User, "RMCSkillMedical", 0);
            state.ChangeMobState(scene.Patient, MobState.Critical);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, scene.Patient), Is.False);
            skills.SetSkill(scene.User, "RMCSkillMedical", 2);
            state.ChangeMobState(scene.Patient, MobState.Alive);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, scene.Patient), Is.False);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, scene.User), Is.False);
            state.ChangeMobState(scene.Patient, MobState.Critical);
            var armor = scene.Spawn("CMArmorM3Medium");
            Assert.That(inventory.TryEquip(scene.Patient, armor, CMUAutocompressorSystem.PatientSlot, force: true), Is.True);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, scene.Patient), Is.False);
            Assert.That(inventory.TryUnequip(scene.Patient, CMUAutocompressorSystem.PatientSlot, force: true), Is.True);
            Assert.That(inventory.CanEquip(scene.User, scene.Patient, scene.Device,
                CMUAutocompressorSystem.PatientSlot, out _), Is.True,
                "A valid patient must accept the armor-slot fitting route.");
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, scene.Patient), Is.True);
            Assert.That(inventory.TryGetSlotEntity(scene.Patient, CMUAutocompressorSystem.PatientSlot, out var worn), Is.True);
            Assert.That(worn, Is.EqualTo(scene.Device));
            Assert.That(scene.Component.Running, Is.True);
            var attempt = new ReceiveCPRAttemptEvent(scene.User, scene.Patient, true);
            SEntMan.EventBus.RaiseLocalEvent(scene.Patient, ref attempt);
            Assert.That(attempt.Cancelled, Is.True, "Manual CPR cannot stack with a running compressor.");
            Assert.That(inventory.TryUnequip(scene.Patient, CMUAutocompressorSystem.PatientSlot, force: true), Is.True);
            Assert.That(scene.Component.Running, Is.False);
            Assert.That(scene.Component.Patient, Is.Null);
            var afterRemoval = new ReceiveCPRAttemptEvent(scene.User, scene.Patient, true);
            SEntMan.EventBus.RaiseLocalEvent(scene.Patient, ref afterRemoval);
            Assert.That(afterRemoval.Cancelled, Is.False, "Removing the device must release its CPR reservation.");
        });

    [Test]
    public Task CompressionUsesNativeCprWithoutHealingTraumaOrAllowingRepeatedPulses()
        => RunCase(scene =>
        {
            var damage = Server.System<DamageableSystem>();
            var cpr = Server.System<CPRSystem>();
            var revival = Server.System<RMCUnrevivableSystem>();
            damage.TryChangeDamage(scene.Patient, new DamageSpecifier
            {
                DamageDict = { ["Blunt"] = 200, ["Asphyxiation"] = 40 },
            }, true);
            Server.System<MobStateSystem>().ChangeMobState(scene.Patient, MobState.Dead);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, scene.Patient), Is.True);
            var damageable = SEntMan.GetComponent<DamageableComponent>(scene.Patient);
            var before = damage.GetAllDamage((scene.Patient, damageable));
            var oxygenBefore = before.DamageDict["Asphyxiation"];
            var traumaBefore = before.DamageDict["Blunt"];
            var chargeBefore = scene.Charge;
            var revivalBefore = revival.GetTimeUntilUnrevivable(scene.Patient);
            Assert.That(cpr.CMUTryMechanicalCPR(scene.Patient, scene.Device, scene.Component.ChargePerCompression), Is.True);
            var after = damage.GetAllDamage((scene.Patient, damageable));
            Assert.That(after.DamageDict["Asphyxiation"], Is.LessThan(oxygenBefore));
            Assert.That(after.DamageDict["Blunt"], Is.EqualTo(traumaBefore));
            Assert.That(scene.Charge, Is.LessThan(chargeBefore));
            Assert.That(revival.GetTimeUntilUnrevivable(scene.Patient), Is.GreaterThan(revivalBefore));
            var chargeAfter = scene.Charge;
            Assert.That(cpr.CMUTryMechanicalCPR(scene.Patient, scene.Device, scene.Component.ChargePerCompression), Is.False);
            Assert.That(scene.Charge, Is.EqualTo(chargeAfter), "A blocked repeat must not consume power or apply CPR.");
        });

    [Test]
    public Task RecoveryAndBatteryLossDetachAndAllowReuse()
        => RunCase(scene =>
        {
            var state = Server.System<MobStateSystem>();
            var hands = Server.System<SharedHandsSystem>();
            state.ChangeMobState(scene.Patient, MobState.Critical);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, scene.Patient), Is.True);
            state.ChangeMobState(scene.Patient, MobState.Alive);
            Assert.That(scene.Component.Running, Is.False);
            Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(scene.Patient,
                CMUAutocompressorSystem.PatientSlot, out _), Is.False);
            state.ChangeMobState(scene.Patient, MobState.Critical);
            Assert.That(hands.TryPickupAnyHand(scene.User, scene.Device), Is.True);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, scene.Patient), Is.True);
            scene.System.Update(0);
            Assert.That(SEntMan.HasComponent<CMUAutocompressorRunningComponent>(scene.Device), Is.True,
                "An earlier stop must not remove the update marker from a refitted device.");
            Server.System<SharedBatterySystem>().SetCharge(scene.Battery.AsNullable(), 0);
            Assert.That(scene.Component.Running, Is.False);
            Assert.That(scene.Component.Patient, Is.Null);
            Assert.That(hands.TryPickupAnyHand(scene.User, scene.Device), Is.True);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, scene.Patient), Is.False);
            Server.System<SharedBatterySystem>().SetCharge(scene.Battery.AsNullable(), 1000);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, scene.Patient), Is.True);
        });

    [Test]
    public Task PermanentDeathCannotBeReversedByCompression()
        => RunCase(scene =>
        {
            var revival = Server.System<RMCUnrevivableSystem>();
            Server.System<MobStateSystem>().ChangeMobState(scene.Patient, MobState.Dead);
            revival.MakeUnrevivable(scene.Patient);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, scene.Patient), Is.False);
            Assert.That(Server.System<CPRSystem>().CMUTryMechanicalCPR(scene.Patient,
                scene.Device, scene.Component.ChargePerCompression), Is.False);
            Assert.That(scene.Component.Running, Is.False);
        });

    [Test]
    public Task AnExpiredDeadlineCannotBeExtendedBeforeTheNativeExpiryScan()
        => RunCase(scene =>
        {
            Server.System<MobStateSystem>().ChangeMobState(scene.Patient, MobState.Dead);
            var revivable = SEntMan.GetComponent<RMCRevivableComponent>(scene.Patient);
#pragma warning disable RA0002
            revivable.UnrevivableAt = SGameTiming.CurTime - TimeSpan.FromSeconds(1);
#pragma warning restore RA0002
            var charge = scene.Charge;
            Assert.That(Server.System<CPRSystem>().CMUTryMechanicalCPR(scene.Patient,
                scene.Device, scene.Component.ChargePerCompression), Is.False);
            Assert.That(scene.Charge, Is.EqualTo(charge));
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, scene.Patient), Is.False);
        });

    [Test]
    public Task SyntheticPatientsCannotReceiveOrganicCpr()
        => RunCase(scene =>
        {
            SEntMan.EnsureComponent<SynthComponent>(scene.Patient);
            Server.System<MobStateSystem>().ChangeMobState(scene.Patient, MobState.Critical);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, scene.Patient), Is.False);
            Assert.That(Server.System<CPRSystem>().CMUTryMechanicalCPR(scene.Patient,
                scene.Device, scene.Component.ChargePerCompression), Is.False);
        });

    [Test]
    public Task DeletingTheCompressorReleasesThePatient()
        => RunCase(scene =>
        {
            Server.System<MobStateSystem>().ChangeMobState(scene.Patient, MobState.Critical);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, scene.Patient), Is.True);
            SEntMan.DeleteEntity(scene.Device);
            var attempt = new ReceiveCPRAttemptEvent(scene.User, scene.Patient, true);
            SEntMan.EventBus.RaiseLocalEvent(scene.Patient, ref attempt);
            Assert.That(attempt.Cancelled, Is.False);
        });

    [Test]
    public Task ProlongedUseFracturesOnlyTheTorsoOncePerStage()
        => RunCase(scene =>
        {
            Server.System<MobStateSystem>().ChangeMobState(scene.Patient, MobState.Critical);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, scene.Patient), Is.True);
            var index = Server.System<CMUMedicalBodyIndexSystem>();
            EntityUid? torso = null;
            foreach (var (part, anatomy) in index.GetBodyParts(scene.Patient))
            {
                if (anatomy.PartType == BodyPartType.Torso)
                    torso = part;
            }
            Assert.That(torso, Is.Not.Null);
            scene.System.Update(0);
            Assert.That(SEntMan.HasComponent<FractureComponent>(torso!.Value), Is.False,
                "Fitting must not fracture ribs immediately.");
            MakeFractureDue(scene);
            scene.System.Update(0);
            var fracture = SEntMan.GetComponent<FractureComponent>(torso.Value);
            Assert.That(fracture.Severity, Is.EqualTo(FractureSeverity.Simple));
            Assert.That(scene.Component.Running, Is.True, "Rib trauma must not end CPR support.");
            foreach (var (part, anatomy) in index.GetBodyParts(scene.Patient))
            {
                if (anatomy.PartType != BodyPartType.Torso)
                    Assert.That(SEntMan.HasComponent<FractureComponent>(part), Is.False);
            }
            Server.System<SharedFractureSystem>().SetSeverity((torso.Value, fracture), FractureSeverity.None);
            scene.System.Update(0);
            Assert.That(SEntMan.HasComponent<FractureComponent>(torso.Value), Is.False,
                "The same exposure stage must not repeatedly reapply an injury.");
        });

    [Test]
    public Task ProlongedUsePreservesAnExistingWorseTorsoFracture()
        => RunCase(scene =>
        {
            var index = Server.System<CMUMedicalBodyIndexSystem>();
            EntityUid? torso = null;
            foreach (var (part, anatomy) in index.GetBodyParts(scene.Patient))
            {
                if (anatomy.PartType == BodyPartType.Torso)
                    torso = part;
            }
            Assert.That(torso, Is.Not.Null);
            Assert.That(Server.System<SharedBoneSystem>().SeedFracture(torso!.Value, FractureSeverity.Compound), Is.True);
            Server.System<MobStateSystem>().ChangeMobState(scene.Patient, MobState.Critical);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, scene.Patient), Is.True);
            MakeFractureDue(scene);
            scene.System.Update(0);
            Assert.That(SEntMan.GetComponent<FractureComponent>(torso.Value).Severity, Is.EqualTo(FractureSeverity.Compound));
        });

    [Test]
    public Task RemovingTheDeviceBeforeTheLimitPreventsItsTimedFracture()
        => RunCase(scene =>
        {
            Server.System<MobStateSystem>().ChangeMobState(scene.Patient, MobState.Critical);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, scene.Patient), Is.True);
            scene.System.Stop(scene.DeviceEntity, true);
            MakeFractureDue(scene);
            scene.System.Update(0);
            foreach (var (part, _) in Server.System<CMUMedicalBodyIndexSystem>().GetBodyParts(scene.Patient))
                Assert.That(SEntMan.HasComponent<FractureComponent>(part), Is.False);
        });

    [Test]
    public Task ContinuousUseEscalatesFromSimpleToCompoundToShattered()
        => RunCase(scene =>
        {
            Server.System<MobStateSystem>().ChangeMobState(scene.Patient, MobState.Critical);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, scene.Patient), Is.True);
            EntityUid? torso = null;
            foreach (var (part, anatomy) in Server.System<CMUMedicalBodyIndexSystem>().GetBodyParts(scene.Patient))
            {
                if (anatomy.PartType == BodyPartType.Torso)
                    torso = part;
            }
            Assert.That(torso, Is.Not.Null);
            MakeFractureDue(scene);
            scene.System.Update(0);
            Assert.That(SEntMan.GetComponent<FractureComponent>(torso!.Value).Severity, Is.EqualTo(FractureSeverity.Simple));
            MakeFractureDue(scene, scene.Component.CompoundFractureDelay + TimeSpan.FromMilliseconds(1));
            scene.System.Update(0);
            Assert.That(SEntMan.GetComponent<FractureComponent>(torso.Value).Severity, Is.EqualTo(FractureSeverity.Compound));
            MakeFractureDue(scene, scene.Component.ShatteredFractureDelay + TimeSpan.FromMilliseconds(1));
            scene.System.Update(0);
            Assert.That(SEntMan.GetComponent<FractureComponent>(torso.Value).Severity, Is.EqualTo(FractureSeverity.Shattered));
            MakeFractureDue(scene, scene.Component.ShatteredFractureDelay + TimeSpan.FromMinutes(5));
            scene.System.Update(0);
            Assert.That(SEntMan.GetComponent<FractureComponent>(torso.Value).Severity, Is.EqualTo(FractureSeverity.Shattered));
            Assert.That(scene.Component.Running, Is.True);
        });

    [Test]
    public Task RefittingStartsAFreshExposureIntervalWithoutHealingExistingTrauma()
        => RunCase(scene =>
        {
            Server.System<MobStateSystem>().ChangeMobState(scene.Patient, MobState.Critical);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, scene.Patient), Is.True);
            MakeFractureDue(scene);
            scene.System.Update(0);
            scene.System.Stop(scene.DeviceEntity, true);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(scene.User, scene.Device), Is.True);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, scene.Patient), Is.True);
            Assert.That(scene.Component.FractureStage, Is.EqualTo(FractureSeverity.None));
            scene.System.Update(0);
            foreach (var (part, anatomy) in Server.System<CMUMedicalBodyIndexSystem>().GetBodyParts(scene.Patient))
            {
                if (anatomy.PartType == BodyPartType.Torso)
                    Assert.That(SEntMan.GetComponent<FractureComponent>(part).Severity, Is.EqualTo(FractureSeverity.Simple));
            }
        });

    [Test]
    public Task WornAnimationPrefixFollowsTreatmentState()
        => RunCase(scene =>
        {
            Server.System<MobStateSystem>().ChangeMobState(scene.Patient, MobState.Critical);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, scene.Patient), Is.True);
            var clothing = SEntMan.GetComponent<ClothingComponent>(scene.Device);
            Assert.That(clothing.EquippedPrefix, Is.EqualTo("running"));
            scene.System.Stop(scene.DeviceEntity, true);
            Assert.That(clothing.EquippedPrefix, Is.Null);
        });

    [Test]
    public Task FinalStageWithTorsoBleedingInjuresALungOnce()
        => RunCase(scene =>
        {
            var (torso, lung) = FindTorsoAndLung(scene.Patient);
            var before = lung.Current;
            Server.System<MobStateSystem>().ChangeMobState(scene.Patient, MobState.Critical);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, scene.Patient), Is.True);
            MakeFractureDue(scene, scene.Component.ShatteredFractureDelay + TimeSpan.FromMilliseconds(1));
            scene.System.Update(0);
            Server.System<SharedCMUWoundsSystem>().SeedInternalBleed(torso, "autocompressor-test", 1f);
            Assert.That(scene.Component.LungDamageApplied, Is.True);
            Assert.That(lung.Current, Is.LessThan(before));
            var after = lung.Current;
            Server.System<SharedCMUWoundsSystem>().SeedInternalBleed(torso, "autocompressor-test-repeat", 2f);
            scene.System.Update(0);
            Assert.That(lung.Current, Is.EqualTo(after), "Bleeding changes must not repeatedly damage the lung.");
        });

    [Test]
    public Task EarlierStageBleedingDoesNotApplyTheFinalLungComplication()
        => RunCase(scene =>
        {
            var (torso, lung) = FindTorsoAndLung(scene.Patient);
            var before = lung.Current;
            Server.System<MobStateSystem>().ChangeMobState(scene.Patient, MobState.Critical);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, scene.Patient), Is.True);
            MakeFractureDue(scene, scene.Component.CompoundFractureDelay + TimeSpan.FromMilliseconds(1));
            scene.System.Update(0);
            Server.System<SharedCMUWoundsSystem>().SeedInternalBleed(torso, "autocompressor-test", 1f);
            Assert.That(scene.Component.LungDamageApplied, Is.False);
            Assert.That(lung.Current, Is.EqualTo(before));
            MakeFractureDue(scene, scene.Component.ShatteredFractureDelay + TimeSpan.FromMilliseconds(1));
            scene.System.Update(0);
            Assert.That(scene.Component.LungDamageApplied, Is.True);
            Assert.That(lung.Current, Is.LessThan(before), "Existing active torso bleeding must also qualify at the final stage.");
        });

    [Test]
    public Task ArmorSlotPlacementUsesTheNativeFittingDelay()
        => RunCase(scene =>
        {
            Server.System<MobStateSystem>().ChangeMobState(scene.Patient, MobState.Critical);
            var inventory = Server.System<InventorySystem>();
            Assert.That(inventory.TryEquip(scene.User, scene.Patient, scene.Device,
                CMUAutocompressorSystem.PatientSlot, checkDoafter: true), Is.False,
                "Starting a fitting delay must not equip the device immediately.");
            Assert.That(scene.Component.Running, Is.False);
            Assert.That(inventory.TryGetSlotEntity(scene.Patient, CMUAutocompressorSystem.PatientSlot, out _), Is.False);
            Assert.That(Server.System<SharedHandsSystem>().TryGetActiveItem(scene.User, out var held), Is.True);
            Assert.That(held, Is.EqualTo(scene.Device));
        });

    [Test]
    public Task ConsciousWearerStaysOnStandbyUntilCritical()
        => RunCase(scene =>
        {
            var inventory = Server.System<InventorySystem>();
            Assert.That(inventory.TryEquip(scene.User, scene.Device, CMUAutocompressorSystem.PatientSlot), Is.True);
            Assert.That(scene.Component.Patient, Is.EqualTo(scene.User));
            Assert.That(scene.Component.Running, Is.False);
            Assert.That(SEntMan.HasComponent<CMUAutocompressorRunningComponent>(scene.Device), Is.False);
            var charge = scene.Charge;
            scene.System.Update(0);
            Assert.That(scene.Charge, Is.EqualTo(charge));
            Server.System<MobStateSystem>().ChangeMobState(scene.User, MobState.Critical);
            Assert.That(scene.Component.Running, Is.True);
            Assert.That(SEntMan.HasComponent<CMUAutocompressorRunningComponent>(scene.Device), Is.True);
            Server.System<MobStateSystem>().ChangeMobState(scene.User, MobState.Alive);
            Assert.That(scene.Component.Running, Is.False);
            Assert.That(scene.Component.Patient, Is.Null);
        });

    [Test]
    public Task RemovingAStandbyDeviceClearsItsPatientLink()
        => RunCase(scene =>
        {
            var inventory = Server.System<InventorySystem>();
            Assert.That(inventory.TryEquip(scene.User, scene.Device, CMUAutocompressorSystem.PatientSlot), Is.True);
            Assert.That(inventory.TryUnequip(scene.User, CMUAutocompressorSystem.PatientSlot), Is.True);
            Assert.That(scene.Component.Patient, Is.Null);
            Server.System<MobStateSystem>().ChangeMobState(scene.User, MobState.Critical);
            Assert.That(scene.Component.Running, Is.False);
        });

    [Test]
    public Task DeployedDummyAcceptsUntrainedPracticeAndRecordsCompressions()
        => RunCase(scene =>
        {
            var dummy = scene.Spawn("RMCCPRDummy");
            Server.System<SkillsSystem>().SetSkill(scene.User, "RMCSkillMedical", 0);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, dummy), Is.False);
            DeployDummy(dummy);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, dummy), Is.True);
            Assert.That(scene.Component.Training, Is.True);
            Assert.That(scene.Component.Patient, Is.EqualTo(dummy));
            Assert.That(scene.Component.Running, Is.True);
            var counters = SEntMan.GetComponent<CPRDummyComponent>(dummy);
            var successes = counters.CPRSuccess;
            var failures = counters.CPRFailed;
            var charge = scene.Charge;
            Assert.That(Server.System<CPRSystem>().CMUTryMechanicalCPR(dummy,
                scene.Device, scene.Component.ChargePerCompression), Is.True);
            Assert.That(counters.CPRSuccess, Is.GreaterThan(successes));
            Assert.That(counters.CPRFailed, Is.EqualTo(failures));
            Assert.That(scene.Charge, Is.LessThan(charge));
            scene.System.Stop(scene.DeviceEntity, true);
            Assert.That(scene.Component.Patient, Is.Null);
            Assert.That(scene.Component.Running, Is.False);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(scene.User, scene.Device), Is.True);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, dummy), Is.True);
        });

    [Test]
    public Task FoldingTheDummyStopsAndEjectsItsCompressor()
        => RunCase(scene =>
        {
            var dummy = scene.Spawn("RMCCPRDummy");
            DeployDummy(dummy);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, dummy), Is.True);
            Server.System<SharedTransformSystem>().Unanchor(dummy);
            Assert.That(scene.Component.Running, Is.False);
            Assert.That(scene.Component.Patient, Is.Null);
            Assert.That(Server.System<SharedContainerSystem>().TryGetContainer(dummy,
                CMUAutocompressorSystem.TrainingContainer, out var container), Is.True);
            Assert.That(container!.ContainedEntities, Does.Not.Contain(scene.Device));
        });

    [Test]
    public Task TrainingNeverAppliesFracturesOrLungTraumaAndReleasesManualCpr()
        => RunCase(scene =>
        {
            var dummy = scene.Spawn("RMCCPRDummy");
            DeployDummy(dummy);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, dummy), Is.True);
            MakeFractureDue(scene, scene.Component.ShatteredFractureDelay + TimeSpan.FromMinutes(10));
            scene.System.Update(0);
            Assert.That(scene.Component.FractureStage, Is.EqualTo(FractureSeverity.None));
            Assert.That(scene.Component.LungDamageApplied, Is.False);
            Assert.That(SEntMan.HasComponent<FractureComponent>(dummy), Is.False);
            Assert.That(SEntMan.HasComponent<InternalBleedingComponent>(dummy), Is.False);
            var during = new ReceiveCPRAttemptEvent(scene.User, dummy, true);
            SEntMan.EventBus.RaiseLocalEvent(dummy, ref during);
            Assert.That(during.Cancelled, Is.True);
            scene.System.Stop(scene.DeviceEntity, true);
            var after = new ReceiveCPRAttemptEvent(scene.User, dummy, true);
            SEntMan.EventBus.RaiseLocalEvent(dummy, ref after);
            Assert.That(after.Cancelled, Is.False);
        });

    [Test]
    public Task DummyRejectsASecondCompressorWithoutReplacingTheFirst()
        => RunCase(scene =>
        {
            var dummy = scene.Spawn("RMCCPRDummy");
            DeployDummy(dummy);
            Assert.That(scene.System.TryFit(scene.DeviceEntity, scene.User, dummy), Is.True);
            var other = scene.Spawn("CMUAutocompressor");
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(scene.User, other), Is.True);
            var component = SEntMan.GetComponent<CMUAutocompressorComponent>(other);
            Assert.That(scene.System.TryFit((other, component), scene.User, dummy), Is.False);
            Assert.That(component.Patient, Is.Null);
            Assert.That(scene.Component.Patient, Is.EqualTo(dummy));
            Assert.That(scene.Component.Running, Is.True);
        });

    private void DeployDummy(EntityUid dummy)
    {
        Server.System<SharedTransformSystem>().AnchorEntity(dummy);
        Assert.That(SEntMan.GetComponent<TransformComponent>(dummy).Anchored, Is.True);
    }

    private (EntityUid Torso, OrganHealthComponent Lung) FindTorsoAndLung(EntityUid patient)
    {
        var index = Server.System<CMUMedicalBodyIndexSystem>();
        foreach (var (part, anatomy) in index.GetBodyParts(patient))
        {
            if (anatomy.PartType != BodyPartType.Torso)
                continue;
            foreach (var (organ, _) in index.GetPartOrgans(part))
            {
                if (SEntMan.HasComponent<LungsComponent>(organ)
                    && SEntMan.TryGetComponent<OrganHealthComponent>(organ, out var health))
                    return (part, health);
            }
        }
        throw new InvalidOperationException("The human fixture must have a torso and a native lung organ.");
    }

    private void MakeFractureDue(Scene scene, TimeSpan? exposure = null)
    {
#pragma warning disable RA0002
        scene.Component.CompressionStartedAt = SGameTiming.CurTime
            - (exposure ?? scene.Component.FractureDelay + TimeSpan.FromMilliseconds(1));
#pragma warning restore RA0002
    }

    private async Task RunCase(Action<Scene> test)
    {
        var map = await Pair.CreateTestMap();
        try
        {
            await Server.WaitAssertion(() =>
            {
                EntityUid Spawn(string prototype)
                {
                    var uid = SEntMan.SpawnEntity(prototype, map.GridCoords);
                    return uid;
                }
                var user = Spawn("CMMobHuman");
                var patient = Spawn("CMMobHuman");
                var device = Spawn("CMUAutocompressor");
                Server.System<SkillsSystem>().SetSkill(user, "RMCSkillMedical", 2);
                Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(user, device), Is.True);
                var component = SEntMan.GetComponent<CMUAutocompressorComponent>(device);
                Assert.That(Server.System<PowerCellSystem>().TryGetBatteryFromSlot(device, out var battery), Is.True);
                test(new Scene(this, user, patient, device, component, battery!.Value, Spawn));
            });
        }
        finally
        {
            await Server.WaitPost(() => SEntMan.DeleteEntity(map.MapUid));
        }
    }

    private sealed class Scene(CMUAutocompressorTest fixture, EntityUid user, EntityUid patient, EntityUid device,
        CMUAutocompressorComponent component, Entity<Content.Shared.Power.Components.BatteryComponent> battery,
        Func<string, EntityUid> spawn)
    {
        public readonly EntityUid User = user;
        public readonly EntityUid Patient = patient;
        public readonly EntityUid Device = device;
        public readonly CMUAutocompressorComponent Component = component;
        public readonly Entity<Content.Shared.Power.Components.BatteryComponent> Battery = battery;
        public readonly Func<string, EntityUid> Spawn = spawn;
        public Entity<CMUAutocompressorComponent> DeviceEntity => (Device, Component);
        public CMUAutocompressorSystem System => fixture.Server.System<CMUAutocompressorSystem>();
        public float Charge => fixture.Server.System<SharedBatterySystem>().GetCharge(Battery.AsNullable());
    }
}
