using Content.Server._RMC14.Requisitions;
using Content.Shared.CMU14.Requisitions;

namespace Content.Server.CMU14.Pool;

public sealed partial class CMUPoolRequisitionsSystem : EntitySystem
{
    [Dependency] private RequisitionsSystem _requisitions = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMURequisitionsCatalogBuiltEvent>(OnCatalogBuilt);
    }

    private void OnCatalogBuilt(ref CMURequisitionsCatalogBuiltEvent args)
        => _requisitions.AddPoolTableParts(args.Computer);
}
