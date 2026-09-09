using System.IO;
using System.Numerics;
using Content.Server.CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Medical.Treatment.Surgery;

[TestFixture]
public sealed class CMUAutodocIdentityTest
{
    [Test]
    public async Task ContextlessEjectVerbIsUnavailableAndRecreatedPodRejectsOldCommands()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var rig = CreateRig(em, map.GridCoords);
            try
            {
                var oldContext = Context(em, rig);
                var formerVerb = new AlternativeVerb
                {
                    Category = VerbCategory.Eject,
                    Text = Loc.GetString("medical-scanner-verb-noun-occupant"),
                    Priority = 1,
                };
                using var stream = new MemoryStream();
                var serializer = pair.Server.ResolveDependency<IRobustSerializer>();
                serializer.Serialize(stream, formerVerb);
                stream.Position = 0;
                var requested = serializer.Deserialize<AlternativeVerb>(stream);
                var currentVerbs = em.System<SharedVerbSystem>().GetLocalVerbs(rig.Pod, rig.Operator, typeof(AlternativeVerb));
                Assert.That(currentVerbs.TryGetValue(requested, out _), Is.False,
                    "The regenerated remote verb list cannot resolve the former contextless Eject shortcut.");
                em.RemoveComponent<CMUAutodocPodComponent>(rig.Pod);
                var recreated = em.AddComponent<CMUAutodocPodComponent>(rig.Pod);
                Assert.That(recreated.OccupantGeneration, Is.GreaterThan(oldContext.OccupantGeneration));
                Assert.That(recreated.BodyContainer.ContainedEntity, Is.EqualTo(rig.Patient));
                Send(em, rig, new CMUAutodocEjectPatientMessage(oldContext));
                Assert.That(recreated.BodyContainer.ContainedEntity, Is.EqualTo(rig.Patient));
                var state = em.System<CMUAutodocSystem>().BuildStateForViewer(rig.Console,
                    em.GetComponent<CMUAutodocConsoleComponent>(rig.Console), rig.Operator);
                Send(em, rig, new CMUAutodocEjectPatientMessage(state.CommandContext!.Value));
                Assert.That(recreated.BodyContainer.ContainedEntity, Is.Null);
            }
            finally
            {
                DeleteRig(em, rig);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ProjectedProcedureDurationSurvivesSerializationAndMatchesQueueDeadline()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var rig = CreateRig(em, map.GridCoords);
            try
            {
                var hand = InjureHand(em, rig.Patient, BodyPartSymmetry.Left);
                var state = em.System<CMUAutodocSystem>().BuildStateForViewer(rig.Console,
                    em.GetComponent<CMUAutodocConsoleComponent>(rig.Console), rig.Operator);
                var serializer = pair.Server.ResolveDependency<IRobustSerializer>();
                using var stream = new MemoryStream();
                serializer.Serialize(stream, new CMUAutodocStateMessage(state));
                stream.Position = 0;
                var delivered = serializer.Deserialize<CMUAutodocStateMessage>(stream).State;
                var part = delivered.Parts.Single(p => p.Part == em.GetNetEntity(hand));
                var procedure = part.EligibleSurgeries.Single(p => p.SurgeryId == "CMUAutodocRepairWounds");
                Assert.That(procedure.AutodocDurationSeconds, Is.EqualTo(30f),
                    "The visible procedure retains the established server duration rather than the former client estimate.");
                Assert.That(delivered.CommandContext, Is.Not.Null);
                Send(em, rig, new CMUAutodocQueueStepMessage(part.Part, part.Type, part.Symmetry,
                    procedure.SurgeryId, procedure.NextStepIndex, delivered.CommandContext!.Value));
                Assert.That(rig.Component.Queue.Count, Is.EqualTo(1));
                Assert.That(rig.Component.Queue[0].DurationSeconds, Is.EqualTo(procedure.AutodocDurationSeconds));
                var now = pair.Server.ResolveDependency<IGameTiming>().CurTime;
                Send(em, rig, new CMUAutodocStartMessage(Context(em, rig)));
                Assert.That(rig.Component.IsRunning, Is.True);
                Assert.That((rig.Component.NextStepAt - now).TotalSeconds, Is.EqualTo(procedure.AutodocDurationSeconds));
            }
            finally
            {
                DeleteRig(em, rig);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReentrantPatientReplacementDuringTreatmentPreservesTheNewQueueAndDeadline()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.EntMan;
        Rig rig = default!;
        EntityUid replacement = default;
        CMUAutodocReplacementTestComponent probe = default!;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                rig = CreateRig(em, map.GridCoords);
                replacement = em.SpawnEntity("CMMobHuman", map.GridCoords);
                var replacementHand = InjureHand(em, replacement, BodyPartSymmetry.Right);
                var hand = InjureHand(em, rig.Patient, BodyPartSymmetry.Left);
                var health = em.GetComponent<BodyPartHealthComponent>(hand);
                em.System<SharedBodyPartHealthSystem>().SetCurrent((hand, health), health.Max / 20);
                QueueRepair(em, rig, hand, BodyPartSymmetry.Left);
                Assert.That(rig.Component.Queue.Count, Is.EqualTo(1));
                // Shorten only the fixture's wait; the real queue command, scheduler and effect still execute.
                rig.Component.Queue[0] = rig.Component.Queue[0] with { DurationSeconds = 1 };
                probe = em.EnsureComponent<CMUAutodocReplacementTestComponent>(hand);
                probe.Pod = rig.Pod;
                probe.Console = rig.Console;
                probe.Operator = rig.Operator;
                probe.Replacement = replacement;
                probe.ReplacementPart = replacementHand;
                Send(em, rig, new CMUAutodocStartMessage(Context(em, rig)));
                Assert.That(rig.Component.IsRunning, Is.True);
            });

