using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Cassette;
using Robust.Client;
using Robust.Client.GameStates;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.CMU14.Memory;

public sealed class CMUClientMemoryTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Fresh = true,
        Dirty = true,
    };

    [Test]
    public async Task DisconnectReleasesServerStateAndReconnects()
    {
        var states = Client.ResolveDependency<IClientGameStateManager>();
        var client = Client.ResolveDependency<IBaseClient>();
        var map = await Pair.CreateTestMap();

        for (var connection = 0; connection < 2; connection++)
        {
            if (connection != 0)
                await Pair.Connect();

            EntityUid actor = default;
            NetEntity networkActor = default;
            var name = $"Connection {connection}";
            await Server.WaitPost(() =>
            {
                actor = SEntMan.SpawnEntity(null, map.GridCoords);
                SEntMan.System<MetaDataSystem>().SetEntityName(actor, name);
                Server.PlayerMan.SetAttachedEntity(ServerSession!, actor);
                networkActor = SEntMan.GetNetEntity(actor);
            });
            await Pair.RunUntilSynced();

            await Client.WaitAssertion(() =>
            {
                Assert.That(states.GetFullRep().ContainsKey(networkActor), Is.True);
                Assert.That(CEntMan.GetComponent<MetaDataComponent>(CEntMan.GetEntity(networkActor)).EntityName,
                    Is.EqualTo(name), "A reconnect must apply fresh server state normally.");
            });

            await Pair.Disconnect("Testing disconnected memory cleanup.");
            await Client.WaitAssertion(() =>
            {
                Assert.That(client.RunLevel, Is.EqualTo(ClientRunLevel.Initialize));
                Assert.That(CEntMan.EntityCount, Is.Zero);
                Assert.That(states.GetFullRep(), Is.Empty, "Deleted server entities must not remain in the prediction cache.");
            });
            await Server.WaitPost(() => SEntMan.DeleteEntity(actor));
        }
    }
}
