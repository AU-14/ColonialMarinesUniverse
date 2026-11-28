using System.Linq;
using Content.Server.CMU14.CorporateWork;
using Content.Shared.CMU14.CorporateWork;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.CorporateWork;

/// <summary>
///     The review desks and supplier negotiation: every case can be played, right answers pay in
///     full, guessing doesn't, and the supplier keeps to its limits.
/// </summary>
[TestFixture]
[TestOf(typeof(CMUReviewDeskSystem))]
[TestOf(typeof(CMUNegotiationTerminalSystem))]
public sealed class CMUCorporateDesksTest
{
    [TestCase("CMUCorporateClaimsTerminal")]
    [TestCase("CMUCorporateCommsTerminal")]
    [TestCase("CMUCorporateHiringTerminal")]
    [TestCase("CMUCorporateClearanceTerminal")]
    public async Task DeskPaysForRightDecisionsAndNothingForGuessing(string terminalId)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var desks = server.System<CMUReviewDeskSystem>();
            var terminalUid = entities.SpawnEntity(terminalId, map.GridCoords);
            var consoleUid = entities.SpawnEntity("AU14CorporateConsole", map.GridCoords);
            var workers = new List<EntityUid>();
            try
            {
                var terminal = new Entity<CMUReviewDeskComponent>(terminalUid, entities.GetComponent<CMUReviewDeskComponent>(terminalUid));
                Assert.That(terminal.Comp.Options, Has.Count.EqualTo(2));

                for (var tierIndex = 0; tierIndex < terminal.Comp.Tiers.Count; tierIndex++)
                {
                    var tier = terminal.Comp.Tiers[tierIndex];

                    // A fresh worker each time, so repeat-work reductions don't apply.
                    var worker = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                    workers.Add(worker);

                    terminal.Comp.NextCaseAt = TimeSpan.Zero;
                    Assert.That(desks.TryStartCase(terminal, tierIndex, worker), Is.True);
                    var deskCase = terminal.Comp.Case!;
                    Assert.Multiple(() =>
                    {
                        Assert.That(deskCase.Items, Has.Count.EqualTo(tier.Items));
                        Assert.That(deskCase.Rules, Has.Count.EqualTo(tier.Rules));
                        Assert.That(deskCase.Items.All(i => i.Lines.Count > 0 && !string.IsNullOrWhiteSpace(i.Title)), Is.True);
                        Assert.That(deskCase.Answers.Distinct().Count(), Is.EqualTo(2), "Every document in the queue had the same answer.");
                    });

                    var perfect = desks.Submit(terminal, deskCase.Answers.ToArray(), worker)!.Value;
                    Assert.Multiple(() =>
                    {
                        Assert.That(perfect.Correct, Is.EqualTo(tier.Items));
                        Assert.That(perfect.Payout, Is.EqualTo(tier.Reward), "A perfect desk didn't pay the full reward.");
                    });
                }

                // Answering everything the same way can't do better than break even.
                var guesser = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                workers.Add(guesser);
                terminal.Comp.NextCaseAt = TimeSpan.Zero;
                desks.TryStartCase(terminal, terminal.Comp.Tiers.Count - 1, guesser);
                var answers = terminal.Comp.Case!.Answers;
                var flipped = answers.Select(a => 1 - a).ToArray();
                Assert.That(desks.Submit(terminal, flipped, guesser)!.Value.Payout, Is.Zero, "Every answer wrong still paid.");

                terminal.Comp.NextCaseAt = TimeSpan.Zero;
                desks.TryStartCase(terminal, 0, guesser);
                var none = Enumerable.Repeat(-1, terminal.Comp.Case!.Answers.Count).ToArray();
                Assert.That(desks.Submit(terminal, none, guesser)!.Value.Payout, Is.Zero, "Deciding nothing still paid.");
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

    [Test]
    public async Task NegotiationKeepsToTheSuppliersLimits()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var negotiation = server.System<CMUNegotiationTerminalSystem>();
            var terminalUid = entities.SpawnEntity("CMUCorporateNegotiationTerminal", map.GridCoords);
            var consoleUid = entities.SpawnEntity("AU14CorporateConsole", map.GridCoords);
            var workers = new List<EntityUid>();
            EntityUid Worker()
            {
                var worker = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                workers.Add(worker);
                return worker;
            }

            try
            {
                var terminal = new Entity<CMUNegotiationTerminalComponent>(terminalUid, entities.GetComponent<CMUNegotiationTerminalComponent>(terminalUid));
                var tier = terminal.Comp.Tiers[0];

                // Fair offers bring the ask down, but never below the walk-away price.
                var haggler = Worker();
                Assert.That(negotiation.TryStartCase(terminal, 0, haggler), Is.True);
                var deal = terminal.Comp.Case!;
                var floor = deal.Floor;
                Assert.That(floor, Is.LessThan(deal.ListPrice));
                var reply = negotiation.Offer(terminal, floor, haggler);
                Assert.Multiple(() =>
                {
                    Assert.That(reply, Is.EqualTo(CMUNegotiationReply.Counter));
                    Assert.That(deal.Ask, Is.LessThan(deal.ListPrice).And.AtLeast(floor));
                });

                // Closing at the walk-away price pays the full reward.
                negotiation.Close(terminal, floor, haggler);
                Assert.That(terminal.Comp.LastResult!.Value.Payout, Is.EqualTo(tier.Reward));

                // Paying the list price earns nothing.
                var pushover = Worker();
                terminal.Comp.NextCaseAt = TimeSpan.Zero;
                negotiation.TryStartCase(terminal, 0, pushover);
                Assert.That(negotiation.Offer(terminal, terminal.Comp.Case!.ListPrice, pushover), Is.EqualTo(CMUNegotiationReply.Accepted));
                Assert.That(terminal.Comp.LastResult!.Value.Payout, Is.Zero);

                // Lowballing insults the supplier and, kept up, drives them away for nothing.
                var lowballer = Worker();
                terminal.Comp.NextCaseAt = TimeSpan.Zero;
                negotiation.TryStartCase(terminal, 0, lowballer);
                var patience = terminal.Comp.Case!.Patience;
                Assert.That(negotiation.Offer(terminal, 10, lowballer), Is.EqualTo(CMUNegotiationReply.Insulted));
                Assert.That(terminal.Comp.Case!.Patience, Is.EqualTo(patience - 2), "An insult didn't cost extra patience.");
                CMUNegotiationReply? last = null;
                for (var i = 0; i < patience && terminal.Comp.Case != null; i++)
                    last = negotiation.Offer(terminal, 10, lowballer);

                Assert.Multiple(() =>
                {
                    Assert.That(last, Is.EqualTo(CMUNegotiationReply.WalkedAway));
                    Assert.That(terminal.Comp.Case, Is.Null);
                    Assert.That(terminal.Comp.LastResult!.Value.Price, Is.Null);
                    Assert.That(terminal.Comp.LastResult!.Value.Payout, Is.Zero);
                });
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
}
