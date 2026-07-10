using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Steps;
using Content.Shared.Body.Part;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Medical.Treatment.Surgery;

[TestFixture]
public sealed class CMUSurgerySessionTest
{
    private static readonly CMUMedicalBodyPartKey Site = new(BodyPartType.Arm, BodyPartSymmetry.Left);
    private static readonly EntProtoId<CMSurgeryComponent> Procedure = "CMUSurgeryCauterizeInternalBleeding";
    private static readonly EntProtoId<CMSurgeryStepComponent> Step = "CMSurgeryStepOpenIncisionScalpel";

    [Test]
    public async Task CancelledAttemptCanBeContinuedByDifferentSurgeon()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var sessions = entMan.System<CMUSurgerySessionSystem>();
            var patient = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var firstSurgeon = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var secondSurgeon = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var firstTool = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var secondTool = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var target = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            try
            {
                Assert.That(
                    sessions.TryBeginAttempt(
                        patient,
                        firstSurgeon,
                        firstTool,
                        target,
                        Site,
                        Procedure,
                        Step,
                        out var firstToken),
                    Is.EqualTo(CMUSurgeryAttemptStartResult.Started));
                Assert.That(
                    sessions.TryConsumeAttempt(
                        patient,
                        firstToken,
                        firstSurgeon,
                        firstTool,
                        target,
                        new EntProtoId<CMSurgeryStepComponent>("CMSurgeryStepClampBleeders")),
                    Is.False);
                Assert.That(
                    sessions.TryConsumeAttempt(patient, firstToken, firstSurgeon, firstTool, target, Step),
                    Is.True);
                Assert.That(sessions.TryGetSession(patient, out var awaiting), Is.True);

                Assert.That(
                    sessions.TryBeginAttempt(
                        patient,
                        secondSurgeon,
                        secondTool,
                        target,
                        Site,
                        Procedure,
                        Step,
                        out var secondToken),
                    Is.EqualTo(CMUSurgeryAttemptStartResult.Started));
                Assert.That(sessions.TryGetSession(patient, out var performing), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(awaiting.Phase, Is.EqualTo(CMUSurgerySessionPhase.AwaitingAction));
                    Assert.That(awaiting.ActiveSurgeon, Is.Null);
                    Assert.That(performing.Id, Is.EqualTo(awaiting.Id));
                    Assert.That(performing.Phase, Is.EqualTo(CMUSurgerySessionPhase.Performing));
                    Assert.That(performing.ActiveSurgeon, Is.EqualTo(secondSurgeon));
                    Assert.That(secondToken, Is.Not.EqualTo(firstToken));
                });
            }
            finally
            {
                entMan.DeleteEntity(target);
                entMan.DeleteEntity(secondTool);
                entMan.DeleteEntity(firstTool);
                entMan.DeleteEntity(secondSurgeon);
                entMan.DeleteEntity(firstSurgeon);
                entMan.DeleteEntity(patient);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ConcurrentAttemptIsRejectedWithoutChangingActiveAttempt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var sessions = entMan.System<CMUSurgerySessionSystem>();
            var patient = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var firstSurgeon = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var secondSurgeon = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var firstTool = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var secondTool = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var target = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            try
            {
                Assert.That(
                    sessions.TryBeginAttempt(
                        patient,
                        firstSurgeon,
                        firstTool,
                        target,
                        Site,
                        Procedure,
                        Step,
                        out var firstToken),
                    Is.EqualTo(CMUSurgeryAttemptStartResult.Started));

                Assert.That(
                    sessions.TryBeginAttempt(
                        patient,
                        secondSurgeon,
                        secondTool,
                        target,
                        Site,
                        Procedure,
                        Step,
                        out var rejectedToken),
                    Is.EqualTo(CMUSurgeryAttemptStartResult.Busy));
                Assert.That(sessions.TryGetSession(patient, out var session), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(rejectedToken, Is.EqualTo(default(CMUSurgeryAttemptToken)));
                    Assert.That(session.Phase, Is.EqualTo(CMUSurgerySessionPhase.Performing));
                    Assert.That(session.ActiveSurgeon, Is.EqualTo(firstSurgeon));
                    Assert.That(session.ActiveAttempt, Is.EqualTo(firstToken));
                });
            }
            finally
            {
                entMan.DeleteEntity(target);
                entMan.DeleteEntity(secondTool);
                entMan.DeleteEntity(firstTool);
                entMan.DeleteEntity(secondSurgeon);
                entMan.DeleteEntity(firstSurgeon);
                entMan.DeleteEntity(patient);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StaleAttemptCannotConsumeReplacementAttempt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var sessions = entMan.System<CMUSurgerySessionSystem>();
            var patient = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var firstSurgeon = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var secondSurgeon = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var firstTool = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var secondTool = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var target = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            try
            {
                Assert.That(
                    sessions.TryBeginAttempt(
                        patient,
                        firstSurgeon,
                        firstTool,
                        target,
                        Site,
                        Procedure,
                        Step,
                        out var staleToken),
                    Is.EqualTo(CMUSurgeryAttemptStartResult.Started));
                Assert.That(sessions.TryGetSession(patient, out var firstSession), Is.True);
                Assert.That(
                    sessions.TryConsumeAttempt(patient, staleToken, firstSurgeon, firstTool, target, Step),
                    Is.True);
                Assert.That(
                    sessions.TryBeginAttempt(
                        patient,
                        secondSurgeon,
                        secondTool,
                        target,
                        Site,
                        Procedure,
                        Step,
                        out var currentToken),
                    Is.EqualTo(CMUSurgeryAttemptStartResult.Started));

                Assert.That(
                    sessions.TryConsumeAttempt(patient, staleToken, firstSurgeon, firstTool, target, Step),
                    Is.False);
                Assert.That(sessions.TryGetSession(patient, out var session), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        sessions.MatchesExpectedState(patient, firstSession.Id, staleToken),
                        Is.False);
                    Assert.That(
                        sessions.MatchesExpectedState(patient, session.Id, currentToken),
                        Is.True);
                    Assert.That(session.Phase, Is.EqualTo(CMUSurgerySessionPhase.Performing));
                    Assert.That(session.ActiveSurgeon, Is.EqualTo(secondSurgeon));
                    Assert.That(session.ActiveAttempt, Is.EqualTo(currentToken));
                });
            }
            finally
            {
                entMan.DeleteEntity(target);
                entMan.DeleteEntity(secondTool);
                entMan.DeleteEntity(firstTool);
                entMan.DeleteEntity(secondSurgeon);
                entMan.DeleteEntity(firstSurgeon);
                entMan.DeleteEntity(patient);
            }
        });

        await pair.CleanReturnAsync();
    }
}