            await pair.RunTicksSync(pair.SecondsToTicks(1.25f));
            await pair.Server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(probe.Replaced, Is.True, "The real healing callback must replace the occupant during the old procedure.");
                    Assert.That(rig.Component.Patient, Is.EqualTo(replacement));
                    Assert.That(rig.Component.BodyContainer.ContainedEntity, Is.EqualTo(replacement));
                    Assert.That(rig.Component.IsRunning, Is.True);
                    Assert.That(rig.Component.Queue.Count, Is.EqualTo(1));
                    Assert.That(rig.Component.Queue[0].Part, Is.EqualTo(probe.ReplacementPart));
                    Assert.That(rig.Component.NextStepAt, Is.EqualTo(probe.ReplacementDeadline));
                    Assert.That(em.HasComponent<CMUAutodocContainedPatientComponent>(rig.Patient), Is.False);
                });
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                em.DeleteEntity(replacement);
                if (rig != null)
                    DeleteRig(em, rig);
            });
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task QueueCommandsRejectDuplicatesStaleViewsAndRemovedEntryIds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var rig = CreateRig(em, map.GridCoords);
            try
            {
                var left = InjureHand(em, rig.Patient, BodyPartSymmetry.Left);
                var right = InjureHand(em, rig.Patient, BodyPartSymmetry.Right);
                QueueRepair(em, rig, left, BodyPartSymmetry.Left);
                Assert.That(rig.Component.Queue.Count, Is.EqualTo(1), "The actual console command must enqueue eligible work.");
                var firstId = rig.Component.Queue[0].Id;
                var firstRevision = rig.Component.StateRevision;
                QueueRepair(em, rig, left, BodyPartSymmetry.Left);
                Assert.Multiple(() =>
                {
                    Assert.That(rig.Component.Queue.Count, Is.EqualTo(1));
                    Assert.That(rig.Component.StateRevision, Is.EqualTo(firstRevision), "A duplicate has no state transition.");
                });

                QueueRepair(em, rig, right, BodyPartSymmetry.Right);
                Assert.That(rig.Component.Queue.Count, Is.EqualTo(2));
                var secondId = rig.Component.Queue[1].Id;
                Assert.That(secondId, Is.GreaterThan(firstId));
                var concurrentView = Context(em, rig);
                Send(em, rig, new CMUAutodocRemoveQueueStepMessage(firstId, concurrentView));
                Assert.That(rig.Component.Queue.Count, Is.EqualTo(1));
                Assert.That(rig.Component.Queue[0].Id, Is.EqualTo(secondId));

                Send(em, rig, new CMUAutodocRemoveQueueStepMessage(secondId, concurrentView));
                Send(em, rig, new CMUAutodocRemoveQueueStepMessage(firstId, Context(em, rig)));
                Assert.That(rig.Component.Queue.Count, Is.EqualTo(1), "Neither a stale view nor a removed row ID may remove the row now at index zero.");
                Assert.That(rig.Component.Queue[0].Id, Is.EqualTo(secondId));

                Send(em, rig, new CMUAutodocRemoveQueueStepMessage(secondId, Context(em, rig)));
                Assert.That(rig.Component.Queue, Is.Empty);
                QueueRepair(em, rig, left, BodyPartSymmetry.Left);
                Assert.That(rig.Component.Queue[0].Id, Is.GreaterThan(secondId), "Clearing a queue never recycles a row capability.");
            }
            finally
            {
                DeleteRig(em, rig);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DirectDepartureCancelsWorkAndReentryRejectsOldCommands()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var rig = CreateRig(em, map.GridCoords);
            var replacement = em.SpawnEntity("CMMobHuman", map.GridCoords);
            var neighbor = em.SpawnEntity("CMUAutodocPod", map.GridCoords.Offset(new Vector2(3, 0)));
            try
            {
                var hand = InjureHand(em, rig.Patient, BodyPartSymmetry.Left);
                QueueRepair(em, rig, hand, BodyPartSymmetry.Left);
                Send(em, rig, new CMUAutodocStartMessage(Context(em, rig)));
                Assert.That(rig.Component.IsRunning, Is.True);
                var oldContext = Context(em, rig);
                var deadline = rig.Component.NextStepAt;
                Send(em, rig, new CMUAutodocStartMessage(oldContext));
                Assert.That(rig.Component.NextStepAt, Is.EqualTo(deadline), "Repeated Start does not restart elapsed work.");

                Assert.That(em.System<SharedContainerSystem>().Remove(rig.Patient, rig.Component.BodyContainer), Is.True);
                AssertReleased(em, rig);
                Assert.That(em.System<CMUMedicalPatientBaySystem>().TryInsertPatient(rig.Pod, rig.Component.BodyContainer, rig.Patient), Is.True);
                Assert.That(rig.Component.OccupantGeneration, Is.GreaterThan(oldContext.OccupantGeneration));
                Assert.That(em.GetComponent<CMUAutodocContainedPatientComponent>(rig.Patient).Pod, Is.EqualTo(rig.Pod));
                QueueRepair(em, rig, hand, BodyPartSymmetry.Left);
                var revision = rig.Component.StateRevision;
                Send(em, rig, new CMUAutodocClearQueueMessage(oldContext));
                Send(em, rig, new CMUAutodocStartMessage(oldContext));
                Send(em, rig, new CMUAutodocStopMessage(oldContext));
                Send(em, rig, new CMUAutodocEjectPatientMessage(oldContext));
                Send(em, rig, new CMUAutodocEjectPatientMessage(Context(em, rig) with { Pod = em.GetNetEntity(neighbor) }));
                Assert.Multiple(() =>
                {
                    Assert.That(rig.Component.StateRevision, Is.EqualTo(revision));
                    Assert.That(rig.Component.Queue.Count, Is.EqualTo(1));
                    Assert.That(rig.Component.BodyContainer.ContainedEntity, Is.EqualTo(rig.Patient));
                    Assert.That(rig.Component.IsRunning, Is.False);
                });

                var previousPatientContext = Context(em, rig);
                Assert.That(em.System<SharedContainerSystem>().Remove(rig.Patient, rig.Component.BodyContainer), Is.True);
                Assert.That(em.System<CMUMedicalPatientBaySystem>().TryInsertPatient(rig.Pod, rig.Component.BodyContainer, replacement), Is.True);
                Send(em, rig, new CMUAutodocEjectPatientMessage(previousPatientContext));
                Assert.That(rig.Component.BodyContainer.ContainedEntity, Is.EqualTo(replacement));
                Assert.That(em.GetComponent<CMUAutodocContainedPatientComponent>(replacement).Pod, Is.EqualTo(rig.Pod));
                Assert.That(em.HasComponent<CMUAutodocContainedPatientComponent>(rig.Patient), Is.False);
            }
            finally
            {
                em.DeleteEntity(neighbor);
                em.DeleteEntity(replacement);
                DeleteRig(em, rig);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RefusedEjectionPreservesWorkMembershipAndCommandRevision()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var rig = CreateRig(em, map.GridCoords);
            try
            {
                QueueRepair(em, rig, InjureHand(em, rig.Patient, BodyPartSymmetry.Left), BodyPartSymmetry.Left);
                Send(em, rig, new CMUAutodocStartMessage(Context(em, rig)));
                Assert.That(rig.Component.IsRunning, Is.True);
                em.EnsureComponent<CMUAutodocEjectionVetoTestComponent>(rig.Pod);
                var context = Context(em, rig);
                var deadline = rig.Component.NextStepAt;
                Send(em, rig, new CMUAutodocEjectPatientMessage(context));
                Assert.Multiple(() =>
                {
                    Assert.That(Context(em, rig), Is.EqualTo(context));
                    Assert.That(rig.Component.IsRunning, Is.True);
                    Assert.That(rig.Component.NextStepAt, Is.EqualTo(deadline));
                    Assert.That(rig.Component.Queue.Count, Is.EqualTo(1));
                    Assert.That(rig.Component.BodyContainer.ContainedEntity, Is.EqualTo(rig.Patient));
                    Assert.That(em.GetComponent<CMUAutodocContainedPatientComponent>(rig.Patient).Pod, Is.EqualTo(rig.Pod));
                });

                em.RemoveComponent<CMUAutodocEjectionVetoTestComponent>(rig.Pod);
                Send(em, rig, new CMUAutodocEjectPatientMessage(context));
                Assert.That(rig.Component.BodyContainer.ContainedEntity, Is.Null);
                AssertReleased(em, rig);
            }
            finally
            {
                DeleteRig(em, rig);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task QueueLimitAndCurrentConsoleAccessAreEnforcedAtTheCommandBoundary()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var rig = CreateRig(em, map.GridCoords);
            try
            {
                var hand = InjureHand(em, rig.Patient, BodyPartSymmetry.Left);
                // Fill the input state directly: eligibility currently exposes fewer than 32 distinct procedures.
                // The next real command must reject an otherwise eligible new procedure at the storage boundary.
                for (var i = 0; i < CMUAutodocPodComponent.MaximumQueueEntries; i++)
                    rig.Component.Queue.Add(new CMUAutodocQueuedStep(hand, BodyPartType.Hand, BodyPartSymmetry.Left,
                        $"capacity-fixture-{i}", "fixture", "wound_repair", 0, "fixture", "left hand", 30,
                        ++rig.Component.NextQueueEntryId));
                var context = Context(em, rig);
                QueueRepair(em, rig, hand, BodyPartSymmetry.Left);
                Assert.That(rig.Component.Queue.Count, Is.EqualTo(CMUAutodocPodComponent.MaximumQueueEntries));
                Assert.That(Context(em, rig), Is.EqualTo(context));

                em.System<SkillsSystem>().SetSkill(rig.Operator, "RMCSkillSurgery", 0);
                Send(em, rig, new CMUAutodocClearQueueMessage(context));
                Assert.That(rig.Component.Queue.Count, Is.EqualTo(CMUAutodocPodComponent.MaximumQueueEntries));
                em.System<SkillsSystem>().SetSkill(rig.Operator, "RMCSkillSurgery", 2);
                em.System<SharedTransformSystem>().SetCoordinates(rig.Operator, map.GridCoords.Offset(new Vector2(20, 0)));
                Send(em, rig, new CMUAutodocClearQueueMessage(context));
                Assert.That(rig.Component.Queue.Count, Is.EqualTo(CMUAutodocPodComponent.MaximumQueueEntries));
                em.System<SharedTransformSystem>().SetCoordinates(rig.Operator, map.GridCoords);
                Send(em, rig, new CMUAutodocClearQueueMessage(context));
                Assert.That(rig.Component.Queue, Is.Empty);
            }
            finally
            {
                DeleteRig(em, rig);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PatientDeletionAndPodComponentRemovalReleaseMachineOwnedWork()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var rig = CreateRig(em, map.GridCoords);
            var replacement = em.SpawnEntity("CMMobHuman", map.GridCoords);
            try
            {
                QueueRepair(em, rig, InjureHand(em, rig.Patient, BodyPartSymmetry.Left), BodyPartSymmetry.Left);
                Send(em, rig, new CMUAutodocStartMessage(Context(em, rig)));
                Assert.That(rig.Component.IsRunning, Is.True);
                em.DeleteEntity(rig.Patient);
                AssertReleased(em, rig);
                Assert.That(rig.Component.BodyContainer.ContainedEntity, Is.Null);

                Assert.That(em.System<CMUMedicalPatientBaySystem>().TryInsertPatient(rig.Pod, rig.Component.BodyContainer, replacement), Is.True);
                em.RemoveComponent<CMUAutodocPodComponent>(rig.Pod);
                Assert.That(em.HasComponent<CMUAutodocContainedPatientComponent>(replacement), Is.False);
                Assert.That(rig.Component.Patient, Is.Null);
            }
            finally
            {
                em.DeleteEntity(replacement);
                DeleteRig(em, rig);
            }
        });
        await pair.CleanReturnAsync();
    }

    private static Rig CreateRig(IEntityManager em, EntityCoordinates coordinates)
    {
        var pod = em.SpawnEntity("CMUAutodocPod", coordinates.Offset(new Vector2(1, 0)));
        var console = em.SpawnEntity("CMUAutodocConsole", coordinates);
        var patient = em.SpawnEntity("CMMobHuman", coordinates);
        var actor = em.SpawnEntity("CMMobHuman", coordinates);
        em.System<SkillsSystem>().SetSkill(actor, "RMCSkillSurgery", 2);
        var component = em.GetComponent<CMUAutodocPodComponent>(pod);
        Assert.That(em.System<CMUMedicalPatientBaySystem>().TryInsertPatient(pod, component.BodyContainer, patient), Is.True);
        Assert.That(em.GetComponent<CMUAutodocContainedPatientComponent>(patient).Pod, Is.EqualTo(pod));
        return new Rig(pod, console, patient, actor, component);
    }

    private static EntityUid InjureHand(IEntityManager em, EntityUid patient, BodyPartSymmetry symmetry)
    {
        Assert.That(em.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(patient,
            new(BodyPartType.Hand, symmetry), out var hand), Is.True);
        var health = em.GetComponent<BodyPartHealthComponent>(hand);
        em.System<SharedBodyPartHealthSystem>().SetCurrent((hand, health), health.Max - 10);
        return hand;
    }

    private static void QueueRepair(IEntityManager em, Rig rig, EntityUid part, BodyPartSymmetry symmetry)
    {
        Send(em, rig, new CMUAutodocQueueStepMessage(em.GetNetEntity(part), BodyPartType.Hand, symmetry,
            "CMUAutodocRepairWounds", 0, Context(em, rig)));
    }

    private static CMUAutodocCommandContext Context(IEntityManager em, Rig rig)
    {
        return new CMUAutodocCommandContext(em.GetNetEntity(rig.Pod), em.GetNetEntity(rig.Component.Patient!.Value),
            rig.Component.OccupantGeneration, rig.Component.StateRevision);
    }

    private static void Send<T>(IEntityManager em, Rig rig, T message) where T : BoundUserInterfaceMessage
    {
        message.Actor = rig.Operator;
        message.UiKey = CMUAutodocUIKey.Key;
        em.EventBus.RaiseLocalEvent(rig.Console, message);
    }

    private static void AssertReleased(IEntityManager em, Rig rig)
    {
        Assert.Multiple(() =>
        {
            Assert.That(rig.Component.Patient, Is.Null);
            Assert.That(rig.Component.Queue, Is.Empty);
            Assert.That(rig.Component.IsRunning, Is.False);
            Assert.That(rig.Component.Operator, Is.EqualTo(EntityUid.Invalid));
            Assert.That(rig.Component.NextStepAt, Is.EqualTo(TimeSpan.Zero));
            Assert.That(em.HasComponent<CMUAutodocContainedPatientComponent>(rig.Patient), Is.False);
            Assert.That(em.System<CMUMedicalSchedulerSystem>().Cancel(rig.Pod, new("autodoc-procedure-step")), Is.False,
                "Departure already cancelled the machine deadline.");
        });
    }

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
public sealed partial class CMUAutodocEjectionVetoTestComponent : Component
{
}

public sealed class CMUAutodocEjectionVetoTestSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUAutodocEjectionVetoTestComponent, ContainerIsRemovingAttemptEvent>(OnRemoving);
    }

    private void OnRemoving(Entity<CMUAutodocEjectionVetoTestComponent> ent, ref ContainerIsRemovingAttemptEvent args)
    {
        if (args.Container.ID == CMUAutodocPodComponent.BodyContainerId)
            args.Cancel();
    }
}

