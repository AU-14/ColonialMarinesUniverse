using Content.Shared._RMC14.Dropship;

namespace Content.Shared._CMU14.Dropship.Rappel;

public abstract partial class SharedEEXRappelSystem : EntitySystem
{
    [Dependency] protected SharedDropshipSystem Dropship = default!;
}
