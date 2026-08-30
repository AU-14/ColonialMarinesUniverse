using System;
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.CMU14.Radio;
using Content.Shared.CMU14.CCVar;
using Content.Shared.CMU14.Radio;
using Content.Shared.FixedPoint;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Radio;

[TestFixture]
[TestOf(typeof(RadioFrequency))]
[EnsureCVar(Side.Server, typeof(AU14CCVars), nameof(AU14CCVars.NewCommsSystem), true)]
public sealed class RadioFrequencyTest : GameTest
{
    private static readonly IReadOnlyDictionary<string, int> CustomChannels = new Dictionary<string, int>
    {
        ["radioGovforAlpha"] = 250_200,
        ["radioGovforBravo"] = 160_600,
        ["radioGovforCharlie"] = 160_700,
        ["radioGovforCommand"] = 259_200,
        ["radioGovforIntel"] = 160_500,
        ["radioGovforJTAC"] = 259_800,
        ["radioGovforMILP"] = 259_500,
        ["radioOpforAlpha"] = 160_100,
        ["radioOpforBravo"] = 160_300,
        ["radioOpforCharlie"] = 160_400,
        ["radioOpforCommand"] = 147_900,
        ["radioOpforIntel"] = 160_200,
        ["radioOpforJTAC"] = 147_400,
        ["radioOpforMILP"] = 147_700,
        ["radioProvost"] = 186_300,
        ["Colony"] = 146_900,
        ["colonyAlert"] = 143_100,
        ["radioWEYU"] = 123_100,
        ["radioCMB"] = 122_000,
        ["radioCLF"] = 188_600,
        ["radioCLFCommand"] = 188_700,
        ["radioMobFamily"] = 146_700,
        ["radioAI"] = 767_600,
        ["ColonyHandheld"] = 146_100,
        ["Hivemind"] = 42_600,
        ["MyceliumLink"] = 42_500,
        ["ICSCSOF"] = 155_800,
        ["UASOF"] = 155_300,
        ["TWESOF"] = 155_700,
        ["UPPSOF"] = 153_300,
        ["CCASOF"] = 153_700,
        ["radioVAI"] = 155_900,
        ["PART"] = 557_100,
        ["ANPRCActiveChannel"] = 0,
        ["TunableFrequencyChannel"] = 0,
        ["UNISOF"] = 176_500,
        ["Abomination"] = 42_800,
        ["AbominationMimic"] = 42_700,
        ["CMUYautja"] = 154_100,
        ["CMUYautjaOverseer"] = 154_200,
        ["CMUYautjaBadBlood"] = 154_300,
        ["MarineHighCommand"] = 147_100,
        ["MarineCommon"] = 148_000,
        ["MarineCommand"] = 148_100,
        ["MarineMedical"] = 148_200,
        ["MarineEngineer"] = 148_300,
        ["MarineMilitaryPolice"] = 148_400,
        ["MarineRequisition"] = 148_500,
        ["MarineIntel"] = 148_600,
        ["MarineJTAC"] = 148_700,
        ["MarineAlpha"] = 149_100,
        ["MarineBravo"] = 149_200,
        ["MarineCharlie"] = 149_300,
        ["MarineDelta"] = 149_400,
        ["MarineEcho"] = 149_500,
        ["MarineFoxtrot"] = 149_600,
        ["TSE"] = 155_100,
    };

