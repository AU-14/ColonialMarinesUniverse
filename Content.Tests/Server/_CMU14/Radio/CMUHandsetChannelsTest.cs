#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Content.Server.CMU14.Radio;
using Content.Shared.Radio;
using NUnit.Framework;
using Robust.Shared.Prototypes;

namespace Content.Tests.Server.CMU14.Radio;

[TestFixture]
public sealed class CMUHandsetChannelsTest
{
    private static HashSet<string> Channels(int mask) => Enumerable.Range(0, 4)
        .Where(i => (mask & (1 << i)) != 0).Select(i => $"Channel{i}").ToHashSet();

    [Test]
    public void MatchesSetDifferenceWithoutMutatingInputs()
    {
        for (var radioMask = 0; radioMask < 16; radioMask++)
        for (var headsetMask = -1; headsetMask < 16; headsetMask++)
        for (var grantedMask = 0; grantedMask < 16; grantedMask++)
        {
            var radio = Channels(radioMask);
            var granted = Channels(grantedMask);
            var headset = headsetMask < 0 ? null : Channels(headsetMask)
                .Select(c => new ProtoId<RadioChannelPrototype>(c)).ToHashSet();
            var expected = radio.Where(c => headset == null || !headset.Contains(c)).ToHashSet();
            Assert.That(ANPRCRadioSystem.HandsetChannelsMatch(granted, radio, headset),
                Is.EqualTo(expected.SetEquals(granted)), $"Masks {radioMask}/{headsetMask}/{grantedMask}");
            Assert.That(radio.SetEquals(Channels(radioMask)), Is.True);
            Assert.That(granted.SetEquals(Channels(grantedMask)), Is.True);
            if (headset != null)
                Assert.That(headset.Select(c => c.Id).ToHashSet().SetEquals(Channels(headsetMask)), Is.True);
        }
    }

    // Explicit so routine test runs do not perform timing experiments.

}
