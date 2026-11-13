using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.CMU14.Round;
using Content.Shared.CMU14.Round;
using Content.Shared.Roles;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.CMU.Tests.Round;

[TestFixture]
public sealed class CMUDistressColonistLimitTest
{
    [TestCase(-1, 0)]
    [TestCase(0, 0)]
    [TestCase(3, 0)]
    [TestCase(4, 1)]
    [TestCase(7, 1)]
    [TestCase(8, 2)]
    [TestCase(100, 25)]
    public void CapRoundsDownFromAssignedGovfor(int govfor, int expected)
    {
        Assert.That(AddColonistRuleSystem.GetDistressColonistLimit(govfor), Is.EqualTo(expected));
    }

    [TestCase(true, 2)]
    [TestCase(false, 0)]
    public void CapOnlyAppliesToColonistAssignments(bool markersAvailable, int expectedColonists)
    {
        ProtoId<JobPrototype> govfor = "TestGovfor";
        ProtoId<JobPrototype> colonist = "TestColonist";
        ProtoId<JobPrototype> survivor = "AU14JobThirdPartyMember";
        var rule = new AddColonistRuleComponent
        {
            Jobs = new Dictionary<ProtoId<JobPrototype>, int> { [colonist] = 3 },
        };
        var assignments = new Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>();
        for (var i = 0; i < 8; i++)
            assignments.Add(new NetUserId(Guid.NewGuid()), (govfor, EntityUid.Invalid));
        for (var i = 0; i < 3; i++)
            assignments.Add(new NetUserId(Guid.NewGuid()), (colonist, EntityUid.Invalid));
        assignments.Add(new NetUserId(Guid.NewGuid()), (survivor, EntityUid.Invalid));

        var removed = AddColonistRuleSystem.LimitDistressColonistAssignments(
            assignments, rule, job => job == govfor, _ => { }, markersAvailable);

        Assert.That(removed, Is.EqualTo(3 - expectedColonists));
        Assert.That(assignments.Values.Count(value => value.Item1 == colonist), Is.EqualTo(expectedColonists));
        Assert.That(assignments.Values.Count(value => value.Item1 == govfor), Is.EqualTo(8));
        Assert.That(assignments.Values.Count(value => value.Item1 == survivor), Is.EqualTo(1));
    }
}
