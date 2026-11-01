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
    public async Task CassetteTracksLoadOnlyWhenPlayed()
    {
        const string firstSong = "/Audio/CMU14/Private/Music/Cassette/RisingSun/CityIsDeath.ogg";
        const string secondSong = "/Audio/CMU14/Private/Music/Cassette/RisingSun/Cult.ogg";
        var resources = Client.ResolveDependency<IResourceCache>();
        var map = await Pair.CreateTestMap();
        EntityUid actor = default;
        EntityUid player = default;

        await Server.WaitPost(() =>
        {
            actor = SEntMan.SpawnEntity(null, map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, actor);
            player = SEntMan.SpawnEntity("RMCCassettePlayer", map.GridCoords);
            var tape = SEntMan.SpawnEntity("AU14CassetteTapeRisingSun", map.GridCoords);
            var playerComp = SEntMan.GetComponent<CassettePlayerComponent>(player);
            // Public audio assets are short placeholders; keep playback alive while networking it.
#pragma warning disable RA0002
            playerComp.AudioParams = playerComp.AudioParams.WithLoop(true);
#pragma warning restore RA0002
            var containers = SEntMan.System<SharedContainerSystem>();
            var slot = containers.EnsureContainer<ContainerSlot>(player, playerComp.ContainerId);
            Assert.That(containers.Insert(tape, slot), Is.True);
        });
        await Pair.RunTicksSync(5);

        await Client.WaitAssertion(() =>
        {
            Assert.That(resources.GetAllResources<AudioResource>()
                .Where(resource => resource.Key.ToString().StartsWith("/Audio/CMU14/Private/Music/Cassette/")),
                Is.Empty, "Starting a client and spawning a tape must not decode the cassette library.");
        });

        await Server.WaitPost(() =>
        {
            var restart = new CassetteRestartActionEvent { Performer = actor };
            SEntMan.EventBus.RaiseLocalEvent(player, restart);
        });
        await Pair.RunTicksSync(5);

        await Client.WaitAssertion(() =>
        {
            var loaded = resources.GetAllResources<AudioResource>().Select(resource => resource.Key.ToString()).ToArray();
            Assert.That(loaded, Does.Contain(firstSong));
            Assert.That(loaded, Does.Not.Contain(secondSong));
            Assert.That(CEntMan.EntityQuery<AudioComponent>().Single(audio => audio.FileName == firstSong).Loaded,
                Is.True, "The first song must play without a preload.");
        });

        await Server.WaitPost(() =>
        {
            var next = new CassetteNextActionEvent { Performer = actor };
            SEntMan.EventBus.RaiseLocalEvent(player, next);
        });
        await Pair.RunTicksSync(5);

        await Client.WaitAssertion(() =>
        {
            Assert.That(resources.GetAllResources<AudioResource>().Select(resource => resource.Key.ToString()),
                Does.Contain(secondSong));
            var tracks = CEntMan.EntityQuery<AudioComponent>()
                .Where(audio => audio.FileName == firstSong || audio.FileName == secondSong).ToArray();
            Assert.That(tracks, Has.Length.EqualTo(1));
            Assert.That(tracks[0].FileName, Is.EqualTo(secondSong));
            Assert.That(tracks[0].Loaded, Is.True);
        });
    }

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
