using System.Collections.Generic;
using System.Linq;
using Content.Server._CMU14.Medical.Treatment.Surgery;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared.Body.Part;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Medical.Treatment.Surgery;

[TestFixture]
public sealed class CMUBodyScannerCalibrationSchedulerTest
{
    [Test]
    public async Task PuzzleProjectionIsSharedWithinTickAndInvalidatedByRevision()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid patient = default;
        CMUBodyScannerConsoleComponent scanner = default!;
        List<CMUBodyScannerSliceSignal> initialTargets = default!;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            Assert.That(index.TryGetBodyPart(
                patient,
                new CMUMedicalBodyPartKey(BodyPartType.Torso, BodyPartSymmetry.None),
                out var torso), Is.True);
            entMan.EnsureComponent<InternalBleedingComponent>(torso);
            scanner = CreateScanner(boostDurationSeconds: 1f);
        });

        // Flush spawn-time structural changes so the next mutation owns a new revision.
        await pair.RunTicksSync(1);

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var calibration = entMan.System<CMUBodyScannerCalibrationSystem>();
            var changes = entMan.System<CMUMedicalChangeSystem>();
            var first = calibration.BuildView(null, patient, canScan: true, scanner);
            var repeated = calibration.BuildView(null, patient, canScan: true, scanner);
            Assert.That(first.Targets, Is.Not.Empty);
            Assert.That(repeated.Targets, Is.SameAs(first.Targets));
            initialTargets = first.Targets;

            Assert.That(changes.MarkChanged(patient, CMUMedicalChangeFlags.Wounds), Is.True);
            var invalidated = calibration.BuildView(null, patient, canScan: true, scanner);
            Assert.That(invalidated.Targets, Is.Not.SameAs(initialTargets));
            initialTargets = invalidated.Targets;
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var calibration = server.EntMan.System<CMUBodyScannerCalibrationSystem>();
            var nextTick = calibration.BuildView(null, patient, canScan: true, scanner);
            Assert.That(nextTick.Targets, Is.Not.SameAs(initialTargets));
            server.EntMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RecalibratingReplacesTheExistingBoostExpiry()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid user = default;
        EntityUid patient = default;
        CMUBodyScannerConsoleComponent scanner = default!;
        TimeSpan initialExpiry = default;
        TimeSpan replacementExpiry = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var calibration = entMan.System<CMUBodyScannerCalibrationSystem>();
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            user = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            Assert.That(index.TryGetBodyPart(
                patient,
                new CMUMedicalBodyPartKey(BodyPartType.Torso, BodyPartSymmetry.None),
                out var torso), Is.True);
            entMan.EnsureComponent<InternalBleedingComponent>(torso);

            scanner = CreateScanner(boostDurationSeconds: 0.25f);
            CompletePuzzle(calibration, user, patient, scanner);
            initialExpiry = entMan.GetComponent<CMUBodyScannerSurgerySpeedComponent>(user).ExpiresAt;
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.1f));

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var calibration = entMan.System<CMUBodyScannerCalibrationSystem>();
            scanner.BoostDurationSeconds = 0.6f;
            CompletePuzzle(calibration, user, patient, scanner);
            replacementExpiry = entMan.GetComponent<CMUBodyScannerSurgerySpeedComponent>(user).ExpiresAt;
            Assert.That(replacementExpiry, Is.GreaterThan(initialExpiry));
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.25f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var calibration = entMan.System<CMUBodyScannerCalibrationSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<CMUBodyScannerSurgerySpeedComponent>(user), Is.True);
                Assert.That(calibration.GetSurgeryDelayMultiplier(user, patient), Is.EqualTo(0.5f));
            });
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.5f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var calibration = entMan.System<CMUBodyScannerCalibrationSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<CMUBodyScannerSurgerySpeedComponent>(user), Is.False);
                Assert.That(calibration.GetSurgeryDelayMultiplier(user, patient), Is.EqualTo(1f));
            });
            entMan.DeleteEntity(user);
            entMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BoostAndLockoutExpireIndependently()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid user = default;
        EntityUid patient = default;
        CMUBodyScannerConsoleComponent scanner = default!;
        CMUBodyScannerSliceSignal target = default!;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var calibration = entMan.System<CMUBodyScannerCalibrationSystem>();
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            user = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            Assert.That(index.TryGetBodyPart(
                patient,
                new CMUMedicalBodyPartKey(BodyPartType.Torso, BodyPartSymmetry.None),
                out var torso), Is.True);
            entMan.EnsureComponent<InternalBleedingComponent>(torso);

            scanner = CreateScanner(boostDurationSeconds: 0.8f);
            CompletePuzzle(calibration, user, patient, scanner);

            scanner.CalibrationDurationSeconds = 0.05f;
            scanner.CalibrationLockoutSeconds = 0.2f;
            Assert.That(calibration.ResetPuzzle(user, patient, scanner), Is.True);
            target = calibration.BuildView(user, patient, canScan: true, scanner)
                .Targets
                .First(signal => !signal.IsDecoy);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.1f));

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var calibration = entMan.System<CMUBodyScannerCalibrationSystem>();
            Assert.That(
                calibration.TryConfirmPuzzle(user, patient, scanner, target.LayerId, target.Id, clientPhase: 0f),
                Is.True);
            Assert.That(entMan.HasComponent<CMUBodyScannerCalibrationLockoutComponent>(user), Is.True);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.3f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var calibration = entMan.System<CMUBodyScannerCalibrationSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<CMUBodyScannerCalibrationLockoutComponent>(user), Is.False);
                Assert.That(entMan.HasComponent<CMUBodyScannerSurgerySpeedComponent>(user), Is.True);
                Assert.That(calibration.GetSurgeryDelayMultiplier(user, patient), Is.EqualTo(0.5f));
            });
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.5f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<CMUBodyScannerCalibrationLockoutComponent>(user), Is.False);
                Assert.That(entMan.HasComponent<CMUBodyScannerSurgerySpeedComponent>(user), Is.False);
            });
            entMan.DeleteEntity(user);
            entMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }

    private static CMUBodyScannerConsoleComponent CreateScanner(float boostDurationSeconds)
    {
        return new CMUBodyScannerConsoleComponent
        {
            BoostDurationSeconds = boostDurationSeconds,
            CalibrationDurationSeconds = 10f,
            PulseWindowSize = 1f,
            MinPulseWindowSize = 1f,
            PulseGraceSize = 1f,
        };
    }

    private static void CompletePuzzle(
        CMUBodyScannerCalibrationSystem calibration,
        EntityUid user,
        EntityUid patient,
        CMUBodyScannerConsoleComponent scanner)
    {
        Assert.That(calibration.ResetPuzzle(user, patient, scanner), Is.True);
        var view = calibration.BuildView(user, patient, canScan: true, scanner);
        var targets = view.Targets.Where(target => !target.IsDecoy).ToArray();
        Assert.That(targets, Is.Not.Empty);

        foreach (var target in targets)
        {
            Assert.That(
                calibration.TryConfirmPuzzle(user, patient, scanner, target.LayerId, target.Id, clientPhase: 0f),
                Is.True);
        }

        Assert.That(calibration.GetSurgeryDelayMultiplier(user, patient), Is.EqualTo(0.5f));
    }
}
