using System.Collections.Generic;
using System.Linq;
using Content.Server.CMU14.Medical.Treatment.Surgery;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared.CMU14.Medical.Treatment.Surgery.Markers;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Standing;
using Robust.Shared.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Containers;

namespace Content.IntegrationTests.CMU14.Medical.Treatment.Surgery;

[TestFixture]
public sealed class CMUSurgeryTransactionTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: CMUSurgeryStepReattachLimb
          id: CMUSurgeryTestStepReattachQuarterHealth
          components:
          - type: CMUSurgeryStepReattachLimbEffect
            startingHpFraction: 0.25
        """;

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    public async Task NonfiniteReattachmentHealthRejectsBeforeDroppingOrAttaching(float fraction)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var cfg = pair.Server.ResolveDependency<IConfigurationManager>();
            var originalFraction = cfg.GetCVar(CMUMedicalCCVars.SurgeryLimbReattachStartingHpFraction);
            var patient = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var index = em.System<CMUMedicalBodyIndexSystem>();
            var hands = em.System<SharedHandsSystem>();
            try
            {
                Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Torso, BodyPartSymmetry.None), out var torso), Is.True);
                Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Arm, BodyPartSymmetry.Left), out var arm), Is.True);
                var carrier = em.System<DetachableOrganSystem>().Detach(arm);
                Assert.That(carrier, Is.Not.Null);
                Assert.That(hands.TryPickupAnyHand(surgeon, carrier!.Value), Is.True);
                var step = em.System<SharedCMSurgerySystem>().GetSingleton("CMUSurgeryStepReattachLimb")!.Value;
                var effect = new CMSurgeryStepEvent(surgeon, patient, torso, new List<EntityUid>())
                {
                    Used = carrier,
                    TargetType = BodyPartType.Arm,
                    TargetSymmetry = BodyPartSymmetry.Left,
                };
                cfg.SetCVar(CMUMedicalCCVars.SurgeryLimbReattachStartingHpFraction, fraction);

                Assert.That(em.System<SharedCMUSurgerySystem>().TryExecuteStep(step, ref effect), Is.EqualTo(CMUSurgeryStepOutcome.Failed));
                Assert.Multiple(() =>
                {
                    Assert.That(hands.IsHolding(surgeon, carrier), Is.True);
                    Assert.That(em.GetComponent<BodyPartComponent>(arm).Body, Is.EqualTo(carrier));
                    Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Arm, BodyPartSymmetry.Left), out _), Is.False);
                    Assert.That(em.HasComponent<CMUReattachCompleteComponent>(torso), Is.False);
                });
            }
            finally
            {
                cfg.SetCVar(CMUMedicalCCVars.SurgeryLimbReattachStartingHpFraction, originalFraction);
                em.DeleteEntity(surgeon);
                em.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LateChildRemovalRefusalRollsBackReattachmentAndHeldCarrier()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var patient = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var index = em.System<CMUMedicalBodyIndexSystem>();
            var hands = em.System<SharedHandsSystem>();
            try
            {
                Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Torso, BodyPartSymmetry.None), out var torso), Is.True);
                Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Arm, BodyPartSymmetry.Left), out var arm), Is.True);
                Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Hand, BodyPartSymmetry.Left), out var hand), Is.True);
                var carrier = em.System<DetachableOrganSystem>().Detach(arm);
                Assert.That(carrier, Is.Not.Null);
                Assert.That(hands.TryPickupAnyHand(surgeon, carrier!.Value), Is.True);
                var veto = em.EnsureComponent<CMUSurgeryTransferVetoTestComponent>(carrier.Value);
                veto.Target = hand;
                veto.Root = arm;
                veto.Destination = patient;
                var step = em.System<SharedCMSurgerySystem>().GetSingleton("CMUSurgeryStepReattachLimb")!.Value;
                var effect = new CMSurgeryStepEvent(surgeon, patient, torso, new List<EntityUid>())
                {
                    Used = carrier,
                    TargetType = BodyPartType.Arm,
                    TargetSymmetry = BodyPartSymmetry.Left,
                };

                Assert.That(em.System<SharedCMUSurgerySystem>().TryExecuteStep(step, ref effect), Is.EqualTo(CMUSurgeryStepOutcome.Failed));
                Assert.Multiple(() =>
                {
                    Assert.That(veto.RootWasMovedAtRefusal, Is.True, "The veto must occur after the root moved, not during preflight.");
                    Assert.That(em.GetComponent<BodyPartComponent>(arm).Body, Is.EqualTo(carrier));
                    Assert.That(em.GetComponent<BodyPartComponent>(hand).Body, Is.EqualTo(carrier));
                    Assert.That(hands.IsHolding(surgeon, carrier), Is.True);
                    Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Arm, BodyPartSymmetry.Left), out _), Is.False);
                    Assert.That(em.HasComponent<CMUReattachCompleteComponent>(torso), Is.False);
                });
            }
            finally
            {
                em.DeleteEntity(surgeon);
                em.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WrongDonorThroughToolDoAfterLeavesReplacementArmed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var em = pair.Server.EntMan;
        EntityUid patient = default;
        EntityUid surgeon = default;
        EntityUid torso = default;
        EntityUid donor = default;
        CMUSurgeryArmedStepComponent armed = default!;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                patient = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                surgeon = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                var surgery = em.System<SharedCMUSurgerySystem>();
                Assert.That(em.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(patient,
                    new(BodyPartType.Torso, BodyPartSymmetry.None), out torso), Is.True);
                Assert.That(surgery.TryGetOrganInSlot(torso, "liver", out var liver), Is.True);
                Assert.That(surgery.TryGetOrganInSlot(torso, "kidneys", out donor), Is.True);
                Assert.That(em.System<SharedBodySystem>().RemoveOrgan(liver), Is.True);
                em.DeleteEntity(liver);
                Assert.That(em.System<SharedBodySystem>().RemoveOrgan(donor), Is.True);
                Assert.That(em.System<SharedHandsSystem>().TryPickupAnyHand(surgeon, donor), Is.True);
                em.System<SkillsSystem>().SetSkill(surgeon, "RMCSkillSurgery", 4);
                em.System<StandingStateSystem>().Down(patient, playSound: false, dropHeldItems: false, force: true);
                em.System<SharedPainShockSystem>().AddPainSuppressionProfile(patient, 1f, 4, 0f, TimeSpan.FromSeconds(30));
                em.EnsureComponent<CMIncisionOpenComponent>(torso);
                em.EnsureComponent<CMBleedersClampedComponent>(torso);
                em.EnsureComponent<CMSkinRetractedComponent>(torso);
                em.EnsureComponent<CMRibcageOpenComponent>(torso);
                em.EnsureComponent<CMULiverRemovedMarkerComponent>(torso);
                em.EnsureComponent<CMULiverVesselsClampedMarkerComponent>(torso);
                var flow = em.System<CMUSurgeryFlowSystem>();
                armed = flow.TryArmStep(surgeon, patient, torso, "CMUSurgeryReplaceLiver", 1,
                    BodyPartType.Torso, BodyPartSymmetry.None)!;
                Assert.That(armed, Is.Not.Null);
                Assert.That(armed.StepIndex, Is.EqualTo(1));
                Assert.That(flow.TryHandleArmedToolUse(patient, armed, surgeon, donor, patient,
                    out var handled, out var started), Is.True);
                Assert.That(handled && started, Is.True);
            });
            await pair.RunSeconds(3);
            await pair.Server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(em.GetComponent<CMUSurgeryArmedStepComponent>(patient), Is.SameAs(armed));
                    Assert.That(armed.StepIndex, Is.EqualTo(1));
                    Assert.That(em.HasComponent<CMULiverRemovedMarkerComponent>(torso), Is.True);
                    Assert.That(em.HasComponent<CMULiverVesselsClampedMarkerComponent>(torso), Is.True);
                    Assert.That(em.System<SharedCMUSurgerySystem>().TryGetOrganInSlot(torso, "liver", out _), Is.False);
                    Assert.That(em.System<SharedHandsSystem>().IsHolding(surgeon, donor), Is.True);
                });
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                em.DeleteEntity(surgeon);
                em.DeleteEntity(patient);
            });
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WrongOrganPreservesMarkersAndHeldDonorsThenExactDamagedDonorSucceeds()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var patient = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var body = em.System<SharedBodySystem>();
            var surgery = em.System<SharedCMUSurgerySystem>();
            var hands = em.System<SharedHandsSystem>();
            var index = em.System<CMUMedicalBodyIndexSystem>();
            try
            {
                Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Torso, BodyPartSymmetry.None), out var torso), Is.True);
                Assert.That(surgery.TryGetOrganInSlot(torso, "liver", out var liver), Is.True);
                Assert.That(surgery.TryGetOrganInSlot(torso, "kidneys", out var kidneys), Is.True);
                var liverHealth = em.GetComponent<OrganHealthComponent>(liver);
                var injury = new DamageSpecifier();
                injury.DamageDict["Blunt"] = liverHealth.Current - FixedPoint2.New(10);
                var damage = new OrganDamagedEvent(patient, liver, injury, OrganDamageSource.Surgery);
                em.EventBus.RaiseLocalEvent(liver, ref damage);
                Assert.That(body.RemoveOrgan(liver), Is.True);
                Assert.That(body.RemoveOrgan(kidneys), Is.True);
                Assert.That(hands.TryPickupAnyHand(surgeon, liver), Is.True);
                Assert.That(hands.TryPickupAnyHand(surgeon, kidneys), Is.True);
                em.EnsureComponent<CMULiverRemovedMarkerComponent>(torso);

                var step = em.System<SharedCMSurgerySystem>().GetSingleton("CMUSurgeryStepReinsertLiver")!.Value;
                var effect = new CMSurgeryStepEvent(surgeon, patient, torso, new List<EntityUid> { liver, kidneys })
                {
                    Used = kidneys,
                };
                Assert.That(surgery.TryExecuteStep(step, ref effect), Is.EqualTo(CMUSurgeryStepOutcome.Failed));
                Assert.Multiple(() =>
                {
                    Assert.That(em.HasComponent<CMULiverRemovedMarkerComponent>(torso), Is.True);
                    Assert.That(hands.IsHolding(surgeon, kidneys), Is.True);
                    Assert.That(hands.IsHolding(surgeon, liver), Is.True);
                    Assert.That(surgery.TryGetOrganInSlot(torso, "liver", out _), Is.False);
                });

                effect.Used = liver;
                Assert.That(surgery.TryExecuteStep(step, ref effect), Is.EqualTo(CMUSurgeryStepOutcome.Succeeded));
                Assert.Multiple(() =>
                {
                    Assert.That(surgery.TryGetOrganInSlot(torso, "liver", out var inserted) && inserted == liver, Is.True);
                    Assert.That(em.HasComponent<CMULiverRemovedMarkerComponent>(torso), Is.False);
                    Assert.That(hands.IsHolding(surgeon, kidneys), Is.True);
                    Assert.That(em.System<StatusEffectsSystem>().HasStatusEffect(patient, "StatusEffectCMUHepaticFailure"), Is.True);
                });
            }
            finally
            {
                em.DeleteEntity(surgeon);
                em.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(BodyPartType.Arm, BodyPartSymmetry.Left)]
    [TestCase(BodyPartType.Hand, BodyPartSymmetry.Right)]
    [TestCase(BodyPartType.Leg, BodyPartSymmetry.Right)]
    [TestCase(BodyPartType.Foot, BodyPartSymmetry.Left)]
    public async Task SurgicalAmputationCommitsEveryAdvertisedExtremity(BodyPartType type, BodyPartSymmetry symmetry)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var patient = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var index = em.System<CMUMedicalBodyIndexSystem>();
            try
            {
                Assert.That(index.TryGetBodyPart(patient, new(type, symmetry), out var part), Is.True);
                var step = em.System<SharedCMSurgerySystem>().GetSingleton("CMUSurgeryStepAmputateLimb")!.Value;
                var effect = new CMSurgeryStepEvent(surgeon, patient, part, new List<EntityUid>());
                Assert.That(em.System<SharedCMUSurgerySystem>().TryExecuteStep(step, ref effect, automated: true),
                    Is.EqualTo(CMUSurgeryStepOutcome.Succeeded));
                Assert.Multiple(() =>
                {
                    Assert.That(index.TryGetBodyPart(patient, new(type, symmetry), out _), Is.False);
                    Assert.That(em.GetComponent<BodyPartComponent>(part).Body, Is.Not.EqualTo(patient));
                    Assert.That(em.System<SharedHandsSystem>().EnumerateHeld(surgeon), Is.Not.Empty);
                });
            }
            finally
            {
                em.DeleteEntity(surgeon);
                em.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ReattachmentUsesSelectedHandAndDoesNotHealAWorseFracture(bool overrideHealthFraction)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var patient = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var index = em.System<CMUMedicalBodyIndexSystem>();
            var hands = em.System<SharedHandsSystem>();
            try
            {
                Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Torso, BodyPartSymmetry.None), out var torso), Is.True);
                Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Arm, BodyPartSymmetry.Left), out var leftArm), Is.True);
                Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Arm, BodyPartSymmetry.Right), out var rightArm), Is.True);
                var leftAttempt = new BodyPartSeverAttemptEvent(patient, leftArm, BodyPartType.Arm, Surgical: true);
                em.EventBus.RaiseLocalEvent(leftArm, ref leftAttempt);
                var rightAttempt = new BodyPartSeverAttemptEvent(patient, rightArm, BodyPartType.Arm, Surgical: true);
                em.EventBus.RaiseLocalEvent(rightArm, ref rightAttempt);
                Assert.That(leftAttempt.Succeeded && rightAttempt.Succeeded, Is.True);
                Assert.That(hands.TryPickupAnyHand(surgeon, leftAttempt.DetachedBody!.Value), Is.True);
                Assert.That(hands.TryPickupAnyHand(surgeon, rightAttempt.DetachedBody!.Value), Is.True);
                var fracture = em.EnsureComponent<FractureComponent>(rightArm);
                em.System<SharedFractureSystem>().SetSeverity((rightArm, fracture), FractureSeverity.Shattered, forceUpgrade: true);

                var step = em.System<SharedCMSurgerySystem>().GetSingleton(overrideHealthFraction
                    ? "CMUSurgeryTestStepReattachQuarterHealth"
                    : "CMUSurgeryStepReattachLimb")!.Value;
                var effect = new CMSurgeryStepEvent(surgeon, patient, torso, new List<EntityUid>())
                {
                    Used = rightAttempt.DetachedBody,
                    TargetType = BodyPartType.Arm,
                    TargetSymmetry = BodyPartSymmetry.Right,
                };
                Assert.That(em.System<SharedCMUSurgerySystem>().TryExecuteStep(step, ref effect), Is.EqualTo(CMUSurgeryStepOutcome.Succeeded));
                Assert.Multiple(() =>
                {
                    Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Arm, BodyPartSymmetry.Right), out var attached) && attached == rightArm, Is.True);
                    Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Arm, BodyPartSymmetry.Left), out _), Is.False);
                    Assert.That(hands.IsHolding(surgeon, leftAttempt.DetachedBody), Is.True);
                    Assert.That(em.GetComponent<FractureComponent>(rightArm).Severity, Is.EqualTo(FractureSeverity.Shattered));
                    var health = em.GetComponent<BodyPartHealthComponent>(rightArm);
                    var fraction = overrideHealthFraction
                        ? 0.25f
                        : pair.Server.ResolveDependency<IConfigurationManager>().GetCVar(CMUMedicalCCVars.SurgeryLimbReattachStartingHpFraction);
                    Assert.That(health.Current, Is.EqualTo(health.Max * (FixedPoint2) fraction));
                });
            }
            finally
            {
                em.DeleteEntity(surgeon);
                em.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ScannerRejectsForgedPhaseNonfiniteAndStaleAttempts()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var user = em.SpawnEntity(null, MapCoordinates.Nullspace);
            var patient = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            try
            {
                var index = em.System<CMUMedicalBodyIndexSystem>();
                Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Torso, BodyPartSymmetry.None), out var torso), Is.True);
                em.EnsureComponent<InternalBleedingComponent>(torso);
                var scanner = new CMUBodyScannerConsoleComponent
                {
                    PulseWindowSize = 0.01f,
                    MinPulseWindowSize = 0.01f,
                    PulseGraceSize = 0f,
                };
                var calibration = em.System<CMUBodyScannerCalibrationSystem>();
                Assert.That(calibration.ResetPuzzle(user, patient, scanner), Is.True);
                var view = calibration.BuildView(user, patient, true, scanner);
                var target = view.Targets.First(t => !t.IsDecoy);
                Assert.That(calibration.TryConfirmPuzzle(user, patient, scanner, target.LayerId, target.Id,
                    view.PulseTargetPhase, view.AttemptId, 0), Is.True);
                Assert.That(em.GetComponent<CMUBodyScannerPuzzleProgressComponent>(user).Assignments, Is.Empty);
                Assert.That(em.HasComponent<CMUBodyScannerSurgerySpeedComponent>(user), Is.False);
                Assert.That(calibration.TryConfirmPuzzle(user, patient, scanner, target.LayerId, target.Id,
                    float.NaN, view.AttemptId, 0), Is.False);
                scanner.PulseWindowSize = scanner.MinPulseWindowSize = scanner.PulseGraceSize = 1f;
                Assert.That(calibration.TryConfirmPuzzle(user, patient, scanner, target.LayerId, target.Id,
                    0f, view.AttemptId + 1, 0), Is.False);
                Assert.That(calibration.TryConfirmPuzzle(user, patient, scanner, target.LayerId, target.Id,
                    0f, view.AttemptId, 1), Is.False);
                Assert.That(calibration.TryConfirmPuzzle(user, patient, scanner, target.LayerId, target.Id,
                    0f, view.AttemptId, 0), Is.True);
            }
            finally
            {
                em.DeleteEntity(user);
                em.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }
}

[RegisterComponent]
public sealed partial class CMUSurgeryTransferVetoTestComponent : Component
{
    public EntityUid Target;
    public EntityUid Root;
    public EntityUid Destination;
    public int Checks;
    public bool RootWasMovedAtRefusal;
}

public sealed class CMUSurgeryTransferVetoTestSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUSurgeryTransferVetoTestComponent, ContainerIsRemovingAttemptEvent>(OnRemoving);
    }

    private void OnRemoving(Entity<CMUSurgeryTransferVetoTestComponent> ent, ref ContainerIsRemovingAttemptEvent args)
    {
        if (args.EntityUid != ent.Comp.Target || ++ent.Comp.Checks <= 1)
            return;
        ent.Comp.RootWasMovedAtRefusal = Comp<BodyPartComponent>(ent.Comp.Root).Body == ent.Comp.Destination;
        args.Cancel();
    }
}
