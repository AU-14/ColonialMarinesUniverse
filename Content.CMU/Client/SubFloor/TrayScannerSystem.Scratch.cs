using Content.Shared.SubFloor;

namespace Content.Client.SubFloor;

public sealed partial class TrayScannerSystem
{
    private readonly HashSet<Entity<SubFloorHideComponent>> _cmuScannerInRange = new();
    private readonly HashSet<Entity<SubFloorHideComponent>> _cmuScannerCandidates = new();
}
