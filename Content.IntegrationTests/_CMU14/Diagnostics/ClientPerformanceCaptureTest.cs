using System.Linq;
using Content.Client.CMU14.Diagnostics.Performance;
using Content.Shared.CMU14.ZLevels;
using Robust.Client;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;
using Robust.UnitTesting;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class ClientPerformanceCaptureTest
{
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
