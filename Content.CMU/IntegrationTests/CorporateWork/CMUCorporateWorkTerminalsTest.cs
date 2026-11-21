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
    [TestCase(new[] { 0, 0, 1, 1 }, new[] { 0, 0, 1, 1 }, 4, 0)]
    [TestCase(new[] { 0, 1, 2, 3 }, new[] { 3, 2, 1, 0 }, 0, 4)]
    [TestCase(new[] { 0, 0, 1, 2 }, new[] { 0, 3, 0, 0 }, 1, 1)]
    [TestCase(new[] { 1, 1, 1, 1 }, new[] { 2, 2, 2, 2 }, 0, 0)]
    public void TransmissionsScoringCountsRepeatsOnce(int[] key, int[] guess, int exact, int misplaced)
    {
        var attempt = CMUTransmissionsTerminalSystem.Score(key, guess, 6);
        Assert.Multiple(() =>
        {
            Assert.That(attempt.Exact, Is.EqualTo(exact));
            Assert.That(attempt.Misplaced, Is.EqualTo(misplaced));
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

    [Test]
    public async Task TransmissionsPaysFullOnFirstTryAndNothingOnFailure()
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
            var worker = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            try
            {
                var terminal = new Entity<CMUTransmissionsTerminalComponent>(terminalUid, entities.GetComponent<CMUTransmissionsTerminalComponent>(terminalUid));
                var tier = terminal.Comp.Tiers[0];

                Assert.That(transmissions.TryStartCase(terminal, 0, worker), Is.True);
                var key = terminal.Comp.Case!.Key;
                Assert.That(key, Has.Length.EqualTo(tier.Length));
                transmissions.Guess(terminal, key, worker);
                Assert.Multiple(() =>
                {
                    Assert.That(terminal.Comp.Case, Is.Null, "A correct key didn't close the case.");
                    Assert.That(terminal.Comp.LastResult!.Value.Solved, Is.True);
                    Assert.That(terminal.Comp.LastResult!.Value.Payout, Is.EqualTo(tier.Reward));
                });

                // Wrong guesses until the attempts run out.
                terminal.Comp.NextCaseAt = TimeSpan.Zero;
                transmissions.TryStartCase(terminal, 0, worker);
                var wrong = terminal.Comp.Case!.Key.Select(s => (s + 1) % tier.Symbols).ToArray();
                for (var i = 0; i < tier.Attempts; i++)
                {
                    transmissions.Guess(terminal, wrong, worker);
                }

                Assert.Multiple(() =>
                {
                    Assert.That(terminal.Comp.Case, Is.Null, "Running out of attempts didn't close the case.");
                    Assert.That(terminal.Comp.LastResult!.Value.Solved, Is.False);
                    Assert.That(terminal.Comp.LastResult!.Value.Payout, Is.Zero);
                });
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
