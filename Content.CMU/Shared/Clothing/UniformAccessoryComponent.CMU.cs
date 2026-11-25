using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Shared._RMC14.UniformAccessories;

public sealed partial class UniformAccessoryComponent
{
    [DataField, AutoNetworkedField]
    public Color PlayerColor = Color.White;
}
