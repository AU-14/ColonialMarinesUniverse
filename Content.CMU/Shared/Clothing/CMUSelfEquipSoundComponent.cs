using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Clothing;

/// <summary>
///     A sound only the wearer hears when they put this on, like powered armor starting up.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUSelfEquipSoundComponent : Component
{
    [DataField(required: true)]
    public SoundSpecifier Sound = default!;
}
