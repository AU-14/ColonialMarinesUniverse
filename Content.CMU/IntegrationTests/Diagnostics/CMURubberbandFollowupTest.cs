using System.Reflection;
using Content.Client.CMU14.Diagnostics;
using Content.Client.CMU14.Diagnostics.Performance;
using Content.Client.CMU14.ZLevels.Core;
using Content.Client.CMU14.ZLevels.Culling;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Construction.Tunnel;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.CCVar;
using Content.Shared.CMU14.ZLevels;
using Moq;
using Robust.Client.GameObjects;
using Robust.Client.GameStates;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.Timing;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.GameStates;
using Robust.Shared.Graphics;
using Robust.Shared.Log;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Serilog.Events;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class CMURubberbandFollowupTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task TunnelDestinationListsRemainServerAuthoritative()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var map = maps.CreateMap(runMapInit: true);
            var otherMap = maps.CreateMap(runMapInit: true);
            var hive = SEntMan.SpawnEntity("CMXenoHive", new EntityCoordinates(map, Vector2.Zero));
            var tunnels = SEntMan.System<XenoTunnelSystem>();
            Assert.That(tunnels.TryPlaceTunnel(hive, "source", new EntityCoordinates(map, Vector2.Zero), out var source), Is.True);
            Assert.That(tunnels.TryPlaceTunnel(hive, "reachable", new EntityCoordinates(map, Vector2.One), out var destination), Is.True);
            Assert.That(tunnels.TryPlaceTunnel(hive, "unrelated", new EntityCoordinates(otherMap, Vector2.Zero), out _), Is.True);
            var stale = SEntMan.SpawnEntity(null, new EntityCoordinates(map, Vector2.Zero));
            SEntMan.DeleteEntity(stale);
            var entries = SEntMan.GetComponent<HiveComponent>(hive).HiveTunnels;
            entries.Add("unresolved", EntityUid.Invalid);
            entries.Add("deleted", stale);
            var actor = SEntMan.SpawnEntity("CMXenoDrone", new EntityCoordinates(map, Vector2.Zero));
            SEntMan.System<SharedXenoHiveSystem>().SetHive(actor, hive);
            SEntMan.System<SharedUserInterfaceSystem>().OpenUi(source!.Value, SelectDestinationTunnelUI.Key, actor);
            var state = (SelectDestinationTunnelInterfaceState) SEntMan.GetComponent<UserInterfaceComponent>(source.Value).States[SelectDestinationTunnelUI.Key];
            Assert.That(state.HiveTunnels.Keys, Is.EquivalentTo(new[] { "source", "reachable" }));
            Assert.That(state.HiveTunnels["reachable"], Is.EqualTo(SEntMan.GetNetEntity(destination!.Value)));
        });

        await Client.WaitAssertion(() =>
        {
            var entities = Client.EntMan;
            var map = entities.System<SharedMapSystem>().CreateMap(runMapInit: true);
            var hive = entities.SpawnEntity("CMXenoHive", new EntityCoordinates(map, Vector2.Zero));
            var tunnel = entities.SpawnEntity("XenoTunnel", new EntityCoordinates(map, Vector2.Zero));
            var actor = entities.SpawnEntity(null, new EntityCoordinates(map, Vector2.Zero));
            entities.System<SharedXenoHiveSystem>().SetHive(tunnel, hive);
#pragma warning disable RA0002 // Reproduce an unresolved reference in the replicated hive list.
            entities.GetComponent<HiveComponent>(hive).HiveTunnels.Add("remote", EntityUid.Invalid);
#pragma warning restore RA0002
            var authoritative = new SelectDestinationTunnelInterfaceState(new() { ["remote"] = new NetEntity(999999) });
            var ui = entities.GetComponent<UserInterfaceComponent>(tunnel);
            entities.System<SharedUserInterfaceSystem>().SetUiState(tunnel, SelectDestinationTunnelUI.Key, authoritative);
            var open = new OpenBoundInterfaceMessage
            {
                Actor = actor, Entity = entities.GetNetEntity(tunnel), UiKey = SelectDestinationTunnelUI.Key,
            };
            entities.EventBus.RaiseLocalEvent(tunnel, open);
            Assert.That(ui.States[SelectDestinationTunnelUI.Key], Is.SameAs(authoritative),
                "An unresolved local hive reference must not replace the server's remote destinations.");
        });
    }

    [Test]
    public async Task PredictionRewindsDoNotHidePostReplaySnaps()
    {
        await Client.WaitAssertion(() =>
        {
            var entities = Client.EntMan;
            var map = entities.System<SharedMapSystem>().CreateMap(runMapInit: true);
            var actor = entities.SpawnEntity(null, new EntityCoordinates(map, new Vector2(4, 0)));
            var system = entities.System<CMURubberbandDiagnosticsSystem>();
            var timingField = Field(system, "_timing");
            var playersField = Field(system, "_players");
            var originalTiming = timingField.GetValue(system);
            var originalPlayers = playersField.GetValue(system);
            var timing = new Mock<IClientGameTiming>();
            var players = new Mock<IPlayerManager>();
            uint frame = 100;
            uint tick = 110;
            var now = TimeSpan.FromSeconds(100);
            timing.SetupGet(t => t.CurFrame).Returns(() => frame);
            timing.SetupGet(t => t.CurTick).Returns(() => new GameTick(tick));
            timing.SetupGet(t => t.RealTime).Returns(() => now);
            timing.SetupGet(t => t.RealFrameTime).Returns(TimeSpan.FromSeconds(1d / 30));
            players.SetupGet(p => p.LocalEntity).Returns(actor);
            var logger = Client.ResolveDependency<ILogManager>().GetSawmill("cmu.rubberband");
            var capture = new LogCapture();
            bool ExpectedSnap(string sawmill, LogEvent message) => sawmill == "cmu.rubberband" &&
                message.Level == LogEventLevel.Warning && message.Exception == null &&
                message.RenderMessage().StartsWith("snap reason=frame dist=5.6 ");
            logger.AddHandler(capture);
            Pair.ClientLogHandler.JudgeLog += ExpectedSnap;
            try
            {
                timingField.SetValue(system, timing.Object);
                playersField.SetValue(system, players.Object);
                var config = Client.ResolveDependency<IConfigurationManager>();
                config.SetCVar(CCVars.CMURubberbandDiagnosticsEnabled, true);
                config.SetCVar(CCVars.CMURubberbandDiagnosticsLogCooldown, 0f);
                var transform = entities.System<SharedTransformSystem>();
                system.FrameUpdate(1f / 30);

                // The server sample precedes prediction replay and is ten ticks behind the rendered position.
                frame++;
                tick = 100;
                now += TimeSpan.FromSeconds(1d / 30);
                transform.SetCoordinates(actor, new EntityCoordinates(map, new Vector2(0.44f, 0)));
                var apply = typeof(CMURubberbandDiagnosticsSystem).GetMethod("OnGameStateApplied", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var state = new GameState(new GameTick(99), new GameTick(100), 0,
                    Array.Empty<EntityState>(), Array.Empty<SessionState>(), Array.Empty<NetEntity>());
                apply.Invoke(system, new object[] { new GameStateAppliedArgs(state, []) });
                tick = 111;
                transform.SetCoordinates(actor, new EntityCoordinates(map, new Vector2(4.4f, 0)));
                system.FrameUpdate(1f / 30);
                Assert.That(capture.Messages.Where(m => m.StartsWith("snap ")), Is.Empty);

                frame++;
                now += TimeSpan.FromSeconds(1d / 30);
                transform.SetCoordinates(actor, new EntityCoordinates(map, new Vector2(10, 0)));
                system.FrameUpdate(1f / 30);
                Assert.That(capture.Messages.Count(m => m.StartsWith("snap reason=frame ")), Is.EqualTo(1),
                    "A real post-replay jump must still be reported.");

                var followed = entities.SpawnEntity(null, new EntityCoordinates(map, new Vector2(30, 0)));
                frame++;
                now += TimeSpan.FromSeconds(1d / 30);
                transform.SetCoordinates(actor, new EntityCoordinates(followed, Vector2.Zero));
                system.FrameUpdate(1f / 30);
                Assert.That(capture.Messages.Count(m => m.StartsWith("snap ")), Is.EqualTo(1),
                    "Changing the camera's parent establishes a new comparison baseline.");
            }
            finally
            {
                Client.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMURubberbandDiagnosticsEnabled, false);
                timingField.SetValue(system, originalTiming);
                playersField.SetValue(system, originalPlayers);
                logger.RemoveHandler(capture);
                Pair.ClientLogHandler.JudgeLog -= ExpectedSnap;
            }
        });
    }

    [Test]
    public async Task DiagnosticFrameStallsRemainInRawCapture()
    {
        await Client.WaitAssertion(() =>
        {
            var system = Client.EntMan.System<CMUClientPerformanceSystem>();
            var resources = Client.ResolveDependency<IResourceManager>();
            var directory = new ResPath("/client-performance");
            resources.UserData.CreateDir(directory);
            var previousFiles = resources.UserData.DirectoryEntries(directory).ToArray();
            var timingField = Field(system, "_timing");
            var original = timingField.GetValue(system);
            var timing = new Mock<IClientGameTiming>();
            uint frame = 100;
            double ms = 16;
            var now = TimeSpan.FromSeconds(100);
            timing.SetupGet(t => t.CurFrame).Returns(() => frame);
            timing.SetupGet(t => t.RealFrameTime).Returns(() => TimeSpan.FromMilliseconds(ms));
            timing.SetupGet(t => t.RealTime).Returns(() => now);
            try
            {
                timingField.SetValue(system, timing.Object);
                system.StartCapture(30, 33.333);
                frame = 101;
                system.FrameUpdate(0.016f); // Startup frame, counted only in raw time.
                frame = 102;
                system.FrameUpdate(0.016f);
                system.ManualReport(); // Frame 102 becomes a diagnostic frame.
                frame = 103;
                ms = 2983.54;
                now += TimeSpan.FromMilliseconds(ms);
                system.FrameUpdate((float) (ms / 1000));
                frame = 104;
                ms = 16;
                system.FrameUpdate(0.016f);
                system.FrameUpdate(0.016f); // A repeated observation must not count twice.
                system.StopCapture();
                var file = resources.UserData.DirectoryEntries(directory).Except(previousFiles).Single();
                using var reader = resources.UserData.OpenText(directory / file);
                var text = reader.ReadToEnd();
                Assert.That(text, Does.Contain("raw-wall: frames=2"));
                Assert.That(text, Does.Contain("maxMs=2983.540 maxFrame=102 maxFrameExcluded=True spikes=1"));
                Assert.That(text, Does.Contain("rawFrames=4 rawSpikes=1 rawWorstWallMs=2983.540"));
                Assert.That(text, Does.Contain("measuredFrames=2 spikes=0 worstWallMs=16.000"),
                    "Filtered work statistics remain separate from the unfiltered stall evidence.");
            }
            finally
            {
                if (system.Capturing)
                    system.StopCapture();
                timingField.SetValue(system, original);
            }
        });
    }

    [Test]
    public async Task FullViewMasksSkipCullingAndNarrowedMasksStillApply()
    {
        await Client.WaitAssertion(() =>
        {
            var entities = Client.EntMan;
            var map = entities.System<SharedMapSystem>().CreateMap(out var mapId, runMapInit: true);
            var uid = entities.SpawnEntity("MobObserver", new EntityCoordinates(map, Vector2.Zero));
            var sprite = entities.GetComponent<SpriteComponent>(uid);
            var culling = entities.System<CMUZLevelSpriteCullingSystem>();
            Client.ResolveDependency<IConfigurationManager>().SetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled, true);
            var viewport = new Mock<IClydeViewport>();
            viewport.SetupProperty(v => v.Eye, new Eye { Position = new MapCoordinates(Vector2.Zero, mapId) });
            viewport.Setup(v => v.Render()).Callback(() => Assert.That(sprite.Visible, Is.True));
            var mask = new CMUZVisibilityMask();
            var bounds = new Box2(-10, -10, 10, 10);
            mask.SetOpenings(mapId, bounds, [], complete: false);
            culling.RenderViewport(viewport.Object, mask);
            Assert.That(culling.LastCandidates, Is.Zero, "A full-view fallback cannot cull any visible sprite.");
            viewport.Verify(v => v.Render(), Times.Once, "Skipping culling must still render the world.");

            mask.SetOpenings(mapId, bounds, new[] { new Box2(5, 5, 6, 6) }, complete: true);
            viewport.Setup(v => v.Render()).Callback(() => Assert.That(sprite.Visible, Is.False));
            culling.RenderViewport(viewport.Object, mask);
            viewport.Verify(v => v.Render(), Times.Exactly(2));
            Assert.That(sprite.Visible, Is.True, "Culling must not persist into another render.");
        });
    }

    private static FieldInfo Field(object target, string name)
        => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    private sealed class LogCapture : ILogHandler
    {
        public readonly List<string> Messages = new();
        public void Log(string sawmillName, LogEvent message) => Messages.Add(message.RenderMessage());
    }
}
