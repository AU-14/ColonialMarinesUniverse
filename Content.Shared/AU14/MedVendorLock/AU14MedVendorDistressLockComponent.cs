using Robust.Shared.GameObjects;

namespace Content.Shared.AU14.MedVendorLock;

/// <summary>
/// Marks a medical vendor as disabled while a distress-signal round is active but
/// govfor has not yet deployed to the planet (i.e. no dropship has landed on the
/// planet map). The vendor becomes usable the moment the first dropship lands.
/// </summary>
[RegisterComponent]
public sealed partial class AU14MedVendorDistressLockComponent : Component
{
    /// <summary>Runtime: true while govfor is not yet planetside in a DS round.</summary>
    public bool Locked;
}
