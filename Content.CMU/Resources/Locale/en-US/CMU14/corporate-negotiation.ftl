## Supplier Negotiation terminal

cmu-negotiation-title = Procurement
cmu-negotiation-heading = Supplier Negotiation
cmu-negotiation-instructions = Talk the supplier down from their list price. They have a price they won't go below and only so much patience. A fair offer gets a counter-offer and costs a little patience; a lowball offer insults them and costs more. Offer at or above their asking price, or accept it, to close. The closer you get to their limit, the more you earn. If they run out of patience, they walk away and you earn nothing.
cmu-negotiation-tiers-section = Open talks
cmu-negotiation-tier =
    {$name}, up to ${$reward}
    patience: {$patience} offers
cmu-negotiation-tier-routine = Routine order
cmu-negotiation-tier-bulk = Bulk contract
cmu-negotiation-tier-strategic = Strategic supplier
cmu-negotiation-log = Talks
cmu-negotiation-offer = Make offer
cmu-negotiation-accept = Accept their price
cmu-negotiation-step = { $percent ->
    [0] 0%
   *[other] {$percent}%
}
cmu-negotiation-your-offer = Your offer: ${$price}
cmu-negotiation-row-supplier = Supplier: {$supplier}
cmu-negotiation-row-goods = Goods: {$goods}
cmu-negotiation-row-list = List price: ${$price}
cmu-negotiation-row-ask = Asking now: ${$price}
cmu-negotiation-row-patience = Patience: {$patience}
cmu-negotiation-reply-counter = You offered ${$offer}. They come down to ${$ask}.
cmu-negotiation-reply-insulted = You offered ${$offer}. They're insulted and stay at ${$ask}.
cmu-negotiation-reply-accepted = You offered ${$offer}. Deal.
cmu-negotiation-reply-walkedaway = You offered ${$offer}. They've had enough and walk away.
cmu-negotiation-status-active = {$name}. Make your offer.
cmu-negotiation-status-closed = Closed at ${$price} against a list price of ${$list}. Paid ${$payout} to the corporate budget.
cmu-negotiation-status-walked = The supplier walked away. No deal.
cmu-negotiation-status-cooldown = Next supplier in {$seconds}s.
cmu-negotiation-status-idle = Pick a supplier to start.
cmu-negotiation-cooldown = The terminal is still filing the last deal.
cmu-negotiation-supplier-1 = Kelland Mining
cmu-negotiation-supplier-2 = Seegson Logistics
cmu-negotiation-supplier-3 = Arcturian Shipping
cmu-negotiation-supplier-4 = Grant Corporation
cmu-negotiation-supplier-5 = Lasalle Bionational
cmu-negotiation-supplier-6 = Hyperdyne Supply
cmu-negotiation-supplier-7 = Gateway Freight
cmu-negotiation-supplier-8 = Ares Industrial
cmu-negotiation-goods-1 = water purification filters
cmu-negotiation-goods-2 = atmospheric processor parts
cmu-negotiation-goods-3 = medical supplies
cmu-negotiation-goods-4 = laboratory reagents
cmu-negotiation-goods-5 = construction steel
cmu-negotiation-goods-6 = ration packs
cmu-negotiation-goods-7 = power cells
cmu-negotiation-goods-8 = specimen containment units
cmu-negotiation-goods-9 = vehicle spares
cmu-negotiation-goods-10 = security equipment
