using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ColonyEconomy;
using Content.Shared.CMU14.ColonyEconomy;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class BudgetConsoleGuardRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task BudgetConsoleWithdrawRejectsInvalidAmounts()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var budget = Server.System<ColonyBudgetSystem>();
            var console = SEntMan.SpawnEntity("AUbudgetConsole", map.GridCoords);

            try
            {
                budget.SetBudget(1000f);

                // BUI messages are client-authoritative; a NaN slipping through poisons the
                // float ledger and turns every later (amount > ledger) guard false.
                SEntMan.EventBus.RaiseLocalEvent(console, new BudgetConsoleWithdrawBuiMsg(float.NaN));
                SEntMan.EventBus.RaiseLocalEvent(console, new BudgetConsoleWithdrawBuiMsg(-100f));
                SEntMan.EventBus.RaiseLocalEvent(console, new BudgetConsoleWithdrawBuiMsg(5000f));
                Assert.That(budget.GetBudget(), Is.EqualTo(1000f).Within(0.01f));

                SEntMan.EventBus.RaiseLocalEvent(console, new BudgetConsoleWithdrawBuiMsg(250f));
                Assert.That(budget.GetBudget(), Is.EqualTo(750f).Within(0.01f));
            }
            finally
            {
                SEntMan.DeleteEntity(console);
            }
        });
    }

    [Test]
    public async Task DepartmentConsoleGuardsRejectInvalidAmounts()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var budget = Server.System<ColonyBudgetSystem>();
            var console = SEntMan.SpawnEntity("AUbudgetConsole", map.GridCoords);
            var dept = SEntMan.SpawnEntity("AUDepartmentConsoleLabor", map.GridCoords);
            var deptComp = SComp<DepartmentConsoleComponent>(dept);

            try
            {
                budget.SetBudget(1000f);
                deptComp.DepartmentBudget = 500f;

                // (int) float.NaN is platform-defined; the guard must keep the ledger untouched.
                SEntMan.EventBus.RaiseLocalEvent(dept, new DepartmentConsoleWithdrawBuiMsg(float.NaN));
                SEntMan.EventBus.RaiseLocalEvent(dept, new DepartmentConsoleWithdrawBuiMsg(-100f));
                Assert.That(deptComp.DepartmentBudget, Is.EqualTo(500f).Within(0.01f));

                SEntMan.EventBus.RaiseLocalEvent(dept, new DepartmentConsoleWithdrawBuiMsg(150f));
                Assert.That(deptComp.DepartmentBudget, Is.EqualTo(350f).Within(0.01f));

                var deptNet = SEntMan.GetNetEntity(dept);
                SEntMan.EventBus.RaiseLocalEvent(console, new BudgetConsoleTransferToDeptBuiMsg(deptNet, float.NaN));
                SEntMan.EventBus.RaiseLocalEvent(console, new BudgetConsoleTransferToDeptBuiMsg(deptNet, -100f));
                SEntMan.EventBus.RaiseLocalEvent(console, new BudgetConsoleTransferToDeptBuiMsg(deptNet, 2000f));
                Assert.That(budget.GetBudget(), Is.EqualTo(1000f).Within(0.01f));
                Assert.That(deptComp.DepartmentBudget, Is.EqualTo(350f).Within(0.01f));

                SEntMan.EventBus.RaiseLocalEvent(console, new BudgetConsoleTransferToDeptBuiMsg(deptNet, 200f));
                Assert.That(budget.GetBudget(), Is.EqualTo(800f).Within(0.01f));
                Assert.That(deptComp.DepartmentBudget, Is.EqualTo(550f).Within(0.01f));
            }
            finally
            {
                SEntMan.DeleteEntity(console);
                SEntMan.DeleteEntity(dept);
            }
        });
    }
}
