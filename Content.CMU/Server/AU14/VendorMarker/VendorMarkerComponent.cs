using Content.Shared.AU14;
using Content.Shared.AU14.util;
using Robust.Shared.GameObjects;

namespace Content.Server.AU14.VendorMarker
{
    /// <summary>
    /// Component to mark vendor marker entities for platoon spawning or other logic.
    /// </summary>
    [RegisterComponent]
    public sealed partial class VendorMarkerComponent : Component
    {

        // Indicates if this marker is for Govfor or Opfor
        [DataField("govfor")]
        public bool Govfor { get; set; } = false;

        [DataField("opfor")]
        public bool Opfor { get; set; } = false;

        [DataField("dropship")]
        public bool DropShip { get; set; } = false;



        [DataField("ship")]
        public bool Ship { get; set; } = false;


        // Designates the vendor's job
        [DataField("class")]
        public PlatoonMarkerClass Class { get; set; }
    }
}