[RegisterComponent]
public sealed partial class CMUAutodocReplacementTestComponent : Component
{
    public EntityUid Pod;
    public EntityUid Console;
    public EntityUid Operator;
    public EntityUid Replacement;
    public EntityUid ReplacementPart;
    public bool Replaced;
    public TimeSpan ReplacementDeadline;
}

public sealed class CMUAutodocReplacementTestSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUAutodocReplacementTestComponent, BodyPartPainThresholdCrossedEvent>(OnHealed);
    }

    private void OnHealed(Entity<CMUAutodocReplacementTestComponent> ent, ref BodyPartPainThresholdCrossedEvent args)
    {
        if (ent.Comp.Replaced || args.CurrentFraction <= args.PreviousFraction)
            return;

        var probe = ent.Comp;
        var pod = Comp<CMUAutodocPodComponent>(probe.Pod);
        var bay = EntityManager.System<CMUMedicalPatientBaySystem>();
        Assert.That(bay.TryEjectPatient(probe.Pod, pod.BodyContainer, args.Body), Is.True);
        Assert.That(bay.TryInsertPatient(probe.Pod, pod.BodyContainer, probe.Replacement), Is.True);
        probe.Replaced = true;
        var context = new CMUAutodocCommandContext(GetNetEntity(probe.Pod), GetNetEntity(probe.Replacement),
            pod.OccupantGeneration, pod.StateRevision);
        EntityManager.EventBus.RaiseLocalEvent(probe.Console, new CMUAutodocQueueStepMessage(
            GetNetEntity(probe.ReplacementPart), BodyPartType.Hand, BodyPartSymmetry.Right,
            "CMUAutodocRepairWounds", 0, context)
        {
            Actor = probe.Operator,
            UiKey = CMUAutodocUIKey.Key,
        });
        EntityManager.EventBus.RaiseLocalEvent(probe.Console, new CMUAutodocStartMessage(context with { StateRevision = pod.StateRevision })
        {
            Actor = probe.Operator,
            UiKey = CMUAutodocUIKey.Key,
        });
        probe.ReplacementDeadline = pod.NextStepAt;
    }
}
