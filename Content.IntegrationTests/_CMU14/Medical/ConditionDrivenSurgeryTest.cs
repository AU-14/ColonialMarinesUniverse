using System;
using System.Collections.Generic;
using System.Reflection;
using Content.Server._CMU14.Medical.Surgery;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Organs.Brain;
using Content.Shared._CMU14.Medical.Organs.Ears;
using Content.Shared._CMU14.Medical.Organs.Eyes;
using Content.Shared._CMU14.Medical.Organs.Heart;
using Content.Shared._CMU14.Medical.Organs.Kidneys;
using Content.Shared._CMU14.Medical.Organs.Liver;
using Content.Shared._CMU14.Medical.Organs.Lungs;
using Content.Shared._CMU14.Medical.Organs.Stomach;
using Content.Shared._CMU14.Medical.Surgery;
using Content.Shared._CMU14.Medical.Surgery.Markers;
using Content.Shared._CMU14.Medical.Surgery.Traits;
using Content.Shared._CMU14.Medical.Shrapnel;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Medical;

[TestFixture]
public sealed class ConditionDrivenSurgeryTest
{
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
                fracture.SetSeverity((arm, frac), FractureSeverity.Comminuted);

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

                AssertNext(flow, human, arm, "CMUSurgerySetComminutedFracture");
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
    public async Task ComminutedFractureRepairAdvancesPastFirstBoneGel()
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
                fracture.SetSeverity((arm, frac), FractureSeverity.Comminuted);
                ClearSurgicalTraits(traits, arm);

                var armed = flow.TryArmStep(
                    surgeon,
                    human,
                    arm,
                    "CMUSurgerySetComminutedFracture",
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
                    Assert.That(armed.SurgeryId, Is.EqualTo("CMUSurgerySetComminutedFracture"));
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
    public async Task ComminutedFractureWithBoneFragmentsCleanupAdvancesPastRealign()
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
                fracture.SetSeverity((arm, frac), FractureSeverity.Comminuted);
                ClearSurgicalTraits(traits, arm);
                traits.EnsureTrait(arm, CMUSurgicalTrait.BoneSplintered);

                var armed = ArmStep(
                    flow,
                    surgeon,
                    human,
                    arm,
                    "CMUSurgerySetComminutedFracture",
                    BodyPartType.Arm,
                    BodyPartSymmetry.Right,
                    "comminuted fracture with bone fragments");

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
                    "remove bone fragments before comminuted repair")!;

                Assert.Multiple(() =>
                {
                    Assert.That(traits.HasTrait(arm, CMUSurgicalTrait.BoneSplintered), Is.False);
                    Assert.That(armed.SurgeryId, Is.EqualTo("CMUSurgerySetComminutedFracture"));
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
                    "realign comminuted fracture after bone fragments cleanup")!;

                if (armed.SurgeryId != "CMUSurgerySetComminutedFracture")
                {
                    Assert.That(flow.TryCompleteAutomatedStep(human, armed, surgeon), Is.True, "injected cleanup after realign");
                    armed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(human);
                }

                Assert.Multiple(() =>
                {
                    Assert.That(armed.SurgeryId, Is.EqualTo("CMUSurgerySetComminutedFracture"));
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
    public async Task ComminutedFractureContaminatedCleanupBeforeFinalSetDoesNotRepeatBoneGel()
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
                fracture.SetSeverity((arm, frac), FractureSeverity.Comminuted);
                ClearSurgicalTraits(traits, arm);

                var armed = ArmStep(
                    flow,
                    surgeon,
                    human,
                    arm,
                    "CMUSurgerySetComminutedFracture",
                    BodyPartType.Arm,
                    BodyPartSymmetry.Right,
                    "comminuted fracture with late contamination");

                armed = CompleteExpectedStep(
                    entMan,
                    flow,
                    human,
                    surgeon,
                    armed,
                    "bone_setter",
                    "realign comminuted fracture")!;
                armed = CompleteInjectedCleanupsUntilLeaf(entMan, flow, traits, human, surgeon, arm, armed, "CMUSurgerySetComminutedFracture");
                ClearSurgicalTraits(traits, arm);

                armed = CompleteExpectedStep(
                    entMan,
                    flow,
                    human,
                    surgeon,
                    armed,
                    "bone_gel",
                    "apply comminuted bone gel")!;
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
                    Assert.That(armed.SurgeryId, Is.EqualTo("CMUSurgerySetComminutedFracture"));
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
                    Assert.That(armed.SurgeryId, Is.EqualTo("CMUSurgerySetComminutedFracture"));
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
                "CMUSurgeryCauterizeInternalBleedingCavity",
                BodyPartType.Torso,
                BodyPartSymmetry.None,
                OpenBoneCavity);

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
                "hemostat");

            RunOrganRepairCase<EyesComponent>(
                entMan,
                flow,
                traits,
                organHealth,
                "CMUSurgeryRepairEyes",
                BodyPartType.Head,
                BodyPartSymmetry.None,
                "hemostat");

            RunOrganRepairCase<EarsComponent>(
                entMan,
                flow,
                traits,
                organHealth,
                "CMUSurgeryRepairEars",
                BodyPartType.Head,
                BodyPartSymmetry.None,
                "hemostat");
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
                wounds.SeedInternalBleed(arm, "fracture:Comminuted", 0.5f);

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
                fracture.SetSeverity((arm, armFrac), FractureSeverity.Comminuted);

                var torsoFrac = entMan.EnsureComponent<FractureComponent>(torso);
                fracture.SetSeverity((torso, torsoFrac), FractureSeverity.Comminuted);

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
            Assert.That(CMUSurgicalTraitGenerationSystem.ComminutedSecondTraitChance, Is.EqualTo(0.5f));
            Assert.That(CMUSurgicalTraitGenerationSystem.DamagedOrganComplicationChance, Is.EqualTo(0.25f));
            Assert.That(CMUSurgicalTraitGenerationSystem.FailingOrganComplicationChance, Is.EqualTo(0.6f));
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedCompoundContamination(0.64f), Is.True);
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedCompoundContamination(0.65f), Is.False);
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedComminutedSecondTrait(0.49f), Is.True);
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedComminutedSecondTrait(0.5f), Is.False);
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedDamagedOrganComplication(0.24f), Is.True);
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedDamagedOrganComplication(0.25f), Is.False);
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedFailingOrganComplication(0.59f), Is.True);
            Assert.That(CMUSurgicalTraitGenerationSystem.ShouldSeedFailingOrganComplication(0.6f), Is.False);
        });
    }

    private static void AssertNext(SharedCMUSurgeryFlowSystem flow, EntityUid human, EntityUid part, string surgeryId)
    {
        Assert.That(flow.TryResolveNextStep(human, part, "CMUSurgerySetComminutedFracture", out var resolved), Is.True);
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
            CompleteExpectedStep(entMan, flow, human, surgeon, armed, "hemostat", surgeryId);

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
}
