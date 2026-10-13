using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Crayon;

/// <summary>
/// The custom symbol a crayon stamps instead of its decal, while set.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUCrayonSymbolComponent : Component
{
    [AutoNetworkedField]
    public CMUCrayonSymbol? Symbol;
}

/// <summary>
/// A custom symbol stamped on the floor.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CMUCrayonStampComponent : Component
{
    [AutoNetworkedField]
    public CMUCrayonSymbol Symbol = new();
}

[Serializable, NetSerializable]
public sealed class CMUCrayonSelectSymbolMessage(CMUCrayonSymbol symbol) : BoundUserInterfaceMessage
{
    public readonly CMUCrayonSymbol Symbol = symbol;
}
