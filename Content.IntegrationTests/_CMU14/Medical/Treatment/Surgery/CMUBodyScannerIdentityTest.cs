using System.IO;
using Content.Server.CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Medical.Treatment.Surgery;

[TestFixture]
public sealed class CMUBodyScannerIdentityTest
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
            var replacement = CreatePatient(em, map.GridCoords);
            try
            {
                var verbs = em.System<SharedVerbSystem>();
                var old = new AlternativeVerb
                {
                    Category = VerbCategory.Eject,
                    Text = Loc.GetString("medical-scanner-verb-noun-occupant"),
                    Priority = 1,
                };
                using var stream = new MemoryStream();
                var serializer = pair.Server.ResolveDependency<IRobustSerializer>();
                serializer.Serialize(stream, old);
                stream.Position = 0;
                var requested = serializer.Deserialize<AlternativeVerb>(stream);
                ReplacePatient(em, rig, replacement);
                var fresh = verbs.GetLocalVerbs(rig.Pod, rig.User, typeof(AlternativeVerb));
                Assert.That(fresh.TryGetValue(requested, out _), Is.False,
                    "The remote verb resolver must not match patient A's serialized request to patient B's newly generated verb.");
                Assert.That(rig.PodComponent.BodyContainer.ContainedEntity, Is.EqualTo(replacement));

                var oldContext = State(em, rig).CommandContext!.Value;
                var generation = rig.PodComponent.OccupantGeneration;
                em.RemoveComponent<CMUBodyScannerPodComponent>(rig.Pod);
                var recreated = em.AddComponent<CMUBodyScannerPodComponent>(rig.Pod);
                Assert.That(recreated.OccupantGeneration, Is.GreaterThan(generation));
                Send(em, rig, new CMUBodyScannerEjectPatientMessage(oldContext));
                Assert.That(recreated.BodyContainer.ContainedEntity, Is.EqualTo(replacement));
                Send(em, rig, new CMUBodyScannerEjectPatientMessage(State(em, rig).CommandContext!.Value));
                Assert.That(recreated.BodyContainer.ContainedEntity, Is.Null);
            }
            finally
            {
                em.DeleteEntity(replacement);
                DeleteRig(em, rig);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CommandsBindExactOriginAndCannotReplayAPenalty()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var rig = CreateRig(em, map.GridCoords);
            var otherConsole = em.SpawnEntity("CMUBodyScannerConsole", map.GridCoords.Offset(new Vector2(0, -1)));
            try
            {
                var idle = State(em, rig);
                using var stream = new MemoryStream();
                var serializer = pair.Server.ResolveDependency<IRobustSerializer>();
                serializer.Serialize(stream, new CMUBodyScannerResetPuzzleMessage(idle.CommandContext!.Value));
                stream.Position = 0;
                Send(em, rig, serializer.Deserialize<CMUBodyScannerResetPuzzleMessage>(stream));
                var started = State(em, rig);
                var progress = em.GetComponent<CMUBodyScannerPuzzleProgressComponent>(rig.User);
                var deadline = progress.EndsAt;
                var target = started.Targets.First(t => !t.IsDecoy);
                var other = em.System<CMUBodyScannerSystem>().BuildStateForViewer(otherConsole,
                    em.GetComponent<CMUBodyScannerConsoleComponent>(otherConsole), rig.User);
                Assert.That(other.CalibrationActiveElsewhere, Is.True);
                Assert.That(other.CalibrationAttempt, Is.Zero);
                var foreignConfirm = new CMUBodyScannerConfirmPuzzleMessage(target.LayerId, target.Id, 0,
                    other.CommandContext!.Value, 0) { Actor = rig.User, UiKey = CMUBodyScannerUIKey.Key };
                em.EventBus.RaiseLocalEvent(otherConsole, foreignConfirm);
                Send(em, rig, new CMUBodyScannerResetPuzzleMessage(idle.CommandContext.Value));
                Assert.That(progress.EndsAt, Is.EqualTo(deadline));
                Assert.That(progress.Assignments, Is.Empty);

                var wrongLayer = target.LayerId == "vitals" ? "tissue" : "vitals";
                var wrong = new CMUBodyScannerConfirmPuzzleMessage(wrongLayer, target.Id, 0,
                    started.CommandContext!.Value, 0);
                Send(em, rig, wrong);
                Assert.That(progress.EndsAt, Is.LessThan(deadline));
                var penalized = progress.EndsAt;
                Send(em, rig, wrong);
                Assert.That(progress.EndsAt, Is.EqualTo(penalized), "The same received view may incur at most one penalty.");
                var fresh = State(em, rig);
                Send(em, rig, new CMUBodyScannerConfirmPuzzleMessage(target.LayerId, target.Id, 0,
                    fresh.CommandContext!.Value, 0));
                Assert.That(em.HasComponent<CMUBodyScannerSurgerySpeedComponent>(rig.User), Is.True);
                Assert.That(em.HasComponent<CMUBodyScannerPuzzleProgressComponent>(rig.User), Is.False);
            }
            finally
            {
                em.DeleteEntity(otherConsole);
                DeleteRig(em, rig);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DepartureReplacementAndReentryPreserveAttemptButInvalidateOldCommands()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var rig = CreateRig(em, map.GridCoords);
            var replacement = CreatePatient(em, map.GridCoords);
            try
            {
                Send(em, rig, new CMUBodyScannerResetPuzzleMessage(State(em, rig).CommandContext!.Value));
                var old = State(em, rig);
                var progress = em.GetComponent<CMUBodyScannerPuzzleProgressComponent>(rig.User);
                var deadline = progress.EndsAt;
                var target = old.Targets.First(t => !t.IsDecoy);
                ReplacePatient(em, rig, replacement);
                var replacementState = State(em, rig);
                Assert.That(replacementState.CalibrationActiveElsewhere, Is.True);
                Send(em, rig, new CMUBodyScannerResetPuzzleMessage(replacementState.CommandContext!.Value));
                Send(em, rig, new CMUBodyScannerEjectPatientMessage(old.CommandContext!.Value));
                Send(em, rig, new CMUBodyScannerConfirmPuzzleMessage(target.LayerId, target.Id, 0,
                    old.CommandContext.Value, 0));
                Assert.Multiple(() =>
                {
                    Assert.That(rig.PodComponent.BodyContainer.ContainedEntity, Is.EqualTo(replacement));
                    Assert.That(progress.Patient, Is.EqualTo(rig.Patient));
                    Assert.That(progress.EndsAt, Is.EqualTo(deadline));
                    Assert.That(progress.Assignments, Is.Empty);
                });
                ReplacePatient(em, rig, rig.Patient);
                var reentered = State(em, rig);
                Assert.That(reentered.CommandContext!.Value.OccupantGeneration,
                    Is.GreaterThan(old.CommandContext.Value.OccupantGeneration));
                Send(em, rig, new CMUBodyScannerConfirmPuzzleMessage(target.LayerId, target.Id, 0,
                    reentered.CommandContext.Value, 0));
                Send(em, rig, new CMUBodyScannerEjectPatientMessage(old.CommandContext.Value));
                Assert.That(progress.Assignments, Is.Empty, "Reentry cannot continue an attempt from an earlier visit.");
                Assert.That(rig.PodComponent.BodyContainer.ContainedEntity, Is.EqualTo(rig.Patient));
                em.DeleteEntity(rig.Patient);
                ReplacePatient(em, rig, replacement);
                var afterDeletion = State(em, rig);
                Assert.That(afterDeletion.CanStartCalibration, Is.True, "A deleted patient cannot retain the operator's attempt.");
            }
            finally
            {
                em.DeleteEntity(replacement);
                DeleteRig(em, rig);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PatientLockoutsExpireIndependentlyAndPauseTogetherWithoutUiPolling()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.EntMan;
        Rig rig = default!;
        EntityUid second = default;
        TimeSpan firstExpiry = default;
        TimeSpan secondExpiry = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                rig = CreateRig(em, map.GridCoords);
                second = CreatePatient(em, map.GridCoords);
                rig.ConsoleComponent.CalibrationDurationSeconds = 0.05f;
                rig.ConsoleComponent.CalibrationLockoutSeconds = 1.5f;
                Send(em, rig, new CMUBodyScannerResetPuzzleMessage(State(em, rig).CommandContext!.Value));
                rig.ConsoleComponent.CalibrationLockoutSeconds = 0.5f;
            });
            await pair.RunTicksSync(pair.SecondsToTicks(0.1f));
            await pair.Server.WaitAssertion(() =>
            {
                var locks = em.GetComponent<CMUBodyScannerCalibrationLockoutComponent>(rig.User);
                firstExpiry = locks.Expiries[rig.Patient];
                Assert.That((firstExpiry - pair.Server.ResolveDependency<IGameTiming>().CurTime).TotalSeconds,
                    Is.GreaterThan(1), "Timeout uses the originating attempt's captured policy, even after console configuration changes.");
                ReplacePatient(em, rig, second);
                Send(em, rig, new CMUBodyScannerResetPuzzleMessage(State(em, rig).CommandContext!.Value));
            });
            await pair.RunTicksSync(pair.SecondsToTicks(0.1f));
            await pair.Server.WaitAssertion(() =>
            {
                var locks = em.GetComponent<CMUBodyScannerCalibrationLockoutComponent>(rig.User);
                secondExpiry = locks.Expiries[second];
                Assert.That(locks.Expiries[rig.Patient], Is.EqualTo(firstExpiry));
                em.System<MetaDataSystem>().SetEntityPaused(rig.User, true);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(0.7f));
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(em.GetComponent<CMUBodyScannerCalibrationLockoutComponent>(rig.User).Expiries.Count, Is.EqualTo(2));
                em.System<MetaDataSystem>().SetEntityPaused(rig.User, false);
                var locks = em.GetComponent<CMUBodyScannerCalibrationLockoutComponent>(rig.User);
                Assert.That(locks.Expiries[rig.Patient], Is.GreaterThan(firstExpiry));
                Assert.That(locks.Expiries[second], Is.GreaterThan(secondExpiry));
            });
            await pair.RunTicksSync(pair.SecondsToTicks(0.6f));
            await pair.Server.WaitAssertion(() =>
            {
                var locks = em.GetComponent<CMUBodyScannerCalibrationLockoutComponent>(rig.User);
                Assert.That(locks.Expiries.ContainsKey(second), Is.False);
                Assert.That(locks.Expiries.ContainsKey(rig.Patient), Is.True);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                em.DeleteEntity(second);
                if (rig != null)
                    DeleteRig(em, rig);
            });
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ViewingAPausedAttemptCannotExpireIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.EntMan;
        Rig rig = default!;
        TimeSpan originalDeadline = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                rig = CreateRig(em, map.GridCoords);
                rig.ConsoleComponent.CalibrationDurationSeconds = 0.2f;
                Send(em, rig, new CMUBodyScannerResetPuzzleMessage(State(em, rig).CommandContext!.Value));
                originalDeadline = em.GetComponent<CMUBodyScannerPuzzleProgressComponent>(rig.User).EndsAt;
                em.System<MetaDataSystem>().SetEntityPaused(rig.User, true);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(0.5f));
            await pair.Server.WaitAssertion(() =>
            {
                _ = State(em, rig);
                Assert.That(em.HasComponent<CMUBodyScannerPuzzleProgressComponent>(rig.User), Is.True);
                Assert.That(em.HasComponent<CMUBodyScannerCalibrationLockoutComponent>(rig.User), Is.False);
                em.System<MetaDataSystem>().SetEntityPaused(rig.User, false);
                Assert.That(em.GetComponent<CMUBodyScannerPuzzleProgressComponent>(rig.User).EndsAt,
                    Is.GreaterThan(originalDeadline));
            });
            await pair.RunTicksSync(pair.SecondsToTicks(0.3f));
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(em.HasComponent<CMUBodyScannerPuzzleProgressComponent>(rig.User), Is.False);
                Assert.That(em.GetComponent<CMUBodyScannerCalibrationLockoutComponent>(rig.User).Expiries.ContainsKey(rig.Patient), Is.True);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                if (rig != null)
                    DeleteRig(em, rig);
            });
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FullLockoutSetRefusesAnotherAttemptWithoutEvictingPatients()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var rig = CreateRig(em, map.GridCoords);
            var patients = new List<EntityUid> { rig.Patient };
            try
            {
                rig.ConsoleComponent.WrongMovePenaltySeconds = rig.ConsoleComponent.CalibrationDurationSeconds;
                for (var i = 0; i < CMUBodyScannerCalibrationLockoutComponent.MaximumPatients; i++)
                {
                    var patient = i == 0 ? rig.Patient : CreatePatient(em, map.GridCoords);
                    if (i != 0)
                    {
                        patients.Add(patient);
                        ReplacePatient(em, rig, patient);
                    }
                    Send(em, rig, new CMUBodyScannerResetPuzzleMessage(State(em, rig).CommandContext!.Value));
                    var active = State(em, rig);
                    var target = active.Targets.First(t => !t.IsDecoy);
                    Send(em, rig, new CMUBodyScannerConfirmPuzzleMessage(
                        target.LayerId == "vitals" ? "tissue" : "vitals", target.Id, 0, active.CommandContext!.Value, 0));
                }
                var lockouts = em.GetComponent<CMUBodyScannerCalibrationLockoutComponent>(rig.User);
                Assert.That(lockouts.Expiries.Count, Is.EqualTo(CMUBodyScannerCalibrationLockoutComponent.MaximumPatients));
                var extra = CreatePatient(em, map.GridCoords);
                patients.Add(extra);
                ReplacePatient(em, rig, extra);
                var full = State(em, rig);
                Assert.That(full.CanStartCalibration, Is.False);
                Send(em, rig, new CMUBodyScannerResetPuzzleMessage(full.CommandContext!.Value));
                Assert.That(em.HasComponent<CMUBodyScannerPuzzleProgressComponent>(rig.User), Is.False);
                Assert.That(lockouts.Expiries.Keys, Is.EquivalentTo(patients.Take(patients.Count - 1)));
                em.DeleteEntity(patients[0]);
                var released = State(em, rig);
                Assert.That(released.CanStartCalibration, Is.True);
                Send(em, rig, new CMUBodyScannerResetPuzzleMessage(released.CommandContext!.Value));
                Assert.That(em.GetComponent<CMUBodyScannerPuzzleProgressComponent>(rig.User).Patient, Is.EqualTo(extra));
            }
            finally
            {
                foreach (var patient in patients)
                    em.DeleteEntity(patient);
                DeleteRig(em, rig);
            }
        });
        await pair.CleanReturnAsync();
    }

    private static Rig CreateRig(IEntityManager em, EntityCoordinates coordinates)
    {
        var console = em.SpawnEntity("CMUBodyScannerConsole", coordinates);
        var pod = em.SpawnEntity("CMUBodyScannerPod", coordinates.Offset(new Vector2(1, 0)));
        var patient = CreatePatient(em, coordinates);
        var user = em.SpawnEntity("CMMobHuman", coordinates);
        em.System<SkillsSystem>().SetSkill(user, "RMCSkillSurgery", 1);
        var podComponent = em.GetComponent<CMUBodyScannerPodComponent>(pod);
        var consoleComponent = em.GetComponent<CMUBodyScannerConsoleComponent>(console);
        consoleComponent.PulseWindowSize = consoleComponent.MinPulseWindowSize = consoleComponent.PulseGraceSize = 1;
        Assert.That(em.System<CMUMedicalPatientBaySystem>().TryInsertPatient(pod, podComponent.BodyContainer, patient), Is.True);
        return new Rig(console, pod, patient, user, consoleComponent, podComponent);
    }

    private static EntityUid CreatePatient(IEntityManager em, EntityCoordinates coordinates)
    {
        var patient = em.SpawnEntity("CMMobHuman", coordinates);
        Assert.That(em.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(patient,
            new(BodyPartType.Torso, BodyPartSymmetry.None), out var torso), Is.True);
        em.System<SharedCMUWoundsSystem>().SeedInternalBleed(torso, "scanner-fixture", 0.1f);
        return patient;
    }

    private static CMUBodyScannerBuiState State(IEntityManager em, Rig rig)
        => em.System<CMUBodyScannerSystem>().BuildStateForViewer(rig.Console, rig.ConsoleComponent, rig.User);

    private static void Send<T>(IEntityManager em, Rig rig, T message) where T : BoundUserInterfaceMessage
    {
        message.Actor = rig.User;
        message.UiKey = CMUBodyScannerUIKey.Key;
        em.EventBus.RaiseLocalEvent(rig.Console, message);
    }

    private static void ReplacePatient(IEntityManager em, Rig rig, EntityUid replacement)
    {
        if (rig.PodComponent.BodyContainer.ContainedEntity is { } current)
            Assert.That(em.System<SharedContainerSystem>().Remove(current, rig.PodComponent.BodyContainer), Is.True);
        Assert.That(em.System<CMUMedicalPatientBaySystem>().TryInsertPatient(rig.Pod, rig.PodComponent.BodyContainer, replacement), Is.True);
    }

    private static void DeleteRig(IEntityManager em, Rig rig)
    {
        em.DeleteEntity(rig.Patient);
        em.DeleteEntity(rig.User);
        em.DeleteEntity(rig.Console);
        em.DeleteEntity(rig.Pod);
    }

    private sealed record Rig(EntityUid Console, EntityUid Pod, EntityUid Patient, EntityUid User,
        CMUBodyScannerConsoleComponent ConsoleComponent, CMUBodyScannerPodComponent PodComponent);
}
