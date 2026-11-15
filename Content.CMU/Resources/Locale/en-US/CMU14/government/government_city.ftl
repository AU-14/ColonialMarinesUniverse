# The tabs
rumc-government-tab-relations = Factions
rumc-government-tab-cities = Cities
rumc-government-tab-credit = Credit
rumc-government-tab-licenses = Licences

# The kinds of cities
rumc-government-cap-tradehub = Trade hub
rumc-government-cap-armsmarket = Arms market
rumc-government-cap-intelligence = Intelligence
rumc-government-cap-blackmarket = Black market

# Credit
rumc-government-credit-rating = Credit rating: { $value }
rumc-government-credit-rating-hint = The higher it is, the more is lent and the better the terms. A payment on time raises it, a missed one hits it, a default breaks it.
rumc-government-credit-lenders = Lenders
rumc-government-credit-terms = Interest for the whole term: { $interest }%, instalments: { $count }, one every { $time }
rumc-government-credit-available = Can be taken: { $amount }
rumc-government-credit-take = Take { $amount }
rumc-government-credit-loans = Loans of the city
rumc-government-credit-no-loans = There are no loans.
rumc-government-credit-loan-line = { $lender }: instalment { $instalment }, { $left } left, the next in { $time }
rumc-government-credit-missed-line = Missed: { $count }
rumc-government-credit-repay = Pay off now ({ $sum })
rumc-government-investments = Investments
rumc-government-investments-hint = An investment takes a time and either pays with a profit or fails and gives back a small part.
rumc-government-investment-line = Cost { $cost }, time { $time }, chance { $chance }%, gives back { $payout } if it pays
rumc-government-investment-income = After it pays: money { $income } a minute
rumc-government-invest = Invest
rumc-government-investments-active = Investments at work
rumc-government-investment-running = { $name }: { $time } more
rumc-government-investment-success = { $name }: paid, brings money
rumc-government-investment-failure = { $name }: failed
rumc-government-loan-blocked-rating = A rating of { $rating } is needed.
rumc-government-loan-blocked-default = Will not lend for { $minutes } more min after the default.
rumc-government-loan-blocked-many = { $max } loans already.
rumc-government-loan-blocked-limit = The limit of this lender is used up.
rumc-government-investment-blocked-many = { $max } investments are at work already.

# The cities of the map
rumc-government-cities-title = The other cities of the map
rumc-government-cities-hint = Gifts and help in troubles make a city think better of you. With a good attitude, agreements can be made.
rumc-government-city-relation = Its attitude to you: { $value }
rumc-government-city-aid = Help in trouble ({ $cost })
rumc-government-city-crisis = Trouble: { $crisis }, { $time } more
rumc-government-city-calm = All is calm.
rumc-government-accords = Agreements with the cities
rumc-government-accords-slots = Agreements with the cities: { $used } of { $slots }
rumc-government-no-accords = There are no agreements.
rumc-government-accord-cost = Cost: { $money }, time { $time }, attitude from { $relation }
rumc-government-accord-requires = The city must be a: { $kind }
rumc-government-accord-sign = Make
rumc-government-accord-left = { $time } left
rumc-government-accord-in-force = It is in force already.
rumc-government-accord-slots-full = All the places for agreements are taken.
rumc-government-accord-need = The attitude must be { $relation }.
rumc-government-accord-city-lacks = The city is not of that kind.
rumc-government-city-gift = Gift ({ $cost })

# Licences
rumc-government-licenses-title = Giving licences
rumc-government-licenses-hint = A licence is given to a person who stands by the console. The fee is taken from his bank account into the budget of the city, and a card is printed for the mayor. The law officers write down the offences and the fines.
rumc-government-license-cost = Fee { $fee }, time { $time }
rumc-government-license-nearby = By the console
rumc-government-license-no-nearby = There is no one by.
rumc-government-license-issue = Give
rumc-government-licenses-issued = Licences given
rumc-government-license-none = There are no licences.
rumc-government-license-line = No. { $serial }, { $type }: { $holder }
rumc-government-license-valid = in force, { $time } left
rumc-government-license-expired = run out
rumc-government-license-revoked = taken back
rumc-government-license-revoke = Take back

