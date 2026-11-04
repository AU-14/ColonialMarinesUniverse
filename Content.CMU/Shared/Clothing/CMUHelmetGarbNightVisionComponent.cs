using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Clothing;

/// <summary>
///     Night vision goggles that live in a helmet. Pulled down, they give whoever is wearing the helmet night vision
///     through the item's <c>NightVisionItem</c> settings.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUHelmetGarbNightVisionComponent : Component
{
    /// <summary>The inventory slot the helmet has to be worn in.</summary>
    [DataField]
    public string Slot = "head";
}
