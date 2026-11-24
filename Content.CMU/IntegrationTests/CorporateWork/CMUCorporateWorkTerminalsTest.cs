using System.Linq;
using Content.Server.CMU14.CorporateWork;
using Content.Shared.CMU14.CorporateWork;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.CorporateWork;

[TestFixture]
[TestOf(typeof(CMULogisticsTerminalSystem))]
[TestOf(typeof(CMUTransmissionsTerminalSystem))]
public sealed class CMUCorporateWorkTerminalsTest
{
    /// <summary>
    ///     First pick from the top row, then alternately the last pick's column and row, never a cell twice.
    /// </summary>
    [Test]
    public void TransmissionsPicksFollowTheRowColumnRule()
    {
        const int size = 5;
        var picks = new List<int>();
        Assert.Multiple(() =>
        {
            Assert.That(CMUTransmissionsRules.CanPick(size, picks, 2), Is.True, "The top row is open first.");
            Assert.That(CMUTransmissionsRules.CanPick(size, picks, size + 2), Is.False, "Only the top row is open first.");
        });

        picks.Add(2); // row 0, column 2
        Assert.Multiple(() =>
        {
            Assert.That(CMUTransmissionsRules.CanPick(size, picks, 3 * size + 2), Is.True, "The second pick stays in the column.");
            Assert.That(CMUTransmissionsRules.CanPick(size, picks, 3), Is.False, "The second pick can't stay in the row.");
            Assert.That(CMUTransmissionsRules.CanPick(size, picks, 2), Is.False, "A cell can't be picked twice.");
        });

        picks.Add(3 * size + 2); // row 3, column 2
        Assert.Multiple(() =>
        {
            Assert.That(CMUTransmissionsRules.CanPick(size, picks, 3 * size + 4), Is.True, "The third pick stays in the row.");
            Assert.That(CMUTransmissionsRules.CanPick(size, picks, 4 * size + 2), Is.False, "The third pick can't stay in the column.");
        });
    }

    [Test]
    public void TransmissionsSequencesMustBeUnbroken()
    {
        var buffer = new List<int> { 4, 1, 2, 3, 1 };
        Assert.Multiple(() =>
        {
            Assert.That(CMUTransmissionsRules.Contains(buffer, new[] { 1, 2, 3 }), Is.True);
            Assert.That(CMUTransmissionsRules.Contains(buffer, new[] { 4, 2 }), Is.False, "A broken sequence counted.");
            Assert.That(CMUTransmissionsRules.Contains(buffer, new[] { 3, 1, 5 }), Is.False, "A sequence past the buffer counted.");
        });
    }

