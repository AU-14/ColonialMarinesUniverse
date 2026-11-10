using Robust.Shared.GameObjects;

namespace Content.Shared._RMC14.Chemistry.ChemMaster;

public sealed class RMCChemMasterPillScanEvent(EntityUid user, EntityUid pill) : HandledEntityEventArgs
{
    public EntityUid User { get; } = user;
    public EntityUid Pill { get; } = pill;
}
