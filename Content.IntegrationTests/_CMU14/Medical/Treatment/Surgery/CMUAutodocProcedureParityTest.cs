using System.IO;
using System.Numerics;
using Content.Server.CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Injuries.Wounds.Events;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared.CMU14.Medical.Treatment.Surgery.Markers;
using Content.Shared.CMU14.Medical.Treatment.Surgery.Traits;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Standing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.IntegrationTests.CMU14.Medical.Treatment.Surgery;

[TestFixture]
public sealed class CMUAutodocProcedureParityTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: CMUSurgeryStepDebrideContaminatedWound
          id: CMUAutodocParityDeletionCleanupStep
          components:
          - type: CMSurgeryStep
            add:
            - type: CMUBoneAlignedMarker
        """;

    [Test]
    public async Task LostOwnershipBeforeStepCannotApplyCleanupOrMarkers()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var patient = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            try
            {
                var hand = PrepareFracture(em, patient);
                var step = em.System<SharedCMSurgerySystem>().GetSingleton("CMUAutodocParityDeletionCleanupStep")!.Value;
                var effect = new CMSurgeryStepEvent(patient, patient, hand, new())
                {
                    IsCurrent = static () => false,
                };
                var result = em.System<SharedCMUSurgerySystem>().TryExecuteStep(step, ref effect, automated: true);
                Assert.Multiple(() =>
                {
                    Assert.That(result, Is.EqualTo(CMUSurgeryStepOutcome.Failed));
                    Assert.That(effect.Failed, Is.True);
                    Assert.That(em.HasComponent<CMUContaminatedWoundComponent>(hand), Is.True,
                        "a lost attempt must be rejected before applying its anatomical cleanup");
                    Assert.That(em.HasComponent<CMUBoneAlignedMarkerComponent>(hand), Is.False);
                    Assert.That(em.GetComponent<FractureComponent>(hand).Severity, Is.EqualTo(FractureSeverity.Simple));
                });
            }
            finally
            {
                em.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeletedAnatomyDuringCleanupCannotReceiveLaterMarkersOrSucceed()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var patient = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            try
            {
                var hand = PrepareFracture(em, patient);
                var probe = em.GetComponent<CMUAutodocAccessProbeComponent>(hand);
                probe.DeleteOnTraitCleanup = true;
                var step = em.System<SharedCMSurgerySystem>().GetSingleton("CMUAutodocParityDeletionCleanupStep")!.Value;
                var effect = new CMSurgeryStepEvent(patient, patient, hand, new());
                var result = em.System<SharedCMUSurgerySystem>().TryExecuteStep(step, ref effect, automated: true);
                Assert.Multiple(() =>
                {
                    Assert.That(probe.DeletedDuringCleanup, Is.True);
                    Assert.That(em.EntityExists(hand), Is.False);
                    Assert.That(result, Is.EqualTo(CMUSurgeryStepOutcome.Failed));
                    Assert.That(effect.Failed, Is.True);
                });
            }
            finally
            {
                em.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task RegenerationRemainsBoundToTheSelectedMissingSlotParent(bool replaceParent)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.EntMan;
        Rig rig = default!;
        EntityUid handCarrier = default;
        EntityUid armCarrier = default;
        EntityUid originalArm = default;
        string selectedSlot = default!;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                rig = CreateRig(em, map.GridCoords);
                originalArm = Part(em, rig.Patient, BodyPartType.Arm, BodyPartSymmetry.Left);
                var originalHand = Part(em, rig.Patient, BodyPartType.Hand, BodyPartSymmetry.Left);
                var detach = em.System<DetachableOrganSystem>();
                handCarrier = detach.Detach(originalHand)!.Value;
                Queue(pair.Server.ResolveDependency<IRobustSerializer>(), em, rig, rig.Patient, "CMUAutodocRegenerateLimb");
                Assert.That(rig.Component.Queue[0].TargetAnchor, Is.EqualTo(originalArm));
                Assert.That(rig.Component.Queue[0].TargetSlot, Is.Not.Null.And.Not.Empty);
                selectedSlot = rig.Component.Queue[0].TargetSlot!;
                if (replaceParent)
                {
                    var torso = Part(em, rig.Patient, BodyPartType.Torso, BodyPartSymmetry.None);
                    var slot = em.System<CMUMedicalBodyIndexSystem>().GetBodyPartSlots(torso).Single(s => s.Part == originalArm);
                    armCarrier = detach.Detach(originalArm)!.Value;
                    var replacementArm = em.SpawnEntity("CMUPartHumanLeftArm", map.GridCoords);
                    Assert.That(em.System<SharedBodySystem>().AttachPart(torso, slot.SlotId, replacementArm), Is.True);
                    Assert.That(Part(em, rig.Patient, BodyPartType.Arm, BodyPartSymmetry.Left), Is.EqualTo(replacementArm));
                    Assert.That(em.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(rig.Patient,
                        new(BodyPartType.Hand, BodyPartSymmetry.Left), out _), Is.False);
                }
                Start(em, rig);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(1.25f));
            await pair.Server.WaitAssertion(() =>
            {
                var attached = em.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(rig.Patient,
                    new(BodyPartType.Hand, BodyPartSymmetry.Left), out var hand);
                Assert.That(attached, Is.EqualTo(!replaceParent));
                Assert.That(rig.Component.IsRunning, Is.False);
                Assert.That(rig.Component.Queue.Count, Is.EqualTo(replaceParent ? 1 : 0));
                if (replaceParent)
                    Assert.That(rig.Component.Patient, Is.EqualTo(rig.Patient));
                else
                    Assert.That(em.System<CMUMedicalBodyIndexSystem>().TryGetBodyPartInSlot(originalArm,
                        selectedSlot, out var current) && current == hand, Is.True);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                if (em.EntityExists(handCarrier)) em.DeleteEntity(handCarrier);
                if (em.EntityExists(armCarrier)) em.DeleteEntity(armCarrier);
                if (rig != null) DeleteRig(em, rig);
            });
        }
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task WoundTreatmentCallbackDepartureCannotClearEscharOrRestoreStructure(bool detachPart)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.EntMan;
        Rig rig = default!;
        EntityUid hand = default;
        CMUAutodocAccessProbeComponent probe = default!;
        FixedPoint2 initialHealth = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                rig = CreateRig(em, map.GridCoords);
                hand = Part(em, rig.Patient, BodyPartType.Hand, BodyPartSymmetry.Left);
                var health = em.GetComponent<BodyPartHealthComponent>(hand);
                // A zero-damage, already-treated retained-fragment row naturally
                // expires before the queue deadline. Use a real untreated injury.
                Assert.That(em.System<SharedBodyPartHealthSystem>().TryApplyPartDamage(rig.Patient, hand,
                    new DamageSpecifier { DamageDict = { ["Slash"] = FixedPoint2.New(6) } }), Is.True);
                var wounds = em.GetComponent<BodyPartWoundComponent>(hand);
                Assert.That(em.System<CMUWoundLedgerSystem>().GetEntries(wounds)
                    .Count(entry => !entry.Wound.Treated && entry.Wound.Damage > FixedPoint2.Zero), Is.EqualTo(1));
                em.System<SharedBodyPartHealthSystem>().SetCurrent((hand, health), health.Max / 2);
                initialHealth = health.Current;
                em.EnsureComponent<CMUEscharComponent>(hand);
                probe = em.EnsureComponent<CMUAutodocAccessProbeComponent>(hand);
                probe.PodOnWoundTreatment = rig.Pod;
                probe.DetachOnWoundTreatment = detachPart;
                Queue(pair.Server.ResolveDependency<IRobustSerializer>(), em, rig, hand, "CMUAutodocRepairWounds");
                Start(em, rig);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(1.25f));
            await pair.Server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(probe.TreatmentCallbackRan, Is.True, "Exercise the actual WoundTreated callback during queue execution.");
                    Assert.That(em.HasComponent<CMUEscharComponent>(hand), Is.True);
                    Assert.That(em.GetComponent<BodyPartHealthComponent>(hand).Current, Is.EqualTo(initialHealth));
                    Assert.That(rig.Component.IsRunning, Is.False);
                    Assert.That(rig.Component.Queue.Count, Is.EqualTo(detachPart ? 1 : 0));
                });
                if (detachPart)
                    Assert.That(em.GetComponent<BodyPartComponent>(hand).Body, Is.EqualTo(probe.DetachedCarrier));
                else
                    Assert.That(rig.Component.Patient, Is.Null);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                if (probe?.DetachedCarrier is { } carrier && em.EntityExists(carrier)) em.DeleteEntity(carrier);
                if (rig != null) DeleteRig(em, rig);
            });
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ManualToolsAndAutodocBothOpenCleanRepairAndCloseSelectedSite()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.EntMan;
        Rig rig = default!;
        EntityUid manual = default;
        EntityUid manualHand = default;
        EntityUid automaticHand = default;
        EntityUid tool = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                rig = CreateRig(em, map.GridCoords);
                manual = em.SpawnEntity("CMMobHuman", map.GridCoords);
                em.System<StandingStateSystem>().Down(manual, playSound: false, dropHeldItems: false, force: true);
                // Expertise removes random field-surgery failures; tools and DoAfters still execute normally.
                em.EnsureComponent<BypassSkillChecksComponent>(rig.Operator);
                manualHand = PrepareFracture(em, manual);
                automaticHand = PrepareFracture(em, rig.Patient);
                var flow = em.System<CMUSurgeryFlowSystem>();
                Assert.That(flow.TryArmStep(rig.Operator, manual, manualHand, "CMUSurgerySetSimpleFracture", 0), Is.Not.Null);
                var armed = em.GetComponent<CMUSurgeryArmedStepComponent>(manual);
                Assert.That(armed.SurgeryId, Is.EqualTo("CMUSurgeryOpenSoftTissue"));
                Assert.That(armed.StepIndex, Is.Zero);
                Queue(pair.Server.ResolveDependency<IRobustSerializer>(), em, rig, automaticHand, "CMUSurgerySetSimpleFracture");
                Start(em, rig);
            });

            var finished = false;
            for (var i = 0; i < 16 && !finished; i++)
            {
                await pair.Server.WaitAssertion(() =>
                {
                    if (em.EntityExists(tool))
                        em.DeleteEntity(tool);
                    var flow = em.System<CMUSurgeryFlowSystem>();
                    if (!em.TryGetComponent<CMUSurgeryArmedStepComponent>(manual, out var armed))
                    {
                        if (!em.HasComponent<CMIncisionOpenComponent>(manualHand))
                        {
                            finished = true;
                            return;
                        }
                        armed = flow.TryArmStep(rig.Operator, manual, manualHand, "CMUSurgeryCloseIncision", 0,
                            allowSamePartInFlightSwitch: true);
                    }
                    Assert.That(armed, Is.Not.Null);
                    tool = em.SpawnEntity(ToolPrototype(armed!.RequiredToolCategory), map.GridCoords);
                    Assert.That(em.System<SharedHandsSystem>().TryPickupAnyHand(rig.Operator, tool), Is.True);
                    Assert.That(flow.TryHandleArmedToolUse(manual, armed, rig.Operator, tool, manualHand,
                        out var handled, out var started), Is.True);
                    Assert.That(handled && started, Is.True, armed.SurgeryId);
                });
                if (!finished)
                    await pair.RunTicksSync(pair.SecondsToTicks(2.75f));
            }

            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(finished, Is.True, "Manual public tool interactions must finish within the bounded step count.");
                foreach (var part in new[] { manualHand, automaticHand })
                {
                    var probe = em.GetComponent<CMUAutodocAccessProbeComponent>(part);
                    Assert.Multiple(() =>
                    {
                        Assert.That(probe.Opened, Is.True);
                        Assert.That(probe.CleanedWithAccess, Is.True, "Cleanup must occur after physical soft-tissue access.");
                        Assert.That(em.HasComponent<CMUContaminatedWoundComponent>(part), Is.False);
                        Assert.That(em.HasComponent<FractureComponent>(part), Is.False);
                        Assert.That(em.HasComponent<CMIncisionOpenComponent>(part), Is.False);
                        Assert.That(em.HasComponent<CMSkinRetractedComponent>(part), Is.False);
                    });
                }
                Assert.That(rig.Component.Queue, Is.Empty);
                Assert.That(rig.Component.Patient, Is.Null);
                Assert.That(em.HasComponent<CMUSurgeryInProgressComponent>(rig.Patient), Is.False);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                if (em.EntityExists(tool)) em.DeleteEntity(tool);
                if (em.EntityExists(manual)) em.DeleteEntity(manual);
                if (rig != null) DeleteRig(em, rig);
            });
        }
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task MissingOrRoboticReplacementCannotInheritQueuedOrganicSite(bool replaceWithRobotic)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.EntMan;
        Rig rig = default!;
        EntityUid carrier = default;
        EntityUid replacement = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                rig = CreateRig(em, map.GridCoords);
                var hand = PrepareFracture(em, rig.Patient);
                Queue(pair.Server.ResolveDependency<IRobustSerializer>(), em, rig, hand, "CMUSurgerySetSimpleFracture");
                carrier = em.System<DetachableOrganSystem>().Detach(hand)!.Value;
                if (replaceWithRobotic)
                {
                    var arm = Part(em, rig.Patient, BodyPartType.Arm, BodyPartSymmetry.Left);
                    var slot = em.System<CMUMedicalBodyIndexSystem>().GetBodyPartSlots(arm).Single(s => s.Type == BodyPartType.Hand);
                    replacement = em.SpawnEntity("CMUPartRoboticLeftHand", map.GridCoords);
                    Assert.That(em.System<SharedBodySystem>().AttachPart(arm, slot.SlotId, replacement), Is.True);
                    var health = em.GetComponent<BodyPartHealthComponent>(replacement);
                    em.System<SharedBodyPartHealthSystem>().SetCurrent((replacement, health), health.Max - 10);
                    var manual = em.System<CMUSurgeryDispatchSystem>().BuildPartEntries(rig.Patient, rig.Operator);
                    var replacementRow = manual.Single(p => p.Part == em.GetNetEntity(replacement));
                    Assert.That(replacementRow.EligibleSurgeries.Any(s => s.Category == "fracture"), Is.False);
                    var automatic = State(em, rig).Parts.Single(p => p.Part == em.GetNetEntity(replacement));
                    Assert.That(automatic.EligibleSurgeries.Any(s => s.SurgeryId == "CMUAutodocRepairWounds"), Is.True,
                        "The existing automated structural repair capability for robotic limbs is preserved.");
                }
                Assert.That(em.System<CMUSurgeryRulebookSystem>().IsProcedureEligible(rig.Patient, hand,
                    rig.Operator, "CMUSurgerySetSimpleFracture"), Is.False);
                Start(em, rig);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(1.25f));
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(rig.Component.IsRunning, Is.False);
                Assert.That(rig.Component.Queue.Count, Is.EqualTo(1));
                Assert.That(rig.Component.Patient, Is.EqualTo(rig.Patient));
                if (replaceWithRobotic)
                {
                    var health = em.GetComponent<BodyPartHealthComponent>(replacement);
                    Assert.That(health.Current, Is.EqualTo(health.Max - 10));
                    Assert.That(em.HasComponent<CMIncisionOpenComponent>(replacement), Is.False);
                }
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                if (em.EntityExists(carrier)) em.DeleteEntity(carrier);
                if (rig != null) DeleteRig(em, rig);
            });
        }
        await pair.CleanReturnAsync();
    }

    [TestCase("healed")]
    [TestCase("missing")]
    [TestCase("transplant")]
    public async Task QueuedOrganRepairRejectsChangedConditionOrTransplantedOrgan(string change)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.EntMan;
        Rig rig = default!;
        EntityUid donor = default;
        EntityUid removed = default;
        EntityUid torso = default;
        EntityUid selected = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                rig = CreateRig(em, map.GridCoords);
                torso = Part(em, rig.Patient, BodyPartType.Torso, BodyPartSymmetry.None);
                Assert.That(em.System<CMUMedicalBodyIndexSystem>().TryGetOrganInSlot(torso, "liver", out selected), Is.True);
                DamageOrgan(em, rig.Patient, selected);
                Queue(pair.Server.ResolveDependency<IRobustSerializer>(), em, rig, torso, "CMUSurgeryRepairLiver");
                Assert.That(rig.Component.Queue[0].TargetOrgan, Is.EqualTo(selected));
                if (change == "transplant")
                {
                    donor = em.SpawnEntity("CMMobHuman", map.GridCoords);
                    var donorTorso = Part(em, donor, BodyPartType.Torso, BodyPartSymmetry.None);
                    Assert.That(em.System<CMUMedicalBodyIndexSystem>().TryGetOrganInSlot(donorTorso, "liver", out var newLiver), Is.True);
                    DamageOrgan(em, donor, newLiver);
                    var body = em.System<SharedBodySystem>();
                    Assert.That(body.RemoveOrgan(selected), Is.True);
                    removed = selected;
                    Assert.That(body.RemoveOrgan(newLiver), Is.True);
                    Assert.That(body.InsertOrgan(torso, newLiver, "liver"), Is.True);
                    selected = newLiver;
                }
                else if (change == "missing")
                {
                    Assert.That(em.System<SharedBodySystem>().RemoveOrgan(selected), Is.True);
                    removed = selected;
                }
                else
                {
                    em.System<SharedOrganHealthSystem>().HealOrgan((selected, null), rig.Patient, 100);
                }
                var manual = em.System<CMUSurgeryDispatchSystem>().BuildPartEntries(rig.Patient, rig.Operator);
                Assert.That(manual.Single(p => p.Part == em.GetNetEntity(torso)).EligibleSurgeries
                    .Any(s => s.SurgeryId == "CMUSurgeryRepairLiver"), Is.EqualTo(change == "transplant"));
                Start(em, rig);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(1.25f));
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(rig.Component.IsRunning, Is.False);
                Assert.That(rig.Component.Queue.Count, Is.EqualTo(1));
                Assert.That(em.HasComponent<CMIncisionOpenComponent>(torso), Is.False,
                    "Failure must occur before any access or repair step.");
                if (change == "transplant")
                    Assert.That(em.GetComponent<OrganHealthComponent>(selected).Stage, Is.EqualTo(OrganDamageStage.Damaged));
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                if (em.EntityExists(removed)) em.DeleteEntity(removed);
                if (em.EntityExists(donor)) em.DeleteEntity(donor);
                if (rig != null) DeleteRig(em, rig);
            });
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DepartureDuringAccessStepStopsBeforeCleanupAndRepair()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.EntMan;
        Rig rig = default!;
        EntityUid hand = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                rig = CreateRig(em, map.GridCoords);
                hand = PrepareFracture(em, rig.Patient);
                em.GetComponent<CMUAutodocAccessProbeComponent>(hand).EjectFromPodOnOpening = rig.Pod;
                Queue(pair.Server.ResolveDependency<IRobustSerializer>(), em, rig, hand, "CMUSurgerySetSimpleFracture");
                Start(em, rig);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(1.25f));
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(em.GetComponent<CMUAutodocAccessProbeComponent>(hand).Opened, Is.True);
                Assert.That(rig.Component.Patient, Is.Null);
                Assert.That(rig.Component.Queue, Is.Empty);
                Assert.That(rig.Component.IsRunning, Is.False);
                Assert.That(em.HasComponent<FractureComponent>(hand), Is.True);
                Assert.That(em.HasComponent<CMUContaminatedWoundComponent>(hand), Is.True);
                Assert.That(em.HasComponent<CMSkinRetractedComponent>(hand), Is.False);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => { if (rig != null) DeleteRig(em, rig); });
        }
        await pair.CleanReturnAsync();
    }

    private static Rig CreateRig(IEntityManager em, EntityCoordinates coordinates)
    {
        var pod = em.SpawnEntity("CMUAutodocPod", coordinates.Offset(new Vector2(1, 0)));
        var console = em.SpawnEntity("CMUAutodocConsole", coordinates);
        var patient = em.SpawnEntity("CMMobHuman", coordinates);
        var actor = em.SpawnEntity("CMMobHuman", coordinates);
        em.System<SkillsSystem>().SetSkill(actor, "RMCSkillSurgery", 3);
        var component = em.GetComponent<CMUAutodocPodComponent>(pod);
        Assert.That(em.System<CMUMedicalPatientBaySystem>().TryInsertPatient(pod, component.BodyContainer, patient), Is.True);
        return new(pod, console, patient, actor, component);
    }

    private static EntityUid Part(IEntityManager em, EntityUid patient, BodyPartType type, BodyPartSymmetry symmetry)
    {
        Assert.That(em.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(patient, new(type, symmetry), out var part), Is.True);
        return part;
    }

    private static EntityUid PrepareFracture(IEntityManager em, EntityUid patient)
    {
        var hand = Part(em, patient, BodyPartType.Hand, BodyPartSymmetry.Left);
        Assert.That(em.System<SharedBoneSystem>().SeedFracture(hand, FractureSeverity.Simple), Is.True);
        em.System<SharedCMUSurgicalTraitSystem>().EnsureTrait(hand, CMUSurgicalTrait.ContaminatedWound);
        em.EnsureComponent<CMUAutodocAccessProbeComponent>(hand);
        return hand;
    }

    private static void DamageOrgan(IEntityManager em, EntityUid patient, EntityUid organ)
    {
        var damage = new DamageSpecifier { DamageDict = { ["Blunt"] = FixedPoint2.New(30) } };
        var ev = new OrganDamagedEvent(patient, organ, damage, OrganDamageSource.Direct);
        em.EventBus.RaiseLocalEvent(organ, ref ev, broadcast: true);
        Assert.That(em.GetComponent<OrganHealthComponent>(organ).Stage, Is.EqualTo(OrganDamageStage.Damaged));
    }

    private static CMUAutodocBuiState State(IEntityManager em, Rig rig) => em.System<CMUAutodocSystem>()
        .BuildStateForViewer(rig.Console, em.GetComponent<CMUAutodocConsoleComponent>(rig.Console), rig.Operator);

    private static void Queue(IRobustSerializer serializer, IEntityManager em, Rig rig, EntityUid part, string surgery)
    {
        var state = State(em, rig);
        var row = state.Parts.Single(p => p.Part == em.GetNetEntity(part));
        var entry = row.EligibleSurgeries.Single(s => s.SurgeryId == surgery);
        var message = new CMUAutodocQueueStepMessage(row.Part, row.Type, row.Symmetry, surgery,
            entry.NextStepIndex, state.CommandContext!.Value);
        using var stream = new MemoryStream();
        serializer.Serialize(stream, message);
        stream.Position = 0;
        Send(em, rig, serializer.Deserialize<CMUAutodocQueueStepMessage>(stream));
        Assert.That(rig.Component.Queue.Count, Is.EqualTo(1));
        // Shorten only fixture latency; public commands, scheduler, eligibility and effects remain real.
        rig.Component.Queue[0] = rig.Component.Queue[0] with { DurationSeconds = 1 };
    }

    private static void Start(IEntityManager em, Rig rig) => Send(em, rig,
        new CMUAutodocStartMessage(State(em, rig).CommandContext!.Value));

    private static void Send<T>(IEntityManager em, Rig rig, T message) where T : BoundUserInterfaceMessage
    {
        message.Actor = rig.Operator;
        message.UiKey = CMUAutodocUIKey.Key;
        em.EventBus.RaiseLocalEvent(rig.Console, message);
    }

    private static string ToolPrototype(string? category) => category switch
    {
        "scalpel" => "CMScalpel",
        "hemostat" => "CMHemostat",
        "retractor" => "CMRetractor",
        "bone_setter" => "CMBonesetter",
        "bone_gel" => "CMBoneGel",
        "cautery" => "CMCautery",
        _ => throw new InvalidOperationException($"Unexpected manual step tool: {category}"),
    };

    private static void DeleteRig(IEntityManager em, Rig rig)
    {
        em.DeleteEntity(rig.Patient);
        em.DeleteEntity(rig.Operator);
        em.DeleteEntity(rig.Console);
        em.DeleteEntity(rig.Pod);
    }

    private sealed record Rig(EntityUid Pod, EntityUid Console, EntityUid Patient, EntityUid Operator,
        CMUAutodocPodComponent Component);
}

[RegisterComponent]
public sealed partial class CMUAutodocAccessProbeComponent : Component
{
    public bool Opened;
    public bool CleanedWithAccess;
    public EntityUid? EjectFromPodOnOpening;
    public EntityUid? PodOnWoundTreatment;
    public bool DetachOnWoundTreatment;
    public bool TreatmentCallbackRan;
    public EntityUid? DetachedCarrier;
    public bool DeleteOnTraitCleanup;
    public bool DeletedDuringCleanup;
}

public sealed class CMUAutodocAccessProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMIncisionOpenComponent, ComponentStartup>(OnOpening);
        SubscribeLocalEvent<CMUContaminatedWoundComponent, ComponentShutdown>(OnCleanup);
        SubscribeLocalEvent<WoundTreatedEvent>(OnWoundTreated);
        SubscribeLocalEvent<CMUSurgicalTraitChangedEvent>(OnTraitChanged);
    }

    private void OnOpening(Entity<CMIncisionOpenComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<CMUAutodocAccessProbeComponent>(ent.Owner, out var probe))
            return;
        probe.Opened = true;
        if (probe.EjectFromPodOnOpening is { } pod && TryComp<CMUAutodocPodComponent>(pod, out var machine) &&
            Comp<BodyPartComponent>(ent.Owner).Body is { } patient)
        {
            Assert.That(EntityManager.System<CMUMedicalPatientBaySystem>().TryEjectPatient(pod, machine.BodyContainer, patient), Is.True);
        }
    }

    private void OnCleanup(Entity<CMUContaminatedWoundComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent.Owner) && TryComp<CMUAutodocAccessProbeComponent>(ent.Owner, out var probe))
            probe.CleanedWithAccess = EntityManager.System<CMUSurgeryFlowSystem>().GetSiteState(ent.Owner).Access >= CMUSurgicalAccess.Shallow;
    }

    private void OnWoundTreated(ref WoundTreatedEvent args)
    {
        if (!TryComp<CMUAutodocAccessProbeComponent>(args.Part, out var probe) || probe.TreatmentCallbackRan ||
            probe.PodOnWoundTreatment is not { } pod)
            return;
        probe.TreatmentCallbackRan = true;
        if (probe.DetachOnWoundTreatment)
        {
            probe.DetachedCarrier = EntityManager.System<DetachableOrganSystem>().Detach(args.Part);
            Assert.That(probe.DetachedCarrier, Is.Not.Null);
        }
        else
        {
            var machine = Comp<CMUAutodocPodComponent>(pod);
            Assert.That(EntityManager.System<CMUMedicalPatientBaySystem>().TryEjectPatient(pod, machine.BodyContainer, args.Body), Is.True);
        }
    }

    private void OnTraitChanged(ref CMUSurgicalTraitChangedEvent args)
    {
        if (!args.Removed || !TryComp<CMUAutodocAccessProbeComponent>(args.Part, out var probe) || !probe.DeleteOnTraitCleanup)
            return;
        probe.DeletedDuringCleanup = true;
        Del(args.Part);
    }
}
