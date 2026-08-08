using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Content.Server._CMU14.Ops.ThirdParty;
using Content.Shared._CMU14.Threats;
using NUnit.Framework;

namespace Content.Tests.Server._CMU14;

[TestFixture]
public sealed class ThirdPartyScheduleTest
{
    [Test]
    public void PartitionThirdPartiesFindsInterleavedRoundStartEntries()
    {
        ThirdPartyPrototype scheduledFirst = CreateParty("ScheduledFirst", false);
        ThirdPartyPrototype roundStartFirst = CreateParty("RoundStartFirst", true);
        ThirdPartyPrototype scheduledSecond = CreateParty("ScheduledSecond", false);
        ThirdPartyPrototype roundStartSecond = CreateParty("RoundStartSecond", true);

        (List<ThirdPartyPrototype> roundStart, List<ThirdPartyPrototype> scheduled) =
            ThirdPartySystem.PartitionThirdParties(new[]
            {
                scheduledFirst,
                roundStartFirst,
                scheduledSecond,
                roundStartSecond,
            });

        Assert.Multiple(() =>
        {
            Assert.That(roundStart, Is.EqualTo(new[] { roundStartFirst, roundStartSecond }));
            Assert.That(scheduled, Is.EqualTo(new[] { scheduledFirst, scheduledSecond }));
            Assert.That(scheduled, Has.All.Matches<ThirdPartyPrototype>(party => !party.RoundStart));
        });
    }

    private static ThirdPartyPrototype CreateParty(string id, bool roundStart)
    {
        var party = (ThirdPartyPrototype) RuntimeHelpers.GetUninitializedObject(typeof(ThirdPartyPrototype));
        SetBackingField(party, nameof(ThirdPartyPrototype.ID), id);
        SetBackingField(party, nameof(ThirdPartyPrototype.RoundStart), roundStart);
        return party;
    }

    private static void SetBackingField<T>(ThirdPartyPrototype party, string property, T value)
    {
        typeof(ThirdPartyPrototype)
            .GetField($"<{property}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(party, value);
    }
}
