using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;

namespace Content.Server._RMC14.Requisitions;

public sealed partial class RequisitionsSystem
{
    /// <summary>
    /// Replaces a console's ordered catalog with a caller-owned detached projection and synchronizes it to clients.
    /// </summary>
    internal void ReplaceCatalog(
        Entity<RequisitionsComputerComponent> computer,
        List<RequisitionsCategory> categories)
    {
        computer.Comp.Categories = categories;
        Dirty(computer);
    }
}
