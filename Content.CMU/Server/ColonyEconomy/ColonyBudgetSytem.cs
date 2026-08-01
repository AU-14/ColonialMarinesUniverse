// Content.Server/AU14/ColonyEconomy/ColonyBudgetSystem.cs
using Content.Shared.AU14.ColonyEconomy;

namespace Content.Server.AU14.ColonyEconomy;

public sealed class ColonyBudgetSystem : EntitySystem
{
    private float _budget;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ColonyBudgetMapComponent, MapInitEvent>(OnMapInit);
    }

    /// <summary>
    ///     When a map entity with ColonyBudgetMapComponent initializes, set the colony budget.
    /// </summary>
    private void OnMapInit(EntityUid uid, ColonyBudgetMapComponent comp, MapInitEvent args)
    {
        _budget = comp.Budget;
    }

    public void AddToBudget(float amount, EntityUid? by = null)
    {
        _budget += amount;
    }

    /// <summary>
    ///     Sets the colony budget to an exact value.
    /// </summary>
    public void SetBudget(float amount)
    {
        _budget = amount;
    }

    public float GetBudget() => _budget;
}
