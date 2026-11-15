# The tab
rumc-citizen-tab-trade = Trade

# The mayor
rumc-land-sales-tax = Sales tax: { $value }% of the price
rumc-land-sales-tax-hint = Of every sale between the townsfolk (from hand to hand, at a stall, at an auction) this share goes to the budget. The seller gets the rest.

# The console of the people
rumc-trade-title = Selling of finished goods
rumc-trade-hint = The buyer always pays out of his own pocket and gets the thing in his hands at the same moment, so neither can cheat the other. { $tax }% of every sale goes to the budget of the city. The third way is the market stall, which has a window of its own.
rumc-trade-hand = From hand to hand
rumc-trade-hand-hint = Hold the goods in your hand, pick a person who stands by the console and set a price. He confirms at his console, and the thing goes to him.
rumc-trade-price-placeholder = Price
rumc-trade-offer = Offer the thing in the hand
rumc-trade-offers = Offers
rumc-trade-offers-none = There are no offers.
rumc-trade-offer-line = { $seller } sells to { $buyer }: { $item } for { $sum }
rumc-trade-accept = Buy for { $sum }
rumc-trade-auctions = Auctions of things
rumc-trade-auction-hint = Hold a thing in your hand and put it up for auction. The thing stays in the console while the auction goes. The highest bid wins, and a bid is held out of the account at once. If there are no bids, the thing comes back to you.
rumc-trade-auction-start-placeholder = Start price
rumc-trade-auction-start = Put the thing in the hand up for auction
rumc-trade-auctions-none = There are no auctions of things.
rumc-trade-auction-line = { $item } (seller { $seller })
rumc-trade-auction-cancel = Take the thing back
rumc-trade-stall = Market stall
rumc-trade-stall-hint = A stall is a table kept by a trader with a licence. Put a thing on it (click it with the thing in your hand), set the price in the window of the stall, and any buyer buys the goods at once.
rumc-trade-offer-refused = It did not work: a thing in the hand is needed, you have an offer already, or the queue is full.
rumc-trade-offered = The offer is sent: { $name }.
rumc-trade-offered-to-you = { $name } offers you a thing to buy. Open the console of the people, the tab "Trade".
rumc-trade-both-here = Both sides must stand by the console.
rumc-trade-item-gone = The thing is not in the hand of the seller any more.
rumc-trade-no-account = Both sides must have bank accounts.
rumc-trade-no-money = Not enough money on the account: { $sum }.
rumc-trade-sold = Sold: { $item } for { $sum }.
rumc-trade-bought = Bought: { $item } for { $sum }.
rumc-trade-auction-refused = It did not work: a thing in the hand and a bank account are needed, or the auctions are full.
rumc-trade-own-auction = You cannot bid at your own auction.
rumc-trade-auction-won = You won the auction: { $item } for { $sum }.

# The market stall
ent-RUMCMarketStall = market stall
    .desc = A table for the goods of a trader. A trade licence is needed: click the stall with a thing in your hand to put it on, and set the price in the window of the stall. A buyer sees the prices and buys at once.
rumc-stall-title = Market stall
rumc-stall-free = The stall is free
rumc-stall-owner = Trading: { $owner }
rumc-stall-tax = Sales tax: { $value }% (paid by the seller out of the takings)
rumc-stall-owner-hint = Set a price for each thing, at most { $max } things on the stall. A price of nought means "not for sale".
rumc-stall-free-hint = To take the stall a trade licence is needed: click it with a thing in your hand.
rumc-stall-empty = There is nothing on the stall.
rumc-stall-price = Price: { $sum }
rumc-stall-not-for-sale = Not for sale
rumc-stall-price-placeholder = Price
rumc-stall-set-price = Set the price
rumc-stall-take = Take the thing back
rumc-stall-buy = Buy for { $sum }
rumc-stall-release = Let the stall go
rumc-stall-examine-free = The stall is free.
rumc-stall-examine-owner = Trading: { $owner }. Things on the stall: { $count }.
rumc-stall-not-yours = The stall is taken: { $owner } trades here.
rumc-stall-need-license = A trade licence is needed to trade.
rumc-stall-full = There is no room on the stall.
rumc-stall-cannot = The thing could not be put on the stall.
rumc-stall-placed = Put on the stall: { $item }. Set the price in the window of the stall.
rumc-stall-cannot-pay = Could not pay: { $sum } is needed on the account, and the seller must have an account.
rumc-stall-bought = Bought: { $item } for { $sum }.
rumc-stall-sold = Sold: { $item } for { $sum }.
