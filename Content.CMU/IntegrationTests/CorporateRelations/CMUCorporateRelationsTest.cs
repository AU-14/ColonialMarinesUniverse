using System.Linq;
using Content.Server.CMU14.Ambassador;
using Content.Server.CMU14.ColonyEconomy;
using Content.Server.CMU14.CorporateRelations;
using Content.Server.CMU14.Ops.ThirdParty;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared.CMU14.CorporateRelations;
using Content.Shared.GameTicking;
using Content.Shared.Tag;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.CorporateRelations;

[TestFixture]
[TestOf(typeof(CMUCorporateRelationsSystem))]
public sealed class CMUCorporateRelationsTest
{
    private const string Faction = "UPP";

    [Test]
    public async Task LoansRentWorkOrdersAndWithdrawalMoveTheRightMoney()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var relations = server.System<CMUCorporateRelationsSystem>();
        var corporate = server.System<CorporateConsoleSystem>();
        var colony = server.System<ColonyBudgetSystem>();
        var ambassador = server.System<AmbassadorConsoleSystem>();

        EntityUid adminConsole = default, corporateConsole = default, ambassadorConsole = default, user = default;
        await server.WaitPost(() =>
        {
            entities.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            adminConsole = entities.SpawnEntity("AU14AdminConsole", map.GridCoords);
            corporateConsole = entities.SpawnEntity("AU14CorporateConsole", map.GridCoords);
            ambassadorConsole = entities.SpawnEntity("AU14AmbassadorConsoleUPP", map.GridCoords);
            user = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            corporate.AddToCorporateBudget(10000 - corporate.GetCorporateBudget());
            colony.SetBudget(5000);
        });

        // The first tick with a colony starts the cycle and its existing debt.
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(relations.GetOwed(CMUCorporateRelationsSystem.ColonyBorrower), Is.EqualTo(CMUCorporateRelationsSystem.StartingDebtOwed));
            var ambassadorStart = ambassador.GetFactionBudget(Faction);

            // UPP asks for $2000, WY counters at 20%, UPP accepts.
            Raise(ambassadorConsole, new CMUContractsRequestLoanBuiMsg(2000));
            Raise(corporateConsole, new CMUContractsCounterLoanBuiMsg(Faction, 2000, 20));
            Raise(ambassadorConsole, new CMUContractsAnswerLoanBuiMsg(Faction, true));
            Assert.Multiple(() =>
            {
                Assert.That(ambassador.GetFactionBudget(Faction), Is.EqualTo(ambassadorStart + 2000));
                Assert.That(corporate.GetCorporateBudget(), Is.EqualTo(8000));
                Assert.That(relations.GetOwed(Faction), Is.EqualTo(2400));
            });

            // A work order for 10 steel ore at +20%: $20 held, then paid to the colony on delivery
            // while WY takes half the delivery's value as resale margin.
            Raise(corporateConsole, new CMUContractsPostWorkOrderBuiMsg("CMUWorkOrderSteelOre", 10, 20));
            Assert.That(corporate.GetCorporateBudget(), Is.EqualTo(7980));
            var ore = entities.SpawnEntity(null, map.GridCoords);
            server.System<TagSystem>().AddTag(ore, "SteelOre");
            relations.OnDelivery(ore, 10, 100);
            Assert.Multiple(() =>
            {
                Assert.That(colony.GetBudget(), Is.EqualTo(5020));
                Assert.That(corporate.GetCorporateBudget(), Is.EqualTo(8030));
            });
            entities.DeleteEntity(ore);

            // Rent and instalments: $500 rent to the colony, a third of each loan back to WY ($575 of the
            // colony's $1725 starting debt, $800 of UPP's $2400).
            Raise(adminConsole, new CMUContractsSetRentBuiMsg(500));
            relations.RunCycle();
            Assert.Multiple(() =>
            {
                Assert.That(colony.GetBudget(), Is.EqualTo(5020 + 500 - 575));
                Assert.That(corporate.GetCorporateBudget(), Is.EqualTo(8030 - 500 + 575 + 800));
                Assert.That(relations.GetOwed(CMUCorporateRelationsSystem.ColonyBorrower), Is.EqualTo(1150));
                Assert.That(relations.GetOwed(Faction), Is.EqualTo(1600));
            });

            // With nothing in the bank UPP misses two instalments, each charging its 20% interest again.
            // The third miss in a row is collected by force, into the red: $1600 -> 1920 -> 2304, then $1152 taken.
            ambassador.AdjustFactionBudget(Faction, -ambassador.GetFactionBudget(Faction));
            relations.RunCycle();
            Assert.That(relations.GetOwed(Faction), Is.EqualTo(1920));
            relations.RunCycle();
            Assert.That(relations.GetOwed(Faction), Is.EqualTo(2304));
            relations.RunCycle();
            Assert.Multiple(() =>
            {
                Assert.That(relations.GetOwed(Faction), Is.EqualTo(1152), "The third miss wasn't collected by force.");
                Assert.That(ambassador.GetFactionBudget(Faction), Is.EqualTo(-1152));
            });
            ambassador.AdjustFactionBudget(Faction, 1152);

