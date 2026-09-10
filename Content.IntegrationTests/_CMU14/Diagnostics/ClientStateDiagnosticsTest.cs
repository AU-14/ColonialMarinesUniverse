using System.Collections.Concurrent;
using System.Linq;
using Content.Server.CMU14.Diagnostics;
using Content.Client.CMU14.Diagnostics;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Diagnostics;
using Content.Shared.GameTicking;
using Robust.Client.GameStates;
using Robust.Server.GameStates;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Timing;
using Serilog.Events;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class ClientStateDiagnosticsTest
{
    [Test]
    public async Task ActualClientApplicationProgressReachesServerWithoutChangingStateDelivery()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        await pair.RunUntilSynced();
        var capture = new Capture();
        ISawmill logger = null;
        await pair.Server.WaitPost(() =>
        {
            var config = pair.Server.ResolveDependency<IConfigurationManager>();
            config.SetCVar(CCVars.CMUClientStateDiagnosticsEnabled, false);
            config.SetCVar(CCVars.CMUClientStateDiagnosticsEnabled, true);
            logger = pair.Server.ResolveDependency<ILogManager>().GetSawmill(CMUClientStateDiagnosticsSystem.SawmillName);
            logger.AddHandler(capture);
        });
        try
        {
            await pair.Client.WaitPost(() => pair.Client.EntMan.System<CMUClientStateHealthSystem>().ReportHealth());
            await pair.RunTicksSync(5);
            await pair.Client.WaitPost(() => pair.Client.ResolveDependency<IClientGameStateManager>().RequestFullState());
            await pair.RunTicksSync(5);
            await pair.RunUntilSynced();
            await pair.Server.WaitAssertion(() =>
            {
                var request = capture.Messages.Single(message => message.StartsWith("full-state-request "));
                Assert.Multiple(() =>
                {
                    Assert.That(request, Does.Contain("clientAppliedState=client-reported "));
                    Assert.That(request, Does.Not.Contain("clientAppliedTick=0 "));
                    Assert.That(request, Does.Contain("healthReportAgeSeconds="));
                    Assert.That(request, Does.Contain("perfIncidentId="));
                });
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => logger.RemoveHandler(capture));
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InvalidAndFloodedHealthReportsDoNotReplaceAcceptedEvidence()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var server = pair.Server;
            var config = server.ResolveDependency<IConfigurationManager>();
            config.SetCVar(CCVars.CMUClientStateDiagnosticsEnabled, false);
            config.SetCVar(CCVars.CMUClientStateDiagnosticsEnabled, true);
            var system = server.EntMan.System<CMUClientStateDiagnosticsSystem>();
            var states = server.ResolveDependency<IServerGameStateManager>();
            var tick = server.ResolveDependency<IGameTiming>().CurTick;
            var capture = new Capture();
            var logger = server.ResolveDependency<ILogManager>().GetSawmill(CMUClientStateDiagnosticsSystem.SawmillName);
            logger.AddHandler(capture);
            try
            {
                system.ObserveHealth(pair.Player!, new CMUClientStateHealthEvent { AppliedTick = tick + 100 });
                system.ObserveHealth(pair.Player!, new CMUClientStateHealthEvent { AppliedTick = tick, AverageFps = double.NaN });
                for (var i = 0; i < 10; i++)
                    states.ClientRequestFull?.Invoke(pair.Player!, GameTick.Zero, null);
                Assert.That(capture.Messages.Single(), Does.Contain("clientAppliedState=unknown"));
                config.SetCVar(CCVars.CMUClientStateDiagnosticsEnabled, false);
                config.SetCVar(CCVars.CMUClientStateDiagnosticsEnabled, true);
                capture.Messages.Clear();

                system.ObserveHealth(pair.Player!, new CMUClientStateHealthEvent
                {
                    AppliedTick = tick - 2, AppliedAgeSeconds = 0.25, BufferedStates = 4, AverageFps = 60,
                });
                system.ObserveHealth(pair.Player!, new CMUClientStateHealthEvent
                {
                    AppliedTick = tick - 1, BufferedStates = 999, AverageFps = 60,
                });
                states.ClientRequestFull?.Invoke(pair.Player!, tick, null);
                var request = capture.Messages.Single();
                Assert.Multiple(() =>
                {
                    Assert.That(request, Does.Contain($"clientAppliedTick={tick - 2} "));
                    Assert.That(request, Does.Contain("bufferedStates=4 "));
                    Assert.That(request, Does.Not.Contain("bufferedStates=999 "));
                });
            }
            finally
            {
                logger.RemoveHandler(capture);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RealClientRequestIsObservedWithoutPreventingStateDelivery()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var capture = new Capture();
        ISawmill logger = null;
        await server.WaitPost(() =>
        {
            var config = server.ResolveDependency<IConfigurationManager>();
            config.SetCVar(CCVars.CMUClientStateDiagnosticsEnabled, false);
            config.SetCVar(CCVars.CMUClientStateDiagnosticsEnabled, true);
            logger = server.ResolveDependency<ILogManager>().GetSawmill(CMUClientStateDiagnosticsSystem.SawmillName);
            logger.AddHandler(capture);
        });
        try
        {
            await pair.Client.WaitPost(() => pair.Client.ResolveDependency<IClientGameStateManager>().RequestFullState());
            await pair.RunTicksSync(5);
            await pair.RunUntilSynced();
            await server.WaitAssertion(() =>
            {
                Assert.That(capture.Messages.Count(message => message.StartsWith("full-state-request ")), Is.EqualTo(1));

                Assert.That(capture.Messages.Count(message => message.StartsWith("sync-incident-open ")), Is.Zero);
            });
        }
        finally
        {
            await server.WaitPost(() => logger.RemoveHandler(capture));
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MultiClientIncidentHasGlobalDetailCapEvenAcrossCleanup()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var sessions = await server.AddDummySessions(12);
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            var config = server.ResolveDependency<IConfigurationManager>();
            config.SetCVar(CCVars.CMUClientStateDiagnosticsEnabled, false);
            config.SetCVar(CCVars.CMUClientStateDiagnosticsEnabled, true);
            var capture = new Capture();
            var logger = server.ResolveDependency<ILogManager>().GetSawmill(CMUClientStateDiagnosticsSystem.SawmillName);
            logger.AddHandler(capture);
            try
            {
                var states = server.ResolveDependency<IServerGameStateManager>();
                var tick = server.ResolveDependency<IGameTiming>().CurTick;
                foreach (var session in sessions)
                    states.ClientRequestFull?.Invoke(session, tick, new NetEntity(int.MaxValue));

                Assert.That(capture.Messages.Count(message => message.StartsWith("full-state-request ")), Is.EqualTo(8));
                server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
                var summary = capture.Messages.Single(message => message.StartsWith("state-request-summary "));
                Assert.Multiple(() =>
                {
                    Assert.That(summary, Does.Contain("requests=12 "));
                    Assert.That(summary, Does.Contain("affectedConnectedClients=12 "));
                    Assert.That(summary, Does.Contain("suppressedDetails=4 "));
                });

                foreach (var session in sessions)
                    states.ClientRequestFull?.Invoke(session, tick, null);
                Assert.That(capture.Messages.Count(message => message.StartsWith("full-state-request ")), Is.EqualTo(8));
            }
            finally
            {
                logger.RemoveHandler(capture);
            }
        });

        foreach (var session in sessions)
            await server.RemoveDummySession(session);
        await pair.RunTicksSync(5);
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StateRequestHookReportsContextAndBoundsRepeatedRequests()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var config = server.ResolveDependency<IConfigurationManager>();
            config.SetCVar(CCVars.CMUClientStateDiagnosticsEnabled, false);
            config.SetCVar(CCVars.CMUClientStateDiagnosticsEnabled, true);
            var capture = new Capture();
            var logger = server.ResolveDependency<ILogManager>().GetSawmill(CMUClientStateDiagnosticsSystem.SawmillName);
            logger.AddHandler(capture);
            try
            {
                var states = server.ResolveDependency<IServerGameStateManager>();
                var tick = server.ResolveDependency<IGameTiming>().CurTick;
                var session = pair.Player!;
                var ack = tick - 2;
                states.ClientAck?.Invoke(session, ack);
                states.ClientRequestFull?.Invoke(session, tick, null);

                var first = capture.Messages.Single(message => message.StartsWith("full-state-request "));
                Assert.Multiple(() =>
                {
                    Assert.That(first, Does.Contain($"user={session.UserId}"));
                    Assert.That(first, Does.Contain($"requestedTick={tick}"));
                    Assert.That(first, Does.Contain($"lastReceivedAck={ack}"));
                    Assert.That(first, Does.Contain("cleanupTick="));
                    Assert.That(first, Does.Contain("clientAppliedState=unknown"));
                });

                for (var i = 0; i < 20; i++)
                    states.ClientRequestFull?.Invoke(session, tick, null);

                Assert.That(capture.Messages.Count(message => message.StartsWith("full-state-request ")), Is.EqualTo(1));
                Assert.That(capture.Messages.Count(message => message.StartsWith("sync-incident-open ")), Is.EqualTo(1));

                // ACK progress is observed independently of PVS's forced-full-state bookkeeping.
                // Neither that bookkeeping nor an ACK proves that the client applied the state.
                states.ClientAck?.Invoke(session, tick - 1);
                states.ClientAck?.Invoke(session, ack);
                server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
                var summary = capture.Messages.Single(message => message.StartsWith("state-request-summary "));
                Assert.Multiple(() =>
                {
                    Assert.That(summary, Does.Contain("requests=21 "));
                    Assert.That(summary, Does.Contain("recoveryRequests=21 "));
                    Assert.That(summary, Does.Contain("activeSyncClients=1 "));
                    Assert.That(summary, Does.Contain("affectedConnectedClients=1 "));
                    Assert.That(summary, Does.Contain("ackAdvancedAfterLastRequestClients=1 "));
                    Assert.That(summary, Does.Contain("suppressedDetails=20 "));
                    Assert.That(summary, Does.Contain("clientAppliedState=unknown"));
                });

                config.SetCVar(CCVars.CMUClientStateDiagnosticsEnabled, false);
                var disabledCount = capture.Messages.Count;
                states.ClientRequestFull?.Invoke(session, tick, null);
                Assert.That(capture.Messages.Count, Is.EqualTo(disabledCount));

                // Toggling the reporter must not accumulate duplicate network subscriptions.
                config.SetCVar(CCVars.CMUClientStateDiagnosticsEnabled, true);
                states.ClientRequestFull?.Invoke(session, tick, null);
                Assert.That(capture.Messages.Count(message => message.StartsWith("full-state-request ")), Is.EqualTo(2));
            }
            finally
            {
                logger.RemoveHandler(capture);
            }
        });

        await pair.CleanReturnAsync();
    }

    private sealed class Capture : ILogHandler
    {
        public readonly ConcurrentQueue<string> Messages = new();

        public void Log(string sawmillName, LogEvent message)
        {
            Messages.Enqueue(message.RenderMessage());
        }
    }
}
