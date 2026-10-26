// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.CMU14.Government;

namespace Content.Server.CMU14.Government;

// The state of the window of the mayor for the auctions, the tenders, the loans to the players, the posts and the decrees, and what the one who is at the console may do (the rights).
public sealed partial class ColonyGovernmentSystem
{
    private AdminState BuildAdmin(ColonyGovernmentComponent comp, EntityUid console)
    {
        var now = _timing.CurTime;
        return new AdminState(AuctionEntries(comp, now), TenderEntries(comp, now), LoanOfferEntries(comp), PlayerLoanEntries(comp, now));
    }
}
