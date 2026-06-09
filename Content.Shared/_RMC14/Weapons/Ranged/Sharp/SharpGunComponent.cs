using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Weapons.Ranged.Sharp;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SharpGunComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Delayed = true;

    [DataField, AutoNetworkedField]
    public TimeSpan LongDelay = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField]
    public TimeSpan ShortDelay = TimeSpan.FromSeconds(2.5);

    [DataField, AutoNetworkedField]
    public SoundSpecifier ToggleSound = new SoundPathSpecifier("/Audio/_RMC14/Machines/click.ogg");

    public TimeSpan CurrentDelay => Delayed ? LongDelay : ShortDelay;
}
