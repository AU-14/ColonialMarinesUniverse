using System;
using System.Linq;
using Content.Server._AU14.Radio;
using Content.Server._RMC14.Language.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Station.Systems;
using Content.Shared._AU14.CCVar;
using Content.Shared._AU14.Radio;
using Content.Shared._RMC14.Language.Components;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Radio;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._AU14.Radio;

// the AN/PRC net log used to write down the frequency of any net whose prefix the
// wearer typed, whether or not they held the key and whether or not the message ever
// left them. cycling :b :c :i off a pack in the back slot read the whole per-round
// frequency plan straight off the panel
[TestFixture]
public sealed class ANPRCNetLogLeakTest
{
    private static readonly EntProtoId Pack = "ANPRC117GRadioFilled";
    private static readonly EntProtoId Headset = "AU14HeadsetGovforCommand";
    private static readonly ProtoId<JobPrototype> Rifleman = "AU14JobGOVFORSquadRifleman";

    private static readonly ProtoId<RadioChannelPrototype> HeldChannel = "radioGovforCommand";
    private static readonly ProtoId<RadioChannelPrototype> UnheldChannel = "radioOpforCommand";

    private static readonly ProtoId<LanguagePrototype> Foreign = "Russian";
    private static readonly ProtoId<LanguagePrototype> Common = "English";

    private const string BackSlot = "back";
    private const string EarsSlot = "ears";

    [Test]
    public async Task PackOnlyLogsNetsTheWearerActuallyTransmitsOn()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var config = server.ResolveDependency<IConfigurationManager>();
        var wasEnabled = config.GetCVar(AU14CCVars.NewCommsSystem);

        await server.WaitPost(() => config.SetCVar(AU14CCVars.NewCommsSystem, true));

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var spawning = server.System<StationSpawningSystem>();
            var inventory = server.System<InventorySystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var radios = server.System<ANPRCRadioSystem>();

            var wearer = spawning.SpawnPlayerMob(
                testMap.GridCoords,
                Rifleman,
                new HumanoidCharacterProfile(),
                station: null);

            var pack = entities.SpawnEntity(Pack, testMap.GridCoords);
            var headset = entities.SpawnEntity(Headset, testMap.GridCoords);

