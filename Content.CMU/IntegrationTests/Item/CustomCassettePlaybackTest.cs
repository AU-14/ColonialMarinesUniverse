using Content.Client._RMC14.Cassette;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Cassette;
using Robust.Client.Audio;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Item;

[TestFixture]
public sealed class CustomCassettePlaybackTest : GameTest
{
    [Test]
    public async Task ReplayedRestartKeepsCustomTrackControllableAndEjectionStopsIt()
    {
        var map = await Pair.CreateTestMap();
        EntityUid player = default, tape = default, actor = default;
        await Server.WaitPost(() =>
        {
            actor = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(Pair.Player!, actor);
            player = SEntMan.SpawnEntity("RMCCassettePlayer", map.GridCoords);
            tape = SEntMan.SpawnEntity("RMCCassetteTapeCustom", map.GridCoords);
            var container = Server.System<SharedContainerSystem>().EnsureContainer<ContainerSlot>(player,
                SEntMan.GetComponent<CassettePlayerComponent>(player).ContainerId);
            Server.System<SharedContainerSystem>().Insert(tape, container);
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var clientPlayer = Pair.ToClientUid(player);
            var clientTape = Pair.ToClientUid(tape);
            var component = CEntMan.GetComponent<CassettePlayerComponent>(clientPlayer);
            const string path = "/Audio/_RMC14/Machines/click.ogg";
            var resource = Client.ResolveDependency<IResourceCache>().GetResource<AudioResource>(path);
            // Start a real local stream, as file selection does, without opening a native file dialog.
            var stream = Client.System<AudioSystem>().PlayEntity(resource.AudioStream, clientPlayer,
                new ResolvedPathSpecifier(path), AudioParams.Default.WithLoop(true))!.Value.Entity;
#pragma warning disable RA0002 // Seed the client-owned playback produced by a native file selection.
            component.CustomAudioStream = stream;
            component.State = AudioState.Playing;
#pragma warning restore RA0002
            var performer = Pair.ToClientUid(actor);

            CGameTiming.StartPastPrediction();
            try
            {
                CEntMan.EventBus.RaiseLocalEvent(clientPlayer, new CassetteRestartActionEvent { Performer = performer });
            }
            finally
            {
                CGameTiming.EndPastPrediction();
            }
            Assert.That(component.CustomAudioStream, Is.EqualTo(stream),
                "Replay must retain the handle to the still-playing local track.");
            Assert.That(CEntMan.IsQueuedForDeletion(stream), Is.False);

            CEntMan.EventBus.RaiseLocalEvent(clientPlayer, new CassettePlayPauseActionEvent { Performer = performer });
            Assert.That(CEntMan.GetComponent<AudioComponent>(stream).State, Is.EqualTo(AudioState.Paused));
            CEntMan.EventBus.RaiseLocalEvent(clientPlayer, new CassettePlayPauseActionEvent { Performer = performer });
            Assert.That(CEntMan.GetComponent<AudioComponent>(stream).State, Is.EqualTo(AudioState.Playing));

            // Server-only ejection arrives during state application, when AudioSystem.Stop is suppressed.
            CGameTiming.StartStateApplication();
            try
            {
                var containers = Client.System<SharedContainerSystem>();
                Assert.That(containers.Remove(clientTape, containers.GetContainer(clientPlayer, component.ContainerId)), Is.True);
            }
            finally
            {
                CGameTiming.EndStateApplication();
            }
            Client.System<CassetteSystem>().FrameUpdate(0);
            Assert.That(component.CustomAudioStream, Is.Null);
            Assert.That(CEntMan.IsQueuedForDeletion(stream), Is.True,
                "Ejection must stop the real sound, not just lose its handle.");
        });
    }
}
