using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Abilities;

/// <summary>
/// While <see cref="Enabled"/>, a wide swing that connects also punches every enemy it hit
/// (the <c>XenoPunch</c> effect) and puts the punch action on cooldown.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUWendigoPunchSwingComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;
}