    [Test]
    public async Task ValueParsingFormattingAndMaskingPreserveKilohertz()
    {
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    RadioFrequency.FromMegahertz(FixedPoint2.FromHundredths(14_590)).Kilohertz,
                    Is.EqualTo(145_900));
                Assert.That(
                    RadioFrequency.FromMegahertz(FixedPoint2.FromHundredths(14_710)).Kilohertz,
                    Is.EqualTo(147_100));
            });

            AssertParsesAndRoundTrips("30.000", 30_000);
            AssertParsesAndRoundTrips("87.999", 87_999);
            AssertParsesAndRoundTrips("259.237", 259_237);
            AssertParsesAndRoundTrips("299.900", 299_900);

            AssertRejects("1.0000");
            AssertRejects("145,9");
            AssertRejects("-1");
            AssertRejects("2147483.648");

            Assert.That(RadioFrequencyInput.TryParseAnprcScreenInput("2592", out var anprc), Is.True);
            Assert.That(anprc.Kilohertz, Is.EqualTo(259_200));
            Assert.That(RadioFrequencyInput.TryParseAnprcScreenInput("259.237", out anprc), Is.True);
            Assert.That(anprc.Kilohertz, Is.EqualTo(259_237));

            Assert.That(RadioFrequencyInput.TryParseTunableScreenInput("87999", out var tunable), Is.True);
            Assert.That(tunable.Kilohertz, Is.EqualTo(87_999));
            Assert.That(RadioFrequencyInput.TryParseTunableScreenInput("87.999", out tunable), Is.True);
            Assert.That(tunable.Kilohertz, Is.EqualTo(87_999));

            var contact = RadioFrequency.FromKilohertz(259_237);
            var sweepDefaults = new ANPRCRadioComponent();
            var sweepDuration =
                (ANPRCRadioComponent.SweepBandMax.Kilohertz - ANPRCRadioComponent.SweepBandMin.Kilohertz) /
                (double) sweepDefaults.SweepKilohertzPerSecond;
            Assert.Multiple(() =>
            {
                Assert.That(ANPRCSweepSystem.FormatMasked(contact, 1), Is.EqualTo("2XX.XXX"));
                Assert.That(ANPRCSweepSystem.FormatMasked(contact, 2), Is.EqualTo("25X.XXX"));
                Assert.That(ANPRCSweepSystem.FormatMasked(contact, 3), Is.EqualTo("259.XXX"));
                Assert.That(ANPRCSweepSystem.FormatMasked(contact, 4), Is.EqualTo("259.237"));
                Assert.That(ANPRCSweepSystem.MaskFrequency(contact, 4), Is.EqualTo(contact));
                Assert.That(sweepDefaults.SweepKilohertzPerSecond, Is.EqualTo(10_000));
                Assert.That(sweepDuration, Is.EqualTo(19.99).Within(0.001),
                    "The default sweep must cover the full configured band in about 20 seconds.");
            });
        });
    }

    [Test]
    public async Task CustomChannelsLoadAsDirectMegahertzWithoutCollisions()
    {
        await Server.WaitAssertion(() =>
        {
            Assert.That(CustomChannels, Has.Count.EqualTo(57));

            foreach (var (id, expectedKilohertz) in CustomChannels)
            {
                var prototype = SProtoMan.Index<RadioChannelPrototype>(id);
                Assert.That(prototype.Frequency.Kilohertz, Is.EqualTo(expectedKilohertz), id);
                Assert.That(
                    RadioFrequency.ParseMegahertz(prototype.Frequency.FormatMegahertz()),
                    Is.EqualTo(prototype.Frequency),
                    id);
            }

            var positive = CustomChannels.Values.Where(value => value > 0).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(positive, Has.Length.EqualTo(55));
                Assert.That(CustomChannels.Values.Count(value => value == 0), Is.EqualTo(2));
                Assert.That(positive.Distinct().Count(), Is.EqualTo(55),
                    "Every positive custom carrier must have one unambiguous reverse lookup.");
            });
        });
    }

    [Test]
    public async Task FrequencyPlanAndReverseLookupRemainDisjoint()
    {
        await Server.WaitAssertion(() =>
        {
            var plan = Server.System<ANPRCFrequencyPlanSystem>();
            var prototypes = SProtoMan.EnumeratePrototypes<RadioChannelPrototype>().ToArray();
            var staticFrequencies = prototypes
                .Select(channel => channel.Frequency)
                .Where(frequency => frequency != RadioFrequency.Off)
                .ToHashSet();
            var randomized = prototypes
                .Where(channel => channel.Frequency != RadioFrequency.Off && !string.IsNullOrEmpty(channel.Faction))
                .Select(plan.GetFrequency)
                .ToArray();

            Assert.That(randomized, Is.Not.Empty);
            Assert.That(randomized.Distinct().Count(), Is.EqualTo(randomized.Length));
            foreach (var frequency in randomized)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(frequency.Kilohertz, Is.InRange(100_000, 299_900));
                    Assert.That((frequency.Kilohertz - 100_000) % 100, Is.Zero);
                    Assert.That(staticFrequencies, Does.Not.Contain(frequency));
                });
            }

            AssertReverseLookup(plan, 42_500, "MyceliumLink");
            AssertReverseLookup(plan, 42_700, "AbominationMimic");
            AssertReverseLookup(plan, 42_800, "Abomination");
            Assert.That(
                plan.TryGetChannelByFrequency(RadioFrequency.FromKilohertz(42_600), out _),
                Is.False,
                "The Hivemind carrier is deliberately excluded from ordinary reverse lookup.");
        });
    }

    [Test]
    public async Task JammerSeparatesRadioCarriersFromDeviceNetworks()
    {
        await Server.WaitAssertion(() =>
        {
            var jammerUid = SSpawn("XenoborgRadioJammer");
            var jammer = SComp<RadioJammerComponent>(jammerUid);

            Assert.Multiple(() =>
            {
                Assert.That(
                    jammer.FrequenciesExcluded,
                    Is.EquivalentTo(new[]
                    {
                        RadioFrequency.FromKilohertz(200_200),
                        RadioFrequency.FromKilohertz(200_300),
                    }));
                Assert.That(jammer.DeviceFrequenciesExcluded, Is.EquivalentTo(new uint[] { 2002, 2003, 2004, 2005 }));
            });
        });
    }

    [Test]
    public async Task ComponentReplicationPreservesOneKilohertzPrecision()
    {
        var map = await Pair.CreateTestMap();
        NetEntity radioNet = default;

        await Server.WaitPost(() =>
        {
            var radioUid = SSpawnAtPosition("ANPRC117GRadio", map.GridCoords);
            radioNet = SEntMan.GetNetEntity(radioUid);
            var radio = SComp<ANPRCRadioComponent>(radioUid);
            radio.FrequencyOverrides[0] = RadioFrequency.FromKilohertz(259_237);
            SEntMan.Dirty(radioUid, radio);
        });

        await Pair.RunTicksSync(5);

        await Client.WaitAssertion(() =>
        {
            var radioUid = CEntMan.GetEntity(radioNet);
            var radio = CComp<ANPRCRadioComponent>(radioUid);
            Assert.That(radio.FrequencyOverrides[0].Kilohertz, Is.EqualTo(259_237));
        });
    }

    [Test]
    public async Task SweepCatchesArbitraryKilohertzCarriersAndWrapsBandEnd()
    {
        var map = await Pair.CreateTestMap();
        EntityUid radioUid = default;
        EntityUid sourceUid = default;
        var directCarrier = RadioFrequency.FromKilohertz(259_237);
        var wrappedCarrier = RadioFrequency.FromKilohertz(100_050);

        await Server.WaitPost(() =>
        {
            radioUid = SSpawnAtPosition("ANPRC117GRadio", map.GridCoords);
            sourceUid = SSpawnAtPosition(null, map.GridCoords);
            ConfigureSweep(SComp<ANPRCRadioComponent>(radioUid), RadioFrequency.FromKilohertz(259_000), 1_000);
            SEntMan.Dirty(radioUid, SComp<ANPRCRadioComponent>(radioUid));
            Server.System<ANPRCSweepSystem>().RecordEmission(sourceUid, directCarrier);
        });

        await Pair.RunTicksSync(70);

        await Server.WaitAssertion(() =>
        {
            var radio = SComp<ANPRCRadioComponent>(radioUid);
            Assert.Multiple(() =>
            {
                Assert.That(radio.SweepContacts.Keys, Is.EquivalentTo(new[] { directCarrier }));
                Assert.That(radio.SweepContacts[directCarrier], Is.EqualTo(radio.SweepConfidencePerHit));
            });
        });

        await Server.WaitPost(() =>
        {
            var radio = SComp<ANPRCRadioComponent>(radioUid);
            ConfigureSweep(radio, RadioFrequency.FromKilohertz(299_800), 500);
            SEntMan.Dirty(radioUid, radio);
            Server.System<ANPRCSweepSystem>().RecordEmission(sourceUid, wrappedCarrier);
        });

        await Pair.RunTicksSync(70);

        await Server.WaitAssertion(() =>
        {
            var radio = SComp<ANPRCRadioComponent>(radioUid);
            Assert.Multiple(() =>
            {
                Assert.That(radio.SweepContacts.Keys, Is.EquivalentTo(new[] { wrappedCarrier }));
                Assert.That(radio.SweepContacts[wrappedCarrier], Is.EqualTo(radio.SweepConfidencePerHit));
            });
        });
    }

    private static void AssertParsesAndRoundTrips(string text, int expectedKilohertz)
    {
        Assert.That(RadioFrequency.TryParseMegahertz(text, out var frequency), Is.True, text);
        Assert.Multiple(() =>
        {
            Assert.That(frequency.Kilohertz, Is.EqualTo(expectedKilohertz), text);
            Assert.That(frequency.FormatMegahertz(), Is.EqualTo(text), text);
            Assert.That(RadioFrequency.ParseMegahertz(frequency.FormatMegahertz()), Is.EqualTo(frequency), text);
        });
    }

    private static void AssertRejects(string text)
    {
        Assert.That(RadioFrequency.TryParseMegahertz(text, out _), Is.False, text);
        Assert.That(() => RadioFrequency.ParseMegahertz(text), Throws.TypeOf<FormatException>(), text);
    }

    private static void AssertReverseLookup(ANPRCFrequencyPlanSystem plan, int kilohertz, string expectedId)
    {
        Assert.That(
            plan.TryGetChannelByFrequency(RadioFrequency.FromKilohertz(kilohertz), out var channel),
            Is.True,
            expectedId);
        Assert.That(channel.Id, Is.EqualTo(expectedId));
    }

    private static void ConfigureSweep(
        ANPRCRadioComponent radio,
        RadioFrequency start,
        int kilohertzPerSecond)
    {
        radio.Enabled = true;
        radio.Planted = true;
        radio.SweepEnabled = true;
        radio.SweepPosition = start;
        radio.SweepKilohertzPerSecond = kilohertzPerSecond;
        radio.SweepChargeCostPerSecond = 0f;
        radio.SweepConfidenceDecayPerSecond = 0f;
        radio.SweepLastUpdate = TimeSpan.Zero;
        radio.SweepContacts.Clear();
    }
}
