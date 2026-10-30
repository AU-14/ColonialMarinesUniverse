using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Yautja;

/// <summary>
/// Lets a non-yautja use the yautja butcher action (skinning, delimbing, the full carve-up).
/// Yautja get it through <see cref="YautjaComponent"/> already, this is for anyone else, like the serial killer.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class YautjaButcherAbilityComponent : Component
{
    [DataField]
    public EntProtoId ActionId = "CMUActionButcherCorpse";

    [ViewVariables]
    public EntityUid? Action;
}