    [Test]
    public async Task LogisticsPaysForTheBestLoadAndRefusesOverloads()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var logistics = server.System<CMULogisticsTerminalSystem>();
            var terminalUid = entities.SpawnEntity("CMUCorporateLogisticsTerminal", map.GridCoords);
            var consoleUid = entities.SpawnEntity("AU14CorporateConsole", map.GridCoords);
            var worker = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            try
            {
                var terminal = new Entity<CMULogisticsTerminalComponent>(terminalUid, entities.GetComponent<CMULogisticsTerminalComponent>(terminalUid));
                for (var tier = 0; tier < terminal.Comp.Tiers.Count; tier++)
                {
                    terminal.Comp.NextCaseAt = TimeSpan.Zero;
                    Assert.That(logistics.TryStartCase(terminal, tier, worker), Is.True);
                    var manifest = terminal.Comp.Case!;
                    Assert.That(manifest.Crates, Has.Count.EqualTo(terminal.Comp.Tiers[tier].Crates));

                    // Loading everything is always over at least one limit.
                    var everything = Enumerable.Range(0, manifest.Crates.Count).ToList();
                    Assert.That(logistics.Submit(terminal, everything, worker), Is.Null, "An overloaded shuttle was accepted.");
                    Assert.That(terminal.Comp.Case, Is.Not.Null, "A refused load closed the manifest.");

                    var best = BestLoad(manifest);
                    var result = logistics.Submit(terminal, best, worker)!.Value;
                    Assert.Multiple(() =>
                    {
                        Assert.That(result.Value, Is.EqualTo(manifest.BestValue));
                        Assert.That(result.Payout, Is.GreaterThan(0));
                        Assert.That(result.Payout, Is.LessThanOrEqualTo(terminal.Comp.Tiers[tier].Reward));
                    });
                }

                // An empty shuttle is under the minimum and pays nothing.
                terminal.Comp.NextCaseAt = TimeSpan.Zero;
                logistics.TryStartCase(terminal, 0, worker);
                Assert.That(logistics.Submit(terminal, new List<int>(), worker)!.Value.Payout, Is.Zero);
            }
            finally
            {
                entities.DeleteEntity(terminalUid);
                entities.DeleteEntity(consoleUid);
                entities.DeleteEntity(worker);
            }
        });
        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Every tier can be solved: following the path the grid was built around uploads every target
    ///     and pays the full reward. Picks that break the rules are refused, and uploading an empty
    ///     buffer pays nothing.
    /// </summary>
    [Test]
    public async Task TransmissionsEveryTierCanBeSolved()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var transmissions = server.System<CMUTransmissionsTerminalSystem>();
            var terminalUid = entities.SpawnEntity("CMUCorporateTransmissionsTerminal", map.GridCoords);
            var consoleUid = entities.SpawnEntity("AU14CorporateConsole", map.GridCoords);
            var workers = new List<EntityUid>();
            try
            {
                var terminal = new Entity<CMUTransmissionsTerminalComponent>(terminalUid, entities.GetComponent<CMUTransmissionsTerminalComponent>(terminalUid));
                for (var tierIndex = 0; tierIndex < terminal.Comp.Tiers.Count; tierIndex++)
                {
                    // A fresh worker each time, so repeat-work reductions don't apply.
                    var worker = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                    workers.Add(worker);
                    var tier = terminal.Comp.Tiers[tierIndex];

                    terminal.Comp.NextCaseAt = TimeSpan.Zero;
                    Assert.That(transmissions.TryStartCase(terminal, tierIndex, worker), Is.True);
                    var transmission = terminal.Comp.Case!;
                    Assert.That(transmission.Sequences, Has.Count.EqualTo(tier.Sequences.Count));

                    var illegal = Enumerable.Range(0, tier.Size * tier.Size).First(cell => cell / tier.Size != 0);
                    Assert.That(transmissions.Pick(terminal, illegal, worker), Is.False, "A pick off the top row was allowed first.");

                    foreach (var cell in transmission.Solution)
                        Assert.That(transmissions.Pick(terminal, cell, worker), Is.True, "The grid's own path broke the rules.");

                    Assert.Multiple(() =>
                    {
                        Assert.That(terminal.Comp.Case, Is.Null, "A full buffer didn't close the case.");
                        Assert.That(terminal.Comp.LastResult!.Value.Completed, Is.EqualTo(tier.Sequences.Count), $"Tier {tierIndex}'s path didn't upload every target.");
                        Assert.That(terminal.Comp.LastResult!.Value.Payout, Is.EqualTo(tier.Reward));
                    });
                }

                // Uploading with nothing in the buffer pays nothing.
                terminal.Comp.NextCaseAt = TimeSpan.Zero;
                Assert.That(transmissions.TryStartCase(terminal, 0, workers[0]), Is.True);
                transmissions.Finish(terminal, timedOut: false);
                Assert.That(terminal.Comp.LastResult!.Value.Payout, Is.Zero);
            }
            finally
            {
                entities.DeleteEntity(terminalUid);
                entities.DeleteEntity(consoleUid);
                foreach (var worker in workers)
                    entities.DeleteEntity(worker);
            }
        });
        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A timed tier closes on its own once time runs out, and recovers nothing.
    /// </summary>
    [Test]
    public async Task TransmissionsTimedTierClosesWhenTimeRunsOut()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var transmissions = server.System<CMUTransmissionsTerminalSystem>();
        EntityUid terminalUid = default, consoleUid = default, worker = default;
        Entity<CMUTransmissionsTerminalComponent> terminal = default;
        CMUTransmissionsTier? timed = null;

        await server.WaitAssertion(() =>
        {
            terminalUid = entities.SpawnEntity("CMUCorporateTransmissionsTerminal", map.GridCoords);
            consoleUid = entities.SpawnEntity("AU14CorporateConsole", map.GridCoords);
            worker = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            terminal = (terminalUid, entities.GetComponent<CMUTransmissionsTerminalComponent>(terminalUid));

            var tierIndex = terminal.Comp.Tiers.FindIndex(t => t.TimeLimit != null);
            Assert.That(tierIndex, Is.Not.Negative, "No tier has a time limit.");
            timed = terminal.Comp.Tiers[tierIndex];

            Assert.That(transmissions.TryStartCase(terminal, tierIndex, worker), Is.True);
            Assert.That(terminal.Comp.Case!.Deadline, Is.Null, "The clock started before the first pick.");
            transmissions.Pick(terminal, terminal.Comp.Case!.Solution[0], worker);
            Assert.That(terminal.Comp.Case!.Deadline, Is.Not.Null, "The first pick didn't start the clock.");
        });

        await server.WaitRunTicks((int) ((timed!.TimeLimit!.Value.TotalSeconds + 1) / server.Timing.TickPeriod.TotalSeconds));
        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(terminal.Comp.Case, Is.Null, "The case stayed open past its time limit.");
                Assert.That(terminal.Comp.LastResult!.Value.TimedOut, Is.True);
                Assert.That(terminal.Comp.LastResult!.Value.Payout, Is.Zero);
            });

            entities.DeleteEntity(terminalUid);
            entities.DeleteEntity(consoleUid);
            entities.DeleteEntity(worker);
        });
        await pair.CleanReturnAsync();
    }

    private static List<int> BestLoad(CMULogisticsCase manifest)
    {
        var crates = manifest.Crates;
        var bestMask = 0;
        var bestValue = -1;
        for (var mask = 0; mask < 1 << crates.Count; mask++)
        {
            var picked = Enumerable.Range(0, crates.Count).Where(i => (mask & (1 << i)) != 0).ToList();
            if (picked.Sum(i => crates[i].Weight) > manifest.WeightLimit || picked.Sum(i => crates[i].Hazard) > manifest.HazardLimit)
                continue;

            var value = picked.Sum(i => crates[i].Value);
            if (value > bestValue)
            {
                bestValue = value;
                bestMask = mask;
            }
        }

        return Enumerable.Range(0, crates.Count).Where(i => (bestMask & (1 << i)) != 0).ToList();
    }
}
