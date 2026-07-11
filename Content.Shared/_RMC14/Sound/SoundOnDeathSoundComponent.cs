using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Sound;

[RegisterComponent, NetworkedComponent]
[Access(typeof(CMSoundSystem))]
public sealed partial class SoundOnDeathSoundComponent : Component
{
    [DataField]
    public EntityUid? Parent;
}
