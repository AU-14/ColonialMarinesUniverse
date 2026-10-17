using System.Linq;
using Content.Client.CMU14.Diagnostics.Performance;
using Content.Shared.CMU14.ZLevels;
using Robust.Client;
using Robust.Shared;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Robust.UnitTesting;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class ClientPerformanceCaptureTest
{
    [Test]
    public async Task AudioSnapshotsTrackDuplicateLoopsAcrossMapsAndTheirRemoval()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var firstMap = maps.CreateMap(runMapInit: true);
            var secondMap = maps.CreateMap(runMapInit: true);
            var audio = entities.System<SharedAudioSystem>();
            var capture = entities.System<CMUClientPerformanceSystem>();
            var resources = pair.Client.ResolveDependency<IResourceManager>();
            var directory = new ResPath("/client-performance");
            resources.UserData.CreateDir(directory);

            string Snapshot()
            {
                var previousFiles = resources.UserData.DirectoryEntries(directory).ToArray();
                capture.StartCapture(5, 20);
                capture.StopCapture();
                var file = resources.UserData.DirectoryEntries(directory).Except(previousFiles).Single();
                using var reader = resources.UserData.OpenText(directory / file);
                return reader.ReadToEnd();
            }

            try
            {
                const string sound = "/Audio/CMU14/Fighter/jet-exterior.ogg";
                var emitter = entities.SpawnEntity(null, new EntityCoordinates(firstMap, Vector2.Zero));
                var otherEmitter = entities.SpawnEntity(null, new EntityCoordinates(secondMap, Vector2.Zero));
                var parameters = AudioParams.Default.WithLoop(true);
                var first = audio.PlayEntity(new SoundPathSpecifier(sound), Filter.Local(), emitter, false, parameters)!.Value;
                var second = audio.PlayEntity(new SoundPathSpecifier(sound), Filter.Local(), otherEmitter, false, parameters)!.Value;
                var text = Snapshot();
                Assert.That(text, Does.Contain("audio-inventory: entities=2 loaded=2 loops=2 playingState=2"));
                Assert.That(text, Does.Contain("otherMap=2"),
                    "Sounds outside the listener's map must still be counted as loaded work.");
                Assert.That(text, Does.Contain($"{sound} count=2"));

                entities.DeleteEntity(first.Entity);
                text = Snapshot();
                Assert.That(text, Does.Contain("audio-inventory: entities=1 loaded=1 loops=1 playingState=1"));
                Assert.That(text, Does.Contain($"{sound} count=1"),
                    "Each report must reflect live sources rather than accumulating prior snapshots.");
                entities.DeleteEntity(second.Entity);
            }
            finally
            {
                if (capture.Capturing)
                    capture.StopCapture();
                entities.DeleteEntity(firstMap);
                entities.DeleteEntity(secondMap);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClientAssemblyPassesSandboxWithoutLoadingPrototypes()
    {
        using var client = new RobustIntegrationTest.ClientIntegrationInstance(new RobustIntegrationTest.ClientIntegrationOptions
        {
            ContentStart = false,
            ContentAssemblies = [],
            Options = new GameControllerOptions
            {
                LoadConfigAndUserData = false,
                LoadContentResources = false,
                PrototypeDirectory = new ResPath("/ClientPerformanceSandboxPrototypes"),
                MountOptions = new MountOptions(dirMounts: ["../../RobustToolbox/Resources"], zipMounts: []),
            },
        });
        await client.WaitIdleAsync();
        await client.CheckSandboxed(typeof(CMUClientPerformanceSystem).Assembly);
    }

    [Test]
    public async Task CapturesFlushAndRestoreOnlySettingsOwnedByTheCommand()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        await pair.Client.WaitAssertion(() =>
        {
            var config = pair.Client.ResolveDependency<IConfigurationManager>();
            var resources = pair.Client.ResolveDependency<IResourceManager>();
            var system = pair.Client.EntMan.System<CMUClientPerformanceSystem>();
            var profiler = config.GetCVar(CVars.ProfEnabled);
            var bufferSize = config.GetCVar(CVars.ProfBufferSize);
            var zDiagnostics = config.GetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled);
            try
            {
                foreach (var alreadyEnabled in new[] { false, true })
                {
                    config.SetCVar(CVars.ProfEnabled, alreadyEnabled);
                    var previousBufferSize = alreadyEnabled ? 524288 : 8192;
                    config.SetCVar(CVars.ProfBufferSize, previousBufferSize);
                    config.SetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled, alreadyEnabled);
                    system.StartCapture(5, 20);
                    Assert.That(system.Capturing, Is.True);
                    Assert.That(config.GetCVar(CVars.ProfEnabled), Is.True);
                    Assert.That(config.GetCVar(CVars.ProfBufferSize), Is.GreaterThanOrEqualTo(262144),
                        "Keep a busy completed frame while the next frame is being written");
                    Assert.That(config.GetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled), Is.True);
                    Assert.That(system.StartCapture(5, 20), Does.Contain("already running"));
                    system.ManualReport();
                    Assert.That(system.Capturing, Is.True);
                    system.StopCapture();
                    Assert.That(system.Capturing, Is.False);
                    Assert.That(config.GetCVar(CVars.ProfEnabled), Is.EqualTo(alreadyEnabled));
                    Assert.That(config.GetCVar(CVars.ProfBufferSize), Is.EqualTo(previousBufferSize));
                    Assert.That(config.GetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled), Is.EqualTo(alreadyEnabled));
                }

                config.SetCVar(CVars.ProfEnabled, false);
                config.SetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled, false);
                system.StartCapture(5, 20);
                // A manual toggle after startup transfers ownership back to the user.
                config.SetCVar(CVars.ProfBufferSize, 131072);
                config.SetCVar(CVars.ProfEnabled, false);
                config.SetCVar(CVars.ProfEnabled, true);
                config.SetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled, false);
                config.SetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled, true);
                system.StopCapture();
                Assert.That(config.GetCVar(CVars.ProfEnabled), Is.True);
                Assert.That(config.GetCVar(CVars.ProfBufferSize), Is.EqualTo(131072));
                Assert.That(config.GetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled), Is.True);

                var directory = new ResPath("/client-performance");
                var captures = resources.UserData.DirectoryEntries(directory)
                    .Where(name => name.StartsWith("client-perf-"))
                    .Select(name => directory / name).ToArray();
                Assert.That(captures.Length, Is.GreaterThanOrEqualTo(3));
                foreach (var file in captures)
                {
                    using var reader = resources.UserData.OpenText(file);
                    var text = reader.ReadToEnd();
                    Assert.That(text, Does.Contain("CMU CLIENT PERFORMANCE CAPTURE v1"));
                    Assert.That(text, Does.Contain("inventory:"));
                    Assert.That(text, Does.Contain("capture-end reason=manual"));
                    Assert.That(text, Does.Contain("reportDiagnosticMs="));
                }
            }
            finally
            {
                if (system.Capturing)
                    system.StopCapture();
                config.SetCVar(CVars.ProfEnabled, profiler);
                config.SetCVar(CVars.ProfBufferSize, bufferSize);
                config.SetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled, zDiagnostics);
            }
        });
        await pair.CleanReturnAsync();
    }
}
