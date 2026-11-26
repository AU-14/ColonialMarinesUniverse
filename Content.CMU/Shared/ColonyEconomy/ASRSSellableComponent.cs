using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.ColonyEconomy;

/// <summary>
/// Marks an entity as an item the ASRS may appraise and sell.
/// Stack-aware pricing is handled by <c>PricingSystem</c>, so a stack pays for every unit.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ASRSSellableComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool FromOreDeposit;
}
