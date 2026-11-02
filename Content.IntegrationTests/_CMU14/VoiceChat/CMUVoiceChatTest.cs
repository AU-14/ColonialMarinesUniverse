using System.Collections.Generic;
using System.Numerics;
using CMUVoiceRadioFilter = Content.Client.CMU14.VoiceChat.CMUVoiceRadioFilter;
using Content.Server._RMC14.Admin;
using Content.Server.CMU14.VoiceChat;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.CMU14.VoiceChat;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Database;
using Content.Shared.Inventory;
using Content.Shared.Radio.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.VoiceChat;

[TestFixture]
public sealed class CMUVoiceChatTest
{
    [Test]
    public async Task OnlyHumansTakePartAndGhostsOnlyListen()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var voice = server.System<CMUVoiceChatSystem>();
            var coordinates = testMap.GridCoords;

            var human = entities.SpawnEntity("CMMobHuman", coordinates);
            var xeno = entities.SpawnEntity("CMXenoDrone", coordinates);
            var ghost = entities.SpawnEntity("MobObserver", coordinates);
            var yautja = entities.SpawnEntity("CMMobHuman", coordinates);
            entities.EnsureComponent<YautjaComponent>(yautja);

            Assert.Multiple(() =>
            {
                Assert.That(voice.IsVoiceCapable(human), Is.True);
                Assert.That(voice.CanHear(human), Is.True);

                Assert.That(voice.IsVoiceCapable(xeno), Is.False);
                Assert.That(voice.CanHear(xeno), Is.False);

                Assert.That(voice.IsVoiceCapable(yautja), Is.False);
                Assert.That(voice.CanHear(yautja), Is.False);

                Assert.That(voice.IsVoiceCapable(ghost), Is.False, "Ghosts must never transmit.");
                Assert.That(voice.CanHear(ghost), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task VoiceReachesAdjacentZLevelOnlyThroughOpening()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = server.System<SharedMapSystem>();
            var tileDefinitions = server.ResolveDependency<ITileDefinitionManager>();
            var zLevels = server.System<CMUZLevelsSystem>();
            var voice = server.System<CMUVoiceChatSystem>();
            var plating = new Tile(tileDefinitions["Plating"].TileId);

            // Z-level maps are their own grids, like CMU's multi-z maps.
            var lower = maps.CreateMap(runMapInit: true);
            var upper = maps.CreateMap(runMapInit: true);
            var lowerGrid = entities.EnsureComponent<MapGridComponent>(lower);
            var upperGrid = entities.EnsureComponent<MapGridComponent>(upper);

            for (var x = -12; x <= 12; x++)
            {
                for (var y = -4; y <= 4; y++)
                {
                    maps.SetTile(upper, upperGrid, new Vector2i(x, y), plating);
                    maps.SetTile(lower, lowerGrid, new Vector2i(x, y), plating);
                }
            }

            var network = zLevels.CreateZNetwork();
            Assert.That(
                zLevels.TryAddMapsIntoZNetwork(
                    network,
                    new Dictionary<EntityUid, int>
                    {
                        [lower] = 0,
                        [upper] = 1,
                    }),
                Is.True);

            var speaker = entities.SpawnEntity("CMMobHuman", new EntityCoordinates(upper, new Vector2(0.5f, 0.5f)));
            var below = entities.SpawnEntity("CMMobHuman", new EntityCoordinates(lower, new Vector2(3.5f, 0.5f)));

            Assert.That(voice.TryGetHearingDistance(speaker, below, out _), Is.False,
                "A solid floor must block voice between z-levels.");

            // Cut an opening one tile from the speaker; the route is speaker -> opening -> listener.
            maps.SetTile(upper, upperGrid, new Vector2i(1, 0), Tile.Empty);

            Assert.That(voice.TryGetHearingDistance(speaker, below, out var distance), Is.True);
            Assert.That(distance, Is.EqualTo(3f).Within(0.001f));
            Assert.That(voice.TryGetHearingDistance(below, speaker, out var reverse), Is.True,
                "Voice from below must also rise through the opening.");
            Assert.That(reverse, Is.EqualTo(3f).Within(0.001f));

            var far = entities.SpawnEntity("CMMobHuman", new EntityCoordinates(lower, new Vector2(-11.5f, 0.5f)));
            Assert.That(voice.TryGetHearingDistance(speaker, far, out _), Is.False,
                "Listeners beyond voice range on the other level must not hear.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task VoiceChatBanMutesThePlayer()
    {
        // A real ticker gives the ban a round to reference, as on a live server.
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
            InLobby = true,
            Dirty = true,
        });
        var server = pair.Server;
        var user = pair.Player!.UserId;
        var voice = server.System<CMUVoiceChatSystem>();
        var chatBans = server.ResolveDependency<RMCChatBansManager>();

        await server.WaitAssertion(() => Assert.That(voice.IsMuted(user), Is.False));
        await server.WaitPost(() => chatBans.AddChatBan(user, null, ChatType.Voice, user, "voice chat ban test"));

        // Bans are written to the database asynchronously.
        var muted = false;
        for (var i = 0; i < 50 && !muted; i++)
        {
            await pair.RunTicksSync(5);
            await server.WaitPost(() => muted = voice.IsMuted(user));
        }

        Assert.That(muted, Is.True, "A Voice chat ban must mute the player in voice chat.");
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RadioUsesTheWornHeadsetsDefaultChannel()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var voice = server.System<CMUVoiceChatSystem>();
            var inventory = server.System<InventorySystem>();

            var human = entities.SpawnEntity("CMMobHuman", testMap.GridCoords);
            Assert.That(voice.TryGetVoiceRadioChannel(human, out _, out _), Is.False,
                "Without a headset there is nothing to transmit on.");

            var headset = entities.SpawnEntity("RMCHeadsetMarineCommand", testMap.GridCoords);
            Assert.That(inventory.TryEquip(human, headset, "ears", silent: true, force: true), Is.True);

            Assert.That(voice.TryGetVoiceRadioChannel(human, out var radioSource, out var channel), Is.True);
            Assert.That(radioSource, Is.EqualTo(headset));
            Assert.That(entities.GetComponent<EncryptionKeyHolderComponent>(headset).Channels, Does.Contain(channel.ID));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public void RadioFilterKeepsVoiceBandAndCutsTheRest()
    {
        static float Rms(float frequency)
        {
            var filter = new CMUVoiceRadioFilter();
            var pcm = new short[16000];
            for (var i = 0; i < pcm.Length; i++)
            {
                pcm[i] = (short) (MathF.Sin(MathF.Tau * frequency * i / 16000f) * 6000f);
            }

            filter.Process(pcm);

            // Skip the filter settling time.
            double sum = 0;
            for (var i = 4000; i < pcm.Length; i++)
            {
                sum += pcm[i] * (double) pcm[i];
            }

            return (float) Math.Sqrt(sum / (pcm.Length - 4000));
        }

        var voiceBand = Rms(1000f);
        Assert.Multiple(() =>
        {
            Assert.That(Rms(80f), Is.LessThan(voiceBand * 0.25f), "Bass must be cut.");
            Assert.That(Rms(6000f), Is.LessThan(voiceBand * 0.25f), "Treble must be cut.");
            Assert.That(CMUVoiceRadioFilter.CreateOpenClick(0.6f), Is.Not.Empty);
            Assert.That(CMUVoiceRadioFilter.CreateCloseClick(0.6f).Length,
                Is.GreaterThan(CMUVoiceRadioFilter.CreateOpenClick(0.6f).Length));
        });
    }

    [Test]
    public async Task GasMasksMuffleLightlyAndContainersHeavily()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var voice = server.System<CMUVoiceChatSystem>();
            var inventory = server.System<InventorySystem>();
            var containers = server.System<SharedContainerSystem>();

            var human = entities.SpawnEntity("CMMobHuman", testMap.GridCoords);
            Assert.That(voice.GetMuffle(human), Is.EqualTo(CMUVoiceMuffle.None));

            var mask = entities.SpawnEntity("CMMaskGas", testMap.GridCoords);
            Assert.That(inventory.TryEquip(human, mask, "mask", silent: true, force: true), Is.True);
            Assert.That(voice.GetMuffle(human), Is.EqualTo(CMUVoiceMuffle.Light), "A gas mask covers the mouth.");

            Assert.That(inventory.TryUnequip(human, "mask", silent: true, force: true), Is.True);
            Assert.That(voice.GetMuffle(human), Is.EqualTo(CMUVoiceMuffle.None));

            var holder = entities.SpawnEntity(null, testMap.GridCoords);
            var container = containers.EnsureContainer<Container>(holder, "voice-test");
            Assert.That(containers.Insert(human, container), Is.True);
            Assert.That(voice.GetMuffle(human), Is.EqualTo(CMUVoiceMuffle.Heavy), "Voices from inside something are heavily muffled.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RadioChannelCyclesThroughHeadsetChannelsWithoutCommon()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var cfg = server.ResolveDependency<IConfigurationManager>();

        await server.WaitAssertion(() =>
        {
            // Common is on by default; this checks the cycle skips it when a server turns it off.
            cfg.SetCVar(CCVars.CMUVoiceChatRadioCommon, false);

            var entities = server.EntMan;
            var voice = server.System<CMUVoiceChatSystem>();
            var inventory = server.System<InventorySystem>();

            var human = entities.SpawnEntity("CMMobHuman", testMap.GridCoords);
            var headset = entities.SpawnEntity("RMCHeadsetMarineCommand", testMap.GridCoords);
            Assert.That(inventory.TryEquip(human, headset, "ears", silent: true, force: true), Is.True);
            var keys = entities.GetComponent<EncryptionKeyHolderComponent>(headset);

            var seen = new HashSet<string>();
            for (var i = 0; i < keys.Channels.Count + 1; i++)
            {
                Assert.That(voice.TryCycleVoiceRadioChannel(human, out var channel), Is.True);
                Assert.That(keys.Channels, Does.Contain(new Robust.Shared.Prototypes.ProtoId<Content.Shared.Radio.RadioChannelPrototype>(channel!.ID)));
                Assert.That(channel.ID, Is.Not.EqualTo(SharedChatSystem.CommonChannel.Id));
                Assert.That(voice.TryGetVoiceRadioChannel(human, out _, out var active), Is.True);
                Assert.That(active.ID, Is.EqualTo(channel.ID), "Transmissions use the channel picked last.");
                seen.Add(channel.ID);
            }

            Assert.That(seen, Is.Not.Empty);
        });

        await server.WaitPost(() => cfg.SetCVar(CCVars.CMUVoiceChatRadioCommon, true));
        await pair.CleanReturnAsync();
    }

    [Test]
    public void LoudnessTrackerDetectsShoutsAndWhispers()
    {
        var tracker = new CMUVoiceLoudnessTracker();

        // Settle the speaker's normal level around -30 dB.
        for (var i = 0; i < 400; i++)
        {
            tracker.Update(-30f, 14f, 5f);
        }

        Assert.That(tracker.Mode, Is.EqualTo(CMUVoiceLoudness.Normal));

        for (var i = 0; i < 40; i++)
        {
            tracker.Update(-8f, 14f, 5f);
        }

        Assert.That(tracker.Mode, Is.EqualTo(CMUVoiceLoudness.Shout));

        tracker.Reset();
        for (var i = 0; i < 400; i++)
        {
            tracker.Update(-30f, 14f, 5f);
        }

        for (var i = 0; i < 80; i++)
        {
            tracker.Update(-42f, 14f, 5f);
        }

        Assert.That(tracker.Mode, Is.EqualTo(CMUVoiceLoudness.Whisper));
    }

    [Test]
    public void VoiceEffectsAndMufflingChangeTheSignal()
    {
        foreach (var settings in new[]
                 {
                     new CMUVoiceEffectSettings(CMUVoiceEffect.Deep, CMUVoiceMuffle.None),
                     new CMUVoiceEffectSettings(CMUVoiceEffect.None, CMUVoiceMuffle.Heavy),
                 })
        {
            var processor = new CMUVoiceEffectProcessor();
            var changed = false;
            for (var frame = 0; frame < 20; frame++)
            {
                var pcm = new short[CMUVoiceCodec.FrameSamples];
                for (var i = 0; i < pcm.Length; i++)
                {
                    var t = frame * pcm.Length + i;
                    pcm[i] = (short) (MathF.Sin(MathF.Tau * 1500f * t / CMUVoiceCodec.SampleRate) * 8000f);
                }

                var original = (short[]) pcm.Clone();
                processor.Process(pcm, 0, pcm.Length, settings);
                changed |= !pcm.AsSpan().SequenceEqual(original);
            }

            Assert.That(changed, Is.True, $"{settings} must alter the audio.");
        }
    }

    [Test]
    public async Task HeadsetWindowSelectsOnlyChannelsTheHeadsetHas()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var voice = server.System<CMUVoiceChatSystem>();
            var inventory = server.System<InventorySystem>();

            var human = entities.SpawnEntity("CMMobHuman", testMap.GridCoords);
            var headset = entities.SpawnEntity("RMCHeadsetMarineCommand", testMap.GridCoords);
            Assert.That(inventory.TryEquip(human, headset, "ears", silent: true, force: true), Is.True);

            var keys = entities.GetComponent<EncryptionKeyHolderComponent>(headset);
            Robust.Shared.Prototypes.ProtoId<Content.Shared.Radio.RadioChannelPrototype>? usable = null;
            foreach (var channel in keys.Channels)
            {
                if (!keys.ReadOnlyChannels.Contains(channel))
                {
                    usable = channel;
                    break;
                }
            }

            Assert.That(usable, Is.Not.Null);
            Assert.That(voice.TrySelectVoiceRadioChannel(human, usable!.Value), Is.True);
            Assert.That(entities.GetComponent<CMUVoiceRadioComponent>(human).Channel, Is.EqualTo(usable));
            Assert.That(voice.TryGetVoiceRadioChannel(human, out _, out var active), Is.True);
            Assert.That(active.ID, Is.EqualTo(usable.Value.Id));

            Assert.That(voice.TrySelectVoiceRadioChannel(human, "Hivemind"), Is.False,
                "A channel the headset doesn't carry can't be picked.");
        });

        await pair.CleanReturnAsync();
    }
}
