using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._AU14.Radio;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TunableHeadsetComponent : Component
{
    [DataField, AutoNetworkedField]
    public int TunedFrequency = 0;

    [DataField]
    public SlotFlags RequiredSlots = SlotFlags.EARS;

    [DataField]
    public int DefaultFrequency = 0;

    [DataField]
    public int MinFrequency = 30000;

    [DataField]
    public int MaxFrequency = 87999;
}
