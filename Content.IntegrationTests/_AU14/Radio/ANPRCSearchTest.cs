using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Systems.Chat;
using Content.Server.CMU14.Radio;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.Chat;
using Content.Shared.CMU14.Radio;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Radio;
using Content.Shared.Roles;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Radio;

// the search receiver, its payoff and the key break, driven through the real radio send path and the
// same BUI messages the panels send. each test pins one promise the owner signed off (fix times, range,
// payoff, the break) or one edge a live round turned up, so a later change cannot quietly undo it
[TestFixture]
public sealed class ANPRCSearchTest
{
    private static readonly EntProtoId GovforPack = "ANPRC117GRadioFilled";
    private static readonly EntProtoId OpforPack = "ANPRC117GRadioOPFORFilled";
    private static readonly ProtoId<JobPrototype> Rifleman = "AU14JobGOVFORSquadRifleman";
    private static readonly ProtoId<RadioChannelPrototype> OpforBravo = "radioOpforBravo";
    private static readonly ProtoId<RadioChannelPrototype> GovforCommand = "radioGovforCommand";

    private const string BackSlot = "back";
    private const string Opfor = "opfor";

    private int _line;

    [Test]
    public async Task BusyNetFixesInAboutAMinute()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid pack = default, talker = default;
        RadioFrequency frequency = default;

        await server.WaitAssertion(() =>
        {
            (_, pack) = SpawnSearcher(server, testMap.GridCoords);
            talker = server.EntMan.SpawnEntity(null, testMap.GridCoords.Offset(new Vector2(5, 0)));
            frequency = Frequency(server, OpforBravo);
        });

        var fixedAt = await TalkUntilFixed(pair, pack, talker, frequency, interval: 8, limit: 120);

