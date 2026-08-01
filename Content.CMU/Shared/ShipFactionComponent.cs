using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared.AU14
{
    /// <summary>
    /// Attach to a ship entity to designate its faction (e.g., "govfor" or "opfor").
    /// </summary>
    [RegisterComponent, NetworkedComponent]
    public sealed partial class ShipFactionComponent : Component
    {
        [DataField("faction")]
        public string? Faction { get; set; }

    }
}
