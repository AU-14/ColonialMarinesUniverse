## Corporate contracts: rent, licences, loans, work orders and the WY withdrawal

cmu-contracts-open = Corporate contracts
cmu-contracts-section = Contracts
cmu-contracts-hint-ambassador = The Company's site lease, licences and tariff, and any loans or work orders between the colony and the Company.
cmu-contracts-hint-corporate = Rent, licences, loans and work orders with the colony, and divestment.
cmu-contracts-title = Corporate Contracts
cmu-contracts-announcement-sender = Colonial Corporate Registry
cmu-contracts-borrower-colony = the colony

cmu-contracts-budget-corporate = Corporate budget: ${$amount}
cmu-contracts-budget-borrower = Your budget: ${$amount} | Weyland-Yutani budget: ${$corporate}
cmu-contracts-next-cycle = Next payment cycle in {$time}. Rent and loan instalments fall due then.
cmu-contracts-withdrawal-countdown = WEYLAND-YUTANI WITHDRAWAL IN {$time}
cmu-contracts-withdrawn = Weyland-Yutani has withdrawn from the colony. Prices are up and payouts are down.

cmu-contracts-section-rent = Site lease and licences
cmu-contracts-rent = Rent: ${$rent} per cycle (max ${$cap}).
cmu-contracts-rent-arrears = Rent: ${$rent} per cycle (max ${$cap}). WY is ${$arrears} behind.
cmu-contracts-licences = WY research licence: {$research} | WY tax break: {$tax}
cmu-contracts-granted = granted
cmu-contracts-revoked = revoked
cmu-contracts-rent-input = Rent per cycle
cmu-contracts-revoke-research = Revoke WY research licence
cmu-contracts-grant-research = Grant WY research licence
cmu-contracts-revoke-tax = Revoke WY tax break
cmu-contracts-grant-tax = Grant WY tax break

cmu-contracts-section-orders = Work orders
cmu-contracts-orders-description = Weyland-Yutani pays a premium on top of the normal price for goods delivered to the colony's submission storage. WY resells them off-world.
cmu-contracts-no-orders = No open work orders.
cmu-contracts-order = {$good}: {$delivered}/{$quantity} delivered, +{$premium}% premium
cmu-contracts-cancel-order = Cancel
cmu-contracts-quantity = Quantity
cmu-contracts-premium = Premium %
cmu-contracts-post-order = Post order
cmu-contracts-order-cost = ${$value} per unit, up to {$max} units, premium up to {$maxPremium}%. Holds ${$escrow} from the budget until filled.
cmu-contracts-freeze-orders = Freeze work orders until the next cycle

cmu-contracts-section-loans = Loans
cmu-contracts-loans-description-corporate = The colony and ambassadors can ask Weyland-Yutani for a loan. Set the interest, counter, accept or decline. Calling a loan in demands the full balance now.
cmu-contracts-loans-description-borrower = Ask Weyland-Yutani for a loan, then negotiate the amount and interest. It is repaid over three cycles of 15 minutes, taken automatically. A missed instalment charges the loan's interest again on what's left, and after three misses in a row the instalment is taken anyway, even if that puts you in debt.
cmu-contracts-no-loans = No loans.
cmu-contracts-loan-active = {$borrower} owes ${$owed} at {$interest}% over {$left} more {$left ->
    [one] cycle
   *[other] cycles
}.
cmu-contracts-loan-starting-debt = {$borrower} owes ${$owed} in existing debt at {$interest}%, over {$left} more {$left ->
    [one] cycle
   *[other] cycles
}.
cmu-contracts-loan-our-turn = {$borrower}: ${$amount} at {$interest}% interest. Your move.
cmu-contracts-loan-their-turn = {$borrower}: ${$amount} at {$interest}% interest. Waiting for the other side.
cmu-contracts-amount = Amount
cmu-contracts-interest = Interest %
cmu-contracts-counter = Counter
cmu-contracts-accept = Accept
cmu-contracts-decline = Decline
cmu-contracts-call-in = Call in loan
cmu-contracts-request-loan = Request loan

cmu-contracts-section-withdrawal = Withdrawal
cmu-contracts-withdrawal-description = The nuclear option, and there is no calling it off. After a five-minute warning WY pulls out: every loan is collected in full on the spot, even into debt, work orders and rent end, WY stops taking the tariff, and the colony pays 25% more and earns 25% less for the rest of the round. Needs WYC5 or above.
cmu-contracts-withdrawal-start = Begin corporate withdrawal
cmu-contracts-withdrawal-locked = Withdrawal ordered. It cannot be called off.

