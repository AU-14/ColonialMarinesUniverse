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
    [TestCase(100), TestCase(250), TestCase(500), TestCase(600), Explicit]
    public void MeasureUnchangedChannelChecks(int population)
    {
        var radio = Channels(15);
        var headset = Channels(3).Select(c => new ProtoId<RadioChannelPrototype>(c)).ToHashSet();
        var granted = Channels(12);
        Func<bool> before = () =>
        {
            var copy = new HashSet<ProtoId<RadioChannelPrototype>>(headset);
            var wanted = new HashSet<string>();
            foreach (var channel in radio)
                if (!copy.Contains(channel))
                    wanted.Add(channel);
            return granted.SetEquals(wanted);
        };
        Func<bool> after = () => ANPRCRadioSystem.HandsetChannelsMatch(granted, radio, headset);
        for (var i = 0; i < 10000; i++)
        {
            before();
            after();
        }

        var checks = population * 200;
        var oldTimes = new double[7];
        var newTimes = new double[7];
        long oldBytes = 0, newBytes = 0;
        for (var block = 0; block < 7; block++)
        {
            if (block % 2 == 0)
            {
                (oldTimes[block], oldBytes) = Measure(before, checks);
                (newTimes[block], newBytes) = Measure(after, checks);
            }
            else
            {
                (newTimes[block], newBytes) = Measure(after, checks);
                (oldTimes[block], oldBytes) = Measure(before, checks);
            }
        }
        Array.Sort(oldTimes);
        Array.Sort(newTimes);
        TestContext.Out.WriteLine($"CMU_HANDSET_PERF population={population} checks={checks} " +
            $"beforeMedianMs={oldTimes[3]:F3} afterMedianMs={newTimes[3]:F3} " +
            $"beforeBytesPerCheck={oldBytes / checks} afterBytesPerCheck={newBytes / checks}");
        Assert.That(newBytes, Is.Zero);
    }

    private static (double Milliseconds, long Bytes) Measure(Func<bool> check, int count)
    {
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();
        var matches = 0;
        for (var i = 0; i < count; i++)
            if (check())
                matches++;
        var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.That(matches, Is.EqualTo(count));
        return (elapsed, bytes);
    }
}