# The card
rumc-license-card-examine = Licence: { $type }. Holder: { $holder }. No. { $serial }. { $status }
rumc-license-card-valid = Valid, { $minutes } min left.
rumc-license-card-expired = It has run out.
rumc-license-card-revoked = TAKEN BACK.
ent-RUMCCityLicenseCard = city licence card
    .desc = The card of a licence given by the mayor. Look at it closely to read what it says.
ent-RUMCCityLicenseCardWeapon = weapon permit card
    .desc = A card of a licence given by the administration. Look at it closely to read what it says.
ent-RUMCCityLicenseCardTrade = trade licence card
    .desc = A card of a licence given by the administration. Look at it closely to read what it says.
ent-RUMCCityLicenseCardMining = mining licence card
    .desc = A card of a licence given by the administration. Look at it closely to read what it says.
ent-RUMCCityLicenseCardNightWork = night work permit card
    .desc = A card of a licence given by the administration. Look at it closely to read what it says.
ent-RUMCCityLicenseCardBuild = building permit card
    .desc = A card of a licence given by the administration. Look at it closely to read what it says.
ent-RUMCCityDeedCard = plot deed
    .desc = A deed of a plot of the city. Look at it closely to read whose it is and where.

# The lenders
rumc-lender-city-bank-name = City bank
rumc-lender-city-bank-desc = Small sums and a high interest. Lends to anyone, even with a bad rating.
rumc-lender-trading-house-name = Trading house
rumc-lender-trading-house-desc = Middle sums and a fair interest. A decent rating is needed.
rumc-lender-metropolis-name = The metropolis
rumc-lender-metropolis-desc = Big sums and a low interest, but only for a city of good standing.

# The investments
rumc-investment-market-name = City market
rumc-investment-market-desc = A new row of stalls. Sure, but the profit is small.
rumc-investment-port-name = Port
rumc-investment-port-desc = A quay and stores. Dear and slow, but it brings money afterwards.
rumc-investment-mine-name = Mine on the outskirts
rumc-investment-mine-desc = Risky: it may find nothing. But when it does, it pays the most.

# The agreements with the cities
rumc-accord-trade-union-name = Trade union
rumc-accord-trade-union-desc = One set of rules of trade with a hub city: money and a cut of the duty.
rumc-accord-mutual-aid-name = Mutual aid
rumc-accord-mutual-aid-desc = The cities help each other in trouble: the calls of the third parties are cheaper.
rumc-accord-joint-works-name = Joint works
rumc-accord-joint-works-desc = Works and repairs in common: a good income from the common work.
rumc-accord-arms-supply-name = Arms supply
rumc-accord-arms-supply-desc = The arms city sells to the guard at a favour price: the calls of the third parties are much cheaper.

# The licences
rumc-license-weapon-name = Weapon permit
rumc-license-weapon-desc = The right to carry and use arms in the city.
rumc-license-trade-name = Trade licence
rumc-license-trade-desc = The right to trade at the market of the city and through the trade platforms.
rumc-license-mining-name = Mining licence
rumc-license-mining-desc = The right to mine on the lands of the city.
rumc-license-night-work-name = Night work permit
rumc-license-night-work-desc = The right to work the night shift.
rumc-license-build-name = Building permit
rumc-license-build-desc = The right to build outside the marked zones of land planning (building on the plot of another is still not allowed).
rumc-license-card-permanent = No end date while the plot is the holder's.

# The tabs
rumc-government-tab-land = Land

