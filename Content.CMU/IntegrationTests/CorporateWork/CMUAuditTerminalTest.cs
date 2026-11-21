using System.Linq;
using Content.Server.CMU14.CorporateWork;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.CorporateWork;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.CorporateWork;

[TestFixture]
[TestOf(typeof(CMUAuditTerminalSystem))]
public sealed class CMUAuditTerminalTest
{
    private const string Terminal = "CMUCorporateAuditTerminal";
    private const string Console = "AU14CorporateConsole";

    [Test]
    public async Task CasesHaveTheTierDiscrepanciesAndPayOnlyForAccuracy()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var audit = server.System<CMUAuditTerminalSystem>();
            var work = server.System<CMUCorporateWorkSystem>();
            var terminalUid = entities.SpawnEntity(Terminal, map.GridCoords);
            var consoleUid = entities.SpawnEntity(Console, map.GridCoords);
            var worker = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            try
            {
                var terminal = new Entity<CMUAuditTerminalComponent>(terminalUid, entities.GetComponent<CMUAuditTerminalComponent>(terminalUid));
                var console = entities.GetComponent<CorporateConsoleComponent>(consoleUid);

                for (var tier = 0; tier < terminal.Comp.Tiers.Count; tier++)
                {
                    terminal.Comp.NextCaseAt = TimeSpan.Zero;
                    Assert.That(audit.TryStartCase(terminal, tier, worker), Is.True);
                    var auditCase = terminal.Comp.Case!;
                    var info = terminal.Comp.Tiers[tier];
                    Assert.Multiple(() =>
                    {
                        Assert.That(auditCase.Report, Has.Count.EqualTo(info.Rows));
                        Assert.That(auditCase.Discrepancies, Has.Count.EqualTo(info.Errors), "A case had the wrong number of errors.");
                        Assert.That(auditCase.Report.Select(e => e.Item).Distinct().Count(), Is.EqualTo(info.Rows), "Report lines must be matchable by item.");
                    });

                    // Flagging every line cancels the correct flags out.
                    var all = Enumerable.Range(0, auditCase.Report.Count).ToList();
                    var budget = console.CorporateBudget;
                    Assert.That(audit.Submit(terminal, all, worker)!.Value.Payout, Is.Zero);
                    Assert.That(console.CorporateBudget, Is.EqualTo(budget));
                }

                // A perfect audit pays the full reward into the budget, and less on repeats.
                terminal.Comp.NextCaseAt = TimeSpan.Zero;
                audit.TryStartCase(terminal, 0, worker);
                var perfect = terminal.Comp.Case!.Discrepancies.ToList();
                var before = console.CorporateBudget;
                var result = audit.Submit(terminal, perfect, worker)!.Value;
                var reward = terminal.Comp.Tiers[0].Reward;
                Assert.Multiple(() =>
                {
                    Assert.That(result.Found, Is.EqualTo(perfect.Count));
                    Assert.That(result.Payout, Is.EqualTo(reward));
                    Assert.That(console.CorporateBudget, Is.EqualTo(before + reward));
                    Assert.That(work.GetPayout(worker, "audit", reward), Is.LessThan(reward), "Repeating an audit should pay less.");
                });

                // The cooldown blocks a new case straight away.
                Assert.That(audit.TryStartCase(terminal, 0, worker), Is.False);
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
}
