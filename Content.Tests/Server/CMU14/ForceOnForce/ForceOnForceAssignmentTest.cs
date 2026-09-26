using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.CMU14.ForceOnForce;
using NUnit.Framework;

namespace Content.Tests.Server.CMU14.ForceOnForce;

[TestFixture]
public sealed class ForceOnForceAssignmentTest
{
    [Test]
    public void FlexiblePlayersCanFillBothSidesWithoutDiscardingRoles()
    {
        var slots = new[] { new ForceOnForceAssignment.Slot(0, 10), new ForceOnForceAssignment.Slot(1, 10) };
        var candidates = new List<ForceOnForceAssignment.Candidate>();
        for (var i = 0; i < 6; i++)
        {
            candidates.Add(new(i, 0, 0));
            candidates.Add(new(i, 1, 50));
        }
        var result = ForceOnForceAssignment.Assign(10, slots, candidates);
        Assert.That(result.Count, Is.EqualTo(6));
        Assert.That(result.Values.Count(x => x == 0), Is.EqualTo(3));
        Assert.That(result.Values.Count(x => x == 1), Is.EqualTo(3));
    }

    [Test]
    public void UnpopularSideDoesNotCauseAnUnbalancedRound()
    {
        var slots = new[] { new ForceOnForceAssignment.Slot(0, 20), new ForceOnForceAssignment.Slot(1, 20) };
        var candidates = Enumerable.Range(0, 20).Select(i => new ForceOnForceAssignment.Candidate(i, i == 19 ? 1 : 0, 0)).ToArray();
        var result = ForceOnForceAssignment.Assign(20, slots, candidates);
        Assert.That(result.Count, Is.EqualTo(3));
        Assert.That(result[19], Is.EqualTo(1));
    }

    [Test]
    public void MatchesExhaustiveBalancedAssignmentIncludingRoleCapacityAndPreferenceCost()
    {
        var random = new Random(74563);
        for (var sample = 0; sample < 45; sample++)
        {
            const int players = 5;
            var slots = new[] { new ForceOnForceAssignment.Slot(0, 1), new ForceOnForceAssignment.Slot(0, 2),
                new ForceOnForceAssignment.Slot(1, 1), new ForceOnForceAssignment.Slot(1, 2) };
            var candidates = new List<ForceOnForceAssignment.Candidate>();
            for (var player = 0; player < players; player++)
            for (var slot = 0; slot < slots.Length; slot++)
                if (random.Next(3) != 0) candidates.Add(new(player, slot, random.Next(5) * 50));
            var optimum = (Count: 0, Cost: 0);
            var used = new int[slots.Length];
            void Search(int player, int count, int cost, int govfor, int opfor)
            {
                if (player == players)
                {
                    if (Math.Abs(govfor - opfor) <= 1 && (count > optimum.Count || count == optimum.Count && cost < optimum.Cost))
                        optimum = (count, cost);
                    return;
                }
                Search(player + 1, count, cost, govfor, opfor);
                foreach (var candidate in candidates.Where(c => c.Player == player))
                {
                    if (used[candidate.Slot] >= slots[candidate.Slot].Capacity) continue;
                    used[candidate.Slot]++;
                    Search(player + 1, count + 1, cost + candidate.Cost,
                        govfor + (slots[candidate.Slot].Side == 0 ? 1 : 0), opfor + (slots[candidate.Slot].Side == 1 ? 1 : 0));
                    used[candidate.Slot]--;
                }
            }
            Search(0, 0, 0, 0, 0);
            var result = ForceOnForceAssignment.Assign(players, slots, candidates);
            var actualCost = candidates.Where(c => result.TryGetValue(c.Player, out var slot) && slot == c.Slot).Sum(c => c.Cost);
            Assert.That((result.Count, actualCost), Is.EqualTo(optimum), $"Sample {sample}");
            Assert.That(Math.Abs(result.Values.Count(x => slots[x].Side == 0) - result.Values.Count(x => slots[x].Side == 1)), Is.LessThanOrEqualTo(1));
        }
    }
}
