using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Shared.CMU14.Expeditions;
using NUnit.Framework;
using F = Content.Shared.CMU14.Expeditions.CMUTacticalFact;
using A = Content.Shared.CMU14.Expeditions.CMUTacticalAction;

namespace Content.Tests.Shared._CMU14.Expeditions;

[TestFixture]
public sealed class CMUTacticalPlannerTest
{
    [Test]
    public void PlansCoverBeforeReloadAndAttackAndRejectsMissingSupplies()
    {
        var actions = new[]
        {
            new CMUTacticalOperator(A.TakeCover, F.CoverAvailable, F.None, F.Safe, F.None, 2),
            new CMUTacticalOperator(A.Reload, F.Safe | F.SpareAmmo, F.None, F.Armed, F.SpareAmmo, 3),
            new CMUTacticalOperator(A.Attack, F.Contact | F.Armed, F.None, F.Engaged, F.None, 1),
        };
        var result = CMUTacticalPlanner.Plan(F.CoverAvailable | F.Contact | F.SpareAmmo, F.Engaged, F.None, actions, out var expanded);
        Assert.That(result, Is.EqualTo(new[] { A.TakeCover, A.Reload, A.Attack }));
        Assert.That(expanded, Is.LessThanOrEqualTo(128));
        Assert.That(CMUTacticalPlanner.Plan(F.CoverAvailable | F.Contact, F.Engaged, F.None, actions, out _), Is.Null);
        Assert.That(CMUTacticalPlanner.Plan(F.CoverAvailable | F.Contact | F.SpareAmmo, F.Engaged, F.None, actions, out _, 1), Is.Null);
    }

    [Test]
    public void ChoosesCheapestValidSequenceAndHonorsNegativePreconditions()
    {
        var actions = new[]
        {
            new CMUTacticalOperator(A.Flank, F.Contact, F.Wounded, F.Engaged, F.None, 2),
            new CMUTacticalOperator(A.TakeCover, F.None, F.None, F.Safe, F.None, 1),
            new CMUTacticalOperator(A.Treat, F.Safe | F.Wounded, F.None, F.None, F.Wounded, 3),
            new CMUTacticalOperator(A.Attack, F.Contact, F.None, F.Engaged, F.None, 9),
        };
        Assert.That(CMUTacticalPlanner.Plan(F.Contact, F.Engaged, F.None, actions, out _), Is.EqualTo(new[] { A.Flank }));
        Assert.That(CMUTacticalPlanner.Plan(F.Contact | F.Wounded, F.Engaged, F.None, actions, out _),
            Is.EqualTo(new[] { A.TakeCover, A.Treat, A.Flank }));
        Assert.That(CMUTacticalPlanner.Plan(F.Safe | F.Wounded, F.Safe, F.Wounded, actions, out _), Is.EqualTo(new[] { A.Treat }));
    }

    [Test]
    public void RouteDetoursAroundExposureButCannotCrossWaterWallsOrRowBoundaries()
    {
        const int size = 9;
        var start = 4 * size + 1;
        var end = 4 * size + 7;
        bool Walkable(int i) => i % size > 0 && i % size < 8 && i / size > 0 && i / size < 8;
        var direct = CMUTacticalRoute.Find(size, start, end, Walkable, _ => 0, (_, _) => true, out _);
        var safer = CMUTacticalRoute.Find(size, start, end, Walkable, i => i / size == 4 && i % size is > 1 and < 7 ? 8 : 0,
            (_, _) => true, out var expanded);
        Assert.That(direct, Has.Count.EqualTo(7));
        Assert.That(safer, Has.Count.GreaterThan(direct!.Count));
        Assert.That(safer!.Skip(1).SkipLast(1).All(i => i / size != 4), Is.True);
        Assert.That(expanded, Is.LessThanOrEqualTo(256));
        Assert.That(CMUTacticalRoute.Find(size, start, end, i => Walkable(i) && i % size != 4, _ => 0, (_, _) => true, out _), Is.Null);
        Assert.That(CMUTacticalRoute.Find(size, start, end, Walkable, _ => 0, (_, b) => b % size != 4, out _), Is.Null);
        Assert.That(CMUTacticalRoute.Find(size, start, end, Walkable, _ => 0, (_, _) => true, out _, 1), Is.Null);
    }

    [Test]
    public void SavedOutcomesChangeOnlyBoundedTacticalWeights()
    {
        var experience = new CMUTacticalExperience();
        for (var i = 0; i < 100; i++)
        {
            experience.Observe(true, true);
            experience.Observe(false, false);
        }
        var saved = JsonSerializer.Serialize(experience);
        var nextRound = JsonSerializer.Deserialize<CMUTacticalExperience>(saved)!;
        Assert.That(nextRound.FlankCost, Is.InRange(0.75f, 0.8f));
        Assert.That(nextRound.DangerCost, Is.InRange(1.2f, 1.25f));
        Assert.That(nextRound.Samples, Is.EqualTo(200));
        for (var i = 0; i < 100; i++)
            nextRound.Observe(true, false);
        Assert.That(nextRound.FlankCost, Is.InRange(1.2f, 1.25f), "Recent failed flanks must reverse a formerly successful preference.");
    }
}
