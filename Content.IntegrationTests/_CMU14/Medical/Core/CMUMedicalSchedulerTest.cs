using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Core;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Medical.Core;

[TestFixture]
public sealed class CMUMedicalSchedulerTest
{
    private const int RescheduleBurstSize = 1024;
    private static readonly CMUMedicalWorkKey WorkKey = new("scheduler-test");

    [Test]
    public async Task ReschedulingReplacesTheExistingDeadline()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var timing = server.ResolveDependency<IGameTiming>();
        EntityUid target = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            entMan.System<CMUMedicalSchedulerProbeSystem>();
            var scheduler = entMan.System<CMUMedicalSchedulerSystem>();
            target = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<CMUMedicalSchedulerProbeComponent>(target);

            var now = timing.CurTime;
            for (var i = 0; i < RescheduleBurstSize; i++)
                scheduler.Schedule(target, WorkKey, now + TimeSpan.FromSeconds(0.1));

            scheduler.Schedule(target, WorkKey, now + TimeSpan.FromSeconds(0.5));
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.25f));

        await server.WaitAssertion(() =>
        {
            var probe = server.EntMan.GetComponent<CMUMedicalSchedulerProbeComponent>(target);
            Assert.That(probe.DueKeys, Is.Empty);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.5f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var probe = entMan.GetComponent<CMUMedicalSchedulerProbeComponent>(target);
            Assert.That(probe.DueKeys, Is.EqualTo(new[] { WorkKey }));
            entMan.DeleteEntity(target);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CancelledWorkDoesNotDispatch()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var timing = server.ResolveDependency<IGameTiming>();
        EntityUid target = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            entMan.System<CMUMedicalSchedulerProbeSystem>();
            var scheduler = entMan.System<CMUMedicalSchedulerSystem>();
            target = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<CMUMedicalSchedulerProbeComponent>(target);

            scheduler.Schedule(target, WorkKey, timing.CurTime + TimeSpan.FromSeconds(0.1));
            Assert.That(scheduler.Cancel(target, WorkKey), Is.True);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.25f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var probe = entMan.GetComponent<CMUMedicalSchedulerProbeComponent>(target);
            Assert.That(probe.DueKeys, Is.Empty);
            entMan.DeleteEntity(target);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeletingOneTargetDoesNotBlockOtherDueWork()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var timing = server.ResolveDependency<IGameTiming>();
        EntityUid deleted = default;
        EntityUid survivor = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            entMan.System<CMUMedicalSchedulerProbeSystem>();
            var scheduler = entMan.System<CMUMedicalSchedulerSystem>();
            deleted = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            survivor = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<CMUMedicalSchedulerProbeComponent>(deleted);
            entMan.AddComponent<CMUMedicalSchedulerProbeComponent>(survivor);

            var dueAt = timing.CurTime + TimeSpan.FromSeconds(0.1);
            scheduler.Schedule(deleted, WorkKey, dueAt);
            scheduler.Schedule(survivor, WorkKey, dueAt);
            entMan.DeleteEntity(deleted);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.25f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var probe = entMan.GetComponent<CMUMedicalSchedulerProbeComponent>(survivor);
            Assert.That(probe.DueKeys, Is.EqualTo(new[] { WorkKey }));
            entMan.DeleteEntity(survivor);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PausedTargetKeepsItsRemainingDelay()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var timing = server.ResolveDependency<IGameTiming>();
        EntityUid target = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            entMan.System<CMUMedicalSchedulerProbeSystem>();
            var scheduler = entMan.System<CMUMedicalSchedulerSystem>();
            target = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<CMUMedicalSchedulerProbeComponent>(target);

            scheduler.Schedule(target, WorkKey, timing.CurTime + TimeSpan.FromSeconds(0.25));
            entMan.System<MetaDataSystem>().SetEntityPaused(target, true);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.5f));

        await server.WaitAssertion(() =>
        {
            var probe = server.EntMan.GetComponent<CMUMedicalSchedulerProbeComponent>(target);
            Assert.That(probe.DueKeys, Is.Empty);
            server.EntMan.System<MetaDataSystem>().SetEntityPaused(target, false);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.1f));

        await server.WaitAssertion(() =>
        {
            var probe = server.EntMan.GetComponent<CMUMedicalSchedulerProbeComponent>(target);
            Assert.That(probe.DueKeys, Is.Empty);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.25f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var probe = entMan.GetComponent<CMUMedicalSchedulerProbeComponent>(target);
            Assert.That(probe.DueKeys, Is.EqualTo(new[] { WorkKey }));
            entMan.DeleteEntity(target);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ImmediateRescheduleWaitsUntilTheNextUpdate()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var timing = server.ResolveDependency<IGameTiming>();
        EntityUid target = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            entMan.System<CMUMedicalSchedulerProbeSystem>();
            var scheduler = entMan.System<CMUMedicalSchedulerSystem>();
            target = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var probe = entMan.AddComponent<CMUMedicalSchedulerProbeComponent>(target);
            probe.RescheduleUntilCount = 2;

            scheduler.Schedule(target, WorkKey, timing.CurTime);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var probe = server.EntMan.GetComponent<CMUMedicalSchedulerProbeComponent>(target);
            Assert.That(probe.DueKeys, Has.Count.EqualTo(1));
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var probe = entMan.GetComponent<CMUMedicalSchedulerProbeComponent>(target);
            Assert.That(probe.DueKeys, Is.EqualTo(new[] { WorkKey, WorkKey }));
            entMan.DeleteEntity(target);
        });

        await pair.CleanReturnAsync();
    }
}

[RegisterComponent]
public sealed partial class CMUMedicalSchedulerProbeComponent : Component
{
    public readonly List<CMUMedicalWorkKey> DueKeys = new();

    public int RescheduleUntilCount;
}

public sealed partial class CMUMedicalSchedulerProbeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUMedicalSchedulerProbeComponent, CMUMedicalWorkDueEvent>(OnWorkDue);
    }

    private void OnWorkDue(
        Entity<CMUMedicalSchedulerProbeComponent> ent,
        ref CMUMedicalWorkDueEvent args)
    {
        ent.Comp.DueKeys.Add(args.Key);
        if (ent.Comp.DueKeys.Count < ent.Comp.RescheduleUntilCount)
            EntityManager.System<CMUMedicalSchedulerSystem>().Schedule(ent.Owner, args.Key, _timing.CurTime);
    }
}
