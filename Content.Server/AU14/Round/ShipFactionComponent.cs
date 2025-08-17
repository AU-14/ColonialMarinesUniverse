using Robust.Shared.GameObjects;

namespace Content.Server.AU14.Round
{
    /// <summary>
    /// Attach to a ship entity to designate its faction (e.g., "govfor" or "opfor").
    /// </summary>
    [RegisterComponent]
    public sealed partial class ShipFactionComponent : Component
    {
        [DataField("faction")]
        public string? Faction { get; set; }

    }
}
