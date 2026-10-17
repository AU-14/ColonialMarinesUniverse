using Content.Shared.Speech.EntitySystems;
using Content.Shared.Speech.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Speech.Components;

/// <summary>
/// Applies a replacement accent only when relayed through an inventory.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(RelayedReplacementAccentSystem))]
public sealed partial class RelayedReplacementAccentComponent : Component
{
    /// <summary>
    /// The replacement accent applied to the inventory owner.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<ReplacementAccentPrototype> Accent;
}
