using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.Walker;

/// <summary>
/// Gives this entity a hive-visible icon through walls, shown only to allied xenos.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUPathogenWalkerHiveIconComponent : Component
{
    [DataField, AutoNetworkedField]
    public SpriteSpecifier.Rsi Icon = new(new ResPath("_RMC14/Interface/marine_hud.rsi"), "hudmutineer");
}