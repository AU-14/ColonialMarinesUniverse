using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Crayon;

/// <summary>
/// A crayon that stamps its user's custom symbol instead of its decal while they have one picked.
/// Every crayon has it, so a symbol picked with one crayon draws with any other.
/// </summary>
[RegisterComponent]
public sealed partial class CMUCrayonSymbolComponent : Component;

/// <summary>
/// The custom symbol a player picked in a crayon window. Like a stencil it stays with the player,
/// not the crayon, until they pick a stock decal in any crayon window.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUCrayonStencilSystem))]
public sealed partial class CMUCrayonStencilComponent : Component
{
    [DataField, AutoNetworkedField]
    public CMUCrayonSymbol? Symbol;

    // Only the player's own crayon windows show it.
    public override bool SendOnlyToOwner => true;
}

/// <summary>
/// A custom symbol stamped on the floor.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CMUCrayonStampComponent : Component
{
    [DataField, AutoNetworkedField]
    public CMUCrayonSymbol Symbol = new();
}

[Serializable, NetSerializable]
public sealed class CMUCrayonSelectSymbolMessage(CMUCrayonSymbol symbol) : BoundUserInterfaceMessage
{
    public readonly CMUCrayonSymbol Symbol = symbol;
}
