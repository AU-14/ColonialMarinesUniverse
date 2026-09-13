using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Botany.Components;

/// <summary>
/// Reagents that this plant species can gain from a special chemical mutation.
/// This is immutable species data: it is deliberately not inherited through
/// plant snapshots or cross-pollination.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PlantSpecialChemicalsComponent : Component
{
    /// <summary>
    /// The possible special reagents for this plant species.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<ReagentPrototype>> Chemicals = [];
}