        Assert.That(fixedAt, Is.LessThanOrEqualTo(75), "a net talking every 8 s is fixed in about a minute");
        await pair.CleanReturnAsync();
    }

    // the worst case for a single line: said just after the head went past its frequency.
    // it has to still be fresh a whole pass later, or a quiet net can go unheard by chance
    [Test]
    public async Task LineSaidJustBehindTheHeadIsCaughtNextPass()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        EntityUid pack = default, talker = default;
        RadioFrequency frequency = default;
        await server.WaitAssertion(() =>
        {
            (_, pack) = SpawnSearcher(server, testMap.GridCoords);
            talker = server.EntMan.SpawnEntity(null, testMap.GridCoords.Offset(new Vector2(5, 0)));
            frequency = Frequency(server, OpforBravo);
        });
        await Say(pair, talker, OpforBravo);
        await pair.RunSeconds(0.9f);
        await server.WaitAssertion(() =>
        {
            var khz = frequency.Kilohertz + 1;
            if (khz > ANPRCRadioComponent.SweepBandMax.Kilohertz)
                khz = ANPRCRadioComponent.SweepBandMin.Kilohertz;
            server.EntMan.GetComponent<ANPRCRadioComponent>(pack).SweepPosition = RadioFrequency.FromKilohertz(khz);
        });
        await pair.RunSeconds(25);
        await server.WaitAssertion(() =>
            Assert.That(server.EntMan.GetComponent<ANPRCRadioComponent>(pack).SweepContacts.ContainsKey(frequency), Is.True,
                "a line said just after the head passed is still caught on the next pass"));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task QuietNetFixesAndNeverLosesGroundBetweenLines()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid pack = default, talker = default;
        RadioFrequency frequency = default;
        float afterFirst = 0;

        await server.WaitAssertion(() =>
        {
            (_, pack) = SpawnSearcher(server, testMap.GridCoords);
            talker = server.EntMan.SpawnEntity(null, testMap.GridCoords.Offset(new Vector2(5, 0)));
            frequency = Frequency(server, OpforBravo);
        });

        // one line, then the head has to come round to it
        await Say(pair, talker, OpforBravo);
        await pair.RunSeconds(22);

        await server.WaitAssertion(() =>
        {
            var radio = server.EntMan.GetComponent<ANPRCRadioComponent>(pack);
            Assert.That(radio.SweepContacts.TryGetValue(frequency, out afterFirst), Is.True, "the first line makes a contact");
        });

        // a whole quiet minute: the contact must not rot while the net is only pausing
        await pair.RunSeconds(38);

        await server.WaitAssertion(() =>
        {
            var radio = server.EntMan.GetComponent<ANPRCRadioComponent>(pack);
            Assert.That(radio.SweepContacts[frequency], Is.EqualTo(afterFirst), "no decay inside the silence grace");
        });

        var fixedAt = await TalkUntilFixed(pair, pack, talker, frequency, interval: 60, limit: 200);

        Assert.That(fixedAt + 60, Is.LessThanOrEqualTo(240), "a net talking once a minute is fixed in about three minutes");
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RangeScalesWithTheTransmittersPower()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid pack = default, headsetTalker = default, loudTalker = default, loudPack = default;
        RadioFrequency frequency = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            (_, pack) = SpawnSearcher(server, testMap.GridCoords);

            // 130 tiles: past the 120 a MED transmitter carries, inside the 180 a HI one does
            var far = new MapCoordinates(testMap.MapCoords.Position + new Vector2(130, 0), testMap.MapId);
            headsetTalker = entities.SpawnEntity(null, far);
            loudTalker = entities.SpawnEntity(null, far);
            loudPack = entities.SpawnEntity(OpforPack, far);
            entities.GetComponent<ANPRCRadioComponent>(loudPack).TxPower = RadioTxPower.High;

            frequency = Frequency(server, OpforBravo);
        });

        for (var i = 0; i < 6; i++)
        {
            await Say(pair, headsetTalker, OpforBravo);
            await pair.RunSeconds(8);
        }

        await server.WaitAssertion(() =>
        {
            var radio = server.EntMan.GetComponent<ANPRCRadioComponent>(pack);
            Assert.That(radio.SweepContacts.ContainsKey(frequency), Is.False, "a headset 130 tiles out is never heard");
        });

        for (var i = 0; i < 3; i++)
        {
            await Say(pair, loudTalker, OpforBravo, loudPack);
            await pair.RunSeconds(8);
        }

        await server.WaitAssertion(() =>
        {
            var radio = server.EntMan.GetComponent<ANPRCRadioComponent>(pack);
            Assert.That(radio.SweepContacts.ContainsKey(frequency), Is.True, "a set on HI power carries half as far again");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FarSpeakerDoesNotHideANearOne()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid pack = default, near = default, far = default;
        RadioFrequency frequency = default;

        await server.WaitAssertion(() =>
        {
            (_, pack) = SpawnSearcher(server, testMap.GridCoords);
            near = server.EntMan.SpawnEntity(null, testMap.GridCoords.Offset(new Vector2(5, 0)));
            far = server.EntMan.SpawnEntity(null, new MapCoordinates(testMap.MapCoords.Position + new Vector2(500, 0), testMap.MapId));
            frequency = Frequency(server, OpforBravo);
        });

        // the squad shares a net. the far man always answers a second after the near one, so a band
        // that only remembered the last speaker would never see the near man at all
        var fixedAt = -1;

        for (var second = 0; second <= 120; second += 8)
        {
            await Say(pair, near, OpforBravo);
            await pair.RunSeconds(1);
            await Say(pair, far, OpforBravo);
            await pair.RunSeconds(7);

            if (await IsFixed(server, pack, frequency))
            {
                fixedAt = second + 8;
                break;
            }
        }

        Assert.That(fixedAt, Is.InRange(1, 90), "the near speaker is still intercepted");
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OneLineIsWorthOneHitHoweverManyPassesItSitsUnder()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid pack = default, talker = default;
        RadioFrequency frequency = default;

        await server.WaitAssertion(() =>
        {
            (_, pack) = SpawnSearcher(server, testMap.GridCoords);
            talker = server.EntMan.SpawnEntity(null, testMap.GridCoords.Offset(new Vector2(3, 0)));
            frequency = Frequency(server, OpforBravo);
        });

        await Say(pair, talker, OpforBravo);
        await pair.RunSeconds(60);

        await server.WaitAssertion(() =>
        {
            var radio = server.EntMan.GetComponent<ANPRCRadioComponent>(pack);
            Assert.That(radio.SweepContacts[frequency], Is.EqualTo(radio.SweepConfidencePerHit).Within(0.0001f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FixedNetBanksTrialsAndPutsABearingOnTheMap()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid pack = default, talker = default;
        TacticalMapComponent tacmap = default!;
        var intelKey = 0;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            (_, pack) = SpawnSearcher(server, testMap.GridCoords, sweep: false);
            talker = entities.SpawnEntity(null, testMap.GridCoords);
            tacmap = entities.EnsureComponent<TacticalMapComponent>(testMap.Grid.Owner);

            entities.GetComponent<ANPRCRadioComponent>(pack).DiscoveredFrequencies.Add(Frequency(server, OpforBravo));
        });

        await Say(pair, talker, OpforBravo);
        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(Analysis(server, pack).Depth, Is.EqualTo(1), "an encrypted line on a fixed net banks a trial");
            Assert.That(tacmap.LastUpdateGovforBlips, Is.Not.Empty, "the bearing is on the map every tacmap on the side draws");

            // the 3D reconstruction places contacts by the entity behind the key; an intel blip has none,
            // so it has to be able to ask which grid the blip sits on or it drops it
            intelKey = tacmap.LastUpdateGovforBlips.Keys.First();
            Assert.That(server.System<Content.Server._RMC14.TacticalMap.TacticalMapSystem>().TryGetIntelBlipGrid(intelKey, out var grid), Is.True);
            Assert.That(grid, Is.EqualTo(testMap.Grid.Owner));
        });

        await pair.RunSeconds(9);

        await server.WaitAssertion(() =>
        {
            Assert.That(tacmap.LastUpdateGovforBlips, Is.Empty, "and gone again after a few seconds");
            Assert.That(server.System<Content.Server._RMC14.TacticalMap.TacticalMapSystem>().TryGetIntelBlipGrid(intelKey, out _), Is.False);

            // the bank is a buffer, not a hoard
            var crypto = server.System<ANPRCCryptoSystem>();
            for (var i = 0; i < 30; i++)
            {
                crypto.BankTrial(pack, Opfor);
            }

            Assert.That(Analysis(server, pack).Depth, Is.EqualTo(ANPRCCryptoSystem.DepthMax));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task KeyTrialsBreakTheKeyAndARecryptoWipesIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid operatorUid = default, pack = default, talker = default;
        RadioChannelPrototype channel = default!;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            (operatorUid, pack) = SpawnSearcher(server, testMap.GridCoords, sweep: false);
            talker = entities.SpawnEntity(null, testMap.GridCoords);
            channel = server.ProtoMan.Index(OpforBravo);

            entities.GetComponent<ANPRCRadioComponent>(pack).DiscoveredFrequencies.Add(Frequency(server, OpforBravo));

            var crypto = server.System<ANPRCCryptoSystem>();
            for (var i = 0; i < ANPRCCryptoSystem.DepthMax; i++)
            {
                crypto.BankTrial(pack, Opfor);
            }

            Assert.That(server.System<ANPRCGarbleSystem>().ApplyComsecGarble(talker, pack, channel, "move to the ridge"),
                Is.Not.EqualTo("move to the ridge"), "before the break their traffic is static");
        });

        // a careful operator: only ever guess a key that agrees with every answer so far
        var candidates = AllKeys();
        var trials = 0;

        while (true)
        {
            var guess = candidates[0];
            var broken = false;
            var hits = -1;

            await server.WaitAssertion(() =>
            {
                var crypto = server.System<ANPRCCryptoSystem>();
                if (Analysis(server, pack).Depth == 0)
                    crypto.BankTrial(pack, Opfor);

                var ui = server.System<SharedUserInterfaceSystem>();
                ui.OpenUi(pack, ANPRCRadioUI.Key, operatorUid);
                ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCKeyTrialMsg(Opfor, guess) { Actor = operatorUid });

                var work = Analysis(server, pack);
                broken = work.Broken;
                hits = work.Trials[^1].Hits;
            });

            trials++;

            if (broken)
                break;

            candidates = candidates.Where(key => ANPRCKeyAnalysis.Hits(key, guess) == hits).ToList();
            Assert.That(candidates, Is.Not.Empty, "the answers are consistent");
            Assert.That(trials, Is.LessThan(25), "a careful operator breaks the key in a reasonable number of trials");
        }

        EntityUid opforOperator = default, opforPack = default;

        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<ANPRCCryptoSystem>().HasBrokenKey(pack, Opfor), Is.True);
            Assert.That(server.System<ANPRCGarbleSystem>().ApplyComsecGarble(talker, pack, channel, "move to the ridge"),
                Is.EqualTo("move to the ridge"), "a broken key reads their traffic");

            // their command answers with a recrypto
            (opforOperator, opforPack) = SpawnOperator(server, testMap.GridCoords, OpforPack, trained: true);
            server.EntMan.EnsureComponent<ANPRCCryptoAuthorityComponent>(opforOperator);

            var ui = server.System<SharedUserInterfaceSystem>();
            ui.OpenUi(opforPack, ANPRCRadioUI.Key, opforOperator);
            ui.RaiseUiMessage(opforPack, ANPRCRadioUI.Key, new ANPRCCryptoRecryptoMsg { Actor = opforOperator });
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<ANPRCCryptoSystem>().HasBrokenKey(pack, Opfor), Is.False, "a recrypto ends the break");
            Assert.That(server.System<ANPRCGarbleSystem>().ApplyComsecGarble(talker, pack, channel, "move to the ridge"),
                Is.Not.EqualTo("move to the ridge"));

            var work = Analysis(server, pack);
            Assert.Multiple(() =>
            {
                Assert.That(work.Depth, Is.Zero, "trials banked against the old key are worthless");
                Assert.That(work.Trials, Is.Empty);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task KeyTrialsRefuseWhatTheyShould()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var ui = server.System<SharedUserInterfaceSystem>();
            var crypto = server.System<ANPRCCryptoSystem>();
            var (trained, pack) = SpawnSearcher(server, testMap.GridCoords, sweep: false);

            void Trial(EntityUid actor, string key)
            {
                ui.OpenUi(pack, ANPRCRadioUI.Key, actor);
                ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCKeyTrialMsg(Opfor, key) { Actor = actor });
            }

            // no net of theirs fixed yet: nothing to work from, whatever the client claims
            crypto.BankTrial(pack, Opfor);
            Trial(trained, "ABCDEF");
            Assert.That(Analysis(server, pack).Trials, Is.Empty, "no fix, no trial");

            entities.GetComponent<ANPRCRadioComponent>(pack).DiscoveredFrequencies.Add(Frequency(server, OpforBravo));

            // the same wearer without the training: the panel view is the client's choice, the skill is not
            entities.RemoveComponent<ANPRCRadioUserComponent>(trained);
            Trial(trained, "ABCDEF");
            entities.EnsureComponent<ANPRCRadioUserComponent>(trained);

            Trial(trained, "ABCDEI");
            Trial(trained, "ABC");
            Trial(trained, "");

            var work = Analysis(server, pack);
            Assert.Multiple(() =>
            {
                Assert.That(work.Trials, Is.Empty, "untrained, off-alphabet and short trials are refused");
                Assert.That(work.Depth, Is.EqualTo(1), "and cost nothing");
            });

            Trial(trained, "abcdef");
            Trial(trained, "ABCDEF");

            work = Analysis(server, pack);
            Assert.Multiple(() =>
            {
                Assert.That(work.Trials, Has.Count.EqualTo(1), "a trial with nothing banked is refused");
                Assert.That(work.Trials[0].Key, Is.EqualTo("ABCDEF"), "lower case is read as upper");
                Assert.That(work.Depth, Is.Zero);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DwellParksOnTheStrongestLookAlikeAndNeverOnAHiddenContact()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ui = server.System<SharedUserInterfaceSystem>();
            var (wearer, pack) = SpawnSearcher(server, testMap.GridCoords);
            var radio = server.EntMan.GetComponent<ANPRCRadioComponent>(pack);

            // both read 1XX.XXX on the glass. the stronger one is the one the operator is looking at
            var weak = RadioFrequency.FromKilohertz(150_100);
            var strong = RadioFrequency.FromKilohertz(170_300);
            var hidden = RadioFrequency.FromKilohertz(210_700);
            radio.SweepContacts[weak] = 0.3f;
            radio.SweepContacts[strong] = 0.6f;
            radio.SweepContacts[hidden] = 0.1f;

            ui.OpenUi(pack, ANPRCRadioUI.Key, wearer);
            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCSetDwellMsg(100_000) { Actor = wearer });
            Assert.That(radio.SweepDwellKilohertz, Is.EqualTo(strong.Kilohertz));

            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCSetDwellMsg(-1) { Actor = wearer });
            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCSetDwellMsg(0) { Actor = wearer });
            Assert.That(radio.SweepDwellKilohertz, Is.EqualTo(-1), "a contact too faint to list cannot be dwelled on");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SearchStopsWhenTheSetGoesDown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid offPack = default, strippedPack = default, stripped = default;

        await server.WaitAssertion(() =>
        {
            var ui = server.System<SharedUserInterfaceSystem>();
            var (offWearer, pack) = SpawnSearcher(server, testMap.GridCoords);
            offPack = pack;
            (stripped, strippedPack) = SpawnSearcher(server, testMap.GridCoords);

            ui.OpenUi(offPack, ANPRCRadioUI.Key, offWearer);
            ui.RaiseUiMessage(offPack, ANPRCRadioUI.Key, new ANPRCTogglePowerMsg { Actor = offWearer });

            Assert.That(server.System<InventorySystem>().TryUnequip(stripped, BackSlot, force: true), Is.True);
        });

        await pair.RunSeconds(3);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(server.EntMan.GetComponent<ANPRCRadioComponent>(offPack).SweepEnabled, Is.False, "switched off");
                Assert.That(server.EntMan.GetComponent<ANPRCRadioComponent>(strippedPack).SweepEnabled, Is.False, "taken off");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AFactionlessSetKnowsOnlyTheOpenNets()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var plan = server.System<ANPRCFrequencyPlanSystem>();
            var enemy = Frequency(server, OpforBravo);
            var own = Frequency(server, GovforCommand);

            Assert.Multiple(() =>
            {
                Assert.That(plan.IsKnownTo(enemy, string.Empty), Is.False, "no faction is not every faction");
                Assert.That(plan.IsKnownTo(enemy, "govfor"), Is.False);
                Assert.That(plan.IsKnownTo(own, "govfor"), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InterceptsAndNoticesArriveAsTaggedRadioRows()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var testMap = await pair.CreateTestMap();

        EntityUid wearer = default, pack = default, talker = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var ui = server.System<SharedUserInterfaceSystem>();
            (wearer, pack) = SpawnSearcher(server, testMap.GridCoords, sweep: false);
            talker = entities.SpawnEntity(null, testMap.GridCoords.Offset(new Vector2(2, 0)));

            // their net carries here, and the set has fixed it and tuned it into a memory
            entities.SpawnEntity("AU14CommsMastOpfor", testMap.GridCoords);
            var frequency = Frequency(server, OpforBravo);
            entities.GetComponent<ANPRCRadioComponent>(pack).DiscoveredFrequencies.Add(frequency);
            entities.GetComponent<ANPRCRadioComponent>(pack).SlotLabels[0] = "INT";

            server.PlayerMan.SetAttachedEntity(pair.Player, wearer);

            ui.OpenUi(pack, ANPRCRadioUI.Key, wearer);
            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCTuneContactMsg(0, frequency) { Actor = wearer });
            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCSelectSlotMsg(0) { Actor = wearer });

            // a refusal from the set: dwelling needs a running search
            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCSetDwellMsg(100_000) { Actor = wearer });
        });

        await pair.RunTicksSync(5);
        await Say(pair, talker, OpforBravo);
        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var history = client.ResolveDependency<IUserInterfaceManager>()
                .GetUIController<ChatUIController>()
                .History
                .Select(entry => entry.Msg)
                .ToList();

            var intercept = history.FirstOrDefault(msg => msg.Display?.ChannelLabel == "INT");
            var notice = history.FirstOrDefault(msg => msg.Display?.ChannelLabel == "117G");

            Assert.Multiple(() =>
            {
                Assert.That(intercept, Is.Not.Null, "enemy traffic through the pack is an INT row");
                Assert.That(intercept?.Channel, Is.EqualTo(ChatChannel.Radio));
                Assert.That(intercept?.WrappedMessage, Does.Not.Contain("FF6B6B"), "not the old red line");
                Assert.That(notice, Is.Not.Null, "the set's own words are a 117G row");
                Assert.That(notice?.Channel, Is.EqualTo(ChatChannel.Radio));
            });
        });

        await pair.CleanReturnAsync();
    }

    // ----- helpers ---------------------------------------------------------------------------------

    private async Task<int> TalkUntilFixed(
        Pair.TestPair pair,
        EntityUid pack,
        EntityUid talker,
        RadioFrequency frequency,
        int interval,
        int limit)
    {
        for (var second = 0; second <= limit; second += interval)
        {
            await Say(pair, talker, OpforBravo);
            await pair.RunSeconds(interval);

            if (await IsFixed(pair.Server, pack, frequency))
                return second + interval;
        }

        Assert.Fail($"not fixed within {limit} s");
        return -1;
    }

    // a distinct line every time: the radio drops a repeat of the same text inside one tick
    private async Task Say(Pair.TestPair pair, EntityUid talker, ProtoId<RadioChannelPrototype> channel, EntityUid? source = null)
    {
        var line = $"sector clear, moving to phase line {++_line}";

        await pair.Server.WaitAssertion(() =>
        {
            pair.Server.System<RadioSystem>().SendRadioMessage(
                talker,
                line,
                pair.Server.ProtoMan.Index(channel),
                source ?? talker);
        });
    }

    private static async Task<bool> IsFixed(Robust.UnitTesting.RobustIntegrationTest.ServerIntegrationInstance server, EntityUid pack, RadioFrequency frequency)
    {
        var fixedNow = false;
        await server.WaitAssertion(() =>
        {
            fixedNow = server.EntMan.GetComponent<ANPRCRadioComponent>(pack).DiscoveredFrequencies.Contains(frequency);
        });

        return fixedNow;
    }

    private static RadioFrequency Frequency(Robust.UnitTesting.RobustIntegrationTest.ServerIntegrationInstance server, ProtoId<RadioChannelPrototype> channel)
    {
        return server.System<ANPRCFrequencyPlanSystem>().GetFrequency(server.ProtoMan.Index(channel));
    }

    private static ANPRCKeyAnalysisState Analysis(Robust.UnitTesting.RobustIntegrationTest.ServerIntegrationInstance server, EntityUid pack)
    {
        var radio = server.EntMan.GetComponent<ANPRCRadioComponent>(pack);
        return server.System<ANPRCCryptoSystem>().BuildAnalysisStates(pack, radio).Single(work => work.Faction == Opfor);
    }

    private static List<string> AllKeys()
    {
        var keys = new List<string>();
        var symbols = ANPRCKeyAnalysis.KeySymbols;

        void Build(string prefix)
        {
            if (prefix.Length == ANPRCKeyAnalysis.KeyLength)
            {
                keys.Add(prefix);
                return;
            }

            foreach (var symbol in symbols)
            {
                if (!prefix.Contains(symbol))
                    Build(prefix + symbol);
            }
        }

        Build(string.Empty);
        return keys;
    }

    // a trained GOVFOR operator wearing a filled set, searching unless told otherwise
    private static (EntityUid Wearer, EntityUid Pack) SpawnSearcher(
        Robust.UnitTesting.RobustIntegrationTest.ServerIntegrationInstance server,
        EntityCoordinates coords,
        bool sweep = true)
    {
        var (wearer, pack) = SpawnOperator(server, coords, GovforPack, trained: true);

        if (sweep)
        {
            var ui = server.System<SharedUserInterfaceSystem>();
            ui.OpenUi(pack, ANPRCRadioUI.Key, wearer);
            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCSetSweepMsg(true) { Actor = wearer });
            Assert.That(server.EntMan.GetComponent<ANPRCRadioComponent>(pack).SweepEnabled, Is.True);
        }

        return (wearer, pack);
    }

    private static (EntityUid Wearer, EntityUid Pack) SpawnOperator(
        Robust.UnitTesting.RobustIntegrationTest.ServerIntegrationInstance server,
        EntityCoordinates coords,
        EntProtoId packId,
        bool trained)
    {
        var entities = server.EntMan;
        var spawning = server.System<StationSpawningSystem>();
        var inventory = server.System<InventorySystem>();

        var wearer = spawning.SpawnPlayerMob(coords, Rifleman, new HumanoidCharacterProfile(), station: null);
        var pack = entities.SpawnEntity(packId, coords);

        if (inventory.TryGetSlotEntity(wearer, BackSlot, out var oldBack))
            entities.DeleteEntity(oldBack.Value);

        if (trained)
            entities.EnsureComponent<ANPRCRadioUserComponent>(wearer);

        Assert.That(inventory.TryEquip(wearer, pack, BackSlot, force: true), Is.True);

        return (wearer, pack);
    }
}
