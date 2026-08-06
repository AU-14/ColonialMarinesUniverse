using System.Linq;
using System.Reflection;
using Content.Client._RMC14.Medical.Scanner;
using Content.IntegrationTests.Pair;
using Content.Server.Mind;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared.FixedPoint;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Medical.Diagnostics;

[TestFixture]
public sealed class HealthScannerBuiLifecycleTest
{
    [Test]
    public async Task StateAfterWindowDisposalIsIgnored()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Fresh = true,
        });

        try
        {
            var server = pair.Server;
            var client = pair.Client;
            var map = await pair.CreateTestMap();
            var session = server.PlayerMan.Sessions.Single();
            EntityUid patient = default;
            EntityUid viewer = default;
            NetEntity patientNet = default;

            await server.WaitAssertion(() =>
            {
                patient = server.EntMan.SpawnEntity("MobMouse", map.GridCoords);
                viewer = server.EntMan.SpawnEntity("MobMouse", map.GridCoords);
                patientNet = server.EntMan.GetNetEntity(patient);

                var mind = server.EntMan.System<MindSystem>();
                var mindId = mind.CreateMind(session.UserId, "Health scanner viewer");
                mind.TransferTo(mindId, viewer);
                mind.SetUserId(mindId, session.UserId);
            });

            await pair.RunTicksSync(5);

            await client.WaitAssertion(() =>
            {
                var patientClient = client.EntMan.GetEntity(patientNet);
                var bui = new HealthScannerBui(patientClient, HealthScannerUIKey.Key);
                var open = typeof(HealthScannerBui).GetMethod("Open", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var update = typeof(HealthScannerBui).GetMethod(
                    "UpdateState",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    [typeof(BoundUserInterfaceState)],
                    null)!;
                var state = new HealthScannerBuiState(new HealthScanState(
                    patientNet,
                    FixedPoint2.New(50),
                    FixedPoint2.New(100),
                    null,
                    string.Empty,
                    null,
                    false,
                    HealthScanDetailLevel.HealthAnalyzer));

                open.Invoke(bui, null);
                update.Invoke(bui, [state]);
                bui.Dispose();

                Assert.DoesNotThrow(() => update.Invoke(bui, [state]));

                var savePosition = typeof(UserInterfaceSystem).GetMethod(
                    "SavePosition",
                    BindingFlags.Instance | BindingFlags.NonPublic)!;
                savePosition.Invoke(client.EntMan.System<UserInterfaceSystem>(), [bui]);
            });

            await server.WaitPost(() =>
            {
                server.EntMan.DeleteEntity(patient);
                server.EntMan.DeleteEntity(viewer);
            });
            await pair.RunUntilSynced();
        }
        finally
        {
            await pair.CleanReturnAsync();
        }
    }
}
