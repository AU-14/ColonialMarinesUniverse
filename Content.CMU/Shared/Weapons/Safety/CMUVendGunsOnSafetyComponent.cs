using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Weapons.Safety;

/// <summary>
/// Guns handed out by this vendor, and guns inside cases it hands out, come with the safety on and, if
/// <see cref="Unload"/>, with no magazine or rounds in them.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVendGunsOnSafetyComponent : Component
{
    [DataField]
    public bool Unload = true;
}
