using System;
using System.Collections.Generic;
using System.Reflection;
using Content.Server._CMU14.Medical.Injuries.Wounds;
using Content.Server._CMU14.Medical.Treatment.Surgery;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Eyes;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Kidneys;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Liver;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Stomach;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared._CMU14.Medical.Treatment.Surgery.Markers;
using Content.Shared._CMU14.Medical.Treatment.Surgery.Traits;
using Content.Shared._CMU14.Medical.Injuries.Shrapnel;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Medical.Treatment.Surgery;

[TestFixture]
public sealed class ConditionDrivenSurgeryTest
{
    [Test]
    public async Task UiLessPreferenceRoutesToolClickWithoutOpeningWindow()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;

        await client.WaitPost(() =>
            client.CfgMan.SetCVar(CMUMedicalCCVars.UiLessSurgeryEnabled, true));
        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            var player = server.PlayerMan.Sessions[0];
            var netConfig = server.ResolveDependency<INetConfigurationManager>();
            Assert.That(netConfig.GetClientCVar(player.Channel, CMUMedicalCCVars.UiLessSurgeryEnabled), Is.True);

            var entMan = server.EntMan;
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var targeting = entMan.System<SharedBodyZoneTargetingSystem>();
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var scalpel = entMan.SpawnEntity("CMScalpel", MapCoordinates.Nullspace);
            var originalAttached = player.AttachedEntity;

            try
            {
                server.PlayerMan.SetAttachedEntity(player, surgeon);
                entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
                entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(patient);
                targeting.SelectZone((surgeon, null), TargetBodyZone.Chest);
                Assert.That(hands.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false), Is.True);

