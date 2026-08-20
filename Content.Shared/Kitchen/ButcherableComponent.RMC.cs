using Robust.Shared.GameStates;

namespace Content.Shared.Nutrition.Components;

public sealed partial class ButcherableComponent
{
    /// <summary>
    /// Tool category used to butcher this entity.
    /// </summary>
    [DataField("butcheringType"), AutoNetworkedField]
    public ButcheringType Type = ButcheringType.Knife;

    [DataField("waitForRot"), AutoNetworkedField] // CMU14: gate butchering of unrevivable corpses until they rot
    public bool WaitForRot = false;
}

public enum ButcheringType : byte
{
    Knife,
    Spike,
    Gibber,
}
