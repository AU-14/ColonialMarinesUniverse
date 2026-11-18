using Content.Server.Stack;
using Content.Server._RMC14.Requisitions;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.GameTicking;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.ColonyEconomy;

public sealed partial class BudgetConsoleSystem : EntitySystem
{
    private static readonly EntProtoId CashPrototype = "RMCSpaceCash";
    public const float WithdrawalLimit = 3000;
    public static readonly TimeSpan WithdrawalCooldown = TimeSpan.FromMinutes(30);

    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private ColonyBudgetSystem _budget = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private DepartmentConsoleSystem _department = default!;
    [Dependency] private RequisitionsSystem _requisitions = default!;
    [Dependency] private IGameTiming _timing = default!;

    private bool _uiRefreshPending;
    private float _withdrawnThisPeriod;
    private TimeSpan _withdrawalResetAt;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BudgetConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<BudgetConsoleComponent, BudgetConsoleWithdrawBuiMsg>(OnWithdraw);
        SubscribeLocalEvent<BudgetConsoleComponent, BudgetConsoleTransferToDeptBuiMsg>(OnTransferToDept);
        SubscribeLocalEvent<BudgetConsoleComponent, BudgetConsoleTransferAsrsToDeptBuiMsg>(OnTransferAsrsToDept);
        SubscribeLocalEvent<BudgetConsoleComponent, BudgetConsoleDispenseSalariesBuiMsg>(OnDispenseSalaries);
        SubscribeLocalEvent<BudgetConsoleComponent, EntInsertedIntoContainerMessage>(OnCashInserted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<CMUColonyAsrsBalanceChangedEvent>(OnAsrsBalanceChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        RefreshWithdrawalAllowance();
        if (_uiRefreshPending)
            UpdateAllUi();
    }

    private void OnAsrsBalanceChanged(ref CMUColonyAsrsBalanceChangedEvent args)
    {
        _uiRefreshPending = true;
    }

    private void RefreshWithdrawalAllowance()
    {
        if (_withdrawnThisPeriod > 0 && _timing.CurTime >= _withdrawalResetAt)
            ResetWithdrawalAllowance();
    }

    private void ResetWithdrawalAllowance()
    {
        _uiRefreshPending = true;
        _withdrawnThisPeriod = 0;
        _withdrawalResetAt = TimeSpan.Zero;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        ResetWithdrawalAllowance();
    }

    private BudgetConsoleBuiState BuildState()
    {
        RefreshWithdrawalAllowance();
        var departments = _department.GetAllDepartments();
        var remaining = Math.Max(0, WithdrawalLimit - _withdrawnThisPeriod);
        var resetSeconds = _withdrawnThisPeriod > 0
            ? Math.Max(0, (int) Math.Ceiling((_withdrawalResetAt - _timing.CurTime).TotalSeconds))
            : 0;
        return new BudgetConsoleBuiState(_budget.GetBudget(), _requisitions.GetFunds("colony"), departments,
            remaining, WithdrawalLimit, resetSeconds);
    }

    private void OnUiOpened(EntityUid uid, BudgetConsoleComponent comp, BoundUIOpenedEvent args)
    {
        _ui.SetUiState(uid, BudgetConsoleUi.Key, BuildState());
    }

    private void OnWithdraw(EntityUid uid, BudgetConsoleComponent comp, BudgetConsoleWithdrawBuiMsg msg)
    {
        RefreshWithdrawalAllowance();
        var remaining = WithdrawalLimit - _withdrawnThisPeriod;
        if (!float.IsFinite(msg.Amount)
            || msg.Amount != MathF.Truncate(msg.Amount)
            || msg.Amount <= 0
            || msg.Amount > _budget.GetBudget()
            || msg.Amount > remaining)
            return;

        if (_withdrawnThisPeriod <= 0)
            _withdrawalResetAt = _timing.CurTime + WithdrawalCooldown;
        _withdrawnThisPeriod += msg.Amount;
        _budget.AddToBudget(-msg.Amount);
        _stack.SpawnMultipleNextToOrDrop(CashPrototype, (int) msg.Amount, uid);

        UpdateAllUi();
    }

    private void OnTransferToDept(EntityUid uid, BudgetConsoleComponent comp, BudgetConsoleTransferToDeptBuiMsg msg)
    {
        var deptUid = _entities.GetEntity(msg.DeptConsoleUid);
        if (_department.TransferToDepartment(deptUid, msg.Amount))
            UpdateAllUi();
    }

    private void OnTransferAsrsToDept(EntityUid uid, BudgetConsoleComponent comp, BudgetConsoleTransferAsrsToDeptBuiMsg msg)
    {
        var deptUid = _entities.GetEntity(msg.DeptConsoleUid);
        if (_department.TransferASRSToDepartment(deptUid, msg.Amount))
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
        _uiRefreshPending = false;
        BudgetConsoleBuiState? state = null;
        var query = EntityQueryEnumerator<BudgetConsoleComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_ui.IsUiOpen(uid, BudgetConsoleUi.Key))
                _ui.SetUiState(uid, BudgetConsoleUi.Key, state ??= BuildState());
        }
    }
}
