using System.Collections.Generic;
using System.Linq;
using ClientPopupSystem = Content.Client.Popups.PopupSystem;
using Content.Server._CMU14.Medical.Treatment.Surgery;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Injuries.Pain;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Standing;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Medical.Treatment.Surgery;

[TestFixture]
public sealed class CMUSurgeryHandoffTest
{
    [Test]
    public async Task PainShockRejectsSimpleFractureBoneSettingAndExplainsWhy()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var timing = server.ResolveDependency<IGameTiming>();
        EntityUid patient = default;
        EntityUid surgeon = default;
        EntityUid boneSetter = default;
        EntityUid? originalAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var flow = entMan.System<CMUSurgeryFlowSystem>();
                var fracture = entMan.System<SharedFractureSystem>();
                var pain = entMan.System<SharedPainShockSystem>();
                var skills = entMan.System<SkillsSystem>();

                var player = server.PlayerMan.Sessions.Single();
                originalAttached = player.AttachedEntity;
                patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                boneSetter = entMan.SpawnEntity("CMBonesetter", MapCoordinates.Nullspace);
                server.PlayerMan.SetAttachedEntity(player, surgeon);
                skills.SetSkill(surgeon, "RMCSkillSurgery", 3);
                Assert.That(entMan.System<SharedHandsSystem>().TryPickupAnyHand(surgeon, boneSetter), Is.True);
                entMan.System<StandingStateSystem>().Down(patient, playSound: false, dropHeldItems: false, force: true);

                var arm = FindPart(entMan, patient, BodyPartType.Arm, BodyPartSymmetry.Left);
                entMan.EnsureComponent<CMIncisionOpenComponent>(arm);
                entMan.EnsureComponent<CMBleedersClampedComponent>(arm);
                entMan.EnsureComponent<CMSkinRetractedComponent>(arm);

                var fractured = entMan.EnsureComponent<FractureComponent>(arm);
                fracture.SetSeverity((arm, fractured), FractureSeverity.Simple);

                var painState = entMan.GetComponent<PainShockComponent>(patient);
                painState.Pain = painState.PainMax;
                painState.PainTarget = painState.Pain;
                painState.NextUpdate = TimeSpan.Zero;
                pain.TickOne((patient, painState), refreshCache: false);
                entMan.Dirty(patient, painState);

                var feedback = entMan.GetComponent<CMUPainFeedbackComponent>(patient);
                feedback.EffectInterval = TimeSpan.FromSeconds(0.25);
                feedback.NextEffect = timing.CurTime;

                var armed = flow.TryArmStep(
                    surgeon,
                    patient,
                    arm,
                    "CMUSurgerySetSimpleFracture",
                    0,
                    BodyPartType.Arm,
                    BodyPartSymmetry.Left);

