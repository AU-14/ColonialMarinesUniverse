using System.Linq;
using Content.Shared._RMC14.Cassette;
using Robust.Shared.Audio.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._RMC14.Cassette;

[TestFixture]
[TestOf(typeof(CassettePlayerComponent))]
public sealed class CassetteAudioTest
{
    [Test]
    public async Task PlaybackFollowsCassettePlayerAndRestartLeavesOneTrack()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
        });
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var session = server.PlayerMan.Sessions.Single();
        EntityUid actor = default;
        EntityUid cassettePlayer = default;

        await server.WaitPost(() =>
        {
            actor = server.EntMan.SpawnEntity(null, testMap.GridCoords);
            server.PlayerMan.SetAttachedEntity(session, actor);
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            cassettePlayer = entMan.SpawnEntity("RMCCassettePlayer", server.Transform(actor).Coordinates);
            var tape = entMan.SpawnEntity("RMCCassetteTapeWigWoo1", server.Transform(actor).Coordinates);
            var playerComp = entMan.GetComponent<CassettePlayerComponent>(cassettePlayer);
            var containers = entMan.System<SharedContainerSystem>();
            var container = containers.EnsureContainer<ContainerSlot>(cassettePlayer, playerComp.ContainerId);
            Assert.That(containers.Insert(tape, container), Is.True);

            var restart = new CassetteRestartActionEvent { Performer = actor };
            entMan.EventBus.RaiseLocalEvent(cassettePlayer, restart);

            Assert.That(playerComp.AudioStream, Is.Not.Null);
            var audio = playerComp.AudioStream!.Value;
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<AudioComponent>(audio), Is.True);
                Assert.That(server.Transform(audio).ParentUid, Is.EqualTo(cassettePlayer));
            });
        });

        for (var i = 1; i < 10; i++)
        {
            await server.WaitPost(() =>
            {
                var restart = new CassetteRestartActionEvent { Performer = actor };
                server.EntMan.EventBus.RaiseLocalEvent(cassettePlayer, restart);
            });
            await pair.RunTicksSync(1);
        }

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var playerComp = entMan.GetComponent<CassettePlayerComponent>(cassettePlayer);
            var tracks = entMan.EntityQuery<AudioComponent>()
                .Where(audio => audio.FileName == "/Audio/_RMC14/Lobby/Super_Nova_In_The_Catacombs.ogg")
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(tracks, Has.Length.EqualTo(1));
                Assert.That(playerComp.AudioStream, Is.Not.Null);
                Assert.That(entMan.GetComponent<AudioComponent>(playerComp.AudioStream!.Value), Is.SameAs(tracks[0]));
            });
        });

        await pair.RunTicksSync(5);
        var client = pair.Client;
        var clientCassettePlayer = pair.ToClientUid(cassettePlayer);
        await client.WaitAssertion(() =>
        {
            var entMan = client.EntMan;
            var playerComp = entMan.GetComponent<CassettePlayerComponent>(clientCassettePlayer);
            var tracks = entMan.EntityQuery<AudioComponent>()
                .Where(audio => audio.FileName == "/Audio/_RMC14/Lobby/Super_Nova_In_The_Catacombs.ogg")
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(tracks, Has.Length.EqualTo(1));
                Assert.That(playerComp.AudioStream, Is.Not.Null);
                Assert.That(client.Transform(playerComp.AudioStream!.Value).ParentUid, Is.EqualTo(clientCassettePlayer));
            });
        });

        await pair.CleanReturnAsync();
    }
}
