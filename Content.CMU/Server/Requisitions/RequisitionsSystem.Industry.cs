using Content.Server.CMU14.ColonyEconomy;

namespace Content.Server._RMC14.Requisitions;

public sealed partial class RequisitionsSystem
{
    [Dependency] private readonly CMUIndustryExportSystem _industryExports = default!;
}
