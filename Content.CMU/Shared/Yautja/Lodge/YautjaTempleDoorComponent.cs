using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Yautja.Lodge;

// cmss13 runed temple door: only a hunter's wristblades work it, everything else bounces off
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class YautjaTempleDoorComponent : Component
{
    // bolted in whatever state it's in, open or shut
    [DataField, AutoNetworkedField]
    public bool Locked;

    [DataField]
    public SoundSpecifier LockSound = new SoundPathSpecifier("/Audio/Effects/stonedoor_openclose.ogg");
}