# The land
rumc-land-title = Land planning
rumc-land-hint = The mayor marks out the zones where building is allowed: housing, trade, production. People come to the console and ask for a plot (right-click the console). A plot is a square of 5 by 5 in a zone of the kind asked for; its rent is taken from the bank account every cycle, and the land tax is added on top.
rumc-land-tax = Land tax: { $value }% on the rent
rumc-land-tax-hint = The holder of a plot pays the tax together with the rent: the higher it is, the dearer it is to hold land.
rumc-land-enforce-on = No building outside the zones without a permit: ON
rumc-land-enforce-off = Building anywhere: OFF
rumc-land-enforce-hint = When on, the building menu is closed outside the zones (on the map where the console stands) and on the plots of others. The exception is the "{ $permit }" (fee { $fee }, given on the tab of the licences).
rumc-land-tablet = Give a surveyor's tablet
rumc-land-tablet-hint = Stand in the middle of the future zone and open the menu of the tablet with a right-click: pick the size and the kind of the zone. A mark costs { $cost }, at most { $max } zones; at most two tablets.
rumc-land-income = The rent gives { $sum } a cycle now ({ $time })
rumc-land-kinds = Kinds of zones
rumc-land-kind-line = { $name }: rent of a plot { $rent } a cycle
rumc-land-zones = Zones: { $used } of { $max }
rumc-land-zones-none = There are no zones.
rumc-land-zone-line = { $size }×{ $size }, corner X { $x }, Y { $y }; plots taken { $plots } of { $capacity }
rumc-land-zone-remove = Take off the zone
rumc-land-requests = Requests for a plot
rumc-land-requests-hint = The plot is picked by itself: the first free cell in a zone of the kind asked for. The deed falls into the hands of the mayor, who gives it to the holder.
rumc-land-requests-none = There are no requests.
rumc-land-request-line = { $name } asks for a plot: { $kind }
rumc-land-request-nofree = There is no free plot in the zones of this kind.
rumc-land-allot = Give a plot
rumc-land-decline = Refuse
rumc-land-plots = Plots given
rumc-land-plots-none = Nobody holds a plot.
rumc-land-plot-line = No. { $id }: { $holder }, { $zone }
rumc-land-plot-place = { $place }; rent { $rent } a cycle
rumc-land-plot-missed = Has not paid the rent: { $count } time(s)
rumc-land-plot-revoke = Take the plot back

# The land, in the world
rumc-land-zone-name = { $kind } No. { $id }
rumc-land-verb-mark = Mark a zone: { $kind } ({ $size }×{ $size })
rumc-land-verb-size = Size of the zone: { $size }×{ $size }
rumc-land-verb-unmark = Take off the zone you stand in
rumc-land-verb-request = Ask for a plot: { $kind }
rumc-land-verb-zones = Where in the city one may build
rumc-land-size-set = The next zone will be { $size }×{ $size }.
rumc-land-tablet-examine = The size of the next mark: { $size }×{ $size }.
rumc-land-tablet-in-zone = You stand in the zone: { $zone }.
rumc-land-no-grid = A zone can be marked only on the land of the city.
rumc-land-too-many = There are { $max } zones already, no more can be marked.
rumc-land-no-money = The budget has not enough for the marking: { $cost }.
rumc-land-overlap = A zone is marked here already: { $zone }.
rumc-land-marked = A zone is marked: { $zone } ({ $size }×{ $size }).
rumc-land-not-in-zone = You stand outside the zones.
rumc-land-unmarked = The zone is taken off: { $zone }.
rumc-land-request-refused = You have a request already, or the queue is full.
rumc-land-request-has-plots = You have { $max } plots already, no more are given.
rumc-land-request-sent = A request for a plot ({ $kind }) is sent to the mayor.
rumc-land-zones-empty = The mayor has not marked any zones yet.
rumc-land-zones-line = { $zone }: corner X { $x }, Y { $y }, { $size }×{ $size }
rumc-land-card-type = Plot: { $zone }, corner X { $x }, Y { $y }
rumc-land-allotted = You are given a plot in the zone "{ $zone }", corner X { $x }, Y { $y }.
rumc-land-declined = The mayor has refused you a plot.
rumc-land-taken-back = Your plot No. { $plot } is taken back.
rumc-land-rent-missed = You cannot pay the rent of plot No. { $plot } ({ $rent }). Once more and it is taken back.
rumc-land-denied-outside = Building here is not allowed: it is outside the zones of the city. The permit of the mayor is needed.
rumc-land-denied-plot = This is the plot of another: { $holder }.

# The zones
rumc-zone-housing-name = Housing
rumc-zone-housing-desc = Houses, barracks, hostels. The rent is small.
rumc-zone-trade-name = Trade
rumc-zone-trade-desc = Shops and stores by the square. The dearest rent.
rumc-zone-production-name = Production
rumc-zone-production-desc = Workshops, smelters, shops. A middle rent.
ent-RUMCSurveyorTablet = surveyor's tablet
    .desc = A command tablet with the plans of the city. Right-click it to mark zones; with it in the hand the marked zones are shown over the ground.
