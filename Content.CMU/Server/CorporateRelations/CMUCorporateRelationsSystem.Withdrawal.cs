using Content.Server.CMU14.Ops.ThirdParty;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.CorporateRelations;

namespace Content.Server.CMU14.CorporateRelations;

public sealed partial class CMUCorporateRelationsSystem
{
    /// <summary>
    ///     Warning the colony gets between WY starting a withdrawal and it taking effect.
    /// </summary>
    public static readonly TimeSpan WithdrawalCountdown = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     After WY withdraws, colony prices go up by this much and colony payouts down by it.
    /// </summary>
    public const float WithdrawalPenalty = 0.25f;

    /// <summary>
    ///     The company's own shuttle that comes down to collect corporate staff.
    /// </summary>
    public const string WithdrawalTeam = "CMUWYWithdrawal";

    [Dependency] private ThirdPartySystem _thirdParty = default!;

    private TimeSpan? _withdrawalAt;
    private bool _withdrawn;
    private bool _withdrawalTeamCalled;

    /// <summary>
    ///     Whether WY has pulled out of the colony this round. Unlike other factions' withdrawals
    ///     it doesn't end the round.
    /// </summary>
    public bool Withdrawn => _withdrawn;

    /// <summary>
    ///     Whether a withdrawal has been ordered and is counting down.
    /// </summary>
    public bool WithdrawalPending => _withdrawalAt != null;

    /// <summary>
    ///     Multiplier on prices the colony pays (colony ASRS orders, cash vendors).
    /// </summary>
    public float ColonyPriceMultiplier => _withdrawn ? 1 + WithdrawalPenalty : 1;

    /// <summary>
    ///     Multiplier on what the colony earns from submission storage.
    /// </summary>
    public float ColonyPayoutMultiplier => _withdrawn ? 1 - WithdrawalPenalty : 1;

    private void InitializeWithdrawal()
    {
        SubscribeLocalEvent<CMUContractsConsoleComponent, CMUContractsWithdrawalBuiMsg>(OnWithdrawal);
    }

    private void OnWithdrawal(Entity<CMUContractsConsoleComponent> ent, ref CMUContractsWithdrawalBuiMsg args)
    {
        if (ent.Comp.Side != CMUContractsSide.Corporate || _withdrawn)
            return;

        if (!TryComp<RankComponent>(args.Actor, out var rank) ||
            rank.Rank is not { } rankId ||
            !ent.Comp.WithdrawalRanks.Contains(rankId))
        {
            Refuse(ent, args.Actor, "cmu-contracts-refuse-withdrawal-rank");
            return;
        }

        // Once ordered there's no calling it off.
        if (args.Start && _withdrawalAt == null)
        {
            _withdrawalAt = _timing.CurTime + WithdrawalCountdown;
            Announce("cmu-contracts-announce-withdrawal-started", ("minutes", (int) WithdrawalCountdown.TotalMinutes));
            CallWithdrawalTeam();
        }

        UpdateAllUi();
    }

    /// <summary>
    ///     Calls the corporate shuttle to collect WY staff. A withdrawal can't be called off, so
    ///     neither can the shuttle.
    /// </summary>
    private void CallWithdrawalTeam()
    {
        if (_withdrawalTeamCalled ||
            !_prototypes.TryIndex<ThirdPartyPrototype>(WithdrawalTeam, out var party) ||
            !_prototypes.TryIndex(party.PartySpawn, out var spawn))
        {
            return;
        }

        _withdrawalTeamCalled = true;
        _thirdParty.SpawnThirdParty(party, spawn, false, overrideDropship: true);
    }

    private void UpdateWithdrawal(TimeSpan now)
    {
        if (_withdrawalAt is not { } at || now < at)
            return;

        Withdraw();
    }

    /// <summary>
    ///     WY pulls out: every loan is collected on the spot, work orders and rent end, WY stops
    ///     taking the tariff, and the colony pays more and earns less for the rest of the round.
    /// </summary>
    public void Withdraw()
    {
        if (_withdrawn)
            return;

        _withdrawalAt = null;
        var budget = _corporate.GetCorporateBudget();
        CollectAllLoans();
        _markets.IgnoreCorporateIncome(_corporate.GetCorporateBudget() - budget);
        CloseAllWorkOrders();
        _withdrawn = true;
        _markets.ReportSetback(CompanyForce, WithdrawalShock, "Colonization", "cmu-markets-setback-withdrawal");

        Announce("cmu-contracts-announce-withdrawal-done", ("percent", (int) (WithdrawalPenalty * 100)));
        UpdateAllUi();
    }
}
