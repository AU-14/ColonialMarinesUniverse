using Content.Server.CMU14.Comms;
using Content.Server.Stack;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CMU14.ForceOnForce;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.ColonyEconomy;

public sealed partial class BudgetConsoleSystem : EntitySystem
{
    private static readonly EntProtoId CashPrototype = "RMCSpaceCash";

    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private ColonyBudgetSystem _budget = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private DepartmentConsoleSystem _department = default!;
    [Dependency] private ForceOnForceSystem _factions = default!;
    [Dependency] private ColonyGarrisonLiaisonFundsSystem _liaisonFunds = default!;
    [Dependency] private SharedRequisitionsSystem _requisitions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BudgetConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<BudgetConsoleComponent, BudgetConsoleWithdrawBuiMsg>(OnWithdraw);
        SubscribeLocalEvent<BudgetConsoleComponent, BudgetConsoleTransferToDeptBuiMsg>(OnTransferToDept);
        SubscribeLocalEvent<BudgetConsoleComponent, BudgetConsoleDispenseSalariesBuiMsg>(OnDispenseSalaries);
        SubscribeLocalEvent<BudgetConsoleComponent, BudgetConsoleTransferToGarrisonBuiMsg>(OnTransferToGarrison);
        SubscribeLocalEvent<BudgetConsoleComponent, EntInsertedIntoContainerMessage>(OnCashInserted);
    }

    private BudgetConsoleBuiState BuildState()
    {
        var departments = new List<BudgetConsoleDepartmentInfo>();
        foreach (var (netUid, name, budget) in _department.GetAllDepartments())
        {
            departments.Add(new BudgetConsoleDepartmentInfo(netUid, name, budget));
        }

        var garrisons = new List<BudgetConsoleGarrisonInfo>();
        var accounts = EntityQueryEnumerator<RequisitionsAccountComponent>();
        while (accounts.MoveNext(out _, out var account))
        {
            if (account.Faction is { } faction)
                garrisons.Add(new BudgetConsoleGarrisonInfo(faction, account.Balance));
        }

        return new BudgetConsoleBuiState(_budget.GetBudget(), departments, garrisons);
    }

    private void OnTransferToGarrison(Entity<BudgetConsoleComponent> ent, ref BudgetConsoleTransferToGarrisonBuiMsg msg)
    {
        // A garrison can't pay itself out of the colony's budget; that is the colony's call.
        if (msg.Amount <= 0 || msg.Amount > _budget.GetBudget() || _factions.GetFaction(msg.Actor) != null)
            return;

        var accounts = EntityQueryEnumerator<RequisitionsAccountComponent>();
        while (accounts.MoveNext(out _, out var account))
        {
            if (!string.Equals(account.Faction, msg.Faction, StringComparison.OrdinalIgnoreCase))
                continue;

            _budget.AddToBudget(-msg.Amount);
            _requisitions.ChangeBudget(msg.Amount, account.Faction);
            UpdateAllUi();
            _liaisonFunds.RefreshAll();
            return;
        }
    }

    private void OnUiOpened(EntityUid uid, BudgetConsoleComponent comp, BoundUIOpenedEvent args)
    {
        _ui.SetUiState(uid, BudgetConsoleUi.Key, BuildState());
    }

    private void OnWithdraw(EntityUid uid, BudgetConsoleComponent comp, BudgetConsoleWithdrawBuiMsg msg)
    {
        if (!float.IsFinite(msg.Amount)
            || msg.Amount <= 0
            || msg.Amount > _budget.GetBudget())
            return;

        _budget.AddToBudget(-msg.Amount);
        _stack.SpawnMultipleNextToOrDrop(CashPrototype, (int) msg.Amount, uid);

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

    public void UpdateAllUi()
    {
        var state = BuildState();
        var query = EntityQueryEnumerator<BudgetConsoleComponent>();
        while (query.MoveNext(out var uid, out _))
            _ui.SetUiState(uid, BudgetConsoleUi.Key, state);
    }
}
