using System;
using System.Collections.Generic;
using Content.Shared.Chemistry.Reagent;
using NUnit.Framework;

namespace Content.Tests.Shared.Chemistry;

[TestFixture, Parallelizable, TestOf(typeof(ReagentId))]
public sealed class ReagentIdTests
{
    private const string Prototype = "SampleReagent";

    [TestCaseSource(nameof(EqualDataCases))]
    public void GetHashCode_EqualIds_EnablesHashLookup(string[] leftDna, string[] rightDna)
    {
        var left = Reagent(leftDna);
        var right = Reagent(rightDna);
        var dictionary = new Dictionary<ReagentId, int>
        {
            [left] = 1,
        };

        var found = dictionary.TryGetValue(right, out var value);

        Assert.Multiple(() =>
        {
            Assert.That(left, Is.EqualTo(right));
            Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
            Assert.That(found, Is.True);
            Assert.That(value, Is.EqualTo(1));
        });
    }

    private static IEnumerable<TestCaseData> EqualDataCases()
    {
        yield return new TestCaseData(Array.Empty<string>(), Array.Empty<string>())
            .SetName("GetHashCode_EmptyData_EnablesHashLookup");
        yield return new TestCaseData(new[] { "A" }, new[] { "A" })
            .SetName("GetHashCode_EquivalentInstances_EnablesHashLookup");
        yield return new TestCaseData(new[] { "A", "B" }, new[] { "B", "A" })
            .SetName("GetHashCode_ReorderedData_EnablesHashLookup");
        yield return new TestCaseData(new[] { "A", "A", "B" }, new[] { "A", "B", "B" })
            .SetName("GetHashCode_EquivalentDuplicateDistribution_EnablesHashLookup");
    }

    private static ReagentId Reagent(params string[] dna)
    {
        var data = new List<ReagentData>(dna.Length);
        foreach (var value in dna)
        {
            data.Add(new DnaData { DNA = value });
        }

        return new ReagentId(Prototype, data);
    }
}