            // Revoking WY's research licence stops its research terminals opening; the colony's still do.
            var corporateTerminal = entities.SpawnEntity("CMUResearchDataTerminalCorporate", map.GridCoords);
            var colonyTerminal = entities.SpawnEntity("CMUResearchDataTerminalColony", map.GridCoords);
            Raise(adminConsole, new CMUContractsSetResearchLicenseBuiMsg(false));
            Assert.Multiple(() =>
            {
                Assert.That(OpenCancelled(corporateTerminal), Is.True, "WY could still research without a licence.");
                Assert.That(OpenCancelled(colonyTerminal), Is.False, "Revoking WY's licence blocked colony research.");
            });
            Raise(adminConsole, new CMUContractsSetResearchLicenseBuiMsg(true));
            Assert.That(OpenCancelled(corporateTerminal), Is.False);
            entities.DeleteEntity(corporateTerminal);
            entities.DeleteEntity(colonyTerminal);

            // Only WYC5 (the Liaison) and above can order the withdrawal.
            server.System<SharedRankSystem>().SetRank(user, "RMCRankWeYaSeniorExecutive");
            Raise(corporateConsole, new CMUContractsWithdrawalBuiMsg(true));
            Assert.That(relations.Withdrawn, Is.False);
            var thirdParties = server.System<ThirdPartySystem>();
            Assert.That(thirdParties.GetQueuedThirdParties().Any(p => p.ID == CMUCorporateRelationsSystem.WithdrawalTeam), Is.False,
                "A WYC4 called the withdrawal shuttle.");

            // A WYC5 starts the countdown, which calls the company shuttle to collect corporate staff.
            server.System<SharedRankSystem>().SetRank(user, "RMCRankWeYaExecutiveSpecialist");
            Raise(corporateConsole, new CMUContractsWithdrawalBuiMsg(true));
            Assert.That(thirdParties.GetQueuedThirdParties().Count(p => p.ID == CMUCorporateRelationsSystem.WithdrawalTeam), Is.EqualTo(1));

            // Once ordered it can't be called off, and asking again doesn't call a second shuttle.
            Raise(corporateConsole, new CMUContractsWithdrawalBuiMsg(false));
            Assert.That(relations.WithdrawalPending, Is.True, "The withdrawal was called off.");
            Raise(corporateConsole, new CMUContractsWithdrawalBuiMsg(true));
            Assert.That(thirdParties.GetQueuedThirdParties().Count(p => p.ID == CMUCorporateRelationsSystem.WithdrawalTeam), Is.EqualTo(1));

            // Withdrawal collects every loan in full on the spot, even into debt, and turns the
            // colony's prices up. UPP has spent everything, so it goes $1152 into the red.
            ambassador.AdjustFactionBudget(Faction, -ambassador.GetFactionBudget(Faction));
            relations.Withdraw();
            Assert.Multiple(() =>
            {
                Assert.That(relations.Withdrawn, Is.True);
                Assert.That(relations.GetOwed(CMUCorporateRelationsSystem.ColonyBorrower), Is.Zero);
                Assert.That(relations.GetOwed(Faction), Is.Zero);
                Assert.That(ambassador.GetFactionBudget(Faction), Is.EqualTo(-1152));
                Assert.That(relations.ColonyPriceMultiplier, Is.EqualTo(1.25f));
                Assert.That(relations.ColonyPayoutMultiplier, Is.EqualTo(0.75f));
            });

            entities.DeleteEntity(adminConsole);
            entities.DeleteEntity(corporateConsole);
            entities.DeleteEntity(ambassadorConsole);
            entities.DeleteEntity(user);
            entities.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
        });

        await pair.CleanReturnAsync();

        bool OpenCancelled(EntityUid terminal)
        {
            var attempt = new ActivatableUIOpenAttemptEvent(user, true);
            entities.EventBus.RaiseLocalEvent(terminal, attempt);
            return attempt.Cancelled;
        }

        void Raise(EntityUid console, BoundUserInterfaceMessage message)
        {
            message.Actor = user;
            // As object, so the bus dispatches on the message's own type.
            entities.EventBus.RaiseLocalEvent(console, (object) message);
        }
    }

    [Test]
    public async Task WithdrawalCountdownCollectsTheDebts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var relations = server.System<CMUCorporateRelationsSystem>();
        var colony = server.System<ColonyBudgetSystem>();

        EntityUid corporateConsole = default, user = default;
        await server.WaitPost(() =>
        {
            entities.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            entities.SpawnEntity("AU14AdminConsole", map.GridCoords);
            corporateConsole = entities.SpawnEntity("AU14CorporateConsole", map.GridCoords);
            user = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            colony.SetBudget(500);
        });
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(relations.GetOwed(CMUCorporateRelationsSystem.ColonyBorrower), Is.EqualTo(CMUCorporateRelationsSystem.StartingDebtOwed));
            server.System<SharedRankSystem>().SetRank(user, "RMCRankWeYaExecutiveSupervisor");
            entities.EventBus.RaiseLocalEvent(corporateConsole, (object) new CMUContractsWithdrawalBuiMsg(true) { Actor = user });
            Assert.That(relations.WithdrawalPending, Is.True);
        });

        // Let the whole countdown run out on its own.
        var ticks = (int) ((CMUCorporateRelationsSystem.WithdrawalCountdown.TotalSeconds + 2) / server.Timing.TickPeriod.TotalSeconds);
        await server.WaitRunTicks(ticks);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(relations.Withdrawn, Is.True, "The countdown ran out without a withdrawal.");
                Assert.That(relations.GetOwed(CMUCorporateRelationsSystem.ColonyBorrower), Is.Zero, "The colony's debt wasn't collected.");
                Assert.That(colony.GetBudget(), Is.EqualTo(500 - CMUCorporateRelationsSystem.StartingDebtOwed).Within(1), "The debt wasn't forced into the red.");
            });
        });
        await pair.CleanReturnAsync();
    }
}
