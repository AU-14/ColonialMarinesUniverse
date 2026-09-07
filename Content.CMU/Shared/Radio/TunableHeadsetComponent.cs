using Content.Shared.Inventory;
using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.CMU14.Radio;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TunableHeadsetComponent : Component
{
    [DataField, AutoNetworkedField]
    public RadioFrequency TunedFrequency = RadioFrequency.Off;

    [DataField]
    public SlotFlags RequiredSlots = SlotFlags.EARS;

    [DataField]
    public RadioFrequency DefaultFrequency = RadioFrequency.Off;

    [DataField]
    public RadioFrequency MinFrequency = RadioFrequency.FromKilohertz(30_000);

    [DataField]
    public RadioFrequency MaxFrequency = RadioFrequency.FromKilohertz(87_999);
}
