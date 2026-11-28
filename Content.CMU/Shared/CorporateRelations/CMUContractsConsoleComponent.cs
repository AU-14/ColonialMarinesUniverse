using Content.Shared._RMC14.Marines.Roles.Ranks;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.CorporateRelations;

/// <summary>
///     Lets this console open the corporate contracts window: rent, licences, loans, work orders
///     and the corporate withdrawal. <see cref="Side"/> decides what the console can do.
/// </summary>
[RegisterComponent]
public sealed partial class CMUContractsConsoleComponent : Component
{
    [DataField(required: true)]
    public CMUContractsSide Side;

    /// <summary>
    ///     Ranks allowed to start or cancel the corporate withdrawal from this console.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<RankPrototype>> WithdrawalRanks = new();
}