            try
            {
                // whatever the job spawned with is in the way of the two slots we need
                if (inventory.TryGetSlotEntity(wearer, BackSlot, out var oldBack))
                    entities.DeleteEntity(oldBack.Value);

                if (inventory.TryGetSlotEntity(wearer, EarsSlot, out var oldEars))
                    entities.DeleteEntity(oldEars.Value);

                Assert.That(inventory.TryEquip(wearer, pack, BackSlot, force: true), Is.True, BackSlot);
                Assert.That(inventory.TryEquip(wearer, headset, EarsSlot, force: true), Is.True, EarsSlot);

                var radio = entities.GetComponent<ANPRCRadioComponent>(pack);

                Assert.That(radio.Enabled, Is.True);
                Assert.That(radio.IsEquipped, Is.True);
                Assert.That(entities.HasComponent<WearingANPRCComponent>(wearer), Is.True);

                var held = prototypes.Index(HeldChannel);
                var unheld = prototypes.Index(UnheldChannel);

                // the exploit: a prefix for a net this headset holds no key for. the
                // channel is still on the event at this point, the message never goes
                // anywhere, and the log must not write the number down
                var leak = new EntitySpokeEvent(wearer, "probing", unheld, null);
                entities.EventBus.RaiseLocalEvent(wearer, leak);

                Assert.That(radio.NetLog, Is.Empty, "traffic the wearer cannot transmit reached the net log");

                // and if the number ever does reach the log by some other route, it is
                // still not one this govfor set is entitled to print
                Assert.That(radios.KnowsFrequency(radio, unheld), Is.False);
                Assert.That(radios.KnowsFrequency(radio, held), Is.True);

                // a net the headset really carries still logs, frequency and all
                var real = new EntitySpokeEvent(wearer, "actual traffic", held, null);
                entities.EventBus.RaiseLocalEvent(wearer, real);

                Assert.That(radio.NetLog, Has.Count.EqualTo(1));

                var entry = radio.NetLog.Single();
                Assert.That(entry.Message, Is.EqualTo("actual traffic"));
                Assert.That(entry.ChannelDisplay, Does.Contain("MHz"));
            }
            finally
            {
                entities.DeleteEntity(wearer);
                entities.DeleteEntity(pack);
                entities.DeleteEntity(headset);
            }
        });

        await server.WaitPost(() => config.SetCVar(AU14CCVars.NewCommsSystem, wasEnabled));
        await pair.CleanReturnAsync();
    }
    // the panel showed the log in clear while the same traffic reached chat obfuscated:
    // a planted station read back Russian nobody at it spoke. the log stores what came
    // over the air and the language pass runs when it is read, so this covers both ends
    [Test]
    public async Task PlantedStationLogRendersForWhoeverReadsIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var config = server.ResolveDependency<IConfigurationManager>();
        var wasEnabled = config.GetCVar(AU14CCVars.NewCommsSystem);

        await server.WaitPost(() => config.SetCVar(AU14CCVars.NewCommsSystem, true));

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var spawning = server.System<StationSpawningSystem>();
            var radios = server.System<ANPRCRadioSystem>();
            var languages = server.System<LanguageSystem>();

            var reader = spawning.SpawnPlayerMob(
                testMap.GridCoords,
                Rifleman,
                new HumanoidCharacterProfile(),
                station: null);

            var station = entities.SpawnEntity(Pack, testMap.GridCoords);

            try
            {
                var radio = entities.GetComponent<ANPRCRadioComponent>(station);
                radio.Planted = true;

                // whatever the profile happened to grant, this one speaks the common
                // tongue and nothing else, and is not part way through picking any up
                languages.SetExclusiveLanguage(reader, Common);
                entities.RemoveComponent<LanguageLearningComponent>(reader);

                Assert.That(languages.CanUnderstand(reader, Foreign), Is.False);

                const string spoken = "Убей его";

                var traffic = new ANPRCDirectTrafficReceivedEvent(reader, "GIBBS", 1469, spoken, Foreign);
                entities.EventBus.RaiseLocalEvent(station, ref traffic);

                // the set keeps the sounds it caught, whoever ends up reading them
                Assert.That(radio.NetLog, Has.Count.EqualTo(1));
                Assert.That(radio.NetLog.Single().Message, Is.EqualTo(spoken));
                Assert.That(radio.NetLog.Single().Language, Is.EqualTo(Foreign.Id));

                // read by somebody without the language it comes out as syllables, the
                // same way the chat line already did
                var forReader = radios.BuildNetLog((station, radio), new[] { reader });
                Assert.That(forReader.Single().Message, Is.Not.EqualTo(spoken));

                // and with nobody identified at the panel it does not fall open
                var forNobody = radios.BuildNetLog((station, radio), Array.Empty<EntityUid>());
                Assert.That(forNobody.Single().Message, Is.Not.EqualTo(spoken));

                // a language the reader does have still reads straight
                var plain = new ANPRCDirectTrafficReceivedEvent(reader, "GIBBS", 1469, "kill him", Common);
                entities.EventBus.RaiseLocalEvent(station, ref plain);

                var rendered = radios.BuildNetLog((station, radio), new[] { reader });
                Assert.That(rendered.Last().Message, Is.EqualTo("kill him"));
            }
            finally
            {
                entities.DeleteEntity(reader);
                entities.DeleteEntity(station);
            }
        });

        await server.WaitPost(() => config.SetCVar(AU14CCVars.NewCommsSystem, wasEnabled));
        await pair.CleanReturnAsync();
    }
}
