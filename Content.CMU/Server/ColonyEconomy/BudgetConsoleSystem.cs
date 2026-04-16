using Content.Server.Stack;
using Content.Shared.AU14.ColonyEconomy;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server.AU14.ColonyEconomy;

public sealed class BudgetConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ColonyBudgetSystem _budget = default!;
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly DepartmentConsoleSystem _department = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BudgetConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<BudgetConsoleComponent, BudgetConsoleWithdrawBuiMsg>(OnWithdraw);
        SubscribeLocalEvent<BudgetConsoleComponent, BudgetConsoleTransferToDeptBuiMsg>(OnTransferToDept);
        SubscribeLocalEvent<BudgetConsoleComponent, BudgetConsoleDispenseSalariesBuiMsg>(OnDispenseSalaries);
        SubscribeLocalEvent<BudgetConsoleComponent, EntInsertedIntoContainerMessage>(OnCashInserted);
    }

    private BudgetConsoleBuiState BuildState()
    {
        var departments = new List<BudgetConsoleDepartmentInfo>();
        foreach (var (netUid, name, budget) in _department.GetAllDepartments())
        {
            departments.Add(new BudgetConsoleDepartmentInfo(netUid, name, budget));
        }
        return new BudgetConsoleBuiState(_budget.GetBudget(), departments);
    }

    private void OnUiOpened(EntityUid uid, BudgetConsoleComponent comp, BoundUIOpenedEvent args)
    {
        _ui.SetUiState(uid, BudgetConsoleUi.Key, BuildState());
    }

    private void OnWithdraw(EntityUid uid, BudgetConsoleComponent comp, BudgetConsoleWithdrawBuiMsg msg)
    {
        if (msg.Amount > _budget.GetBudget())
            return;

        _budget.AddToBudget(-msg.Amount);
        _stack.SpawnMultiple("RMCSpaceCash", (int) msg.Amount, uid);

        UpdateAllUi();
    }

    private void OnTransferToDept(EntityUid uid, BudgetConsoleComponent comp, BudgetConsoleTransferToDeptBuiMsg msg)
    {
        var deptUid = _entities.GetEntity(msg.DeptConsoleUid);
        _department.TransferToDepartment(deptUid, msg.Amount);

        UpdateAllUi();
    }

    private void OnDispenseSalaries(EntityUid uid, BudgetConsoleComponent comp, BudgetConsoleDispenseSalariesBuiMsg msg)
    {
        _department.DispenseSalaries();
        UpdateAllUi();
    }

    private void OnCashInserted(EntityUid uid, BudgetConsoleComponent comp, EntInsertedIntoContainerMessage args)
    {
        int stackCount = 1;
        if (_entities.TryGetComponent<StackComponent>(args.Entity, out var stack))
            stackCount = stack.Count;

        _budget.AddToBudget(stackCount);
        _entities.QueueDeleteEntity(args.Entity);
        UpdateAllUi();
    }

    private void UpdateAllUi()
    {
        var state = BuildState();
        var query = EntityQueryEnumerator<BudgetConsoleComponent>();
        while (query.MoveNext(out var uid, out _))
            _ui.SetUiState(uid, BudgetConsoleUi.Key, state);
    }
}
