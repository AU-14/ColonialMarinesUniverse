using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.Overmind;

/// <summary>
/// The Pathogen Confluence's equivalent of the Hive Core.
/// Any Pathogen xeno can step on it to become the Overmind (queen)
/// if no Overmind currently exists for the hive.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CMUBlightCoreComponent : Component
{
    /// <summary>The xeno currently merged into this core as Overmind.</summary>
    [DataField, AutoNetworkedField]
    public NetEntity? CurrentOvermind;

    /// <summary>Prototype to transform the xeno into on merge.</summary>
    [DataField, AutoNetworkedField]
    public EntProtoId OvermindPrototype = "CMU14XenoOvermind";

    /// <summary>HP of the core structure itself.</summary>
    [DataField, AutoNetworkedField]
    public int MaxHealth = 600;

    public TimeSpan LastDamageAnnounceAt = TimeSpan.Zero;
}