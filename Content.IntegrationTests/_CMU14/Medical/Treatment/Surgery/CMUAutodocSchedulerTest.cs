using System.Reflection;
using Content.Server._CMU14.Medical.Treatment.Surgery;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared.Body.Part;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Medical.Treatment.Surgery;

[TestFixture]
public sealed class CMUAutodocSchedulerTest
{
    [Test]
    public async Task RestartingProcedureReplacesTheExistingDeadline()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid pod = default;
        TimeSpan firstDeadline = default;
        TimeSpan replacementDeadline = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var autodoc = entMan.System<CMUAutodocSystem>();
            pod = entMan.SpawnEntity("CMUAutodocPod", MapCoordinates.Nullspace);
            var component = entMan.GetComponent<CMUAutodocPodComponent>(pod);
            component.IsRunning = true;

            StartProcedureTimer(autodoc, pod, component, CreateStep(durationSeconds: 2f));
            firstDeadline = component.NextStepAt;
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.5f));

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var autodoc = entMan.System<CMUAutodocSystem>();
            var component = entMan.GetComponent<CMUAutodocPodComponent>(pod);
            StartProcedureTimer(autodoc, pod, component, CreateStep(durationSeconds: 3f));
            replacementDeadline = component.NextStepAt;
            Assert.That(replacementDeadline, Is.GreaterThan(firstDeadline));
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1.75f));

        await server.WaitAssertion(() =>
        {
            var component = server.EntMan.GetComponent<CMUAutodocPodComponent>(pod);
            Assert.Multiple(() =>
            {
                Assert.That(component.IsRunning, Is.True);
                Assert.That(component.NextStepAt, Is.EqualTo(replacementDeadline));
            });
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1.5f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var component = entMan.GetComponent<CMUAutodocPodComponent>(pod);
            Assert.Multiple(() =>
            {
                Assert.That(component.IsRunning, Is.False);
                Assert.That(component.NextStepAt, Is.EqualTo(TimeSpan.Zero));
            });
            entMan.DeleteEntity(pod);
        });

        await pair.CleanReturnAsync();
    }

    private static CMUAutodocQueuedStep CreateStep(float durationSeconds)
    {
        return new CMUAutodocQueuedStep(
            EntityUid.Invalid,
            BodyPartType.Torso,
            BodyPartSymmetry.None,
            "scheduler-test",
            "Scheduler test",
            "scheduler-test",
            0,
            "scheduler test",
            "torso",
            durationSeconds);
    }

    private static void StartProcedureTimer(
        CMUAutodocSystem autodoc,
        EntityUid pod,
        CMUAutodocPodComponent component,
        CMUAutodocQueuedStep queued)
    {
        var method = typeof(CMUAutodocSystem).GetMethod(
            "StartProcedureTimer",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        method!.Invoke(autodoc, [pod, pod, component, queued]);
    }
}