                Assert.That(dispatch.TryDispatch(surgeon, patient, scalpel), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUSurgeryArmedStepComponent>(patient), Is.True);
                    Assert.That(entMan.HasComponent<CMUSurgeryWindowOpenComponent>(surgeon), Is.False);
                });
            }
            finally
            {
                server.PlayerMan.SetAttachedEntity(player, originalAttached);
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(surgeon);
                entMan.DeleteEntity(scalpel);
            }
        });

        await client.WaitPost(() =>
            client.CfgMan.SetCVar(CMUMedicalCCVars.UiLessSurgeryEnabled, false));
        await pair.RunTicksSync(2);
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UiLessScalpelDebridesContaminatedShallowSite()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var targeting = entMan.System<SharedBodyZoneTargetingSystem>();
            var traits = entMan.System<SharedCMUSurgicalTraitSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var scalpel = entMan.SpawnEntity("CMScalpel", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
                entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);
                targeting.SelectZone((surgeon, null), TargetBodyZone.Chest);
                Assert.That(hands.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false), Is.True);

                var torso = GetBodyPart(entMan, human, BodyPartType.Torso, BodyPartSymmetry.None);
                entMan.EnsureComponent<CMIncisionOpenComponent>(torso);
                entMan.EnsureComponent<CMSkinRetractedComponent>(torso);
                traits.EnsureTrait(torso, CMUSurgicalTrait.ContaminatedWound);

                Assert.That(dispatch.TryDispatchUiLess(surgeon, human, scalpel), Is.True);
                var armed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(human);
                Assert.Multiple(() =>
                {
                    Assert.That(armed.SurgeryId, Is.EqualTo("CMUSurgeryDebrideContaminatedWound"));
                    Assert.That(armed.RequiredToolCategory, Is.EqualTo("scalpel"));
                    Assert.That(traits.HasTrait(torso, CMUSurgicalTrait.ContaminatedWound), Is.True);
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
                entMan.DeleteEntity(surgeon);
                entMan.DeleteEntity(scalpel);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SurgeryExamineHidesSiteDetailsWithoutRequiredSkill()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<SharedCMUSurgeryFlowSystem>();
            var skills = entMan.System<SkillsSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var untrained = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);
                skills.SetSkill(surgeon, "RMCSkillSurgery", 1);
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso, BodyPartSymmetry.None);
                entMan.EnsureComponent<CMIncisionOpenComponent>(torso);
                entMan.EnsureComponent<CMSkinRetractedComponent>(torso);

                var armed = flow.TryArmExactStep(
                    surgeon,
                    human,
                    torso,
                    "CMUSurgeryOpenSoftTissue",
                    1,
                    BodyPartType.Torso,
                    BodyPartSymmetry.None);
                Assert.That(armed, Is.Not.Null);

                var basicExamine = new ExaminedEvent(new FormattedMessage(), human, untrained, true, false);
                entMan.EventBus.RaiseLocalEvent(human, basicExamine);
                var basicText = basicExamine.GetTotalMessage().ToMarkup();

                var skilledExamine = new ExaminedEvent(new FormattedMessage(), human, surgeon, true, false);
                entMan.EventBus.RaiseLocalEvent(human, skilledExamine);
                var skilledText = skilledExamine.GetTotalMessage().ToMarkup();

                Assert.Multiple(() =>
                {
                    Assert.That(basicText, Does.Contain("surgical incision"));
                    Assert.That(basicText, Does.Not.Contain("shallow access"));
                    Assert.That(basicText, Does.Not.Contain("Clamp the bleeders"));
                    Assert.That(skilledText, Does.Contain("Torso"));
                    Assert.That(skilledText, Does.Contain("shallow access"));
                    Assert.That(skilledText, Does.Contain("uncontrolled surgical bleeding"));
                    Assert.That(skilledText, Does.Contain("Clamp the bleeders"));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
                entMan.DeleteEntity(untrained);
                entMan.DeleteEntity(surgeon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UiLessOrganClampChoosesWorstDamagedOrganForRepair()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var organHealth = entMan.System<SharedOrganHealthSystem>();
            var targeting = entMan.System<SharedBodyZoneTargetingSystem>();
            var traits = entMan.System<SharedCMUSurgicalTraitSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var organClamp = entMan.SpawnEntity("CMUOrganClampItem", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
                entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);
                targeting.SelectZone((surgeon, null), TargetBodyZone.Chest);
                Assert.That(hands.TryPickupAnyHand(surgeon, organClamp, checkActionBlocker: false), Is.True);

                var torso = GetBodyPart(entMan, human, BodyPartType.Torso, BodyPartSymmetry.None);
                OpenBoneCavity(entMan, torso);
                DamageOrgan<LiverComponent>(entMan, organHealth, human, torso);

                var heart = GetPartOrgan<HeartComponent>(entMan, torso);
                var heartHealth = entMan.GetComponent<OrganHealthComponent>(heart);
                SetPublicField(heartHealth, nameof(OrganHealthComponent.Current), (FixedPoint2)8);
                organHealth.RecomputeStage((heart, heartHealth), human);
                ClearSurgicalTraits(traits, torso);

                Assert.That(dispatch.TryDispatchUiLess(surgeon, human, organClamp), Is.True);
                var armed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(human);
                Assert.Multiple(() =>
                {
                    Assert.That(heartHealth.Stage, Is.EqualTo(OrganDamageStage.Failing));
                    Assert.That(armed.LeafSurgeryId, Is.EqualTo("CMUSurgeryRepairHeart"));
                    Assert.That(armed.RequiredToolCategory, Is.EqualTo("organ_clamp"));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
                entMan.DeleteEntity(surgeon);
                entMan.DeleteEntity(organClamp);
            }
        });

        await pair.CleanReturnAsync();
    }

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
    public async Task HairlineFractureUsesStandardTwoToolRepair()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<SharedCMUSurgeryFlowSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var arm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);
                entMan.EnsureComponent<CMIncisionOpenComponent>(arm);
                entMan.EnsureComponent<CMSkinRetractedComponent>(arm);
                entMan.EnsureComponent<FractureComponent>(arm);

                Assert.That(
                    flow.TryResolveNextStep(
                        human,
                        arm,
                        "CMUSurgerySetSimpleFracture",
                        out var uiResolved),
                    Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(uiResolved.ResolvedSurgeryId, Is.EqualTo("CMUSurgeryOpenSoftTissue"));
                    Assert.That(uiResolved.ToolCategory, Is.EqualTo("hemostat"));
                });

                CMUResolvedStep resolved;
                Assert.That(
                    flow.TryResolveNextStep(
                        human,
                        arm,
                        "CMUSurgerySetSimpleFracture",
                        out resolved,
                        allowOptionalHemostasis: true),
                    Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(resolved.ResolvedSurgeryId, Is.EqualTo("CMUSurgerySetSimpleFracture"));
                    Assert.That(resolved.ToolCategory, Is.EqualTo("bone_setter"));
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
    public async Task UiLessFixOVeinRepairsInternalBleedingAtShallowOrDeepAccess()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var flow = entMan.System<SharedCMUSurgeryFlowSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var targeting = entMan.System<SharedBodyZoneTargetingSystem>();
            var wounds = entMan.System<SharedCMUWoundsSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var fixOVein = entMan.SpawnEntity("CMUFixOVein", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
                entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);
                targeting.SelectZone((surgeon, null), TargetBodyZone.Chest);
                Assert.That(hands.TryPickupAnyHand(surgeon, fixOVein, checkActionBlocker: false), Is.True);

                var torso = GetBodyPart(entMan, human, BodyPartType.Torso, BodyPartSymmetry.None);
                entMan.EnsureComponent<CMIncisionOpenComponent>(torso);
                entMan.EnsureComponent<CMSkinRetractedComponent>(torso);
                wounds.SeedInternalBleed(torso, "vascular:test", 0.5f);

                Assert.That(
                    flow.TryResolveNextStep(
                        human,
                        torso,
                        "CMUSurgeryCauterizeInternalBleeding",
                        out var shallow,
                        allowOptionalHemostasis: true),
                    Is.True);
                Assert.That(shallow.ToolCategory, Is.EqualTo("fix_o_vein"));

                entMan.EnsureComponent<CMRibcageOpenComponent>(torso);
                Assert.That(dispatch.TryDispatchUiLess(surgeon, human, fixOVein), Is.True);
                Assert.That(entMan.TryGetComponent<CMUSurgeryArmedStepComponent>(human, out var armed), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(flow.GetSiteState(torso).Access, Is.EqualTo(CMUSurgicalAccess.Deep));
                    Assert.That(armed!.SurgeryId, Is.EqualTo("CMUSurgeryCauterizeInternalBleeding"));
                    Assert.That(armed.RequiredToolCategory, Is.EqualTo("fix_o_vein"));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
                entMan.DeleteEntity(surgeon);
                entMan.DeleteEntity(fixOVein);
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

    [Test]
    public async Task UiLessRetractionCanPrecedeHemostasis()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var targeting = entMan.System<SharedBodyZoneTargetingSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var retractor = entMan.SpawnEntity("CMRetractor", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
                entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);
                targeting.SelectZone((surgeon, null), TargetBodyZone.Chest);
                Assert.That(hands.TryPickupAnyHand(surgeon, retractor, checkActionBlocker: false), Is.True);

                var torso = GetBodyPart(entMan, human, BodyPartType.Torso, BodyPartSymmetry.None);
                entMan.EnsureComponent<CMIncisionOpenComponent>(torso);

                Assert.That(dispatch.TryDispatchUiLess(surgeon, human, retractor), Is.True);
                Assert.That(entMan.TryGetComponent<CMUSurgeryArmedStepComponent>(human, out var armed), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(armed!.SurgeryId, Is.EqualTo("CMUSurgeryOpenSoftTissue"));
                    Assert.That(armed.StepIndex, Is.EqualTo(2));
                    Assert.That(armed.RequiredToolCategory, Is.EqualTo("retractor"));
                    Assert.That(entMan.HasComponent<CMBleedersClampedComponent>(torso), Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
                entMan.DeleteEntity(surgeon);
                entMan.DeleteEntity(retractor);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UiLessScalpelStartsSelectedClosedSiteWithoutOpeningWindow()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var targeting = entMan.System<SharedBodyZoneTargetingSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var scalpel = entMan.SpawnEntity("CMScalpel", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
                entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);
                targeting.SelectZone((surgeon, null), TargetBodyZone.Chest);
                Assert.That(hands.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false), Is.True);

                Assert.That(dispatch.TryDispatchUiLess(surgeon, human, scalpel), Is.True);
                Assert.That(entMan.TryGetComponent<CMUSurgeryArmedStepComponent>(human, out var armed), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(armed!.SurgeryId, Is.EqualTo("CMUSurgeryOpenSoftTissue"));
                    Assert.That(armed.StepIndex, Is.EqualTo(0));
                    Assert.That(armed.RequiredToolCategory, Is.EqualTo("scalpel"));
                    Assert.That(entMan.HasComponent<CMUSurgeryWindowOpenComponent>(surgeon), Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
                entMan.DeleteEntity(surgeon);
                entMan.DeleteEntity(scalpel);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UiLessScalpelStartsReattachmentAtSelectedMissingLimb()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var targeting = entMan.System<SharedBodyZoneTargetingSystem>();
            var xform = entMan.System<SharedTransformSystem>();
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var scalpel = entMan.SpawnEntity("CMScalpel", MapCoordinates.Nullspace);
            var leftArm = GetBodyPart(entMan, patient, BodyPartType.Arm, BodyPartSymmetry.Left);

            try
            {
                entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
                entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(patient);
                xform.DetachEntity(leftArm, entMan.GetComponent<TransformComponent>(leftArm));
                targeting.SelectZone((surgeon, null), TargetBodyZone.LeftArm);
                Assert.That(hands.TryPickupAnyHand(surgeon, scalpel, checkActionBlocker: false), Is.True);

                Assert.That(dispatch.TryDispatchUiLess(surgeon, patient, scalpel), Is.True);
                Assert.That(entMan.TryGetComponent<CMUSurgeryArmedStepComponent>(patient, out var armed), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(armed!.LeafSurgeryId, Is.EqualTo("CMUSurgeryReattachLimb"));
                    Assert.That(armed.SurgeryId, Is.EqualTo("CMUSurgeryOpenSoftTissue"));
                    Assert.That(armed.TargetPartType, Is.EqualTo(BodyPartType.Arm));
                    Assert.That(armed.TargetSymmetry, Is.EqualTo(BodyPartSymmetry.Left));
                    Assert.That(entMan.HasComponent<CMUSurgeryWindowOpenComponent>(surgeon), Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(leftArm);
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(surgeon);
                entMan.DeleteEntity(scalpel);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RetractedCompoundFractureProvidesDeepAccessWithoutHemostasis()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<SharedCMUSurgeryFlowSystem>();
            var fracture = entMan.System<SharedFractureSystem>();
            var traits = entMan.System<SharedCMUSurgicalTraitSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso, BodyPartSymmetry.None);
                entMan.EnsureComponent<CMIncisionOpenComponent>(torso);
                entMan.EnsureComponent<CMSkinRetractedComponent>(torso);

                var fractured = entMan.EnsureComponent<FractureComponent>(torso);
                fracture.SetSeverity((torso, fractured), FractureSeverity.Compound);
                ClearSurgicalTraits(traits, torso);

                var state = flow.GetSiteState(torso);
                Assert.Multiple(() =>
                {
                    Assert.That(state.Access, Is.EqualTo(CMUSurgicalAccess.Deep));
                    Assert.That(state.Hemostasis, Is.EqualTo(CMUSurgicalHemostasis.Uncontrolled));
                });

                Assert.That(
                    flow.TryResolveNextStep(
                        human,
                        torso,
                        "CMUSurgerySetCompoundFractureCavity",
                        out var uiResolved),
                    Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(uiResolved.ResolvedSurgeryId, Is.EqualTo("CMUSurgeryOpenSoftTissue"));
                    Assert.That(uiResolved.ToolCategory, Is.EqualTo("hemostat"));
                });

                entMan.EnsureComponent<CMBleedersClampedComponent>(torso);
                Assert.That(
                    flow.TryResolveNextStep(
                        human,
                        torso,
                        "CMUSurgerySetCompoundFractureCavity",
                        out var clampedUiResolved),
                    Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(clampedUiResolved.ResolvedSurgeryId,
                        Is.EqualTo("CMUSurgerySetCompoundFractureCavity"));
                    Assert.That(clampedUiResolved.ToolCategory, Is.EqualTo("bone_setter"));
                });
                entMan.RemoveComponent<CMBleedersClampedComponent>(torso);

                Assert.That(
                    flow.TryResolveNextStep(
                        human,
                        torso,
                        "CMUSurgerySetCompoundFractureCavity",
                        out var resolved,
                        allowOptionalHemostasis: true),
                    Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(resolved.ResolvedSurgeryId, Is.EqualTo("CMUSurgerySetCompoundFractureCavity"));
                    Assert.That(resolved.ToolCategory, Is.EqualTo("bone_setter"));
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
    public async Task OpenFractureWithoutTraitsResolvesNormalRepairStep()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<SharedCMUSurgeryFlowSystem>();
            var fracture = entMan.System<SharedFractureSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var arm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);
                OpenSoftTissue(entMan, arm);

                var frac = entMan.EnsureComponent<FractureComponent>(arm);
                fracture.SetSeverity((arm, frac), FractureSeverity.Simple);

                Assert.That(flow.TryResolveNextStep(human, arm, "CMUSurgerySetSimpleFracture", out var resolved), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(resolved.ResolvedSurgeryId, Is.EqualTo("CMUSurgerySetSimpleFracture"));
                    Assert.That(resolved.ToolCategory, Is.EqualTo("bone_setter"));
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
    public async Task SurgicalTraitsResolveInDeterministicOrder()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<SharedCMUSurgeryFlowSystem>();
            var traits = entMan.System<SharedCMUSurgicalTraitSystem>();
            var fracture = entMan.System<SharedFractureSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var arm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);
                OpenSoftTissue(entMan, arm);

                var frac = entMan.EnsureComponent<FractureComponent>(arm);
                fracture.SetSeverity((arm, frac), FractureSeverity.Shattered);

                traits.EnsureTrait(arm, CMUSurgicalTrait.VascularTear);
                traits.EnsureTrait(arm, CMUSurgicalTrait.EmbeddedForeignBody);
                traits.EnsureTrait(arm, CMUSurgicalTrait.CompartmentPressure);
                traits.EnsureTrait(arm, CMUSurgicalTrait.ContaminatedWound);
                traits.EnsureTrait(arm, CMUSurgicalTrait.BoneSplintered);

                AssertNext(flow, human, arm, "CMUSurgeryTieVascularTear");
                traits.RemoveTrait(arm, CMUSurgicalTrait.VascularTear);

                AssertNext(flow, human, arm, "CMUSurgeryExtractForeignBody");
                traits.RemoveTrait(arm, CMUSurgicalTrait.EmbeddedForeignBody);

                AssertNext(flow, human, arm, "CMUSurgeryRelieveCompartmentPressure");
                traits.RemoveTrait(arm, CMUSurgicalTrait.CompartmentPressure);

                AssertNext(flow, human, arm, "CMUSurgeryDebrideContaminatedWound");
                traits.RemoveTrait(arm, CMUSurgicalTrait.ContaminatedWound);

                AssertNext(flow, human, arm, "CMUSurgeryRemoveBoneFragments");
                traits.RemoveTrait(arm, CMUSurgicalTrait.BoneSplintered);

                AssertNext(flow, human, arm, "CMUSurgerySetShatteredFracture");
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ArmedSurgeryReResolvesInjectedCleanupBeforeRunningStaleStep()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var traits = entMan.System<SharedCMUSurgicalTraitSystem>();
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
                Assert.That(armed!.SurgeryId, Is.EqualTo("CMUSurgerySetSimpleFracture"));

                traits.EnsureTrait(arm, CMUSurgicalTrait.VascularTear);

                Assert.That(flow.TryCompleteAutomatedStep(human, armed, surgeon), Is.True);

                var rearmed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(human);
                Assert.Multiple(() =>
                {
                    Assert.That(rearmed.SurgeryId, Is.EqualTo("CMUSurgeryTieVascularTear"));
                    Assert.That(rearmed.RequiredToolCategory, Is.EqualTo("hemostat"));
                    Assert.That(traits.HasTrait(arm, CMUSurgicalTrait.VascularTear), Is.True);
                    Assert.That(entMan.GetComponent<FractureComponent>(arm).Severity, Is.EqualTo(FractureSeverity.Simple));
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
    public async Task ShatteredFractureRepairAdvancesPastFirstBoneGel()
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

                var armed = flow.TryArmStep(
                    surgeon,
                    human,
                    arm,
                    "CMUSurgerySetShatteredFracture",
                    0,
                    BodyPartType.Arm,
                    BodyPartSymmetry.Right);

                Assert.That(armed, Is.Not.Null);
                Assert.That(armed!.RequiredToolCategory, Is.EqualTo("bone_setter"));

                Assert.That(flow.TryCompleteAutomatedStep(human, armed, surgeon), Is.True);

                ClearSurgicalTraits(traits, arm);
                armed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(human);
                if (armed.RequiredToolCategory != "bone_gel")
                {
                    Assert.That(flow.TryCompleteAutomatedStep(human, armed, surgeon), Is.True);
                    armed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(human);
                }

                Assert.That(armed.RequiredToolCategory, Is.EqualTo("bone_gel"));

                Assert.That(flow.TryCompleteAutomatedStep(human, armed, surgeon), Is.True);

                armed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(human);
                Assert.Multiple(() =>
                {
                    Assert.That(armed.SurgeryId, Is.EqualTo("CMUSurgerySetShatteredFracture"));
                    Assert.That(armed.RequiredToolCategory, Is.EqualTo("bone_graft"));
                    Assert.That(armed.StepIndex, Is.EqualTo(2));
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
    public async Task ShatteredFractureWithBoneFragmentsCleanupAdvancesPastRealign()
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
                traits.EnsureTrait(arm, CMUSurgicalTrait.BoneSplintered);

                var armed = ArmStep(
                    flow,
                    surgeon,
                    human,
                    arm,
                    "CMUSurgerySetShatteredFracture",
                    BodyPartType.Arm,
                    BodyPartSymmetry.Right,
                    "shattered fracture with bone fragments");

                Assert.Multiple(() =>
                {
                    Assert.That(armed.SurgeryId, Is.EqualTo("CMUSurgeryRemoveBoneFragments"));
                    Assert.That(armed.RequiredToolCategory, Is.EqualTo("hemostat"));
                });

                armed = CompleteExpectedStep(
                    entMan,
                    flow,
                    human,
                    surgeon,
                    armed,
                    "hemostat",
                    "remove bone fragments before shattered repair")!;

                Assert.Multiple(() =>
                {
                    Assert.That(traits.HasTrait(arm, CMUSurgicalTrait.BoneSplintered), Is.False);
                    Assert.That(armed.SurgeryId, Is.EqualTo("CMUSurgerySetShatteredFracture"));
                    Assert.That(armed.StepIndex, Is.EqualTo(0));
                    Assert.That(armed.RequiredToolCategory, Is.EqualTo("bone_setter"));
                });

                armed = CompleteExpectedStep(
                    entMan,
                    flow,
                    human,
                    surgeon,
                    armed,
                    "bone_setter",
                    "realign shattered fracture after bone fragments cleanup")!;

                if (armed.SurgeryId != "CMUSurgerySetShatteredFracture")
                {
                    Assert.That(flow.TryCompleteAutomatedStep(human, armed, surgeon), Is.True, "injected cleanup after realign");
                    armed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(human);
                }

                Assert.Multiple(() =>
                {
                    Assert.That(armed.SurgeryId, Is.EqualTo("CMUSurgerySetShatteredFracture"));
                    Assert.That(armed.StepIndex, Is.EqualTo(1));
                    Assert.That(armed.RequiredToolCategory, Is.EqualTo("bone_gel"));
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

                Assert.That(flow.TryCompleteAutomatedStep(human, armed, surgeon), Is.True);
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

                Assert.That(flow.TryCompleteAutomatedStep(human, armed, surgeon), Is.True);

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
    public async Task ReattachLimbResolverAdvancesThroughPreparedSocketMarkers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<SharedCMUSurgeryFlowSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var socket = GetBodyPart(entMan, human, BodyPartType.Torso, BodyPartSymmetry.None);
                OpenSoftTissue(entMan, socket);

                Assert.That(flow.TryResolveNextStep(human, socket, "CMUSurgeryReattachLimb", out var resolved), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(resolved.ResolvedSurgeryId, Is.EqualTo("CMUSurgeryReattachLimb"));
                    Assert.That(resolved.StepIndex, Is.EqualTo(0));
                    Assert.That(resolved.ToolCategory, Is.EqualTo("bone_saw"));
                });

                entMan.EnsureComponent<CMUStumpRemovedComponent>(socket);
                Assert.That(flow.TryResolveNextStep(human, socket, "CMUSurgeryReattachLimb", out resolved), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(resolved.ResolvedSurgeryId, Is.EqualTo("CMUSurgeryReattachLimb"));
                    Assert.That(resolved.StepIndex, Is.EqualTo(1));
                    Assert.That(resolved.ToolCategory, Is.EqualTo("hemostat"));
                });

                entMan.EnsureComponent<CMUReattachPreppedComponent>(socket);
                Assert.That(flow.TryResolveNextStep(human, socket, "CMUSurgeryReattachLimb", out resolved), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(resolved.ResolvedSurgeryId, Is.EqualTo("CMUSurgeryReattachLimb"));
                    Assert.That(resolved.StepIndex, Is.EqualTo(2));
                    Assert.That(resolved.ToolCategory, Is.EqualTo("severed_limb"));
                });

                entMan.EnsureComponent<CMUReattachCompleteComponent>(socket);
                Assert.That(flow.TryResolveNextStep(human, socket, "CMUSurgeryReattachLimb", out resolved), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(resolved.ResolvedSurgeryId, Is.EqualTo("CMUSurgeryReattachLimb"));
                    Assert.That(resolved.StepIndex, Is.EqualTo(3));
                    Assert.That(resolved.ToolCategory, Is.EqualTo("cautery"));
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
            var status = entMan.System<SharedStatusEffectsSystem>();
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
            var status = entMan.System<SharedStatusEffectsSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(health.Stage, Is.EqualTo(OrganDamageStage.Healthy));
                Assert.That(heartComp.Stopped, Is.False);
                Assert.That(status.HasStatusEffect(human, "StatusEffectCMUCardiacArrest"), Is.False);
            });
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

    [Test]
    public async Task DamagedNormalHumanBrainAppearsAsRepairableHeadSurgery()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var organHealth = entMan.System<SharedOrganHealthSystem>();
            var skills = entMan.System<SkillsSystem>();

            var human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                Assert.That(entMan.HasComponent<CMUHumanMedicalComponent>(human), Is.True);
                entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);
                skills.SetSkill(surgeon, "RMCSkillSurgery", 3);

                var head = GetBodyPart(entMan, human, BodyPartType.Head, BodyPartSymmetry.None);
                DamageOrgan<CMUBrainComponent>(entMan, organHealth, human, head);

                var entries = dispatch.BuildPartEntries(human, surgeon);
                var headEntry = entries.Find(entry =>
                    entry.Type == BodyPartType.Head &&
                    entry.Symmetry == BodyPartSymmetry.None);

                Assert.That(headEntry, Is.Not.Null);
                Assert.That(
                    headEntry!.EligibleSurgeries.ConvertAll(entry => entry.SurgeryId),
                    Does.Contain("CMUSurgeryRepairBrain"));
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
    public async Task AutodocOffersWoundRepairForHandWounds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var autodoc = entMan.System<CMUAutodocSystem>();

            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);

                var hand = GetBodyPart(entMan, human, BodyPartType.Hand, BodyPartSymmetry.Right);
                AddBodyPartWound(entMan, hand, WoundType.Brute);

                var entries = BuildAutodocPartEntries(autodoc, human, surgeon);
                var handEntry = entries.Find(entry =>
                    entry.Type == BodyPartType.Hand &&
                    entry.Symmetry == BodyPartSymmetry.Right);

                Assert.That(handEntry, Is.Not.Null);
                Assert.That(
                    handEntry!.EligibleSurgeries.ConvertAll(entry => entry.SurgeryId),
                    Does.Contain("CMUAutodocRepairWounds"));
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

    [Test]
    public async Task ResolveTraitStepRemovesTraitAndSuppressesVascularBleeding()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var traits = entMan.System<SharedCMUSurgicalTraitSystem>();
            var wounds = entMan.System<SharedCMUWoundsSystem>();
            var rmcSurgery = entMan.System<Content.Shared._RMC14.Medical.Surgery.SharedCMSurgerySystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var arm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);
                traits.EnsureTrait(arm, CMUSurgicalTrait.VascularTear);
                wounds.SeedInternalBleed(arm, "fracture:Shattered", 0.5f);

                var step = rmcSurgery.GetSingleton("CMUSurgeryStepTieVascularTear");
                Assert.That(step, Is.Not.Null);

                var ev = new CMSurgeryStepEvent(human, human, arm, new List<EntityUid>());
                entMan.EventBus.RaiseLocalEvent(step.Value, ref ev);

                Assert.Multiple(() =>
                {
                    Assert.That(traits.HasTrait(arm, CMUSurgicalTrait.VascularTear), Is.False);
                    Assert.That(entMan.HasComponent<InternalBleedingComponent>(arm), Is.False);
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
    public async Task ResolveForeignBodyStepClearsShrapnelCondition()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var traits = entMan.System<SharedCMUSurgicalTraitSystem>();
            var shrapnel = entMan.System<SharedCMUShrapnelSystem>();
            var rmcSurgery = entMan.System<Content.Shared._RMC14.Medical.Surgery.SharedCMSurgerySystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var arm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);
                Assert.That(shrapnel.AddShrapnel(arm, 1, 10f), Is.True);
                Assert.That(traits.HasTrait(arm, CMUSurgicalTrait.EmbeddedForeignBody), Is.True);

                var step = rmcSurgery.GetSingleton("CMUSurgeryStepExtractForeignBody");
                Assert.That(step, Is.Not.Null);

                var ev = new CMSurgeryStepEvent(human, human, arm, new List<EntityUid>());
                entMan.EventBus.RaiseLocalEvent(step.Value, ref ev);

                Assert.Multiple(() =>
                {
                    Assert.That(traits.HasTrait(arm, CMUSurgicalTrait.EmbeddedForeignBody), Is.False);
                    Assert.That(entMan.HasComponent<CMUShrapnelComponent>(arm), Is.False);
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
    public async Task OrganRepairSurgeryInjectsOrganTraitsInOrder()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<SharedCMUSurgeryFlowSystem>();
            var traits = entMan.System<SharedCMUSurgicalTraitSystem>();
            var organHealth = entMan.System<SharedOrganHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso, BodyPartSymmetry.None);
                OpenBoneCavity(entMan, torso);

                var liver = GetPartOrgan<LiverComponent>(entMan, torso);
                var health = entMan.GetComponent<OrganHealthComponent>(liver);
                SetPublicField(health, nameof(OrganHealthComponent.Current), (FixedPoint2)20);
                organHealth.RecomputeStage((liver, health), human);

                traits.EnsureTrait(torso, CMUSurgicalTrait.EmbeddedForeignBody);
                traits.EnsureTrait(torso, CMUSurgicalTrait.OrganAdhesion);
                traits.EnsureTrait(torso, CMUSurgicalTrait.OrganHemorrhage);

                AssertNextOrgan(flow, human, torso, "CMUSurgeryExtractForeignBody");
                traits.RemoveTrait(torso, CMUSurgicalTrait.EmbeddedForeignBody);

                AssertNextOrgan(flow, human, torso, "CMUSurgeryFreeOrganAdhesions");
                traits.RemoveTrait(torso, CMUSurgicalTrait.OrganAdhesion);

                AssertNextOrgan(flow, human, torso, "CMUSurgeryPackOrganBleed");
                traits.RemoveTrait(torso, CMUSurgicalTrait.OrganHemorrhage);

                AssertNextOrgan(flow, human, torso, "CMUSurgeryRepairLiver");
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FractureSeveritySeedsBoundedTraits()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var traits = entMan.System<SharedCMUSurgicalTraitSystem>();
            var fracture = entMan.System<SharedFractureSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var arm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso, BodyPartSymmetry.None);

                var armFrac = entMan.EnsureComponent<FractureComponent>(arm);
                fracture.SetSeverity((arm, armFrac), FractureSeverity.Shattered);

                var torsoFrac = entMan.EnsureComponent<FractureComponent>(torso);
                fracture.SetSeverity((torso, torsoFrac), FractureSeverity.Shattered);

                Assert.Multiple(() =>
                {
                    Assert.That(traits.HasTrait(arm, CMUSurgicalTrait.BoneSplintered), Is.True);
                    Assert.That(traits.CountTraits(arm), Is.LessThanOrEqualTo(2));
                    Assert.That(traits.HasTrait(torso, CMUSurgicalTrait.BoneSplintered), Is.True);
                    Assert.That(traits.CountTraits(torso), Is.LessThanOrEqualTo(2));
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
    public void SurgicalTraitGenerationUsesApprovedBalanceRates()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CMUSurgicalTraitGenerationSystem.CompoundContaminationChance, Is.EqualTo(0.65f));
            Assert.That(CMUSurgicalTraitGenerationSystem.ShatteredSecondTraitChance, Is.EqualTo(0.5f));
            Assert.That(CMUSurgicalTraitGenerationSystem.DamagedOrganComplicationChance, Is.EqualTo(0.25f));
            Assert.That(CMUSurgicalTraitGenerationSystem.FailingOrganComplicationChance, Is.EqualTo(0.6f));
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedCompoundContamination(0.64f), Is.True);
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedCompoundContamination(0.65f), Is.False);
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedShatteredSecondTrait(0.49f), Is.True);
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedShatteredSecondTrait(0.5f), Is.False);
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedDamagedOrganComplication(0.24f), Is.True);
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedDamagedOrganComplication(0.25f), Is.False);
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedFailingOrganComplication(0.59f), Is.True);
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedFailingOrganComplication(0.6f), Is.False);
        });
    }

    private static void AssertNext(SharedCMUSurgeryFlowSystem flow, EntityUid human, EntityUid part, string surgeryId)
    {
        Assert.That(flow.TryResolveNextStep(human, part, "CMUSurgerySetShatteredFracture", out var resolved), Is.True);
        Assert.That(resolved.ResolvedSurgeryId, Is.EqualTo(surgeryId));
    }

    private static void AssertNextOrgan(SharedCMUSurgeryFlowSystem flow, EntityUid human, EntityUid part, string surgeryId)
    {
        Assert.That(flow.TryResolveNextStep(human, part, "CMUSurgeryRepairLiver", out var resolved), Is.True);
        Assert.That(resolved.ResolvedSurgeryId, Is.EqualTo(surgeryId));
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
            CompleteExpectedStep(entMan, flow, human, surgeon, armed, null, surgeryId);

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
        string context)
    {
        Assert.That(armed.RequiredToolCategory, Is.EqualTo(expectedTool), context);
        Assert.That(flow.TryCompleteAutomatedStep(human, armed, surgeon), Is.True, context);

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

    private static void AddBodyPartWound(IEntityManager entMan, EntityUid part, WoundType type)
    {
        var ledger = entMan.System<CMUWoundLedgerSystem>();
        var wounds = entMan.EnsureComponent<BodyPartWoundComponent>(part);
        Assert.That(ledger.AddEntry(wounds, new CMUWoundEntry(
            new Wound(FixedPoint2.New(10), FixedPoint2.Zero, 0f, null, type, false),
            WoundSize.CutDeep,
            0,
            type == WoundType.Burn ? WoundMechanism.Burn : WoundMechanism.Generic,
            WoundMechanismFlags.None,
            WoundTreatmentQuality.Untreated,
            WoundCleanupFlags.None)), Is.GreaterThanOrEqualTo(0));
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
}
