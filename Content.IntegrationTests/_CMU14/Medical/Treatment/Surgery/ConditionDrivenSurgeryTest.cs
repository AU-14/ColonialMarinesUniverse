#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Server.CMU14.Medical.Injuries.Wounds;
using Content.Server.CMU14.Medical.Treatment.Surgery;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Eyes;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Kidneys;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Liver;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Stomach;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared.CMU14.Medical.Treatment.Surgery.Markers;
using Content.Shared.CMU14.Medical.Treatment.Surgery.Traits;
using Content.Shared.CMU14.Medical.Injuries.Shrapnel;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.StatusEffectNew;
using Content.Shared.Standing;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Medical.Treatment.Surgery;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public sealed class ConditionDrivenSurgeryTest
{

    [Test]
    public async Task BruteTreatmentCancelsUiLessAmputationAtShallowAccess()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bandages = entMan.System<CMUBandageInterceptionSystem>();
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var flow = entMan.System<SharedCMUSurgeryFlowSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var targeting = entMan.System<SharedBodyZoneTargetingSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var drill = entMan.SpawnEntity("CMSurgicalDrill", MapCoordinates.Nullspace);
            var traumaKit = entMan.SpawnEntity("CMTraumaKit10", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
                entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);
                targeting.SelectZone((surgeon, null), TargetBodyZone.RightArm);
                Assert.That(hands.TryPickupAnyHand(surgeon, drill, checkActionBlocker: false), Is.True);

                var arm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);
                entMan.EnsureComponent<CMIncisionOpenComponent>(arm);
                entMan.EnsureComponent<CMSkinRetractedComponent>(arm);

                Assert.That(dispatch.TryDispatchUiLess(surgeon, human, drill), Is.True);
                Assert.That(entMan.GetComponent<CMUSurgeryArmedStepComponent>(human).LeafSurgeryId,
                    Is.EqualTo("CMUSurgeryRemoveLimb"));

                targeting.SelectZone((surgeon, null), TargetBodyZone.Chest);
                var wrongPartInteract = new AfterInteractEvent(surgeon, traumaKit, human, default, true);
                bandages.HandleAfterInteract(surgeon, ref wrongPartInteract);
                Assert.That(entMan.HasComponent<CMUSurgeryArmedStepComponent>(human), Is.True);

                targeting.SelectZone((surgeon, null), TargetBodyZone.RightArm);
                var interact = new AfterInteractEvent(surgeon, traumaKit, human, default, true);
                bandages.HandleAfterInteract(surgeon, ref interact);

                Assert.Multiple(() =>
                {
                    Assert.That(interact.Handled, Is.True);
                    Assert.That(entMan.HasComponent<CMUSurgeryArmedStepComponent>(human), Is.False);
                    Assert.That(entMan.HasComponent<CMUSurgeryInProgressComponent>(human), Is.False);
                    Assert.That(flow.GetSiteState(arm).Access, Is.EqualTo(CMUSurgicalAccess.Shallow));
                    Assert.That(entMan.EntityExists(arm), Is.True);
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
                entMan.DeleteEntity(surgeon);
                entMan.DeleteEntity(drill);
                entMan.DeleteEntity(traumaKit);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SawingHealthyCavityCreatesFractureAndRepairReturnsToShallowAccess()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
                entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso, BodyPartSymmetry.None);
                OpenSoftTissue(entMan, torso);

                var armed = flow.TryArmExactStep(
                    surgeon,
                    human,
                    torso,
                    "CMUSurgeryOpenBoneCavity",
                    0,
                    BodyPartType.Torso,
                    BodyPartSymmetry.None);

                Assert.That(armed, Is.Not.Null);
                Assert.That(flow.TryCompleteAutomatedStep(human, armed!, surgeon), Is.True);
                Assert.That(entMan.GetComponent<FractureComponent>(torso).Severity, Is.EqualTo(FractureSeverity.Simple));

                armed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(human);
                Assert.That(flow.TryCompleteAutomatedStep(human, armed, surgeon), Is.True);
                Assert.That(flow.GetSiteState(torso).Access, Is.EqualTo(CMUSurgicalAccess.Deep));

                armed = flow.TryArmStep(
                    surgeon,
                    human,
                    torso,
                    "CMUSurgerySetSimpleFractureCavity",
                    0,
                    BodyPartType.Torso,
                    BodyPartSymmetry.None);
                Assert.That(armed, Is.Not.Null);
                Assert.That(flow.TryCompleteAutomatedStep(human, armed!, surgeon), Is.True);

                armed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(human);
                Assert.That(flow.TryCompleteAutomatedStep(human, armed, surgeon), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<FractureComponent>(torso), Is.False);
                    Assert.That(entMan.HasComponent<CMRibcageOpenComponent>(torso), Is.False);
                    Assert.That(entMan.HasComponent<CMRibcageSawedComponent>(torso), Is.False);
                    Assert.That(flow.GetSiteState(torso).Access, Is.EqualTo(CMUSurgicalAccess.Shallow));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
                entMan.DeleteEntity(surgeon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClosingUnclampedIncisionCreatesPersistentInternalBleeding()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var wounds = entMan.System<SharedCMUWoundsSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
                entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso, BodyPartSymmetry.None);
                entMan.EnsureComponent<CMIncisionOpenComponent>(torso);
                entMan.EnsureComponent<CMSkinRetractedComponent>(torso);

                var armed = flow.TryArmExactStep(
                    surgeon,
                    human,
                    torso,
                    "CMUSurgeryCloseIncision",
                    0,
                    BodyPartType.Torso,
                    BodyPartSymmetry.None);

                Assert.That(armed, Is.Not.Null);
                Assert.That(flow.TryCompleteAutomatedStep(human, armed!, surgeon), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMIncisionOpenComponent>(torso), Is.False);
                    Assert.That(entMan.HasComponent<CMUSurgicalInternalBleedingComponent>(torso), Is.True);
                    Assert.That(entMan.TryGetComponent<InternalBleedingComponent>(torso, out var bleeding), Is.True);
                    Assert.That(bleeding!.Source, Is.EqualTo("surgical:unclamped-incision"));
                });

                wounds.SuppressInternalBleed(torso);
                wounds.RecomputeInternalBleed(torso);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUSurgicalInternalBleedingComponent>(torso), Is.False);
                    Assert.That(entMan.HasComponent<InternalBleedingComponent>(torso), Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
                entMan.DeleteEntity(surgeon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(BodyPartType.Hand, BodyPartType.Arm, TargetBodyZone.RightHand)]
    [TestCase(BodyPartType.Foot, BodyPartType.Leg, TargetBodyZone.RightFoot)]
    public async Task UiLessReattachesAndClosesSelectedMissingExtremity(
        BodyPartType extremityType,
        BodyPartType anchorType,
        TargetBodyZone selectedZone)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid patient = default;
        EntityUid surgeon = default;
        EntityUid scalpel = default;
        EntityUid retractor = default;
        EntityUid drill = default;
        EntityUid hemostat = default;
        EntityUid cautery = default;
        EntityUid anchor = default;
        EntityUid extremity = default;
        EntityUid detachedBody = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var sessions = entMan.System<CMUSurgerySessionSystem>();
            var targeting = entMan.System<SharedBodyZoneTargetingSystem>();

            patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            scalpel = entMan.SpawnEntity("CMScalpel", MapCoordinates.Nullspace);
            retractor = entMan.SpawnEntity("CMRetractor", MapCoordinates.Nullspace);
            drill = entMan.SpawnEntity("CMSurgicalDrill", MapCoordinates.Nullspace);
            hemostat = entMan.SpawnEntity("CMHemostat", MapCoordinates.Nullspace);
            cautery = entMan.SpawnEntity("CMCautery", MapCoordinates.Nullspace);
            anchor = GetBodyPart(entMan, patient, anchorType, BodyPartSymmetry.Right);
            extremity = GetBodyPart(entMan, patient, extremityType, BodyPartSymmetry.Right);

            entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
            entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(patient);
            detachedBody = DetachBodyPart(entMan, extremity);
            targeting.SelectZone((surgeon, null), selectedZone);
            Assert.That(hands.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false), Is.True);

            Assert.That(dispatch.TryDispatchUiLess(surgeon, patient, scalpel), Is.True);
            Assert.That(sessions.TryGetSession(patient, out var session), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(session.Phase, Is.EqualTo(CMUSurgerySessionPhase.Performing));
                Assert.That(session.Site.Type, Is.EqualTo(extremityType));
                Assert.That(session.Site.Symmetry, Is.EqualTo(BodyPartSymmetry.Right));
                Assert.That(session.ActiveTarget, Is.EqualTo(anchor));
            });
        });

        await pair.RunTicksSync(100);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var armed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(patient);
            Assert.That(armed.RequiredToolCategory, Is.EqualTo("retractor"));
            Assert.That(hands.IsHolding(surgeon, scalpel, out var scalpelHand), Is.True);
            hands.DoDrop(surgeon, scalpelHand, doDropInteraction: false, log: false);
            Assert.That(hands.TryPickupAnyHand(surgeon, retractor, checkActionBlocker: false), Is.True);
            Assert.That(dispatch.TryDispatchUiLess(surgeon, patient, retractor), Is.True);
        });

        await pair.RunTicksSync(100);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var sessions = entMan.System<CMUSurgerySessionSystem>();
            var armed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(patient);
            Assert.That(armed.RequiredToolCategory, Is.EqualTo("bone_saw"));

            Assert.That(hands.IsHolding(surgeon, retractor, out var retractorHand), Is.True);
            hands.DoDrop(surgeon, retractorHand, doDropInteraction: false, log: false);
            Assert.That(hands.TryPickupAnyHand(surgeon, drill, checkActionBlocker: false), Is.True);
            Assert.That(dispatch.TryDispatchUiLess(surgeon, patient, drill), Is.True);
            Assert.That(sessions.TryGetSession(patient, out var session), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(session.Phase, Is.EqualTo(CMUSurgerySessionPhase.Performing));
                Assert.That(session.Site.Type, Is.EqualTo(extremityType));
                Assert.That(session.Site.Symmetry, Is.EqualTo(BodyPartSymmetry.Right));
                Assert.That(session.ActiveTarget, Is.EqualTo(anchor));
            });
        });

        await pair.RunTicksSync(100);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var armed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(patient);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<CMUStumpRemovedComponent>(anchor), Is.True);
                Assert.That(armed.RequiredToolCategory, Is.EqualTo("hemostat"));
                Assert.That(armed.TargetPartType, Is.EqualTo(extremityType));
            });

            Assert.That(hands.IsHolding(surgeon, drill, out var drillHand), Is.True);
            hands.DoDrop(surgeon, drillHand, doDropInteraction: false, log: false);
            Assert.That(hands.TryPickupAnyHand(surgeon, hemostat, checkActionBlocker: false), Is.True);
            Assert.That(dispatch.TryDispatchUiLess(surgeon, patient, hemostat), Is.True);
        });

        await pair.RunTicksSync(100);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<SharedCMUSurgeryFlowSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var armed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(patient);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<CMUReattachPreppedComponent>(anchor), Is.True);
                Assert.That(armed.RequiredToolCategory, Is.EqualTo("severed_limb"));
                Assert.That(armed.TargetPartType, Is.EqualTo(extremityType));
            });

            Assert.That(hands.IsHolding(surgeon, hemostat, out var hemostatHand), Is.True);
            hands.DoDrop(surgeon, hemostatHand, doDropInteraction: false, log: false);
            Assert.That(hands.TryPickupAnyHand(surgeon, detachedBody, checkActionBlocker: false), Is.True);
            Assert.That(
                flow.TryHandleArmedToolUse(patient, armed, surgeon, detachedBody, patient, out var handled, out var started),
                Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(handled, Is.True);
                Assert.That(started, Is.True);
            });
        });

        await pair.RunTicksSync(100);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var medicalIndex = entMan.System<CMUMedicalBodyIndexSystem>();
            var sessions = entMan.System<CMUSurgerySessionSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(
                    medicalIndex.TryGetBodyPart(
                        patient,
                        new CMUMedicalBodyPartKey(extremityType, BodyPartSymmetry.Right),
                        out var attachedExtremity),
                    Is.True);
                Assert.That(attachedExtremity, Is.EqualTo(extremity));
                Assert.That(entMan.EntityExists(detachedBody), Is.False);
                Assert.That(entMan.HasComponent<CMUSurgeryArmedStepComponent>(patient), Is.False);
                Assert.That(sessions.TryGetSession(patient, out var awaitingSession), Is.True);
                Assert.That(awaitingSession.Phase, Is.EqualTo(CMUSurgerySessionPhase.AwaitingDecision));
            });

            Assert.That(hands.TryPickupAnyHand(surgeon, cautery, checkActionBlocker: false), Is.True);
            Assert.That(dispatch.TryDispatchUiLess(surgeon, patient, cautery), Is.True);
            var armed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(patient);
            Assert.Multiple(() =>
            {
                Assert.That(armed.LeafSurgeryId, Is.EqualTo("CMUSurgeryReattachLimb"));
                Assert.That(armed.SurgeryId, Is.EqualTo("CMUSurgeryReattachLimb"));
                Assert.That(armed.StepIndex, Is.EqualTo(3));
                Assert.That(armed.RequiredToolCategory, Is.EqualTo("cautery"));
                Assert.That(armed.TargetPartType, Is.EqualTo(extremityType));
                Assert.That(sessions.TryGetSession(patient, out var closingSession), Is.True);
                Assert.That(closingSession.Phase, Is.EqualTo(CMUSurgerySessionPhase.Performing));
            });
        });

        await pair.RunTicksSync(100);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<CMIncisionOpenComponent>(extremity), Is.False);
                Assert.That(entMan.HasComponent<CMSkinRetractedComponent>(extremity), Is.False);
                Assert.That(entMan.HasComponent<CMUStumpRemovedComponent>(extremity), Is.False);
                Assert.That(entMan.HasComponent<CMUReattachPreppedComponent>(extremity), Is.False);
                Assert.That(entMan.HasComponent<CMUReattachCompleteComponent>(extremity), Is.False);
                Assert.That(entMan.HasComponent<CMUSurgeryArmedStepComponent>(patient), Is.False);
                Assert.That(entMan.HasComponent<CMUSurgeryInProgressComponent>(patient), Is.False);
                Assert.That(entMan.HasComponent<CMUSurgerySessionComponent>(patient), Is.False);
            });
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            if (entMan.EntityExists(detachedBody))
                entMan.DeleteEntity(detachedBody);
            entMan.DeleteEntity(patient);
            entMan.DeleteEntity(surgeon);
            entMan.DeleteEntity(scalpel);
            entMan.DeleteEntity(retractor);
            entMan.DeleteEntity(drill);
            entMan.DeleteEntity(hemostat);
            entMan.DeleteEntity(cautery);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TargetedMeleeHitShattersRetractedChestAndProvidesDeepAccess()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<SharedCMUSurgeryFlowSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso, BodyPartSymmetry.None);
                entMan.EnsureComponent<CMIncisionOpenComponent>(torso);
                entMan.EnsureComponent<CMSkinRetractedComponent>(torso);

                Assert.That(flow.GetSiteState(torso).Access, Is.EqualTo(CMUSurgicalAccess.Shallow));

                var hit = new DamageSpecifier();
                hit.DamageDict["Blunt"] = FixedPoint2.New(1);
                Assert.That(
                    partHealth.TryApplyPartDamage(
                        human,
                        torso,
                        hit,
                        impact: DamageImpact.ForMelee(hit),
                        targetZone: TargetBodyZone.Chest),
                    Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<FractureComponent>(torso).Severity,
                        Is.EqualTo(FractureSeverity.Shattered));
                    Assert.That(entMan.GetComponent<BoneComponent>(torso).Integrity, Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(flow.GetSiteState(torso).Access, Is.EqualTo(CMUSurgicalAccess.Deep));
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
    public async Task SimpleFractureRepairAdvancesToClosureAfterBoneGel()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var fracture = entMan.System<SharedFractureSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
                entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);

                var arm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);
                OpenSoftTissue(entMan, arm);

                var frac = entMan.EnsureComponent<FractureComponent>(arm);
                fracture.SetSeverity((arm, frac), FractureSeverity.Simple);

                var armed = flow.TryArmStep(
                    surgeon,
                    human,
                    arm,
                    "CMUSurgerySetSimpleFracture",
                    0,
                    BodyPartType.Arm,
                    BodyPartSymmetry.Right);

                Assert.That(armed, Is.Not.Null);
                Assert.That(armed!.RequiredToolCategory, Is.EqualTo("bone_setter"));

                Assert.That(flow.TryCompleteAutomatedStep(human, armed, surgeon), Is.True);

                armed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(human);
                Assert.That(armed.RequiredToolCategory, Is.EqualTo("bone_gel"));

                Assert.That(flow.TryCompleteAutomatedStep(human, armed, surgeon), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUSurgeryArmedStepComponent>(human), Is.False);
                    Assert.That(entMan.HasComponent<FractureComponent>(arm), Is.False);
                    Assert.That(entMan.GetComponent<CMUSurgeryInProgressComponent>(human).AwaitingClosureChoice, Is.True);
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
                entMan.DeleteEntity(surgeon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShatteredFractureContaminatedCleanupBeforeFinalSetDoesNotRepeatBoneGel()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var fracture = entMan.System<SharedFractureSystem>();
            var traits = entMan.System<SharedCMUSurgicalTraitSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
                entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);

                var arm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);
                OpenSoftTissue(entMan, arm);

                var frac = entMan.EnsureComponent<FractureComponent>(arm);
                fracture.SetSeverity((arm, frac), FractureSeverity.Shattered);
                ClearSurgicalTraits(traits, arm);

                var armed = ArmStep(
                    flow,
                    surgeon,
                    human,
                    arm,
                    "CMUSurgerySetShatteredFracture",
                    BodyPartType.Arm,
                    BodyPartSymmetry.Right,
                    "shattered fracture with late contamination");

                armed = CompleteExpectedStep(
                    entMan,
                    flow,
                    human,
                    surgeon,
                    armed,
                    "bone_setter",
                    "realign shattered fracture")!;
                armed = CompleteInjectedCleanupsUntilLeaf(entMan, flow, traits, human, surgeon, arm, armed, "CMUSurgerySetShatteredFracture");
                ClearSurgicalTraits(traits, arm);

                armed = CompleteExpectedStep(
                    entMan,
                    flow,
                    human,
                    surgeon,
                    armed,
                    "bone_gel",
                    "apply shattered bone gel")!;
                ClearSurgicalTraits(traits, arm);

                armed = CompleteExpectedStep(
                    entMan,
                    flow,
                    human,
                    surgeon,
                    armed,
                    "bone_graft",
                    "insert bone graft")!;

                Assert.Multiple(() =>
                {
                    Assert.That(armed.SurgeryId, Is.EqualTo("CMUSurgerySetShatteredFracture"));
                    Assert.That(armed.StepIndex, Is.EqualTo(3));
                    Assert.That(armed.RequiredToolCategory, Is.EqualTo("bone_setter"));
                });

                traits.EnsureTrait(arm, CMUSurgicalTrait.ContaminatedWound);

                Assert.That(flow.TryCompleteAutomatedStep(human, armed, surgeon), Is.False,
                    "Rearming cleanup does not commit the original final bone-setting step.");
                armed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(human);
                Assert.Multiple(() =>
                {
                    Assert.That(armed.SurgeryId, Is.EqualTo("CMUSurgeryDebrideContaminatedWound"));
                    Assert.That(armed.RequiredToolCategory, Is.EqualTo("scalpel"));
                });

                armed = CompleteExpectedStep(
                    entMan,
                    flow,
                    human,
                    surgeon,
                    armed,
                    "scalpel",
                    "debride contaminated tissue after bone graft")!;

                Assert.Multiple(() =>
                {
                    Assert.That(traits.HasTrait(arm, CMUSurgicalTrait.ContaminatedWound), Is.False);
                    Assert.That(armed.SurgeryId, Is.EqualTo("CMUSurgerySetShatteredFracture"));
                    Assert.That(armed.StepIndex, Is.EqualTo(3));
                    Assert.That(armed.RequiredToolCategory, Is.EqualTo("bone_setter"));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
                entMan.DeleteEntity(surgeon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InjectedCleanupReturnsToSelectedSurgeryAfterCompletion()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var fracture = entMan.System<SharedFractureSystem>();
            var traits = entMan.System<SharedCMUSurgicalTraitSystem>();
            var wounds = entMan.System<SharedCMUWoundsSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
                entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);

                var arm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);
                OpenSoftTissue(entMan, arm);

                var frac = entMan.EnsureComponent<FractureComponent>(arm);
                fracture.SetSeverity((arm, frac), FractureSeverity.Simple);

                var armed = ArmStep(
                    flow,
                    surgeon,
                    human,
                    arm,
                    "CMUSurgerySetSimpleFracture",
                    BodyPartType.Arm,
                    BodyPartSymmetry.Right,
                    "simple fracture with injected torn vessel");
                Assert.That(armed.RequiredToolCategory, Is.EqualTo("bone_setter"));

                traits.EnsureTrait(arm, CMUSurgicalTrait.VascularTear);
                wounds.SeedInternalBleed(arm, "test:vascular-tear", 0.5f);

                Assert.That(flow.TryCompleteAutomatedStep(human, armed, surgeon), Is.False,
                    "Rearming an injected prerequisite does not commit the stale step.");

                armed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(human);
                Assert.Multiple(() =>
                {
                    Assert.That(armed.SurgeryId, Is.EqualTo("CMUSurgeryTieVascularTear"));
                    Assert.That(armed.RequiredToolCategory, Is.EqualTo("hemostat"));
                    Assert.That(traits.HasTrait(arm, CMUSurgicalTrait.VascularTear), Is.True);
                    Assert.That(entMan.HasComponent<InternalBleedingComponent>(arm), Is.True);
                });

                armed = CompleteExpectedStep(
                    entMan,
                    flow,
                    human,
                    surgeon,
                    armed,
                    "hemostat",
                    "torn vessel cleanup")!;

                Assert.Multiple(() =>
                {
                    Assert.That(traits.HasTrait(arm, CMUSurgicalTrait.VascularTear), Is.False);
                    Assert.That(entMan.HasComponent<InternalBleedingComponent>(arm), Is.False);
                    Assert.That(armed.SurgeryId, Is.EqualTo("CMUSurgerySetSimpleFracture"));
                    Assert.That(armed.RequiredToolCategory, Is.EqualTo("bone_setter"));
                });

                armed = CompleteExpectedStep(
                    entMan,
                    flow,
                    human,
                    surgeon,
                    armed,
                    "bone_setter",
                    "simple fracture after injected cleanup")!;

                Assert.That(armed.RequiredToolCategory, Is.EqualTo("bone_gel"));

                CompleteExpectedStep(
                    entMan,
                    flow,
                    human,
                    surgeon,
                    armed,
                    "bone_gel",
                    "simple fracture bone gel after injected cleanup");

                AssertAwaitingClosure(entMan, human, arm, "simple fracture after injected cleanup");
                Assert.That(entMan.HasComponent<FractureComponent>(arm), Is.False);
            }
            finally
            {
                entMan.DeleteEntity(human);
                entMan.DeleteEntity(surgeon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InjectedCleanupSurgeriesCompleteAndClearConditions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var traits = entMan.System<SharedCMUSurgicalTraitSystem>();
            var wounds = entMan.System<SharedCMUWoundsSystem>();
            var shrapnel = entMan.System<SharedCMUShrapnelSystem>();

            RunInjectedCleanupCase(
                entMan,
                flow,
                traits,
                "CMUSurgeryTieVascularTear",
                CMUSurgicalTrait.VascularTear,
                BodyPartType.Arm,
                BodyPartSymmetry.Right,
                "hemostat",
                part => wounds.SeedInternalBleed(part, "test:vascular-tear", 0.5f),
                part => Assert.That(entMan.HasComponent<InternalBleedingComponent>(part), Is.False));

            RunInjectedCleanupCase(
                entMan,
                flow,
                traits,
                "CMUSurgeryExtractForeignBody",
                CMUSurgicalTrait.EmbeddedForeignBody,
                BodyPartType.Arm,
                BodyPartSymmetry.Right,
                "hemostat",
                part => Assert.That(shrapnel.AddShrapnel(part, 1, 10f), Is.True),
                part => Assert.That(entMan.HasComponent<CMUShrapnelComponent>(part), Is.False));

            RunInjectedCleanupCase(
                entMan,
                flow,
                traits,
                "CMUSurgeryRelieveCompartmentPressure",
                CMUSurgicalTrait.CompartmentPressure,
                BodyPartType.Arm,
                BodyPartSymmetry.Right,
                "scalpel");

            RunInjectedCleanupCase(
                entMan,
                flow,
                traits,
                "CMUSurgeryDebrideContaminatedWound",
                CMUSurgicalTrait.ContaminatedWound,
                BodyPartType.Arm,
                BodyPartSymmetry.Right,
                "scalpel");

            RunInjectedCleanupCase(
                entMan,
                flow,
                traits,
                "CMUSurgeryRemoveBoneFragments",
                CMUSurgicalTrait.BoneSplintered,
                BodyPartType.Arm,
                BodyPartSymmetry.Right,
                "hemostat");

            RunInjectedCleanupCase(
                entMan,
                flow,
                traits,
                "CMUSurgeryFreeOrganAdhesions",
                CMUSurgicalTrait.OrganAdhesion,
                BodyPartType.Torso,
                BodyPartSymmetry.None,
                "scalpel");

            RunInjectedCleanupCase(
                entMan,
                flow,
                traits,
                "CMUSurgeryPackOrganBleed",
                CMUSurgicalTrait.OrganHemorrhage,
                BodyPartType.Torso,
                BodyPartSymmetry.None,
                "organ_clamp");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonFractureSurgeryFamiliesAdvanceAfterFunctionalStep()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var wounds = entMan.System<SharedCMUWoundsSystem>();

            RunInternalBleedCase(
                entMan,
                flow,
                wounds,
                "CMUSurgeryCauterizeInternalBleeding",
                BodyPartType.Arm,
                BodyPartSymmetry.Right,
                OpenSoftTissue);

            RunInternalBleedCase(
                entMan,
                flow,
                wounds,
                "CMUSurgeryCauterizeInternalBleeding",
                BodyPartType.Torso,
                BodyPartSymmetry.None,
                OpenSoftTissue);

            RunEscharCase(entMan, flow);
            RunAmputationCase(entMan, flow);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OrganRepairSurgeriesAdvanceToClosureAfterRepairStep()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var traits = entMan.System<SharedCMUSurgicalTraitSystem>();
            var organHealth = entMan.System<SharedOrganHealthSystem>();

            RunOrganRepairCase<LiverComponent>(
                entMan,
                flow,
                traits,
                organHealth,
                "CMUSurgeryRepairLiver",
                BodyPartType.Torso,
                BodyPartSymmetry.None,
                "organ_clamp",
                "hemostat");

            RunOrganRepairCase<LungsComponent>(
                entMan,
                flow,
                traits,
                organHealth,
                "CMUSurgeryRepairLungs",
                BodyPartType.Torso,
                BodyPartSymmetry.None,
                "organ_clamp",
                "hemostat");

            RunOrganRepairCase<KidneysComponent>(
                entMan,
                flow,
                traits,
                organHealth,
                "CMUSurgeryRepairKidneys",
                BodyPartType.Torso,
                BodyPartSymmetry.None,
                "organ_clamp",
                "hemostat");

            RunOrganRepairCase<HeartComponent>(
                entMan,
                flow,
                traits,
                organHealth,
                "CMUSurgeryRepairHeart",
                BodyPartType.Torso,
                BodyPartSymmetry.None,
                "organ_clamp",
                "hemostat");

            RunOrganRepairCase<CMUStomachComponent>(
                entMan,
                flow,
                traits,
                organHealth,
                "CMUSurgeryRepairStomach",
                BodyPartType.Torso,
                BodyPartSymmetry.None,
                "organ_clamp",
                "hemostat");

            RunOrganRepairCase<CMUBrainComponent>(
                entMan,
                flow,
                traits,
                organHealth,
                "CMUSurgeryRepairBrain",
                BodyPartType.Head,
                BodyPartSymmetry.None,
                "organ_clamp",
                "hemostat");

            RunOrganRepairCase<EyesComponent>(
                entMan,
                flow,
                traits,
                organHealth,
                "CMUSurgeryRepairEyes",
                BodyPartType.Head,
                BodyPartSymmetry.None,
                "organ_clamp",
                "hemostat");

        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RepairingFailingStoppedHeartEndsCardiacArrestTicks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;
        EntityUid surgeon = default;
        EntityUid torso = default;
        EntityUid heart = default;
        FixedPoint2 damageAfterRepair = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var organHealth = entMan.System<SharedOrganHealthSystem>();
            var traits = entMan.System<SharedCMUSurgicalTraitSystem>();

            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
            entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);

            torso = GetBodyPart(entMan, human, BodyPartType.Torso, BodyPartSymmetry.None);
            OpenBoneCavity(entMan, torso);
            ClearSurgicalTraits(traits, torso);

            heart = GetPartOrgan<HeartComponent>(entMan, torso);
            var health = entMan.GetComponent<OrganHealthComponent>(heart);
            SetPublicField(health, nameof(OrganHealthComponent.Current), FixedPoint2.New(8));
            organHealth.RecomputeStage((heart, health), human);

            var heartComp = entMan.GetComponent<HeartComponent>(heart);
            SetPublicField(heartComp, nameof(HeartComponent.StopGracePeriod), TimeSpan.Zero);

            Assert.That(health.Stage, Is.EqualTo(OrganDamageStage.Failing));
        });

        await pair.RunSeconds(8);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var status = entMan.System<StatusEffectsSystem>();
            var heartComp = entMan.GetComponent<HeartComponent>(heart);
            var damage = entMan.GetComponent<DamageableComponent>(human);

            Assert.Multiple(() =>
            {
                Assert.That(heartComp.Stopped, Is.True);
                Assert.That(status.HasStatusEffect(human, "StatusEffectCMUCardiacArrest"), Is.True);
                Assert.That(damage.TotalDamage, Is.GreaterThan(FixedPoint2.Zero));
            });
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var traits = entMan.System<SharedCMUSurgicalTraitSystem>();
            ClearSurgicalTraits(traits, torso);

            var armed = ArmStep(
                flow,
                surgeon,
                human,
                torso,
                "CMUSurgeryRepairHeart",
                BodyPartType.Torso,
                BodyPartSymmetry.None,
                "CMUSurgeryRepairHeart");

            armed = CompleteExpectedStep(entMan, flow, human, surgeon, armed, "organ_clamp", "CMUSurgeryRepairHeart")!;
            CompleteExpectedStep(entMan, flow, human, surgeon, armed, "hemostat", "CMUSurgeryRepairHeart");
            AssertAwaitingClosure(entMan, human, torso, "CMUSurgeryRepairHeart");

            damageAfterRepair = entMan.GetComponent<DamageableComponent>(human).TotalDamage;

            var heartComp = entMan.GetComponent<HeartComponent>(heart);
            var health = entMan.GetComponent<OrganHealthComponent>(heart);

            Assert.Multiple(() =>
            {
                Assert.That(health.Stage, Is.EqualTo(OrganDamageStage.Healthy));
                Assert.That(heartComp.Stopped, Is.False);
            });
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(
                server.EntMan.System<StatusEffectsSystem>()
                    .HasStatusEffect(human, "StatusEffectCMUCardiacArrest"),
                Is.False);
        });

        await pair.RunSeconds(3);

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var damage = entMan.GetComponent<DamageableComponent>(human);

            Assert.That(damage.TotalDamage, Is.LessThanOrEqualTo(damageAfterRepair));

            entMan.DeleteEntity(human);
            entMan.DeleteEntity(surgeon);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("CMMobHuman", false)]
    [TestCase("CMUDroneAndroid", true)]
    public async Task AutodocRegeneratesPristineNativeLimbWithoutConsumingSeveredLimb(
        string patientPrototype,
        bool expectRobotic)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var autodoc = entMan.System<CMUAutodocSystem>();
            var patient = entMan.SpawnEntity(patientPrototype, MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            EntityUid detachedBody = default;

            try
            {
                entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(patient);
                var severedHand = GetBodyPart(entMan, patient, BodyPartType.Hand, BodyPartSymmetry.Right);
                detachedBody = DetachBodyPart(entMan, severedHand);

                var entries = BuildAutodocPartEntries(autodoc, patient, surgeon);
                var missingHand = entries.Find(entry =>
                    entry.Type == BodyPartType.Hand
                    && entry.Symmetry == BodyPartSymmetry.Right);
                var regeneration = missingHand?.EligibleSurgeries.Find(entry =>
                    entry.SurgeryId == "CMUAutodocRegenerateLimb");

                Assert.That(missingHand, Is.Not.Null);
                Assert.That(regeneration, Is.Not.Null);
                Assert.Multiple(() =>
                {
                    Assert.That(regeneration!.DisplayName, Is.EqualTo("Regenerate Limb"));
                    Assert.That(GetAutodocProcedureDuration(regeneration), Is.EqualTo(90f));
                });

                Assert.That(entMan.System<CMUSurgerySystem>().TryGetMissingPartSite(patient,
                    BodyPartType.Hand, BodyPartSymmetry.Right, out var anchor, out var slot), Is.True);
                var queued = new CMUAutodocQueuedStep(
                    patient,
                    BodyPartType.Hand,
                    BodyPartSymmetry.Right,
                    "CMUAutodocRegenerateLimb",
                    regeneration.DisplayName,
                    regeneration.Category,
                    0,
                    "cmu-autodoc-automated-step-label",
                    missingHand!.DisplayName,
                    90f,
                    TargetAnchor: anchor,
                    TargetSlot: slot);

                Assert.That(ApplyAutodocProcedure(autodoc, patient, surgeon, queued), Is.True);

                var replacement = GetBodyPart(entMan, patient, BodyPartType.Hand, BodyPartSymmetry.Right);
                var health = entMan.GetComponent<BodyPartHealthComponent>(replacement);
                var hasFracture = entMan.TryGetComponent<FractureComponent>(replacement, out var fracture);

                Assert.Multiple(() =>
                {
                    Assert.That(replacement, Is.Not.EqualTo(severedHand));
                    Assert.That(health.Current, Is.EqualTo(health.Max));
                    Assert.That(hasFracture && fracture!.Severity != FractureSeverity.None, Is.False);
                    Assert.That(entMan.HasComponent<CMURoboticLimbComponent>(replacement), Is.EqualTo(expectRobotic));
                    Assert.That(entMan.EntityExists(detachedBody), Is.True);
                    Assert.That(entMan.EntityExists(severedHand), Is.True);
                });
            }
            finally
            {
                if (entMan.EntityExists(detachedBody))
                    entMan.DeleteEntity(detachedBody);
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(surgeon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CauterizeSurgeryClampsDrugHeldInternalBleeding()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var wounds = entMan.System<SharedCMUWoundsSystem>();

            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
                entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);

                var arm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);
                OpenSoftTissue(entMan, arm);
                wounds.SeedInternalBleed(arm, "blunt", 0.5f);
                wounds.SuppressInternalBleedFor(arm, TimeSpan.FromSeconds(30));

                var held = entMan.GetComponent<CMUInternalBleedingSuppressedComponent>(arm);
                Assert.That(entMan.HasComponent<InternalBleedingComponent>(arm), Is.False);
                Assert.That(held.ExpiresAt, Is.Not.Null);

                var armed = ArmStep(
                    flow,
                    surgeon,
                    human,
                    arm,
                    "CMUSurgeryCauterizeInternalBleeding",
                    BodyPartType.Arm,
                    BodyPartSymmetry.Right,
                    "drug-held");
                CompleteExpectedStep(entMan, flow, human, surgeon, armed, "fix_o_vein", "drug-held");

                AssertAwaitingClosure(entMan, human, arm, "drug-held");
                Assert.That(entMan.HasComponent<InternalBleedingComponent>(arm), Is.False);
                var clamped = entMan.GetComponent<CMUInternalBleedingSuppressedComponent>(arm);
                Assert.That(clamped.ExpiresAt, Is.Null);
            }
            finally
            {
                entMan.DeleteEntity(human);
                entMan.DeleteEntity(surgeon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReattachingRoboticArmDoesNotRegrowMissingHand()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var detachable = entMan.System<DetachableOrganSystem>();
            var medicalIndex = entMan.System<CMUMedicalBodyIndexSystem>();
            var android = entMan.SpawnEntity("CMUDroneAndroid", MapCoordinates.Nullspace);
            EntityUid severedHand = default;
            EntityUid handCarrier = default;
            EntityUid armCarrier = default;

            try
            {
                var torso = GetBodyPart(entMan, android, BodyPartType.Torso, BodyPartSymmetry.None);
                var arm = GetBodyPart(entMan, android, BodyPartType.Arm, BodyPartSymmetry.Left);
                severedHand = GetBodyPart(entMan, android, BodyPartType.Hand, BodyPartSymmetry.Left);

                handCarrier = detachable.Detach(severedHand) ?? EntityUid.Invalid;
                armCarrier = detachable.Detach(arm) ?? EntityUid.Invalid;
                Assert.Multiple(() =>
                {
                    Assert.That(handCarrier.Valid, Is.True);
                    Assert.That(armCarrier.Valid, Is.True);
                    Assert.That(body.GetRootPartOrNull(handCarrier)?.Entity, Is.EqualTo(severedHand));
                    Assert.That(body.GetRootPartOrNull(armCarrier)?.Entity, Is.EqualTo(arm));
                });
                Assert.That(body.AttachPart(torso, "left_arm", arm), Is.True);
                entMan.DeleteEntity(armCarrier);
                armCarrier = default;

                Assert.That(
                    medicalIndex.TryGetBodyPart(
                        android,
                        new CMUMedicalBodyPartKey(BodyPartType.Hand, BodyPartSymmetry.Left),
                        out _),
                    Is.False);
            }
            finally
            {
                if (handCarrier.Valid && entMan.EntityExists(handCarrier))
                    entMan.DeleteEntity(handCarrier);
                if (armCarrier.Valid && entMan.EntityExists(armCarrier))
                    entMan.DeleteEntity(armCarrier);
                entMan.DeleteEntity(android);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OrganRemovalSurgeriesAdvanceToClosureAfterExtractionStep()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<CMUSurgeryFlowSystem>();

            RunOrganRemovalCase<LiverComponent>(entMan, flow, "CMUSurgeryRemoveLiver");
            RunOrganRemovalCase<LungsComponent>(entMan, flow, "CMUSurgeryRemoveLung");
            RunOrganRemovalCase<KidneysComponent>(entMan, flow, "CMUSurgeryRemoveKidney");
            RunOrganRemovalCase<HeartComponent>(entMan, flow, "CMUSurgeryRemoveHeart");
            RunOrganRemovalCase<CMUStomachComponent>(entMan, flow, "CMUSurgeryRemoveStomach");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OrganReplacementSurgeriesAdvanceToClosureAfterReinsertionStep()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var body = entMan.System<SharedBodySystem>();
            var hands = entMan.System<SharedHandsSystem>();

            RunOrganReplacementCase<LiverComponent>(entMan, flow, body, hands, "CMUSurgeryReplaceLiver");
            RunOrganReplacementCase<LungsComponent>(entMan, flow, body, hands, "CMUSurgeryReplaceLung");
            RunOrganReplacementCase<KidneysComponent>(entMan, flow, body, hands, "CMUSurgeryReplaceKidney");
            RunOrganReplacementCase<HeartComponent>(entMan, flow, body, hands, "CMUSurgeryTransplantHeart");
            RunOrganReplacementCase<CMUStomachComponent>(entMan, flow, body, hands, "CMUSurgeryReplaceStomach");
        });

        await pair.CleanReturnAsync();
    }

    private static void RunInjectedCleanupCase(
        IEntityManager entMan,
        CMUSurgeryFlowSystem flow,
        SharedCMUSurgicalTraitSystem traits,
        string surgeryId,
        CMUSurgicalTrait trait,
        BodyPartType partType,
        BodyPartSymmetry symmetry,
        string expectedTool,
        Action<EntityUid> setup = null,
        Action<EntityUid> assertAfter = null)
    {
        var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

        try
        {
            entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
            entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);

            var part = GetBodyPart(entMan, human, partType, symmetry);
            if (partType is BodyPartType.Head or BodyPartType.Torso)
                OpenBoneCavity(entMan, part);
            else
                OpenSoftTissue(entMan, part);

            setup?.Invoke(part);
            traits.EnsureTrait(part, trait);

            var armed = ArmStep(flow, surgeon, human, part, surgeryId, partType, symmetry, surgeryId);
            CompleteExpectedStep(entMan, flow, human, surgeon, armed, expectedTool, surgeryId);

            Assert.Multiple(() =>
            {
                Assert.That(traits.HasTrait(part, trait), Is.False, surgeryId);
                Assert.That(entMan.HasComponent<CMUSurgeryArmedStepComponent>(human), Is.False, surgeryId);
                Assert.That(entMan.HasComponent<CMUSurgeryInProgressComponent>(human), Is.False, surgeryId);
            });

            assertAfter?.Invoke(part);
        }
        finally
        {
            entMan.DeleteEntity(human);
            entMan.DeleteEntity(surgeon);
        }
    }

    private static void RunInternalBleedCase(
        IEntityManager entMan,
        CMUSurgeryFlowSystem flow,
        SharedCMUWoundsSystem wounds,
        string surgeryId,
        BodyPartType partType,
        BodyPartSymmetry symmetry,
        Action<IEntityManager, EntityUid> openPart)
    {
        var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

        try
        {
            entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
            entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);

            var part = GetBodyPart(entMan, human, partType, symmetry);
            openPart(entMan, part);
            wounds.SeedInternalBleed(part, $"test:{surgeryId}", 0.5f);

            var armed = ArmStep(flow, surgeon, human, part, surgeryId, partType, symmetry, surgeryId);
            CompleteExpectedStep(entMan, flow, human, surgeon, armed, "fix_o_vein", surgeryId);

            AssertAwaitingClosure(entMan, human, part, surgeryId);
            Assert.That(entMan.HasComponent<InternalBleedingComponent>(part), Is.False, surgeryId);
        }
        finally
        {
            entMan.DeleteEntity(human);
            entMan.DeleteEntity(surgeon);
        }
    }

    private static void RunEscharCase(IEntityManager entMan, CMUSurgeryFlowSystem flow)
    {
        var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

        try
        {
            entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
            entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);

            var arm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);
            entMan.EnsureComponent<CMUEscharComponent>(arm);

            var armed = ArmStep(
                flow,
                surgeon,
                human,
                arm,
                "CMUSurgeryDebrideEschar",
                BodyPartType.Arm,
                BodyPartSymmetry.Right,
                "CMUSurgeryDebrideEschar");
            CompleteExpectedStep(entMan, flow, human, surgeon, armed, "scalpel_or_burn_kit", "CMUSurgeryDebrideEschar");

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<CMUEscharComponent>(arm), Is.False);
                Assert.That(entMan.HasComponent<CMUSurgeryArmedStepComponent>(human), Is.False);
                Assert.That(entMan.HasComponent<CMUSurgeryInProgressComponent>(human), Is.False);
            });
        }
        finally
        {
            entMan.DeleteEntity(human);
            entMan.DeleteEntity(surgeon);
        }
    }

    private static void RunAmputationCase(IEntityManager entMan, CMUSurgeryFlowSystem flow)
    {
        var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

        try
        {
            entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
            entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);

            var arm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);
            OpenSoftTissue(entMan, arm);

            var armed = ArmStep(
                flow,
                surgeon,
                human,
                arm,
                "CMUSurgeryRemoveLimb",
                BodyPartType.Arm,
                BodyPartSymmetry.Right,
                "CMUSurgeryRemoveLimb");
            CompleteExpectedStep(entMan, flow, human, surgeon, armed, "bone_saw", "CMUSurgeryRemoveLimb");

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<CMUSurgeryArmedStepComponent>(human), Is.False);
                Assert.That(entMan.HasComponent<CMUSurgeryInProgressComponent>(human), Is.False);
            });
        }
        finally
        {
            entMan.DeleteEntity(human);
            entMan.DeleteEntity(surgeon);
        }
    }

    private static void RunOrganRepairCase<TOrgan>(
        IEntityManager entMan,
        CMUSurgeryFlowSystem flow,
        SharedCMUSurgicalTraitSystem traits,
        SharedOrganHealthSystem organHealth,
        string surgeryId,
        BodyPartType partType,
        BodyPartSymmetry symmetry,
        params string[] expectedTools)
        where TOrgan : IComponent
    {
        var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

        try
        {
            entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
            entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);

            var part = GetBodyPart(entMan, human, partType, symmetry);
            OpenBoneCavity(entMan, part);

            var organ = DamageOrgan<TOrgan>(entMan, organHealth, human, part);
            ClearSurgicalTraits(traits, part);

            var armed = ArmStep(flow, surgeon, human, part, surgeryId, partType, symmetry, surgeryId);
            for (var i = 0; i < expectedTools.Length; i++)
            {
                var next = CompleteExpectedStep(entMan, flow, human, surgeon, armed, expectedTools[i], surgeryId);
                if (i >= expectedTools.Length - 1)
                    continue;

                Assert.That(next, Is.Not.Null, surgeryId);
                armed = next!;
            }

            AssertAwaitingClosure(entMan, human, part, surgeryId);

            var health = entMan.GetComponent<OrganHealthComponent>(organ);
            Assert.Multiple(() =>
            {
                Assert.That(health.Current, Is.EqualTo(health.Max), surgeryId);
                Assert.That(health.Stage, Is.EqualTo(OrganDamageStage.Healthy), surgeryId);
            });
        }
        finally
        {
            entMan.DeleteEntity(human);
            entMan.DeleteEntity(surgeon);
        }
    }

    private static void RunOrganRemovalCase<TOrgan>(
        IEntityManager entMan,
        CMUSurgeryFlowSystem flow,
        string surgeryId)
        where TOrgan : IComponent
    {
        var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

        try
        {
            entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
            entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);

            var torso = GetBodyPart(entMan, human, BodyPartType.Torso, BodyPartSymmetry.None);
            OpenBoneCavity(entMan, torso);
            Assert.That(TryGetPartOrgan<TOrgan>(entMan, torso, out _), Is.True, surgeryId);

            var armed = ArmStep(
                flow,
                surgeon,
                human,
                torso,
                surgeryId,
                BodyPartType.Torso,
                BodyPartSymmetry.None,
                surgeryId);
            armed = CompleteExpectedStep(entMan, flow, human, surgeon, armed, "organ_clamp", surgeryId)!;
            CompleteExpectedStep(entMan, flow, human, surgeon, armed, "hemostat", surgeryId);

            AssertAwaitingClosure(entMan, human, torso, surgeryId);
            Assert.That(TryGetPartOrgan<TOrgan>(entMan, torso, out _), Is.False, surgeryId);
        }
        finally
        {
            entMan.DeleteEntity(human);
            entMan.DeleteEntity(surgeon);
        }
    }

    private static void RunOrganReplacementCase<TOrgan>(
        IEntityManager entMan,
        CMUSurgeryFlowSystem flow,
        SharedBodySystem body,
        SharedHandsSystem hands,
        string surgeryId)
        where TOrgan : IComponent
    {
        var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

        try
        {
            entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
            entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);

            var torso = GetBodyPart(entMan, human, BodyPartType.Torso, BodyPartSymmetry.None);
            OpenBoneCavity(entMan, torso);

            var donorOrgan = GetPartOrgan<TOrgan>(entMan, torso);
            Assert.That(entMan.TryGetComponent<OrganComponent>(donorOrgan, out var organ), Is.True, surgeryId);
            Assert.That(body.RemoveOrgan(donorOrgan, organ), Is.True, surgeryId);
            Assert.That(hands.TryPickupAnyHand(surgeon, donorOrgan, checkActionBlocker: false), Is.True, surgeryId);
            Assert.That(TryGetPartOrgan<TOrgan>(entMan, torso, out _), Is.False, surgeryId);

            var armed = ArmStep(
                flow,
                surgeon,
                human,
                torso,
                surgeryId,
                BodyPartType.Torso,
                BodyPartSymmetry.None,
                surgeryId);
            armed = CompleteExpectedStep(entMan, flow, human, surgeon, armed, "organ_clamp", surgeryId)!;
            Assert.That(flow.TryCompleteAutomatedStep(human, armed, surgeon), Is.False,
                "Automation must identify its donor explicitly even when a compatible organ is held.");
            Assert.That(hands.IsHolding(surgeon, donorOrgan), Is.True);
            Assert.That(TryGetPartOrgan<TOrgan>(entMan, torso, out _), Is.False);
            CompleteExpectedStep(entMan, flow, human, surgeon, armed, null, surgeryId, donorOrgan);

            AssertAwaitingClosure(entMan, human, torso, surgeryId);
            Assert.That(TryGetPartOrgan<TOrgan>(entMan, torso, out _), Is.True, surgeryId);
        }
        finally
        {
            entMan.DeleteEntity(human);
            entMan.DeleteEntity(surgeon);
        }
    }

    private static CMUSurgeryArmedStepComponent ArmStep(
        CMUSurgeryFlowSystem flow,
        EntityUid surgeon,
        EntityUid human,
        EntityUid part,
        string surgeryId,
        BodyPartType partType,
        BodyPartSymmetry symmetry,
        string context)
    {
        var armed = flow.TryArmStep(
            surgeon,
            human,
            part,
            surgeryId,
            0,
            partType,
            symmetry);

        Assert.That(armed, Is.Not.Null, context);
        return armed!;
    }

    private static CMUSurgeryArmedStepComponent CompleteExpectedStep(
        IEntityManager entMan,
        CMUSurgeryFlowSystem flow,
        EntityUid human,
        EntityUid surgeon,
        CMUSurgeryArmedStepComponent armed,
        string expectedTool,
        string context,
        EntityUid? donor = null)
    {
        Assert.That(armed.RequiredToolCategory, Is.EqualTo(expectedTool), context);
        Assert.That(flow.TryCompleteAutomatedStep(human, armed, surgeon, donor), Is.True, context);

        return entMan.TryGetComponent<CMUSurgeryArmedStepComponent>(human, out var next)
            ? next
            : null;
    }

    private static CMUSurgeryArmedStepComponent CompleteInjectedCleanupsUntilLeaf(
        IEntityManager entMan,
        CMUSurgeryFlowSystem flow,
        SharedCMUSurgicalTraitSystem traits,
        EntityUid human,
        EntityUid surgeon,
        EntityUid part,
        CMUSurgeryArmedStepComponent armed,
        string leafSurgeryId)
    {
        while (armed.SurgeryId != leafSurgeryId)
        {
            Assert.That(flow.TryCompleteAutomatedStep(human, armed, surgeon), Is.True, armed.SurgeryId);
            ClearSurgicalTraits(traits, part);
            armed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(human);
        }

        return armed;
    }

    private static void AssertAwaitingClosure(IEntityManager entMan, EntityUid human, EntityUid part, string context)
    {
        Assert.Multiple(() =>
        {
            Assert.That(entMan.HasComponent<CMUSurgeryArmedStepComponent>(human), Is.False, context);
            Assert.That(entMan.TryGetComponent<CMUSurgeryInProgressComponent>(human, out var inProgress), Is.True, context);
            Assert.That(inProgress!.Part, Is.EqualTo(part), context);
            Assert.That(inProgress.AwaitingClosureChoice, Is.True, context);
        });
    }

    private static void OpenSoftTissue(IEntityManager entMan, EntityUid part)
    {
        entMan.EnsureComponent<CMIncisionOpenComponent>(part);
        entMan.EnsureComponent<CMBleedersClampedComponent>(part);
        entMan.EnsureComponent<CMSkinRetractedComponent>(part);
    }

    private static void OpenBoneCavity(IEntityManager entMan, EntityUid part)
    {
        OpenSoftTissue(entMan, part);
        entMan.EnsureComponent<CMRibcageSawedComponent>(part);
        entMan.EnsureComponent<CMRibcageOpenComponent>(part);
    }

    private static void ClearSurgicalTraits(SharedCMUSurgicalTraitSystem traits, EntityUid part)
    {
        foreach (var trait in CMUSurgicalTraitMetadata.ResolutionOrder)
        {
            traits.RemoveTrait(part, trait);
        }
    }

    private static EntityUid GetPartOrgan<TOrgan>(IEntityManager entMan, EntityUid part) where TOrgan : IComponent
    {
        var body = entMan.System<SharedBodySystem>();
        foreach (var (organUid, _) in body.GetPartOrgans(part))
        {
            if (entMan.HasComponent<TOrgan>(organUid))
                return organUid;
        }

        Assert.Fail($"Expected part to contain organ {typeof(TOrgan).Name}.");
        return EntityUid.Invalid;
    }

    private static bool TryGetPartOrgan<TOrgan>(IEntityManager entMan, EntityUid part, out EntityUid organ)
        where TOrgan : IComponent
    {
        var body = entMan.System<SharedBodySystem>();
        foreach (var (organUid, _) in body.GetPartOrgans(part))
        {
            if (!entMan.HasComponent<TOrgan>(organUid))
                continue;

            organ = organUid;
            return true;
        }

        organ = EntityUid.Invalid;
        return false;
    }

    private static EntityUid DamageOrgan<TOrgan>(
        IEntityManager entMan,
        SharedOrganHealthSystem organHealth,
        EntityUid bodyUid,
        EntityUid part)
        where TOrgan : IComponent
    {
        var organ = GetPartOrgan<TOrgan>(entMan, part);
        var health = entMan.GetComponent<OrganHealthComponent>(organ);
        SetPublicField(health, nameof(OrganHealthComponent.Current), (FixedPoint2)20);
        organHealth.RecomputeStage((organ, health), bodyUid);
        return organ;
    }

    private static void SetPublicField<TComponent>(TComponent comp, string name, object value)
        where TComponent : IComponent
    {
        typeof(TComponent).GetField(name, BindingFlags.Instance | BindingFlags.Public)!.SetValue(comp, value);
    }

    private static EntityUid GetBodyPart(
        IEntityManager entMan,
        EntityUid bodyUid,
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        var body = entMan.System<SharedBodySystem>();
        foreach (var (partUid, part) in body.GetBodyChildren(bodyUid))
        {
            if (part.PartType == type && part.Symmetry == symmetry)
                return partUid;
        }

        Assert.Fail($"Expected CMU human to have {symmetry} {type}.");
        return EntityUid.Invalid;
    }

    private static EntityUid DetachBodyPart(IEntityManager entMan, EntityUid part)
    {
        var detachedBody = entMan.System<DetachableOrganSystem>().Detach(part);
        Assert.That(detachedBody, Is.Not.Null, $"Expected {part} to detach into a DetachedBody carrier.");
        Assert.That(entMan.System<SharedBodySystem>().GetRootPartOrNull(detachedBody!.Value), Is.Not.Null);
        return detachedBody!.Value;
    }

    private static List<CMUSurgeryPartEntry> BuildAutodocPartEntries(
        CMUAutodocSystem autodoc,
        EntityUid patient,
        EntityUid viewer)
    {
        var method = typeof(CMUAutodocSystem).GetMethod(
            "BuildAutodocPartEntries",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        return (List<CMUSurgeryPartEntry>) method!.Invoke(autodoc, [patient, viewer])!;
    }

    private static bool ApplyAutodocProcedure(
        CMUAutodocSystem autodoc,
        EntityUid patient,
        EntityUid operatorUid,
        CMUAutodocQueuedStep queued)
    {
        var method = typeof(CMUAutodocSystem).GetMethod(
            "TryApplyAutomatedProcedure",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        return (bool) method!.Invoke(autodoc, [patient, operatorUid, queued, (Func<bool>) (() => true)])!;
    }

    private static float GetAutodocProcedureDuration(CMUSurgeryEntry surgery)
    {
        var method = typeof(CMUAutodocSystem).GetMethod(
            "GetProcedureDurationSeconds",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        return (float) method!.Invoke(null, [surgery])!;
    }
}

#pragma warning restore RA0002
