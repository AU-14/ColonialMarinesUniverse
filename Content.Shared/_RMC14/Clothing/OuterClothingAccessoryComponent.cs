using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Clothing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(OuterClothingAccessoriesSystem))]
public sealed partial class OuterClothingAccessoryComponent : Component
{
    [DataField, AutoNetworkedField]
    public SpriteSpecifier.Rsi Rsi;

    [DataField, AutoNetworkedField]
    public SpriteSpecifier.Rsi? ToggledRsi;
}