cmu-contracts-research-blocked = Weyland-Yutani's research licence has been revoked by the colony.
cmu-contracts-refuse-withdrawn = Weyland-Yutani has withdrawn from the colony.
cmu-contracts-refuse-loan-exists = There is already a loan with Weyland-Yutani.
cmu-contracts-refuse-corporate-funds = The corporate budget can't cover that.
cmu-contracts-refuse-orders-frozen = Work orders are frozen until the next cycle.
cmu-contracts-refuse-orders-full = Only {$max} work orders can be open at once.
cmu-contracts-refuse-withdrawal-rank = Only Executive Specialists and above can order a withdrawal.

cmu-contracts-announce-rent-set = The colony has set Weyland-Yutani's site rent to ${$rent} per cycle.
cmu-contracts-announce-rent-paid = Weyland-Yutani paid ${$paid} in site rent.
cmu-contracts-announce-rent-arrears = Weyland-Yutani paid ${$paid} in site rent and is ${$arrears} in arrears.
cmu-contracts-announce-research-revoked = The colony has revoked Weyland-Yutani's research licence. Corporate research is suspended.
cmu-contracts-announce-research-granted = The colony has granted Weyland-Yutani a research licence.
cmu-contracts-announce-tax-revoked = The colony has revoked Weyland-Yutani's tax break. Corporate supply orders now pay sales tax plus a 50% levy.
cmu-contracts-announce-tax-granted = The colony has granted Weyland-Yutani a tax break on corporate supply orders.
cmu-contracts-announce-order-posted = Weyland-Yutani work order: {$quantity} {$good}, paying +{$premium}% over the normal price.
cmu-contracts-announce-order-filled = Weyland-Yutani's work order for {$quantity} {$good} has been filled.
cmu-contracts-announce-order-expired = Weyland-Yutani's work order for {$good} expired with {$delivered}/{$quantity} delivered.
cmu-contracts-announce-order-cancelled = Weyland-Yutani has cancelled its work order for {$good}.
cmu-contracts-announce-orders-frozen = Weyland-Yutani has cancelled all work orders and won't post new ones until the next cycle.
cmu-contracts-announce-loan-requested = {$borrower} has asked Weyland-Yutani for a ${$amount} loan.
cmu-contracts-announce-loan-agreed = Weyland-Yutani has lent {$borrower} ${$amount} at {$interest}% interest.
cmu-contracts-announce-loan-declined = Loan talks between Weyland-Yutani and {$borrower} have broken down.
cmu-contracts-announce-loan-repaid = {$borrower} has repaid its debt to Weyland-Yutani.
cmu-contracts-announce-loan-missed = {$borrower} missed a loan payment to Weyland-Yutani. It now owes ${$owed}. {$left ->
    [one] One more missed payment and it will be collected by force.
   *[other] {$left} more missed payments and it will be collected by force.
}
cmu-contracts-announce-loan-forced = {$borrower} has missed three loan payments in a row. Weyland-Yutani has collected the ${$amount} instalment by force.
cmu-contracts-announce-loan-called-paid = Weyland-Yutani has called in its loan. {$borrower} paid the ${$owed} balance.
cmu-contracts-announce-loan-called-short = Weyland-Yutani has called in its loan. {$borrower} can't pay and now owes ${$owed}, all due next cycle.
cmu-contracts-announce-withdrawal-started = Weyland-Yutani has begun withdrawing from the colony. Withdrawal in {$minutes} minutes. All outstanding loans will be called in.
cmu-contracts-announce-withdrawal-done = Weyland-Yutani has withdrawn from the colony. All outstanding loans have been collected. Colony prices rise and payouts fall by {$percent}%.

cmu-work-order-good-steel-ore = steel ore
cmu-work-order-good-wood = wood
cmu-work-order-good-potato = potatoes
cmu-work-order-good-carrot = carrots
cmu-work-order-good-wheat = wheat
cmu-work-order-good-cotton = cotton bolls
cmu-work-order-good-salvage = grade 3 salvage
cmu-work-order-good-salvage-rare = grade 5 salvage
