using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.BodyPart.Events;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Bones.Events;
using Content.Shared._CMU14.Medical.Shrapnel;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Server._CMU14.Medical.Wounds;
using Content.Shared._CMU14.Medical.Organs.Heart;
using Content.Shared._RMC14.Explosion;
using Content.Shared._CMU14.Medical.Surgery;
using Content.Shared._CMU14.Medical.Surgery.Markers;
using Content.Server._CMU14.Medical.Surgery;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Fluids.Components;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Eye;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class RMCHumanPrototypeRegressionTest
{
    private static readonly EntProtoId CardiacArrestStatus = "StatusEffectCMUCardiacArrest";

    [Test]
    public async Task CMMobHumanHasExpectedBuis()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ui = entMan.System<SharedUserInterfaceSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(ui.HasUi(human, TacticalMapUserUi.Key), Is.True);
                    Assert.That(ui.HasUi(human, CMUSurgeryUIKey.Key), Is.True);
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MobHumanDummyUsesCmuMedicalBody()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var dummy = entMan.SpawnEntity("MobHumanDummy", MapCoordinates.Nullspace);

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUHumanMedicalComponent>(dummy), Is.True);
                    Assert.That(entMan.HasComponent<CMSurgeryTargetComponent>(dummy), Is.True);
                    Assert.That(entMan.GetComponent<BodyComponent>(dummy).Prototype?.Id, Is.EqualTo("CMUHumanBody"));
                });
            }
            finally
            {
                entMan.DeleteEntity(dummy);
            }
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("CMSurgicalLine", WoundType.Brute)]
    [TestCase("CMSynthGraft", WoundType.Burn)]
    public async Task CmuWoundTreaterClickSelfRequiresMedicalSkill(string treaterId, WoundType woundType)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var skills = entMan.System<SkillsSystem>();
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var treater = entMan.SpawnEntity(treaterId, MapCoordinates.Nullspace);

            try
            {
                skills.SetSkill(patient, "RMCSkillMedical", 0);
                AddBodyPartWound(entMan, GetFirstBodyPart(entMan, patient), woundType);

                var interact = new AfterInteractEvent(patient, treater, patient, default, true);
                entMan.EventBus.RaiseLocalEvent(treater, interact);

                Assert.Multiple(() =>
                {
                    Assert.That(interact.Handled, Is.True);
                    Assert.That(entMan.HasComponent<CMUBandagePendingComponent>(patient), Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(treater);
                entMan.DeleteEntity(patient);
            }
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("CMTraumaKit10", WoundType.Brute)]
    [TestCase("CMBurnKit10", WoundType.Burn)]
    public async Task CmuTraumaAndBurnKitsInstantlyCleanTwoWoundsPerUse(string treaterId, WoundType woundType)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var skills = entMan.System<SkillsSystem>();
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var treater = entMan.SpawnEntity(treaterId, MapCoordinates.Nullspace);

            try
            {
                skills.SetSkill(patient, "RMCSkillMedical", 2);
                var part = GetFirstBodyPart(entMan, patient);
                AddBodyPartWound(entMan, part, woundType);
                AddBodyPartWound(entMan, part, woundType);

                var interact = new AfterInteractEvent(patient, treater, patient, default, true);
                entMan.EventBus.RaiseLocalEvent(treater, interact);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(part);
                var woundList = GetField<List<Wound>>(wounds, nameof(BodyPartWoundComponent.Wounds));
                var bandages = GetField<List<int>>(wounds, nameof(BodyPartWoundComponent.Bandages));
                var treated = 0;
                foreach (var wound in woundList)
                {
                    if (wound.Type == woundType && wound.Treated)
                        treated++;
                }

                Assert.Multiple(() =>
                {
                    Assert.That(interact.Handled, Is.True);
                    Assert.That(entMan.HasComponent<CMUBandagePendingComponent>(patient), Is.False);
                    Assert.That(treated, Is.EqualTo(2));
                    Assert.That(bandages.Count, Is.EqualTo(2));
                    Assert.That(bandages, Has.All.EqualTo(WoundSizeProfile.BandagesRequired(WoundSize.Deep)));
                    Assert.That(entMan.GetComponent<StackComponent>(treater).Count, Is.EqualTo(9));
                });
            }
            finally
            {
                entMan.DeleteEntity(treater);
                entMan.DeleteEntity(patient);
            }
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("CMTraumaKit10", WoundType.Brute, WoundCleanupFlags.PoorClosure, true)]
    [TestCase("CMGauze10", WoundType.Brute, WoundCleanupFlags.PoorClosure, false)]
    [TestCase("AU14HemostaticGauze", WoundType.Brute, WoundCleanupFlags.RetainedFragment, true)]
    [TestCase("CMBurnKit10", WoundType.Burn, WoundCleanupFlags.PoorClosure, true)]
    [TestCase("CMOintment10", WoundType.Burn, WoundCleanupFlags.PoorClosure, true)]
    public async Task CmuBaseWoundTreatersClearCleanupBurdenAndAllowPassiveHealing(
        string treaterId,
        WoundType woundType,
        WoundCleanupFlags cleanup,
        bool instantTreatment)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid patient = default;
        EntityUid treater = default;
        EntityUid part = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var skills = entMan.System<SkillsSystem>();
            patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            treater = entMan.SpawnEntity(treaterId, MapCoordinates.Nullspace);
            part = GetFirstBodyPart(entMan, patient);

            skills.SetSkill(patient, "RMCSkillMedical", 2);
            AddBodyPartWound(entMan, part, woundType, FixedPoint2.New(1), cleanup, WoundSize.Small);
            var health = entMan.GetComponent<BodyPartHealthComponent>(part);
            partHealth.SetCurrent((part, health), health.Max - FixedPoint2.New(4));

            var interact = new AfterInteractEvent(patient, treater, patient, default, true);
            entMan.EventBus.RaiseLocalEvent(treater, interact);

            Assert.That(interact.Handled, Is.True);
            Assert.That(entMan.HasComponent<CMUBandagePendingComponent>(patient), Is.EqualTo(!instantTreatment));
        });

        await pair.RunSeconds(12);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var health = entMan.GetComponent<BodyPartHealthComponent>(part);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<BodyPartWoundComponent>(part), Is.False);
                Assert.That(health.Current, Is.EqualTo(health.Max));
            });
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            if (entMan.EntityExists(treater))
                entMan.DeleteEntity(treater);
            if (entMan.EntityExists(patient))
                entMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("CMTraumaKit10", true, 9)]
    [TestCase("CMGauze10", false, 10)]
    public async Task CmuWoundTreatersPreferDeadShrapnelPatientWoundsOverArmedSurgery(
        string treaterId,
        bool instantTreatment,
        int expectedStackCount)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var mobState = entMan.System<MobStateSystem>();
            var shrapnel = entMan.System<SharedCMUShrapnelSystem>();
            var skills = entMan.System<SkillsSystem>();
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var treater = entMan.SpawnEntity(treaterId, MapCoordinates.Nullspace);

            try
            {
                skills.SetSkill(surgeon, "RMCSkillMedical", 2);
                var part = GetFirstBodyPart(entMan, patient);
                AddBodyPartWound(entMan, part, WoundType.Brute);
                shrapnel.AddShrapnel(part, 1, 10f);
                mobState.ChangeMobState(patient, MobState.Dead);

                var armed = flow.TryArmStep(
                    surgeon,
                    patient,
                    part,
                    "CMUSurgeryExtractForeignBody",
                    0);

                Assert.That(armed, Is.Not.Null);
                Assert.That(armed!.RequiredToolCategory, Is.EqualTo("hemostat"));

                var interact = new AfterInteractEvent(surgeon, treater, patient, default, true);
                entMan.EventBus.RaiseLocalEvent(treater, interact);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(part);
                var woundList = GetField<List<Wound>>(wounds, nameof(BodyPartWoundComponent.Wounds));

                Assert.Multiple(() =>
                {
                    Assert.That(interact.Handled, Is.True);
                    Assert.That(woundList[0].Treated, Is.EqualTo(instantTreatment));
                    Assert.That(entMan.HasComponent<CMUBandagePendingComponent>(surgeon), Is.EqualTo(!instantTreatment));
                    Assert.That(entMan.GetComponent<StackComponent>(treater).Count, Is.EqualTo(expectedStackCount));
                });
            }
            finally
            {
                entMan.DeleteEntity(treater);
                entMan.DeleteEntity(surgeon);
                entMan.DeleteEntity(patient);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuPreparedTraumaDressingsAreInstantForCorpsmenOnly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var skills = entMan.System<SkillsSystem>();
            var unskilled = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var unskilledTreater = entMan.SpawnEntity("CMUHemostaticTraumaDressing4", MapCoordinates.Nullspace);
            var corpsman = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var corpsmanTreater = entMan.SpawnEntity("CMUHemostaticTraumaDressing4", MapCoordinates.Nullspace);

            try
            {
                skills.SetSkill(unskilled, "RMCSkillMedical", 0);
                var unskilledPart = GetFirstBodyPart(entMan, unskilled);
                AddBodyPartWound(entMan, unskilledPart, WoundType.Brute);
                AddBodyPartWound(entMan, unskilledPart, WoundType.Brute);

                var unskilledInteract = new AfterInteractEvent(unskilled, unskilledTreater, unskilled, default, true);
                entMan.EventBus.RaiseLocalEvent(unskilledTreater, unskilledInteract);

                var unskilledWounds = entMan.GetComponent<BodyPartWoundComponent>(unskilledPart);

                skills.SetSkill(corpsman, "RMCSkillMedical", 2);
                var corpsmanPart = GetFirstBodyPart(entMan, corpsman);
                AddBodyPartWound(entMan, corpsmanPart, WoundType.Brute);
                AddBodyPartWound(entMan, corpsmanPart, WoundType.Brute);

                var corpsmanInteract = new AfterInteractEvent(corpsman, corpsmanTreater, corpsman, default, true);
                entMan.EventBus.RaiseLocalEvent(corpsmanTreater, corpsmanInteract);

                var corpsmanWounds = entMan.GetComponent<BodyPartWoundComponent>(corpsmanPart);

                Assert.Multiple(() =>
                {
                    Assert.That(unskilledInteract.Handled, Is.True);
                    Assert.That(entMan.HasComponent<CMUBandagePendingComponent>(unskilled), Is.True);
                    Assert.That(CountTreatedWounds(unskilledWounds, WoundType.Brute), Is.EqualTo(0));
                    Assert.That(entMan.GetComponent<StackComponent>(unskilledTreater).Count, Is.EqualTo(6));

                    Assert.That(corpsmanInteract.Handled, Is.True);
                    Assert.That(entMan.HasComponent<CMUBandagePendingComponent>(corpsman), Is.False);
                    Assert.That(CountTreatedWounds(corpsmanWounds, WoundType.Brute), Is.EqualTo(2));
                    Assert.That(entMan.GetComponent<StackComponent>(corpsmanTreater).Count, Is.EqualTo(5));
                });
            }
            finally
            {
                entMan.DeleteEntity(unskilledTreater);
                entMan.DeleteEntity(unskilled);
                entMan.DeleteEntity(corpsmanTreater);
                entMan.DeleteEntity(corpsman);
            }
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("CMSurgicalLine", WoundType.Brute, "Slash")]
    [TestCase("CMSynthGraft", WoundType.Burn, "Heat")]
    public async Task CmuLegacyLineAndGraftHealToWoundCapWithoutTreatingWounds(
        string treaterId,
        WoundType woundType,
        string damageType)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid patient = default;
        EntityUid treater = default;
        EntityUid torso = default;
        FixedPoint2 cap = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var damageable = entMan.System<DamageableSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var skills = entMan.System<SkillsSystem>();

            patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            treater = entMan.SpawnEntity(treaterId, MapCoordinates.Nullspace);

            skills.SetSkill(patient, "RMCSkillMedical", 2);
            Assert.That(hands.TryPickupAnyHand(patient, treater, checkActionBlocker: false), Is.True);

            torso = GetBodyPart(entMan, patient, BodyPartType.Torso, BodyPartSymmetry.None);
            AddBodyPartWound(entMan, torso, woundType);

            var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
            var health = entMan.GetComponent<BodyPartHealthComponent>(torso);
            cap = health.Max * SharedCMUWoundsSystem.ComputeFieldTreatmentCap(wounds);
            partHealth.SetCurrent((torso, health), cap - FixedPoint2.New(2));

            var damage = entMan.GetComponent<DamageableComponent>(patient);
            damageable.SetDamage(patient, damage, new DamageSpecifier
            {
                DamageDict = { [damageType] = FixedPoint2.New(50) },
            });

            var interact = new AfterInteractEvent(patient, treater, patient, default, true);
            entMan.EventBus.RaiseLocalEvent(treater, interact);

            Assert.That(interact.Handled, Is.True);
            Assert.That(entMan.HasComponent<CMUBandagePendingComponent>(patient), Is.True);
        });

        await pair.RunSeconds(6);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
            var wound = GetField<List<Wound>>(wounds, nameof(BodyPartWoundComponent.Wounds))[0];
            var quality = GetField<List<WoundTreatmentQuality>>(wounds, nameof(BodyPartWoundComponent.TreatmentQualities))[0];
            var afterHealth = entMan.GetComponent<BodyPartHealthComponent>(torso);

            Assert.Multiple(() =>
            {
                Assert.That(wound.Treated, Is.False);
                Assert.That(quality, Is.EqualTo(WoundTreatmentQuality.Untreated));
                Assert.That(afterHealth.Current, Is.EqualTo(cap));
            });
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            if (entMan.EntityExists(treater))
                entMan.DeleteEntity(treater);
            if (entMan.EntityExists(patient))
                entMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("CMSurgicalLine", WoundType.Brute, "Slash")]
    [TestCase("CMSynthGraft", WoundType.Burn, "Heat")]
    public async Task CmuLegacyLineAndGraftSkipCappedPartAndHealAnotherPart(
        string treaterId,
        WoundType woundType,
        string damageType)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid patient = default;
        EntityUid treater = default;
        EntityUid torso = default;
        EntityUid rightArm = default;
        FixedPoint2 torsoCap = default;
        FixedPoint2 armCap = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var damageable = entMan.System<DamageableSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var skills = entMan.System<SkillsSystem>();

            patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            treater = entMan.SpawnEntity(treaterId, MapCoordinates.Nullspace);

            skills.SetSkill(patient, "RMCSkillMedical", 2);
            Assert.That(hands.TryPickupAnyHand(patient, treater, checkActionBlocker: false), Is.True);

            torso = GetBodyPart(entMan, patient, BodyPartType.Torso, BodyPartSymmetry.None);
            rightArm = GetBodyPart(entMan, patient, BodyPartType.Arm, BodyPartSymmetry.Right);

            AddBodyPartWound(entMan, torso, woundType);
            AddBodyPartWound(entMan, rightArm, woundType);

            var torsoWounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
            var armWounds = entMan.GetComponent<BodyPartWoundComponent>(rightArm);
            var torsoHealth = entMan.GetComponent<BodyPartHealthComponent>(torso);
            var armHealth = entMan.GetComponent<BodyPartHealthComponent>(rightArm);

            torsoCap = torsoHealth.Max * SharedCMUWoundsSystem.ComputeFieldTreatmentCap(torsoWounds);
            armCap = armHealth.Max * SharedCMUWoundsSystem.ComputeFieldTreatmentCap(armWounds);
            partHealth.SetCurrent((torso, torsoHealth), torsoCap);
            partHealth.SetCurrent((rightArm, armHealth), armCap - FixedPoint2.New(2));

            var damage = entMan.GetComponent<DamageableComponent>(patient);
            damageable.SetDamage(patient, damage, new DamageSpecifier
            {
                DamageDict = { [damageType] = FixedPoint2.New(50) },
            });

            var interact = new AfterInteractEvent(patient, treater, patient, default, true);
            entMan.EventBus.RaiseLocalEvent(treater, interact);

            Assert.That(interact.Handled, Is.True);
            Assert.That(entMan.HasComponent<CMUBandagePendingComponent>(patient), Is.True);
        });

        await pair.RunSeconds(6);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var torsoHealth = entMan.GetComponent<BodyPartHealthComponent>(torso);
            var armHealth = entMan.GetComponent<BodyPartHealthComponent>(rightArm);
            var torsoWound = GetField<List<Wound>>(entMan.GetComponent<BodyPartWoundComponent>(torso), nameof(BodyPartWoundComponent.Wounds))[0];
            var armWound = GetField<List<Wound>>(entMan.GetComponent<BodyPartWoundComponent>(rightArm), nameof(BodyPartWoundComponent.Wounds))[0];

            Assert.Multiple(() =>
            {
                Assert.That(torsoHealth.Current, Is.EqualTo(torsoCap));
                Assert.That(armHealth.Current, Is.EqualTo(armCap));
                Assert.That(torsoWound.Treated, Is.False);
                Assert.That(armWound.Treated, Is.False);
            });
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            if (entMan.EntityExists(treater))
                entMan.DeleteEntity(treater);
            if (entMan.EntityExists(patient))
                entMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuSurgicalLineStopsArterialBleedingWithoutTreatingWound()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid patient = default;
        EntityUid line = default;
        EntityUid rightArm = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var skills = entMan.System<SkillsSystem>();

            patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            line = entMan.SpawnEntity("CMSurgicalLine", MapCoordinates.Nullspace);

            skills.SetSkill(patient, "RMCSkillMedical", 2);
            Assert.That(hands.TryPickupAnyHand(patient, line, checkActionBlocker: false), Is.True);

            rightArm = GetBodyPart(entMan, patient, BodyPartType.Arm, BodyPartSymmetry.Right);
            AddBodyPartWound(entMan, rightArm, WoundType.Brute);

            var wounds = entMan.GetComponent<BodyPartWoundComponent>(rightArm);
            SetField(wounds, nameof(BodyPartWoundComponent.ExternalBleeding), ExternalBleedTier.Arterial);
            entMan.Dirty(rightArm, wounds);

            var interact = new AfterInteractEvent(patient, line, patient, default, true);
            entMan.EventBus.RaiseLocalEvent(line, interact);

            Assert.That(interact.Handled, Is.True);
            Assert.That(entMan.HasComponent<CMUBandagePendingComponent>(patient), Is.True);
        });

        await pair.RunSeconds(6);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var wounds = entMan.GetComponent<BodyPartWoundComponent>(rightArm);
            var wound = GetField<List<Wound>>(wounds, nameof(BodyPartWoundComponent.Wounds))[0];

            Assert.Multiple(() =>
            {
                Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.None));
                Assert.That(wound.Treated, Is.False);
            });
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            if (entMan.EntityExists(line))
                entMan.DeleteEntity(line);
            if (entMan.EntityExists(patient))
                entMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuSeveredLimbRemovesItsRemainingBruteAndBurnWoundDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var damageable = entMan.System<DamageableSystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var rightArm = GetBodyPart(entMan, patient, BodyPartType.Arm, BodyPartSymmetry.Right);
                AddBodyPartWound(entMan, rightArm, WoundType.Brute, FixedPoint2.New(35));
                AddBodyPartWound(entMan, rightArm, WoundType.Burn, FixedPoint2.New(15));

                var damage = entMan.GetComponent<DamageableComponent>(patient);
                damageable.SetDamage(patient, damage, new DamageSpecifier
                {
                    DamageDict =
                    {
                        ["Slash"] = FixedPoint2.New(35),
                        ["Heat"] = FixedPoint2.New(15),
                    },
                });

                var severed = new BodyPartSeveredEvent(patient, rightArm, BodyPartType.Arm);
                entMan.EventBus.RaiseLocalEvent(rightArm, ref severed);

                var after = entMan.GetComponent<DamageableComponent>(patient);
                Assert.Multiple(() =>
                {
                    Assert.That(DamageInGroup(prototypes, after.Damage, "Brute"), Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(DamageInGroup(prototypes, after.Damage, "Burn"), Is.EqualTo(FixedPoint2.Zero));
                });
            }
            finally
            {
                entMan.DeleteEntity(patient);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuBodyPartHealingPrefersTreatmentOriginPart()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var damageable = entMan.System<DamageableSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, patient, BodyPartType.Torso, BodyPartSymmetry.None);
                var leftArm = GetBodyPart(entMan, patient, BodyPartType.Arm, BodyPartSymmetry.Left);

                damageable.TryChangeDamage(patient, new DamageSpecifier
                {
                    DamageDict = { ["Blunt"] = FixedPoint2.New(50) },
                }, true);

                var torsoHealth = entMan.GetComponent<BodyPartHealthComponent>(torso);
                var armHealth = entMan.GetComponent<BodyPartHealthComponent>(leftArm);
                partHealth.SetCurrent((torso, torsoHealth), FixedPoint2.New(10));
                partHealth.SetCurrent((leftArm, armHealth), FixedPoint2.New(20));

                var torsoBefore = torsoHealth.Current;
                var armBefore = armHealth.Current;

                var damage = entMan.GetComponent<DamageableComponent>(patient);
                damageable.TryChangeDamage(patient, new DamageSpecifier
                {
                    DamageDict = { ["Blunt"] = FixedPoint2.New(-10) },
                }, true, false, damage, origin: leftArm);

                Assert.Multiple(() =>
                {
                    Assert.That(armHealth.Current, Is.GreaterThan(armBefore));
                    Assert.That(torsoHealth.Current, Is.EqualTo(torsoBefore));
                });
            }
            finally
            {
                entMan.DeleteEntity(patient);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuAttachedInternalsUsePrivateVisibilityLayer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var internalLayer = (ushort) VisibilityFlags.CMUMedicalInternals;

            try
            {
                var checkedParts = 0;
                foreach (var (partUid, _) in body.GetBodyChildren(human))
                {
                    checkedParts++;
                    var visibility = entMan.GetComponent<VisibilityComponent>(partUid);
                    Assert.That(visibility.Layer & internalLayer, Is.EqualTo(internalLayer));
                }

                var checkedOrgans = 0;
                foreach (var organ in body.GetBodyOrgans(human))
                {
                    checkedOrgans++;
                    var visibility = entMan.GetComponent<VisibilityComponent>(organ.Id);
                    Assert.That(visibility.Layer & internalLayer, Is.EqualTo(internalLayer));
                }

                Assert.Multiple(() =>
                {
                    Assert.That(checkedParts, Is.GreaterThan(0));
                    Assert.That(checkedOrgans, Is.GreaterThan(0));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuDetachedOrgansLeavePrivateVisibilityLayer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;
        EntityUid organ = default;
        var internalLayer = (ushort) VisibilityFlags.CMUMedicalInternals;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            foreach (var bodyOrgan in body.GetBodyOrgans(human))
            {
                organ = bodyOrgan.Id;
                break;
            }

            Assert.That(organ, Is.Not.EqualTo(default(EntityUid)));

            var visibility = entMan.GetComponent<VisibilityComponent>(organ);
            Assert.That(visibility.Layer & internalLayer, Is.EqualTo(internalLayer));

            Assert.That(body.RemoveOrgan(organ), Is.True);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var visibility = entMan.GetComponent<VisibilityComponent>(organ);
            Assert.That(visibility.Layer & internalLayer, Is.EqualTo(0));

            entMan.DeleteEntity(organ);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuRemovingHeartCausesCardiacArrestAndAsphyxiation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;
        EntityUid heart = default;
        FixedPoint2 beforeDamage = default;
        FixedPoint2 firstDamage = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            foreach (var organ in body.GetBodyOrganEntityComps<HeartComponent>(human))
            {
                heart = organ.Owner;
                break;
            }

            Assert.That(heart, Is.Not.EqualTo(default(EntityUid)));
            beforeDamage = entMan.GetComponent<DamageableComponent>(human).TotalDamage;

            Assert.That(body.RemoveOrgan(heart), Is.True);
        });

        await server.WaitRunTicks(60);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var status = entMan.System<SharedStatusEffectsSystem>();
            var damage = entMan.GetComponent<DamageableComponent>(human);

            Assert.Multiple(() =>
            {
                Assert.That(status.HasStatusEffect(human, CardiacArrestStatus), Is.True);
                Assert.That(damage.TotalDamage, Is.GreaterThan(beforeDamage));
            });

            firstDamage = damage.TotalDamage;
        });

        await server.WaitRunTicks(60);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var damage = entMan.GetComponent<DamageableComponent>(human);

            Assert.That(damage.TotalDamage, Is.GreaterThan(firstDamage));

            entMan.DeleteEntity(heart);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuSynthMissingLimbShowsSynthReattachSurgery()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var standing = entMan.System<StandingStateSystem>();
            var skills = entMan.System<SkillsSystem>();
            var xform = entMan.System<SharedTransformSystem>();

            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<SynthComponent>(patient);
                skills.SetSkill(surgeon, "RMCSkillSurgery", 3);
                standing.Down(patient, playSound: false, dropHeldItems: false, force: true);

                EntityUid leftArm = default;
                foreach (var (partUid, part) in body.GetBodyChildren(patient))
                {
                    if (part.PartType != BodyPartType.Arm || part.Symmetry != BodyPartSymmetry.Left)
                        continue;
                    leftArm = partUid;
                    break;
                }

                Assert.That(leftArm, Is.Not.EqualTo(default(EntityUid)));
                xform.DetachEntity(leftArm, entMan.GetComponent<TransformComponent>(leftArm));

                var entries = dispatch.BuildPartEntries(patient, surgeon);
                var leftArmEntry = entries.Find(entry =>
                    entry.Type == BodyPartType.Arm &&
                    entry.Symmetry == BodyPartSymmetry.Left);

                Assert.That(leftArmEntry, Is.Not.Null);

                var surgeryIds = leftArmEntry!.EligibleSurgeries.ConvertAll(entry => entry.SurgeryId);
                Assert.Multiple(() =>
                {
                    Assert.That(surgeryIds, Does.Contain("RMCSynthSurgeryReattachLimb"));
                    Assert.That(surgeryIds, Does.Not.Contain("CMUSurgeryReattachLimb"));
                });
            }
            finally
            {
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(surgeon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuSynthRepairToolOpensReattachMenuBeforeRepair()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var damageable = entMan.System<DamageableSystem>();
            var standing = entMan.System<StandingStateSystem>();
            var skills = entMan.System<SkillsSystem>();
            var xform = entMan.System<SharedTransformSystem>();

            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var cable = entMan.SpawnEntity("RMCCableCoil30", MapCoordinates.Nullspace);
            EntityUid leftArm = default;

            try
            {
                entMan.EnsureComponent<SynthComponent>(patient);
                skills.SetSkill(surgeon, "RMCSkillSurgery", 3);
                standing.Down(patient, playSound: false, dropHeldItems: false, force: true);

                foreach (var (partUid, part) in body.GetBodyChildren(patient))
                {
                    if (part.PartType != BodyPartType.Arm || part.Symmetry != BodyPartSymmetry.Left)
                        continue;

                    leftArm = partUid;
                    break;
                }

                Assert.That(leftArm, Is.Not.EqualTo(default(EntityUid)));
                xform.DetachEntity(leftArm, entMan.GetComponent<TransformComponent>(leftArm));
                damageable.TryChangeDamage(patient, new DamageSpecifier { DamageDict = { ["Heat"] = 10 } }, true);

                var interact = new InteractUsingEvent(surgeon, cable, patient, entMan.GetComponent<TransformComponent>(patient).Coordinates);
                entMan.EventBus.RaiseLocalEvent(patient, interact);

                Assert.Multiple(() =>
                {
                    Assert.That(interact.Handled, Is.True);
                    Assert.That(entMan.HasComponent<CMUSurgeryWindowOpenComponent>(surgeon), Is.True);
                    Assert.That(entMan.GetComponent<CMUSurgeryWindowOpenComponent>(surgeon).Patient, Is.EqualTo(patient));
                });
            }
            finally
            {
                if (leftArm != default)
                    entMan.DeleteEntity(leftArm);
                entMan.DeleteEntity(cable);
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(surgeon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuSynthLimbReattachUsesHeldLimb()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid patient = default;
        EntityUid surgeon = default;
        EntityUid leftArm = default;
        EntityUid socketAnchor = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var standing = entMan.System<StandingStateSystem>();
            var skills = entMan.System<SkillsSystem>();
            var xform = entMan.System<SharedTransformSystem>();

            patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            entMan.EnsureComponent<SynthComponent>(patient);
            skills.SetSkill(surgeon, "RMCSkillSurgery", 3);
            standing.Down(patient, playSound: false, dropHeldItems: false, force: true);

            var root = body.GetRootPartOrNull(patient);
            Assert.That(root, Is.Not.Null);
            socketAnchor = root!.Value.Entity;

            foreach (var (partUid, part) in body.GetBodyChildren(patient))
            {
                if (part.PartType != BodyPartType.Arm || part.Symmetry != BodyPartSymmetry.Left)
                    continue;

                leftArm = partUid;
                break;
            }

            Assert.That(leftArm, Is.Not.EqualTo(default(EntityUid)));

            xform.DetachEntity(leftArm, entMan.GetComponent<TransformComponent>(leftArm));
            Assert.That(hands.TryPickupAnyHand(surgeon, leftArm, checkActionBlocker: false), Is.True);

            entMan.EnsureComponent<CMUStumpRemovedComponent>(socketAnchor);
            entMan.EnsureComponent<CMUReattachPreppedComponent>(socketAnchor);

            var armed = flow.TryArmStep(
                surgeon,
                patient,
                socketAnchor,
                "RMCSynthSurgeryReattachLimb",
                0,
                BodyPartType.Arm,
                BodyPartSymmetry.Left);

            Assert.That(armed, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(armed!.StepLabel, Is.EqualTo("Reattach Synth Limb"));
                Assert.That(armed.RequiredToolCategory, Is.EqualTo("severed_limb"));
                Assert.That(flow.TryHandleArmedToolUse(patient, armed, surgeon, leftArm, socketAnchor, out var handled, out var started), Is.True);
                Assert.That(handled, Is.True);
                Assert.That(started, Is.True);
            });
        });

        await server.WaitRunTicks(120);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();

            var found = false;
            foreach (var (partUid, part) in body.GetBodyChildren(patient))
            {
                if (partUid != leftArm)
                    continue;

                found = part.PartType == BodyPartType.Arm && part.Symmetry == BodyPartSymmetry.Left;
                break;
            }

            Assert.Multiple(() =>
            {
                Assert.That(found, Is.True);
                Assert.That(entMan.HasComponent<CMUReattachCompleteComponent>(leftArm), Is.True);
                Assert.That(entMan.GetComponent<CMUSurgeryArmedStepComponent>(patient).RequiredToolCategory, Is.EqualTo("blowtorch"));
            });

            entMan.DeleteEntity(patient);
            entMan.DeleteEntity(surgeon);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuSynthBodyPartsRejectFractures()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<SynthComponent>(patient);

                EntityUid leftArm = default;
                foreach (var (partUid, part) in body.GetBodyChildren(patient))
                {
                    if (part.PartType != BodyPartType.Arm || part.Symmetry != BodyPartSymmetry.Left)
                        continue;

                    leftArm = partUid;
                    break;
                }

                Assert.That(leftArm, Is.Not.EqualTo(default(EntityUid)));

                var attempt = new BoneFractureAttemptEvent(leftArm, FractureSeverity.Compound);
                entMan.EventBus.RaiseLocalEvent(leftArm, ref attempt);

                Assert.Multiple(() =>
                {
                    Assert.That(attempt.Cancelled, Is.True);
                    Assert.That(entMan.HasComponent<FractureComponent>(leftArm), Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(patient);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuInternalBleedDrainsBloodWithoutExternalPuddle()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var mapManager = server.ResolveDependency<IMapManager>();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var map = entMan.System<SharedMapSystem>();
            var solutions = entMan.System<SharedSolutionContainerSystem>();
            var wounds = entMan.System<CMUWoundsSystem>();

            map.CreateMap(out var mapId);
            var grid = mapManager.CreateGridEntity(mapId);
            var tile = Vector2i.Zero;
            map.SetTile(grid, tile, new Tile(1));

            var patient = entMan.SpawnEntity("CMMobHuman", map.GridTileToLocal(grid, grid.Comp, tile));

            try
            {
                EntityUid leftArm = default;
                foreach (var (partUid, part) in body.GetBodyChildren(patient))
                {
                    if (part.PartType != BodyPartType.Arm || part.Symmetry != BodyPartSymmetry.Left)
                        continue;

                    leftArm = partUid;
                    break;
                }

                Assert.That(leftArm, Is.Not.EqualTo(default(EntityUid)));

                entMan.EnsureComponent<InternalBleedingComponent>(leftArm);

                var bloodstream = entMan.GetComponent<BloodstreamComponent>(patient);
                Assert.That(
                    solutions.ResolveSolution(patient, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var bloodSolution),
                    Is.True);
                Assert.That(
                    solutions.ResolveSolution(patient, bloodstream.BloodTemporarySolutionName, ref bloodstream.TemporarySolution, out var tempSolution),
                    Is.True);

                var bloodBefore = bloodSolution.Volume;
                var tempBefore = tempSolution.Volume;
                var puddlesBefore = CountPuddles(entMan);

                wounds.Update(1f);

                Assert.That(
                    solutions.ResolveSolution(patient, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out bloodSolution),
                    Is.True);
                Assert.That(
                    solutions.ResolveSolution(patient, bloodstream.BloodTemporarySolutionName, ref bloodstream.TemporarySolution, out tempSolution),
                    Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(bloodSolution.Volume.Float(), Is.LessThan(bloodBefore.Float()));
                    Assert.That(tempSolution.Volume, Is.EqualTo(tempBefore));
                    Assert.That(CountPuddles(entMan), Is.EqualTo(puddlesBefore));
                });
            }
            finally
            {
                entMan.DeleteEntity(patient);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuSurgeryToolUseOnOtherSurgeonInFlightOpensUiInsteadOfAutoResuming()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var standing = entMan.System<StandingStateSystem>();
            var skills = entMan.System<SkillsSystem>();

            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var originalSurgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var newSurgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var scalpel = entMan.SpawnEntity("CMScalpel", MapCoordinates.Nullspace);

            try
            {
                skills.SetSkill(originalSurgeon, "RMCSkillSurgery", 3);
                skills.SetSkill(newSurgeon, "RMCSkillSurgery", 3);
                standing.Down(patient, playSound: false, dropHeldItems: false, force: true);

                EntityUid leftArm = default;
                foreach (var (partUid, part) in body.GetBodyChildren(patient))
                {
                    if (part.PartType != BodyPartType.Arm || part.Symmetry != BodyPartSymmetry.Left)
                        continue;
                    leftArm = partUid;
                    break;
                }

                Assert.That(leftArm, Is.Not.EqualTo(default(EntityUid)));
                entMan.EnsureComponent<InternalBleedingComponent>(leftArm);
                flow.EnsureSurgeryInFlight(
                    patient,
                    leftArm,
                    originalSurgeon,
                    "CMUSurgeryCauterizeInternalBleeding",
                    flow.ResolveSurgeryDisplayName("CMUSurgeryCauterizeInternalBleeding"),
                    BodyPartType.Arm,
                    BodyPartSymmetry.Left);

                Assert.That(dispatch.TryDispatch(newSurgeon, patient, scalpel), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUSurgeryWindowOpenComponent>(newSurgeon), Is.True);
                    Assert.That(entMan.HasComponent<CMUSurgeryArmedStepComponent>(patient), Is.False);
                    Assert.That(entMan.GetComponent<CMUSurgeryInFlightComponent>(leftArm).Surgeon, Is.EqualTo(originalSurgeon));
                });

                var originalState = flow.BuildBuiState(
                    patient,
                    "Patient",
                    dispatch.BuildPartEntries(patient, originalSurgeon),
                    null,
                    originalSurgeon);
                var newState = flow.BuildBuiState(
                    patient,
                    "Patient",
                    dispatch.BuildPartEntries(patient, newSurgeon),
                    null,
                    newSurgeon);

                Assert.Multiple(() =>
                {
                    Assert.That(originalState.InFlight, Is.Not.Null);
                    Assert.That(originalState.InFlight!.OwnedByViewer, Is.True);
                    Assert.That(newState.InFlight, Is.Not.Null);
                    Assert.That(newState.InFlight!.OwnedByViewer, Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(scalpel);
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(originalSurgeon);
                entMan.DeleteEntity(newSurgeon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuSurgeryToolUseByOtherSurgeonExposesArmedStepForTakeover()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var standing = entMan.System<StandingStateSystem>();
            var skills = entMan.System<SkillsSystem>();

            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var originalSurgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var newSurgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var scalpel = entMan.SpawnEntity("CMScalpel", MapCoordinates.Nullspace);

            try
            {
                skills.SetSkill(originalSurgeon, "RMCSkillSurgery", 3);
                skills.SetSkill(newSurgeon, "RMCSkillSurgery", 3);
                standing.Down(patient, playSound: false, dropHeldItems: false, force: true);

                EntityUid leftArm = default;
                foreach (var (partUid, part) in body.GetBodyChildren(patient))
                {
                    if (part.PartType != BodyPartType.Arm || part.Symmetry != BodyPartSymmetry.Left)
                        continue;
                    leftArm = partUid;
                    break;
                }

                Assert.That(leftArm, Is.Not.EqualTo(default(EntityUid)));
                entMan.EnsureComponent<InternalBleedingComponent>(leftArm);

                var armed = flow.TryArmStep(
                    originalSurgeon,
                    patient,
                    leftArm,
                    "CMUSurgeryCauterizeInternalBleeding",
                    0,
                    BodyPartType.Arm,
                    BodyPartSymmetry.Left);

                Assert.That(armed, Is.Not.Null);
                Assert.That(dispatch.TryDispatch(newSurgeon, patient, scalpel), Is.True);

                var currentArmed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(patient);
                var originalState = flow.BuildBuiState(
                    patient,
                    "Patient",
                    dispatch.BuildPartEntries(patient, originalSurgeon),
                    currentArmed,
                    originalSurgeon);
                var newState = flow.BuildBuiState(
                    patient,
                    "Patient",
                    dispatch.BuildPartEntries(patient, newSurgeon),
                    currentArmed,
                    newSurgeon);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUSurgeryWindowOpenComponent>(newSurgeon), Is.True);
                    Assert.That(currentArmed.Surgeon, Is.EqualTo(originalSurgeon));
                    Assert.That(originalState.CurrentArmedStep, Is.Not.Null);
                    Assert.That(newState.CurrentArmedStep, Is.Not.Null);
                });
            }
            finally
            {
                entMan.DeleteEntity(scalpel);
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(originalSurgeon);
                entMan.DeleteEntity(newSurgeon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuOwningSurgeonCanReopenMenuWithScalpelWhenAnotherToolIsArmed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var standing = entMan.System<StandingStateSystem>();
            var skills = entMan.System<SkillsSystem>();

            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var scalpel = entMan.SpawnEntity("CMScalpel", MapCoordinates.Nullspace);

            try
            {
                skills.SetSkill(surgeon, "RMCSkillSurgery", 3);
                standing.Down(patient, playSound: false, dropHeldItems: false, force: true);

                EntityUid leftArm = default;
                foreach (var (partUid, part) in body.GetBodyChildren(patient))
                {
                    if (part.PartType != BodyPartType.Arm || part.Symmetry != BodyPartSymmetry.Left)
                        continue;
                    leftArm = partUid;
                    break;
                }

                Assert.That(leftArm, Is.Not.EqualTo(default(EntityUid)));
                entMan.EnsureComponent<CMIncisionOpenComponent>(leftArm);
                entMan.EnsureComponent<CMBleedersClampedComponent>(leftArm);
                entMan.EnsureComponent<CMSkinRetractedComponent>(leftArm);
                entMan.EnsureComponent<InternalBleedingComponent>(leftArm);

                var armed = flow.TryArmStep(
                    surgeon,
                    patient,
                    leftArm,
                    "CMUSurgeryCauterizeInternalBleeding",
                    0,
                    BodyPartType.Arm,
                    BodyPartSymmetry.Left);

                Assert.That(armed, Is.Not.Null);
                Assert.That(armed!.RequiredToolCategory, Is.EqualTo("hemostat"));
                Assert.That(
                    flow.TryHandleArmedToolUse(
                        patient,
                        armed,
                        surgeon,
                        scalpel,
                        leftArm,
                        out var handled,
                        out var started),
                    Is.False);
                Assert.Multiple(() =>
                {
                    Assert.That(handled, Is.False);
                    Assert.That(started, Is.False);
                    Assert.That(entMan.GetComponent<CMUSurgeryArmedStepComponent>(patient).Surgeon, Is.EqualTo(surgeon));
                });

                Assert.That(dispatch.TryDispatch(surgeon, patient, scalpel), Is.True);

                var currentArmed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(patient);
                var state = flow.BuildBuiState(
                    patient,
                    "Patient",
                    dispatch.BuildPartEntries(patient, surgeon),
                    currentArmed,
                    surgeon);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUSurgeryWindowOpenComponent>(surgeon), Is.True);
                    Assert.That(currentArmed.Surgeon, Is.EqualTo(surgeon));
                    Assert.That(state.CurrentArmedStep, Is.Not.Null);
                    Assert.That(state.CurrentArmedStep!.ToolCategory, Is.EqualTo("hemostat"));
                });
            }
            finally
            {
                entMan.DeleteEntity(scalpel);
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(surgeon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuFreshSurgeryToolUseWithSelectedPartOpensUiInsteadOfAutoStarting()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var standing = entMan.System<StandingStateSystem>();
            var skills = entMan.System<SkillsSystem>();
            var targeting = entMan.System<SharedBodyZoneTargetingSystem>();

            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var scalpel = entMan.SpawnEntity("CMScalpel", MapCoordinates.Nullspace);

            try
            {
                skills.SetSkill(surgeon, "RMCSkillSurgery", 3);
                standing.Down(patient, playSound: false, dropHeldItems: false, force: true);
                targeting.SelectZone((surgeon, null), TargetBodyZone.LeftArm);

                Assert.That(dispatch.TryDispatch(surgeon, patient, scalpel), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUSurgeryWindowOpenComponent>(surgeon), Is.True);
                    Assert.That(entMan.HasComponent<CMUSurgeryArmedStepComponent>(patient), Is.False);
                    Assert.That(entMan.HasComponent<CMUSurgeryInProgressComponent>(patient), Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(scalpel);
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(surgeon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuLimbReattachOpenSocketProgressesPastScalpel()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid patient = default;
        EntityUid surgeon = default;
        EntityUid leftArm = default;
        EntityUid socketAnchor = default;
        EntityUid scalpel = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var standing = entMan.System<StandingStateSystem>();
            var skills = entMan.System<SkillsSystem>();
            var xform = entMan.System<SharedTransformSystem>();

            patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            scalpel = entMan.SpawnEntity("CMScalpel", MapCoordinates.Nullspace);

            skills.SetSkill(surgeon, "RMCSkillSurgery", 3);
            standing.Down(patient, playSound: false, dropHeldItems: false, force: true);

            var root = body.GetRootPartOrNull(patient);
            Assert.That(root, Is.Not.Null);
            socketAnchor = root!.Value.Entity;

            foreach (var (partUid, part) in body.GetBodyChildren(patient))
            {
                if (part.PartType != BodyPartType.Arm || part.Symmetry != BodyPartSymmetry.Left)
                    continue;

                leftArm = partUid;
                break;
            }

            Assert.That(leftArm, Is.Not.EqualTo(default(EntityUid)));

            xform.DetachEntity(leftArm, entMan.GetComponent<TransformComponent>(leftArm));

            var armed = flow.TryArmStep(
                surgeon,
                patient,
                socketAnchor,
                "CMUSurgeryReattachLimb",
                0,
                BodyPartType.Arm,
                BodyPartSymmetry.Left);

            Assert.That(armed, Is.Not.Null);
            Assert.That(armed!.RequiredToolCategory, Is.EqualTo("scalpel"));
            Assert.That(flow.TryHandleArmedToolUse(patient, armed, surgeon, scalpel, socketAnchor, out var handled, out var started), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(handled, Is.True);
                Assert.That(started, Is.True);
            });
        });

        await server.WaitRunTicks(120);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var armed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(patient);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<CMIncisionOpenComponent>(socketAnchor), Is.True);
                Assert.That(entMan.HasComponent<CMIncisionOpenComponent>(patient), Is.False);
                Assert.That(armed.LeafSurgeryId, Is.EqualTo("CMUSurgeryReattachLimb"));
                Assert.That(armed.SurgeryId, Is.EqualTo("CMUSurgeryOpenSoftTissue"));
                Assert.That(armed.StepIndex, Is.EqualTo(1));
                Assert.That(armed.RequiredToolCategory, Is.EqualTo("hemostat"));
            });

            entMan.DeleteEntity(leftArm);
            entMan.DeleteEntity(scalpel);
            entMan.DeleteEntity(patient);
            entMan.DeleteEntity(surgeon);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RMCExplosionPrototypesKeepBaseDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();
        var proto = server.ResolveDependency<IPrototypeManager>();

        Assert.Multiple(() =>
        {
            AssertExplosionDamage(proto, "RMC", 5f, 5f);
            AssertExplosionDamage(proto, "RMCMortar", 6.25f, 6.25f);
            AssertExplosionDamage(proto, "RMCOB", 5f, 5f);
            AssertExplosionDamage(proto, "RMCOBXenoTunnel", 5f, 5f);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RMCExplosionCreatesCmuBlastWoundsOnMultipleParts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var damage = new DamageSpecifier
                {
                    DamageDict =
                    {
                        ["Blunt"] = FixedPoint2.New(50),
                        ["Heat"] = FixedPoint2.New(50),
                    },
                };

                var explosion = new ExplosionReceivedEvent("RMC", MapCoordinates.Nullspace, damage);
                entMan.EventBus.RaiseLocalEvent(human, ref explosion);

                var woundedParts = 0;
                foreach (var (partUid, _) in body.GetBodyChildren(human))
                {
                    if (entMan.TryGetComponent<BodyPartWoundComponent>(partUid, out var wounds) &&
                        wounds.Wounds.Count > 0)
                    {
                        woundedParts++;
                    }
                }

                Assert.That(woundedParts, Is.GreaterThanOrEqualTo(3));
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuExplosionBlastWoundsUsePatientOrientation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var transform = entMan.System<SharedTransformSystem>();
            var frontHit = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var rearHit = entMan.SpawnEntity("CMMobHuman", map.GridCoords);

            try
            {
                transform.SetWorldRotation(frontHit, Angle.Zero);
                transform.SetWorldRotation(rearHit, Angle.Zero);

                var frontHead = GetBodyPart(entMan, frontHit, BodyPartType.Head, BodyPartSymmetry.None);
                var rearHead = GetBodyPart(entMan, rearHit, BodyPartType.Head, BodyPartSymmetry.None);
                var frontBefore = entMan.GetComponent<BodyPartHealthComponent>(frontHead).Current;
                var rearBefore = entMan.GetComponent<BodyPartHealthComponent>(rearHead).Current;
                var forward = transform.GetWorldRotation(frontHit).ToWorldVec();
                var frontCoords = transform.GetMapCoordinates(frontHit);
                var rearCoords = transform.GetMapCoordinates(rearHit);
                // Keep exposure near the neutral normal-damage correction point so this isolates blast propagation weighting.
                const float neutralCorrectionDistance = 6.45f;
                var damage = new DamageSpecifier
                {
                    DamageDict =
                    {
                        ["Blunt"] = FixedPoint2.New(45),
                        ["Heat"] = FixedPoint2.New(45),
                    },
                };

                var frontExplosion = new ExplosionReceivedEvent("RMC", new MapCoordinates(frontCoords.Position + forward * neutralCorrectionDistance, frontCoords.MapId), damage);
                entMan.EventBus.RaiseLocalEvent(frontHit, ref frontExplosion);

                var rearExplosion = new ExplosionReceivedEvent("RMC", new MapCoordinates(rearCoords.Position - forward * neutralCorrectionDistance, rearCoords.MapId), damage);
                entMan.EventBus.RaiseLocalEvent(rearHit, ref rearExplosion);

                var frontAfter = entMan.GetComponent<BodyPartHealthComponent>(frontHead).Current;
                var rearAfter = entMan.GetComponent<BodyPartHealthComponent>(rearHead).Current;
                var frontLoss = frontBefore - frontAfter;
                var rearLoss = rearBefore - rearAfter;

                Assert.That(frontLoss, Is.GreaterThan(rearLoss));
            }
            finally
            {
                entMan.DeleteEntity(frontHit);
                entMan.DeleteEntity(rearHit);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuExplosionRebalancesNormalDamageByExposure()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var damageable = entMan.System<DamageableSystem>();
            var closeHuman = entMan.SpawnEntity("CMMobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
            var farHuman = entMan.SpawnEntity("CMMobHuman", new MapCoordinates(Vector2.Zero, map.MapId));

            try
            {
                var damage = new DamageSpecifier
                {
                    DamageDict =
                    {
                        ["Blunt"] = FixedPoint2.New(40),
                        ["Heat"] = FixedPoint2.New(40),
                    },
                };

                damageable.TryChangeDamage(closeHuman, damage, ignoreResistances: true);
                var closeExplosion = new ExplosionReceivedEvent("RMC", new MapCoordinates(new Vector2(1, 0), map.MapId), damage);
                entMan.EventBus.RaiseLocalEvent(closeHuman, ref closeExplosion);

                damageable.TryChangeDamage(farHuman, damage, ignoreResistances: true);
                var farExplosion = new ExplosionReceivedEvent("RMC", new MapCoordinates(new Vector2(7, 0), map.MapId), damage);
                entMan.EventBus.RaiseLocalEvent(farHuman, ref farExplosion);

                var closeTotal = entMan.GetComponent<DamageableComponent>(closeHuman).TotalDamage;
                var farTotal = entMan.GetComponent<DamageableComponent>(farHuman).TotalDamage;

                Assert.That(closeTotal, Is.GreaterThan(farTotal));
            }
            finally
            {
                entMan.DeleteEntity(closeHuman);
                entMan.DeleteEntity(farHuman);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CmuExplosionCreatesShrapnelForHighExposureBlast()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var human = entMan.SpawnEntity("CMMobHuman", new MapCoordinates(Vector2.Zero, map.MapId));

            try
            {
                var damage = new DamageSpecifier
                {
                    DamageDict =
                    {
                        ["Blunt"] = FixedPoint2.New(60),
                        ["Heat"] = FixedPoint2.New(60),
                    },
                };

                var explosion = new ExplosionReceivedEvent("RMC", new MapCoordinates(new Vector2(1, 0), map.MapId), damage);
                entMan.EventBus.RaiseLocalEvent(human, ref explosion);

                var fragments = 0;
                foreach (var (partUid, _) in body.GetBodyChildren(human))
                {
                    if (entMan.TryGetComponent<CMUShrapnelComponent>(partUid, out var shrapnel))
                        fragments += shrapnel.Fragments;
                }

                Assert.That(fragments, Is.GreaterThan(0));
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HumanAndSynthExplosionResistanceAppliesVulnerability()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var synth = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var humanSynth = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var other = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<SynthComponent>(synth);
                entMan.EnsureComponent<SynthComponent>(humanSynth);

                Assert.Multiple(() =>
                {
                    AssertExplosionCoefficient(entMan, human, 2.25f, "CMU human");
                    AssertExplosionCoefficient(entMan, synth, 2.25f, "Synth");
                    AssertExplosionCoefficient(entMan, humanSynth, 2.25f, "CMU synth");
                    AssertExplosionCoefficient(entMan, other, 1f, "Unmarked entity");
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
                entMan.DeleteEntity(synth);
                entMan.DeleteEntity(humanSynth);
                entMan.DeleteEntity(other);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertExplosionDamage(IPrototypeManager proto, string id, float blunt, float heat)
    {
        var explosion = proto.Index<ExplosionPrototype>(id);
        Assert.That(explosion.DamagePerIntensity.DamageDict["Blunt"], Is.EqualTo((FixedPoint2) blunt), $"{id} Blunt damage");
        Assert.That(explosion.DamagePerIntensity.DamageDict["Heat"], Is.EqualTo((FixedPoint2) heat), $"{id} Heat damage");
    }

    private static int CountPuddles(IEntityManager entMan)
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<PuddleComponent>();
        while (query.MoveNext(out _, out _))
        {
            count++;
        }

        return count;
    }

    private static EntityUid GetFirstBodyPart(IEntityManager entMan, EntityUid bodyUid)
    {
        var body = entMan.System<SharedBodySystem>();
        foreach (var (partUid, _) in body.GetBodyChildren(bodyUid))
        {
            if (entMan.HasComponent<BodyPartComponent>(partUid))
                return partUid;
        }

        Assert.Fail("Expected CMU human to have at least one body part.");
        return EntityUid.Invalid;
    }

    private static EntityUid GetBodyPart(IEntityManager entMan, EntityUid bodyUid, BodyPartType type, BodyPartSymmetry symmetry)
    {
        var body = entMan.System<SharedBodySystem>();
        foreach (var (partUid, part) in body.GetBodyChildren(bodyUid))
        {
            if (part.PartType != type || part.Symmetry != symmetry)
                continue;

            return partUid;
        }

        Assert.Fail($"Expected CMU human to have {symmetry} {type}.");
        return EntityUid.Invalid;
    }

    private static void AddBodyPartWound(
        IEntityManager entMan,
        EntityUid part,
        WoundType type,
        FixedPoint2? damage = null,
        WoundCleanupFlags cleanup = WoundCleanupFlags.None,
        WoundSize size = WoundSize.Deep)
    {
        var wounds = entMan.EnsureComponent<BodyPartWoundComponent>(part);
        GetField<List<Wound>>(wounds, nameof(BodyPartWoundComponent.Wounds)).Add(new Wound(damage ?? FixedPoint2.New(10), FixedPoint2.Zero, 0f, null, type, false));
        GetField<List<WoundSize>>(wounds, nameof(BodyPartWoundComponent.Sizes)).Add(size);
        GetField<List<int>>(wounds, nameof(BodyPartWoundComponent.Bandages)).Add(0);
        GetField<List<WoundMechanism>>(wounds, nameof(BodyPartWoundComponent.Mechanisms)).Add(type == WoundType.Burn ? WoundMechanism.Burn : WoundMechanism.Generic);
        GetField<List<WoundMechanismFlags>>(wounds, nameof(BodyPartWoundComponent.SecondaryMechanisms)).Add(WoundMechanismFlags.None);
        GetField<List<WoundTreatmentQuality>>(wounds, nameof(BodyPartWoundComponent.TreatmentQualities)).Add(WoundTreatmentQuality.Untreated);
        GetField<List<WoundCleanupFlags>>(wounds, nameof(BodyPartWoundComponent.Cleanup)).Add(cleanup);
    }

    private static int CountTreatedWounds(BodyPartWoundComponent comp, WoundType type)
    {
        var count = 0;
        foreach (var wound in GetField<List<Wound>>(comp, nameof(BodyPartWoundComponent.Wounds)))
        {
            if (wound.Type == type && wound.Treated)
                count++;
        }

        return count;
    }

    private static T GetField<T>(BodyPartWoundComponent comp, string name)
        => (T) typeof(BodyPartWoundComponent).GetField(name, BindingFlags.Instance | BindingFlags.Public)!.GetValue(comp)!;

    private static void SetField<T>(BodyPartWoundComponent comp, string name, T value)
        => typeof(BodyPartWoundComponent).GetField(name, BindingFlags.Instance | BindingFlags.Public)!.SetValue(comp, value);

    private static FixedPoint2 DamageInGroup(IPrototypeManager prototypes, DamageSpecifier damage, string groupId)
    {
        var group = prototypes.Index<DamageGroupPrototype>(groupId);
        return damage.TryGetDamageInGroup(group, out var total) ? total : FixedPoint2.Zero;
    }

    private static void AssertExplosionCoefficient(IEntityManager entMan, EntityUid entity, float expected, string message)
    {
        var ev = new GetExplosionResistanceEvent("RMC");
        entMan.EventBus.RaiseLocalEvent(entity, ref ev);

        Assert.That(ev.DamageCoefficient, Is.EqualTo(expected).Within(0.001f), message);
    }
}
