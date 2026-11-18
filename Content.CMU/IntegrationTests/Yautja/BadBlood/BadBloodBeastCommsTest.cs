#pragma warning disable RA0002 // reads tactical map user flags and radio channels for the test
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Language.Systems;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.TacticalMap;
using Content.Shared.Radio.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// CMU14 (Playtest 5 follow-up): every clan beast, the kennel hound and each thrall, is on the clan's map and voice.
// It shows on the Yautja hunting map only (never a hive's), opens that map rather than a hive's, talks and listens on
// the Bad Blood channel, and the clan, its hounds and its thralls understand one another.
[TestFixture]
public sealed class BadBloodBeastCommsTest : GameTest
{
    private const string BadBloodChannel = "CMUYautjaBadBlood";

    private static readonly string[] Beasts =
    {
        "CMUMobYautjaBadBloodHound",
        "CMUBadBloodThrallRunner",
        "CMUBadBloodThrallDefender",
        "CMUBadBloodThrallSentinel",
        "CMUBadBloodThrallLurker",
        "CMUBadBloodThrallWarrior",
        "CMUBadBloodThrallSpitter",
    };

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task ClanBeastsShareTheClansMapAndVoice()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitPost(() =>
            {
                foreach (var proto in Beasts)
                {
                    spawned.Add(Server.EntMan.SpawnEntity(proto, map.GridCoords));
                }
            });

            await Pair.RunSeconds(1);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var language = entMan.System<SharedLanguageSystem>();
                var grunt = party.Grunts[0];

                Assert.Multiple(() =>
                {
                    foreach (var beast in spawned)
                    {
                        var id = entMan.GetComponent<MetaDataComponent>(beast).EntityPrototype!.ID;

                        Assert.That(entMan.HasComponent<YautjaMapTrackedComponent>(beast), Is.True, $"{id} shows on the clan's map");
                        Assert.That(entMan.HasComponent<TacticalMapTrackedComponent>(beast), Is.True, $"{id} is tracked");
                        Assert.That(entMan.TryGetComponent(beast, out TacticalMapUserComponent? user), Is.True, $"{id} can open a map");
                        Assert.That(user!.Yautja, Is.True, $"{id} opens the clan's map");
                        Assert.That(user.Xenos, Is.False, $"{id} does not open a hive's map");
                        Assert.That(user.Marines, Is.False, $"{id} does not see the marines' map");
                        Assert.That(user.Action, Is.Not.Null, $"{id} has the map action");

                        Assert.That(entMan.TryGetComponent(beast, out IntrinsicRadioTransmitterComponent? tx) &&
                                    tx.Channels.Contains(BadBloodChannel), Is.True, $"{id} talks on the Bad Blood channel");
                        Assert.That(entMan.TryGetComponent(beast, out ActiveRadioComponent? rx) &&
                                    rx.Channels.Contains(BadBloodChannel), Is.True, $"{id} hears the Bad Blood channel");
                        Assert.That(entMan.HasComponent<IntrinsicRadioReceiverComponent>(beast), Is.True, $"{id} receives radio");

                        Assert.That(language.CanUnderstand(beast, "Yautja"), Is.True, $"{id} understands the clan");
                        Assert.That(language.CanUnderstand(beast, "Beast"), Is.True, $"{id} understands the hounds");
                        Assert.That(language.CanUnderstand(beast, "Xeno"), Is.True, $"{id} understands the thralls");
                    }

                    // the clan understands every beast's tongue
                    Assert.That(language.CanUnderstand(grunt, "Beast"), Is.True, "a Bad Blood understands its hounds");
                    Assert.That(language.CanUnderstand(grunt, "Xeno"), Is.True, "a Bad Blood understands its thralls");
                    Assert.That(language.CanSpeak(grunt, "Yautja"), Is.True, "a Bad Blood still speaks Yautja");
                });
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Server.WaitPost(() =>
            {
                foreach (var uid in spawned.Concat(party.All))
                {
                    if (Server.EntMan.EntityExists(uid))
                        Server.EntMan.DeleteEntity(uid);
                }
            });
        }
    }
}
