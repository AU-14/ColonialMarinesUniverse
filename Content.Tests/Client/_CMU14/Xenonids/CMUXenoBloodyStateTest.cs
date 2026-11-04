using System;
using Content.Client._RMC14.Xenonids;
using NUnit.Framework;

namespace Content.Tests.Client.CMU14.Xenonids;

[TestFixture]
public sealed class CMUXenoBloodyStateTest
{
    [TestCase("alive")]
    [TestCase("running")]
    [TestCase("crit")]
    [TestCase("dead")]
    [TestCase("sleeping")]
    [TestCase("thrown")]
    [TestCase("fortify")]
    [TestCase("crest")]
    [TestCase("burrowed")]
    [TestCase("custom-state")]
    public void KeepsTheRsiSuffixContract(string state)
        => Assert.That(XenoVisualizerSystem.GetBloodyState(state), Is.EqualTo(state + "_bloody"));

    [Test]
    public void FixedVisualUpdatesDoNotAllocateStateNames()
    {
        string[] states = ["alive", "running", "crit", "dead", "sleeping", "thrown", "fortify", "crest", "burrowed"];
        string last = null;
        for (var i = 0; i < 10000; i++)
            last = XenoVisualizerSystem.GetBloodyState(states[i % states.Length]);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 90000; i++)
            last = XenoVisualizerSystem.GetBloodyState(states[i % states.Length]);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TestContext.Progress.WriteLine($"CMU_XENO_BLOODY_STATE updates=90000 allocated={allocated}");
        Assert.That(last, Is.EqualTo("burrowed_bloody"));
        Assert.That(allocated, Is.Zero);
    }
}
