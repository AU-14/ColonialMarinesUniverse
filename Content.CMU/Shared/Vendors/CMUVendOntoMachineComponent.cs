using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Vendors;

/// <summary>
///     An automated vendor that drops what it vends on top of itself with a sound, like a vending machine's tray,
///     instead of putting it in the buyer's hands.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVendOntoMachineComponent : Component
{
    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Machines/machine_vend.ogg");
}
