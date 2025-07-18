using Content.Server.AU14.ColonyEconomy;
using Content.Server.Stack;
using Content.Shared.AU14.ColonyEconomy;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
namespace Content.Server.AU14.ColonyEconomy;
public sealed class BudgetConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ColonyBudgetSystem _budget = default!;
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BudgetConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<BudgetConsoleComponent, BudgetConsoleWithdrawBuiMsg>(OnWithdraw);
       // SubscribeLocalEvent<BudgetConsoleComponent, ActivateInWorldEvent>(OnActivate);
    }

    private void OnUiOpened(EntityUid uid, BudgetConsoleComponent comp, BoundUIOpenedEvent args)
    {
        var state = new BudgetConsoleBuiState(_budget.GetBudget(),_budget.givewithdrawers());
        _ui.SetUiState(uid, BudgetConsoleUi.Key, state);
    }

    private void OnWithdraw(EntityUid uid, BudgetConsoleComponent comp, BudgetConsoleWithdrawBuiMsg msg)
    {

        if (msg.Amount > _budget.GetBudget())
        {

            return;
        }
        _budget.AddWithdraw(_entities.GetComponent<MetaDataComponent>(msg.Actor).EntityName);




        _budget.AddToBudget(-msg.Amount);
        var xform = _entities.GetComponent<TransformComponent>(uid);
        var stacks = _stack.SpawnMultiple("RMCSpaceCash", (int)msg.Amount, uid);

        var state = new BudgetConsoleBuiState(_budget.GetBudget(),_budget.givewithdrawers());
        _ui.SetUiState(uid,  BudgetConsoleUi.Key, state);
    }



}
