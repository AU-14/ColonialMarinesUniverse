using Robust.Shared.GameStates;

namespace Content.Shared.Nutrition.Components;

public sealed partial class ButcherableComponent
{
    /// <summary>
    /// Tool category used to butcher this entity.
    /// </summary>
    [DataField("butcheringType"), AutoNetworkedField]
    public ButcheringType Type = ButcheringType.Knife;
}

public enum ButcheringType : byte
{
    Knife,
    Spike,
    Gibber,
}