                Assert.That(armed, Is.Not.Null);
                Assert.That(armed!.RequiredToolCategory, Is.EqualTo("bone_setter"));
                Assert.That(painState.Tier, Is.EqualTo(PainTier.Shock));
            });

            await pair.RunTicksSync(10);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var sessions = entMan.System<CMUSurgerySessionSystem>();
                Assert.That(entMan.System<CMUSurgeryDispatchSystem>().TryDispatch(surgeon, patient, boneSetter), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(sessions.TryGetSession(patient, out _), Is.False);
                    Assert.That(
                        entMan.GetComponent<CMUSurgeryArmedStepComponent>(patient).RequiredToolCategory,
                        Is.EqualTo("bone_setter"));
                });
            });

            await pair.RunTicksSync(5);

            await pair.Client.WaitAssertion(() =>
            {
                var popups = pair.Client.EntMan.System<ClientPopupSystem>();
                Assert.That(
                    popups.WorldLabels.Any(label => label.Text ==
                        "The patient is in too much pain to continue surgery. Use anesthesia or strong painkillers before trying again."),
                    Is.True);
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var pain = entMan.System<SharedPainShockSystem>();
                var painState = entMan.GetComponent<PainShockComponent>(patient);
                painState.Pain = FixedPoint2.Zero;
                painState.PainTarget = FixedPoint2.Zero;
                pain.TickOne((patient, painState), refreshCache: false);
                entMan.Dirty(patient, painState);
                Assert.That(painState.Tier, Is.LessThan(PainTier.Severe));

                Assert.That(entMan.System<CMUSurgeryDispatchSystem>().TryDispatch(surgeon, patient, boneSetter), Is.True);
                Assert.That(entMan.System<CMUSurgerySessionSystem>().TryGetSession(patient, out var session), Is.True);
                Assert.That(session.Phase, Is.EqualTo(CMUSurgerySessionPhase.Performing));

                painState.Pain = painState.PainMax;
                painState.PainTarget = painState.Pain;
                pain.TickOne((patient, painState), refreshCache: false);
                entMan.Dirty(patient, painState);
                Assert.That(painState.Tier, Is.EqualTo(PainTier.Shock));
            });

            await pair.RunTicksSync(2);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.That(entMan.System<CMUSurgerySessionSystem>().TryGetSession(patient, out var session), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(session.Phase, Is.EqualTo(CMUSurgerySessionPhase.AwaitingAction));
                    Assert.That(session.ActiveAttempt, Is.Null);
                    Assert.That(
                        entMan.GetComponent<CMUSurgeryArmedStepComponent>(patient).RequiredToolCategory,
                        Is.EqualTo("bone_setter"));
                });
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var pain = entMan.System<SharedPainShockSystem>();
                pain.AddPainSuppressionProfile(patient, 1f, 4, 0f, TimeSpan.FromSeconds(10));
                Assert.That(pain.GetTierSuppression(patient), Is.GreaterThanOrEqualTo(2));
                Assert.That(entMan.System<CMUSurgeryDispatchSystem>().TryDispatch(surgeon, patient, boneSetter), Is.True);
            });

            await pair.RunSeconds(3);

            await server.WaitAssertion(() =>
            {
                var armed = server.EntMan.GetComponent<CMUSurgeryArmedStepComponent>(patient);
                Assert.That(armed.RequiredToolCategory, Is.EqualTo("bone_gel"));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                if (server.PlayerMan.Sessions.Any())
                    server.PlayerMan.SetAttachedEntity(server.PlayerMan.Sessions.Single(), originalAttached);
            });

            await pair.RunTicksSync(2);

            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task CancelledDoAfterReturnsToAwaitingActionForAnotherSurgeon()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        HandoffFixture fixture = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            fixture = StartAttempt(entMan);
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(dispatch.CanAbandonSurgery(fixture.Patient, fixture.FirstSurgeon), Is.True);
                Assert.That(dispatch.CanAbandonSurgery(fixture.Patient, fixture.SecondSurgeon), Is.False);
            });

            var doAfters = entMan.GetComponent<DoAfterComponent>(fixture.FirstSurgeon);
            var doAfterSystem = entMan.System<SharedDoAfterSystem>();
            foreach (var id in new List<ushort>(doAfters.DoAfters.Keys))
                doAfterSystem.Cancel(fixture.FirstSurgeon, id, doAfters);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var sessions = entMan.System<CMUSurgerySessionSystem>();

            Assert.That(entMan.HasComponent<CMUSurgeryArmedStepComponent>(fixture.Patient), Is.True);
            Assert.That(sessions.TryGetSession(fixture.Patient, out var awaiting), Is.True);
            Assert.That(
                entMan.System<CMUSurgeryDispatchSystem>().CanAbandonSurgery(
                    fixture.Patient,
                    fixture.SecondSurgeon),
                Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(awaiting.Phase, Is.EqualTo(CMUSurgerySessionPhase.AwaitingAction));
                Assert.That(awaiting.ActiveSurgeon, Is.Null);
                Assert.That(awaiting.ActiveAttempt, Is.Null);
            });

            Assert.That(
                entMan.System<CMUSurgeryDispatchSystem>().TryDispatch(
                    fixture.SecondSurgeon,
                    fixture.Patient,
                    fixture.Scalpel),
                Is.True);
            Assert.That(sessions.TryGetSession(fixture.Patient, out var resumed), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(resumed.Id, Is.EqualTo(awaiting.Id));
                Assert.That(resumed.Phase, Is.EqualTo(CMUSurgerySessionPhase.Performing));
                Assert.That(resumed.ActiveSurgeon, Is.EqualTo(fixture.SecondSurgeon));
                Assert.That(resumed.ActiveAttempt, Is.Not.EqualTo(fixture.FirstAttempt));
            });
        });

        await server.WaitPost(() =>
        {
            DeleteFixture(server.EntMan, fixture);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeletedSurgeonReturnsSessionToAwaitingAction()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        HandoffFixture fixture = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            fixture = StartAttempt(entMan);
            entMan.DeleteEntity(fixture.FirstSurgeon);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var sessions = entMan.System<CMUSurgerySessionSystem>();

            Assert.That(entMan.HasComponent<CMUSurgeryArmedStepComponent>(fixture.Patient), Is.True);
            Assert.That(sessions.TryGetSession(fixture.Patient, out var awaiting), Is.True);
            Assert.That(awaiting.Phase, Is.EqualTo(CMUSurgerySessionPhase.AwaitingAction));

            Assert.That(
                entMan.System<CMUSurgeryDispatchSystem>().TryDispatch(
                    fixture.SecondSurgeon,
                    fixture.Patient,
                    fixture.Scalpel),
                Is.True);
            Assert.That(sessions.TryGetSession(fixture.Patient, out var resumed), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(resumed.Id, Is.EqualTo(awaiting.Id));
                Assert.That(resumed.Phase, Is.EqualTo(CMUSurgerySessionPhase.Performing));
                Assert.That(resumed.ActiveSurgeon, Is.EqualTo(fixture.SecondSurgeon));
            });
        });

        await server.WaitPost(() =>
        {
            DeleteFixture(server.EntMan, fixture);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StaleCompletionCannotAffectReplacementAttempt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        HandoffFixture fixture = default;
        CMUSurgeryStepDoAfterEvent staleCompletion = default!;
        DoAfter staleDoAfter = default!;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            fixture = StartAttempt(entMan);
            var active = entMan.GetComponent<DoAfterComponent>(fixture.FirstSurgeon)
                .DoAfters.Values
                .Single(doAfter => !doAfter.Cancelled && !doAfter.Completed);
            var armed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(fixture.Patient);
            Assert.That(
                entMan.System<CMUSurgerySessionSystem>().TryGetSession(fixture.Patient, out var session),
                Is.True);
            staleCompletion = new CMUSurgeryStepDoAfterEvent(
                fixture.FirstAttempt,
                armed.SurgeryId,
                armed.LeafSurgeryId,
                armed.StepIndex,
                session.CurrentStep,
                armed.TargetPartType,
                armed.TargetSymmetry);
            staleDoAfter = new DoAfter(
                active.Index,
                new DoAfterArgs(
                    entMan,
                    fixture.FirstSurgeon,
                    TimeSpan.Zero,
                    staleCompletion,
                    fixture.Patient,
                    fixture.TargetPart,
                    fixture.Scalpel),
                TimeSpan.Zero);
            CancelActiveDoAfters(entMan, fixture.FirstSurgeon);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var sessions = entMan.System<CMUSurgerySessionSystem>();
            Assert.That(
                entMan.System<CMUSurgeryDispatchSystem>().TryDispatch(
                    fixture.SecondSurgeon,
                    fixture.Patient,
                    fixture.Scalpel),
                Is.True);
            Assert.That(sessions.TryGetSession(fixture.Patient, out var replacement), Is.True);
            Assert.That(replacement.ActiveAttempt, Is.Not.Null);

            staleCompletion.DoAfter = staleDoAfter;
            entMan.EventBus.RaiseLocalEvent(fixture.Patient, staleCompletion);

            Assert.That(sessions.TryGetSession(fixture.Patient, out var afterStale), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(afterStale.Phase, Is.EqualTo(CMUSurgerySessionPhase.Performing));
                Assert.That(afterStale.ActiveAttempt, Is.EqualTo(replacement.ActiveAttempt));
                Assert.That(afterStale.ActiveSurgeon, Is.EqualTo(fixture.SecondSurgeon));
                Assert.That(entMan.HasComponent<CMIncisionOpenComponent>(fixture.TargetPart), Is.False);
            });
        });

        await server.WaitPost(() => DeleteFixture(server.EntMan, fixture));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DetachedTargetBeforeCompletionAbandonsWithoutApplyingEffect()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var fixture = StartAttempt(entMan);

            try
            {
                var containers = entMan.System<SharedContainerSystem>();
                Assert.That(
                    containers.TryGetContainingContainer((fixture.TargetPart, null, null), out var container),
                    Is.True);
                Assert.That(containers.Remove(fixture.TargetPart, container), Is.True);

                var armed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(fixture.Patient);
                Assert.That(
                    entMan.System<CMUSurgerySessionSystem>().TryGetSession(fixture.Patient, out var session),
                    Is.True);
                var completion = new CMUSurgeryStepDoAfterEvent(
                    fixture.FirstAttempt,
                    armed.SurgeryId,
                    armed.LeafSurgeryId,
                    armed.StepIndex,
                    session.CurrentStep,
                    armed.TargetPartType,
                    armed.TargetSymmetry);
                completion.DoAfter = new DoAfter(
                    ushort.MaxValue,
                    new DoAfterArgs(
                        entMan,
                        fixture.FirstSurgeon,
                        TimeSpan.Zero,
                        completion,
                        fixture.Patient,
                        fixture.TargetPart,
                        fixture.Scalpel),
                    TimeSpan.Zero);

                entMan.EventBus.RaiseLocalEvent(fixture.Patient, completion);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        entMan.System<CMUSurgerySessionSystem>().TryGetSession(fixture.Patient, out _),
                        Is.False);
                    Assert.That(entMan.HasComponent<CMUSurgeryArmedStepComponent>(fixture.Patient), Is.False);
                    Assert.That(entMan.HasComponent<CMUSurgeryInProgressComponent>(fixture.Patient), Is.False);
                    Assert.That(entMan.HasComponent<CMUSurgeryInFlightComponent>(fixture.TargetPart), Is.False);
                    Assert.That(entMan.HasComponent<CMIncisionOpenComponent>(fixture.TargetPart), Is.False);
                });
            }
            finally
            {
                DeleteFixture(entMan, fixture);
                if (entMan.EntityExists(fixture.TargetPart))
                    entMan.DeleteEntity(fixture.TargetPart);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StaleAwaitingUiStateCannotMatchRearmedStep()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        HandoffFixture fixture = default;

        await server.WaitPost(() =>
        {
            fixture = StartAttempt(server.EntMan);
            CancelActiveDoAfters(server.EntMan, fixture.FirstSurgeon);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var sessions = entMan.System<CMUSurgerySessionSystem>();
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            Assert.That(sessions.TryGetSession(fixture.Patient, out var session), Is.True);
            Assert.That(session.ActiveAttempt, Is.Null);

            var previousState = entMan.GetComponent<CMUSurgeryArmedStepComponent>(fixture.Patient).StateId;
            Assert.That(
                dispatch.IsUiStateCurrent(fixture.Patient, session.Id, null, previousState),
                Is.True);

            var rearmed = flow.TryArmStep(
                fixture.SecondSurgeon,
                fixture.Patient,
                fixture.TargetPart,
                "CMUSurgeryCauterizeInternalBleeding",
                0,
                BodyPartType.Arm,
                BodyPartSymmetry.Left);
            Assert.That(rearmed, Is.Not.Null);
            Assert.That(rearmed!.StateId, Is.Not.EqualTo(previousState));

            Assert.Multiple(() =>
            {
                Assert.That(
                    dispatch.IsUiStateCurrent(fixture.Patient, session.Id, null, previousState),
                    Is.False);
                Assert.That(
                    dispatch.IsUiStateCurrent(fixture.Patient, session.Id, null, rearmed.StateId),
                    Is.True);
            });
        });

        await server.WaitPost(() => DeleteFixture(server.EntMan, fixture));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PerformingFirstAttemptIsProjectedAndLocksOtherSites()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var fixture = StartAttempt(entMan);

            try
            {
                var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
                var flow = entMan.System<CMUSurgeryFlowSystem>();
                var parts = dispatch.BuildPartEntries(fixture.Patient, fixture.SecondSurgeon);
                var left = parts.Single(part =>
                    part.Type == BodyPartType.Arm && part.Symmetry == BodyPartSymmetry.Left);
                var right = parts.Single(part =>
                    part.Type == BodyPartType.Arm && part.Symmetry == BodyPartSymmetry.Right);
                var state = flow.BuildBuiState(
                    fixture.Patient,
                    "Patient",
                    parts,
                    entMan.GetComponent<CMUSurgeryArmedStepComponent>(fixture.Patient));

                Assert.Multiple(() =>
                {
                    Assert.That(state.InFlight, Is.Null);
                    Assert.That(state.SessionPhase, Is.EqualTo(CMUSurgerySessionPhase.Performing));
                    Assert.That(state.SessionPartType, Is.EqualTo(BodyPartType.Arm));
                    Assert.That(state.SessionPartSymmetry, Is.EqualTo(BodyPartSymmetry.Left));
                    Assert.That(left.IsInFlightHere, Is.True);
                    Assert.That(left.EligibleSurgeries, Is.Empty);
                    Assert.That(right.LockedByOtherPart, Is.True);
                });
            }
            finally
            {
                DeleteFixture(entMan, fixture);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PerformingAttemptRejectsStaleArmRequest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var fixture = StartAttempt(entMan);

            try
            {
                var rightArm = FindPart(entMan, fixture.Patient, BodyPartType.Arm, BodyPartSymmetry.Right);
                entMan.EnsureComponent<InternalBleedingComponent>(rightArm);
                var staleArm = entMan.System<CMUSurgeryFlowSystem>().TryArmStep(
                    fixture.SecondSurgeon,
                    fixture.Patient,
                    rightArm,
                    "CMUSurgeryCauterizeInternalBleeding",
                    0,
                    BodyPartType.Arm,
                    BodyPartSymmetry.Right);

                Assert.That(staleArm, Is.Null);
                Assert.That(
                    entMan.System<CMUSurgerySessionSystem>().TryGetSession(fixture.Patient, out var session),
                    Is.True);
                var armed = entMan.GetComponent<CMUSurgeryArmedStepComponent>(fixture.Patient);
                Assert.Multiple(() =>
                {
                    Assert.That(armed.TargetPartType, Is.EqualTo(BodyPartType.Arm));
                    Assert.That(armed.TargetSymmetry, Is.EqualTo(BodyPartSymmetry.Left));
                    Assert.That(session.Phase, Is.EqualTo(CMUSurgerySessionPhase.Performing));
                    Assert.That(session.ActiveAttempt, Is.EqualTo(fixture.FirstAttempt));
                });
            }
            finally
            {
                DeleteFixture(entMan, fixture);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClearingCancelledFirstAttemptReleasesSite()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        HandoffFixture fixture = default;

        await server.WaitPost(() =>
        {
            fixture = StartAttempt(server.EntMan);
            CancelActiveDoAfters(server.EntMan, fixture.FirstSurgeon);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var sessions = entMan.System<CMUSurgerySessionSystem>();
            flow.ClearArmed(fixture.Patient);

            Assert.That(sessions.TryGetSession(fixture.Patient, out _), Is.False);

            var rightArm = FindPart(entMan, fixture.Patient, BodyPartType.Arm, BodyPartSymmetry.Right);
            entMan.EnsureComponent<InternalBleedingComponent>(rightArm);
            Assert.That(
                flow.TryArmStep(
                    fixture.SecondSurgeon,
                    fixture.Patient,
                    rightArm,
                    "CMUSurgeryCauterizeInternalBleeding",
                    0,
                    BodyPartType.Arm,
                    BodyPartSymmetry.Right),
                Is.Not.Null);
            Assert.That(
                entMan.System<CMUSurgeryDispatchSystem>().TryDispatch(
                    fixture.SecondSurgeon,
                    fixture.Patient,
                    fixture.Scalpel),
                Is.True);
            Assert.That(sessions.TryGetSession(fixture.Patient, out var replacement), Is.True);
            Assert.That(
                replacement.Site,
                Is.EqualTo(new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Right)));
        });

        await server.WaitPost(() => DeleteFixture(server.EntMan, fixture));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CancelledFirstAttemptCanSwitchSiteWithoutExplicitAbandon()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        HandoffFixture fixture = default;

        await server.WaitPost(() =>
        {
            fixture = StartAttempt(server.EntMan);
            CancelActiveDoAfters(server.EntMan, fixture.FirstSurgeon);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var sessions = entMan.System<CMUSurgerySessionSystem>();
            Assert.That(sessions.TryGetSession(fixture.Patient, out var oldSession), Is.True);
            Assert.That(oldSession.Phase, Is.EqualTo(CMUSurgerySessionPhase.AwaitingAction));

            var rightArm = FindPart(entMan, fixture.Patient, BodyPartType.Arm, BodyPartSymmetry.Right);
            entMan.EnsureComponent<InternalBleedingComponent>(rightArm);
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var rightEntry = dispatch.BuildPartEntries(fixture.Patient, fixture.SecondSurgeon)
                .Single(part => part.Type == BodyPartType.Arm && part.Symmetry == BodyPartSymmetry.Right);
            Assert.Multiple(() =>
            {
                Assert.That(rightEntry.LockedByOtherPart, Is.False);
                Assert.That(
                    rightEntry.EligibleSurgeries.Any(entry =>
                        entry.SurgeryId == "CMUSurgeryCauterizeInternalBleeding"),
                    Is.True);
            });
            var rearmed = flow.TryArmStep(
                fixture.SecondSurgeon,
                fixture.Patient,
                rightArm,
                "CMUSurgeryCauterizeInternalBleeding",
                0,
                BodyPartType.Arm,
                BodyPartSymmetry.Right);

            Assert.That(rearmed, Is.Not.Null);
            Assert.That(sessions.TryGetSession(fixture.Patient, out _), Is.False);
            Assert.That(
                entMan.System<CMUSurgeryDispatchSystem>().TryDispatch(
                    fixture.SecondSurgeon,
                    fixture.Patient,
                    fixture.Scalpel),
                Is.True);
            Assert.That(sessions.TryGetSession(fixture.Patient, out var replacement), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(replacement.Id, Is.Not.EqualTo(oldSession.Id));
                Assert.That(
                    replacement.Site,
                    Is.EqualTo(new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Right)));
                Assert.That(replacement.Phase, Is.EqualTo(CMUSurgerySessionPhase.Performing));
            });
        });

        await server.WaitPost(() => DeleteFixture(server.EntMan, fixture));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnderqualifiedSurgeonCannotContinueArmedProcedure()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var skills = entMan.System<SkillsSystem>();
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var qualified = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var underqualified = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var scalpel = entMan.SpawnEntity("CMScalpel", MapCoordinates.Nullspace);

            try
            {
                skills.SetSkill(qualified, "RMCSkillSurgery", 3);
                skills.SetSkill(underqualified, "RMCSkillSurgery", 1);
                entMan.System<StandingStateSystem>().Down(patient, playSound: false, dropHeldItems: false, force: true);
                var torso = FindPart(entMan, patient, BodyPartType.Torso, BodyPartSymmetry.None);
                Assert.That(
                    flow.TryArmStep(
                        qualified,
                        patient,
                        torso,
                        "CMUSurgeryRemoveLiver",
                        0,
                        BodyPartType.Torso,
                        BodyPartSymmetry.None),
                    Is.Not.Null);

                Assert.That(
                    entMan.System<CMUSurgeryDispatchSystem>().TryDispatch(underqualified, patient, scalpel),
                    Is.True);
                Assert.That(
                    entMan.System<CMUSurgerySessionSystem>().TryGetSession(patient, out _),
                    Is.False);
                Assert.That(
                    entMan.System<CMUSurgeryDispatchSystem>().CanAbandonSurgery(patient, underqualified),
                    Is.False);
                Assert.That(
                    entMan.GetComponent<CMUSurgeryArmedStepComponent>(patient).LastOperator,
                    Is.EqualTo(qualified));
            }
            finally
            {
                entMan.DeleteEntity(scalpel);
                entMan.DeleteEntity(underqualified);
                entMan.DeleteEntity(qualified);
                entMan.DeleteEntity(patient);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CancelledReattachAttemptSurvivesUnrelatedBodyChange()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid patient = default;
        EntityUid surgeon = default;
        EntityUid tool = default;
        EntityUid detachedLeftArm = default;
        EntityUid detachedRightLeg = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            tool = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.System<SkillsSystem>().SetSkill(surgeon, "RMCSkillSurgery", 3);
            entMan.System<StandingStateSystem>().Down(patient, playSound: false, dropHeldItems: false, force: true);

            detachedLeftArm = FindPart(entMan, patient, BodyPartType.Arm, BodyPartSymmetry.Left);
            var containers = entMan.System<SharedContainerSystem>();
            Assert.That(
                containers.TryGetContainingContainer((detachedLeftArm, null, null), out var leftContainer),
                Is.True);
            Assert.That(containers.Remove(detachedLeftArm, leftContainer), Is.True);

            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var armed = flow.TryArmStep(
                surgeon,
                patient,
                patient,
                "CMUSurgeryReattachLimb",
                0,
                BodyPartType.Arm,
                BodyPartSymmetry.Left);
            Assert.That(armed, Is.Not.Null);
            Assert.That(flow.TryGetReattachAnchorPart(patient, out var anchor), Is.True);
            Assert.That(flow.TryGetDefinition(armed!.SurgeryId, out var definition), Is.True);
            Assert.That(definition.TryGetStepAt(armed.StepIndex, out var step), Is.True);
            var sessions = entMan.System<CMUSurgerySessionSystem>();
            Assert.That(
                sessions.TryBeginAttempt(
                    patient,
                    surgeon,
                    tool,
                    anchor,
                    new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left),
                    definition.Id,
                    step.Id,
                    out _),
                Is.EqualTo(CMUSurgeryAttemptStartResult.Started));
            Assert.That(sessions.CancelActiveAttempt(patient), Is.True);
        });

        await pair.RunTicksSync(1);

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            Assert.That(
                entMan.System<CMUSurgerySessionSystem>().TryGetSession(patient, out var session),
                Is.True);
            Assert.That(session.Phase, Is.EqualTo(CMUSurgerySessionPhase.AwaitingAction));

            detachedRightLeg = FindPart(entMan, patient, BodyPartType.Leg, BodyPartSymmetry.Right);
            var containers = entMan.System<SharedContainerSystem>();
            Assert.That(
                containers.TryGetContainingContainer((detachedRightLeg, null, null), out var rightContainer),
                Is.True);
            Assert.That(containers.Remove(detachedRightLeg, rightContainer), Is.True);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<CMUSurgeryArmedStepComponent>(patient), Is.True);
                Assert.That(
                    entMan.System<CMUSurgerySessionSystem>().TryGetSession(patient, out var session),
                    Is.True);
                Assert.That(session.Phase, Is.EqualTo(CMUSurgerySessionPhase.AwaitingAction));
            });
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            foreach (var entity in new[] { detachedRightLeg, detachedLeftArm, tool, surgeon, patient })
            {
                if (entMan.EntityExists(entity))
                    entMan.DeleteEntity(entity);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DetachedSurgeonReturnsSessionToAwaitingAction()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        HandoffFixture fixture = default;
        EntityUid? originalAttached = null;

        await server.WaitPost(() =>
        {
            fixture = StartAttempt(server.EntMan);
            var player = server.PlayerMan.Sessions.Single();
            originalAttached = player.AttachedEntity;
            server.PlayerMan.SetAttachedEntity(player, fixture.FirstSurgeon);
            server.PlayerMan.SetAttachedEntity(player, null);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var sessions = server.EntMan.System<CMUSurgerySessionSystem>();
            Assert.That(sessions.TryGetSession(fixture.Patient, out var session), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(session.Phase, Is.EqualTo(CMUSurgerySessionPhase.AwaitingAction));
                Assert.That(session.ActiveAttempt, Is.Null);
                Assert.That(session.ActiveSurgeon, Is.Null);
            });
        });

        await server.WaitPost(() =>
        {
            server.PlayerMan.SetAttachedEntity(server.PlayerMan.Sessions.Single(), originalAttached);
        });
        await pair.RunTicksSync(1);
        await pair.CleanReturnAsync();
    }

    private static HandoffFixture StartAttempt(IEntityManager entMan)
    {
        var flow = entMan.System<CMUSurgeryFlowSystem>();
        var skills = entMan.System<SkillsSystem>();
        var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        var firstSurgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        var secondSurgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        var scalpel = entMan.SpawnEntity("CMScalpel", MapCoordinates.Nullspace);

        skills.SetSkill(firstSurgeon, "RMCSkillSurgery", 3);
        skills.SetSkill(secondSurgeon, "RMCSkillSurgery", 3);
        entMan.System<StandingStateSystem>().Down(patient, playSound: false, dropHeldItems: false, force: true);

        var leftArm = FindPart(entMan, patient, BodyPartType.Arm, BodyPartSymmetry.Left);
        entMan.EnsureComponent<InternalBleedingComponent>(leftArm);
        Assert.That(
            flow.TryArmStep(
                firstSurgeon,
                patient,
                leftArm,
                "CMUSurgeryCauterizeInternalBleeding",
                0,
                BodyPartType.Arm,
                BodyPartSymmetry.Left),
            Is.Not.Null);
        Assert.That(
            entMan.System<CMUSurgeryDispatchSystem>().TryDispatch(firstSurgeon, patient, scalpel),
            Is.True);

        var sessions = entMan.System<CMUSurgerySessionSystem>();
        Assert.That(sessions.TryGetSession(patient, out var session), Is.True);
        Assert.That(session.ActiveAttempt, Is.Not.Null);
        return new HandoffFixture(
            patient,
            firstSurgeon,
            secondSurgeon,
            scalpel,
            leftArm,
            session.ActiveAttempt.Value);
    }

    private static void DeleteFixture(IEntityManager entMan, HandoffFixture fixture)
    {
        foreach (var entity in new[]
                 {
                     fixture.Scalpel,
                     fixture.SecondSurgeon,
                     fixture.FirstSurgeon,
                     fixture.Patient,
                 })
        {
            if (entMan.EntityExists(entity))
                entMan.DeleteEntity(entity);
        }
    }

    private static EntityUid FindPart(
        IEntityManager entMan,
        EntityUid patient,
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        foreach (var (partUid, part) in entMan.System<SharedBodySystem>().GetBodyChildren(patient))
        {
            if (part.PartType == type && part.Symmetry == symmetry)
                return partUid;
        }

        Assert.Fail($"Could not find {symmetry} {type} on test patient.");
        return default;
    }

    private static void CancelActiveDoAfters(IEntityManager entMan, EntityUid user)
    {
        var doAfters = entMan.GetComponent<DoAfterComponent>(user);
        var doAfterSystem = entMan.System<SharedDoAfterSystem>();
        foreach (var id in new List<ushort>(doAfters.DoAfters.Keys))
            doAfterSystem.Cancel(user, id, doAfters);
    }

    private readonly record struct HandoffFixture(
        EntityUid Patient,
        EntityUid FirstSurgeon,
        EntityUid SecondSurgeon,
        EntityUid Scalpel,
        EntityUid TargetPart,
        CMUSurgeryAttemptToken FirstAttempt);
}
