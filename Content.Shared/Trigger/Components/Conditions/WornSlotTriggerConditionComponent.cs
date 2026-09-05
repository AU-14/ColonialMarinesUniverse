using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Shared.Trigger.Components.Conditions;

/// <summary>
/// Requires the triggered entity to be equipped in an inventory slot with all of the configured flags.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WornSlotTriggerConditionComponent : BaseTriggerConditionComponent
{
    [DataField(required: true), AutoNetworkedField]
    public SlotFlags RequiredSlots;
}
