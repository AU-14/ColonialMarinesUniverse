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
            AssertReverseLookup(plan, 42_700, "BiomorphMimic"); // CMU14
            AssertReverseLookup(plan, 42_800, "Biomorph"); // CMU14
            AssertReverseLookup(plan, 42_900, "CMUWendigoPack"); // CMU14
            Assert.That(
                plan.TryGetChannelByFrequency(RadioFrequency.FromKilohertz(42_600), out _),
                Is.False,
                "The Hivemind carrier is deliberately excluded from ordinary reverse lookup.");
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
