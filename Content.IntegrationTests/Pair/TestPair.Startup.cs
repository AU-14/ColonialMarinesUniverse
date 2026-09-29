#nullable enable
using Robust.Shared;
using Robust.Shared.Log;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Pair;

public sealed partial class TestPair
{
    protected override Task<RobustIntegrationTest.ServerIntegrationInstance> GenerateServer()
    {
        return PrototypeStartup.Start(
            () => new RobustIntegrationTest.ServerIntegrationInstance(ServerOptions()),
            async server =>
            {
                await server.WaitIdleAsync();
                server.Resolve<ILogManager>().GetSawmill("loc").Level = LogLevel.Error;
                server.CfgMan.OnValueChanged(RTCVars.FailureLogLevel, value => ServerLogHandler.FailureLevel = value, true);
            },
            ServerLogHandler.ActiveContext!);
    }

    protected override Task<RobustIntegrationTest.ClientIntegrationInstance> GenerateClient()
    {
        return PrototypeStartup.Start(
            () => new RobustIntegrationTest.ClientIntegrationInstance(ClientOptions()),
            async client =>
            {
                await client.WaitIdleAsync();
                client.Resolve<ILogManager>().GetSawmill("loc").Level = LogLevel.Error;
                client.CfgMan.OnValueChanged(RTCVars.FailureLogLevel, value => ClientLogHandler.FailureLevel = value, true);
                await client.WaitIdleAsync();
            },
            ClientLogHandler.ActiveContext!);
    }
}
