# The tab and the console
rumc-citizen-tab-world = World order
ent-RUMCLogisticsConsole = logistics console
    .desc = The logistics console. The heads pick a landing zone and a city of the world map here, call the trade shuttle, order goods of the cities and send the shuttle away with what the colonists have put on it.

# The logistics console
rumc-foreman-title = Logistics console: the trade shuttle
rumc-foreman-subtitle = What is bought is paid by the budget of the colony, what is sold is put on by the colonists. Of the takings 70% goes to the budget and 30% to those who put the goods on.
rumc-foreman-shuttle = Shuttle
rumc-foreman-phase-idle = The shuttle is not called
rumc-foreman-phase-calling = The shuttle flies to the landing zone
rumc-foreman-phase-landed = The shuttle is on the zone: unload and load
rumc-foreman-phase-departing = The shuttle is leaving
rumc-foreman-phase-away = The shuttle is on its way to the city
rumc-foreman-phase-left = The phase ends in { $time }
rumc-foreman-picked = Landing zone: { $zone }. City: { $city }.
rumc-foreman-call = Call the shuttle
rumc-foreman-depart = Send the shuttle now
rumc-foreman-hint = The shuttle comes with what you ordered, stands on the zone for 5 minutes, and leaves with what the colonists put on board (goods are put on the console of the shuttle). While it is on the zone, the zone is taken: no other shuttle can land there. It cannot leave while there are people aboard. The pay comes when it has reached the city.
rumc-foreman-zones = Landing zones
rumc-foreman-zones-hint = The free zones of the colony. A zone where a shuttle stands is taken.
rumc-foreman-zones-none = There are no landing zones on this map.
rumc-foreman-zone-busy = { $name } (taken)
rumc-foreman-cities = Cities of the world map
rumc-foreman-cities-none = There are no cities on the map.
rumc-foreman-city-line = { $name }: { $caps }
rumc-foreman-city-crisis = Trouble in the city: { $crisis }
rumc-foreman-goods = Goods: { $city }
rumc-foreman-goods-hint = A purchase is at the price of the city (a little off), a sale at the price of now: the more of a good the city has already taken, the less it pays for the next, and the price comes back with time.
rumc-foreman-pick-city = Pick a city first.
rumc-foreman-lots = { $value } lot(s)
rumc-foreman-good-line = The city pays now: { $sell } for a piece ({ $percent }% of the full price). A lot: { $lot } pieces.
rumc-foreman-buy = Buy { $amount } pieces at { $price }: { $cost }
rumc-foreman-not-sold = This city does not sell it.
rumc-foreman-cart = Orders for the next trip
rumc-foreman-cart-none = There are no orders.
rumc-foreman-cart-line = { $good }: { $amount } pieces, paid by { $payer }, { $cost }
rumc-foreman-cart-cancel = Cancel the order
rumc-foreman-orders = Orders of the cities
rumc-foreman-orders-hint = The cities ask for goods on their own and pay more for them. An order is filled when the shuttle carries this good to this very city.
rumc-foreman-orders-none = The cities have no orders.
rumc-foreman-order-line = { $city }: { $good }, { $filled } of { $amount }, { $percent }% more in the price
rumc-foreman-order-left = { $time } left
rumc-foreman-shipment = On board the shuttle to be sold
rumc-foreman-shipment-hint = The colonists put goods on the console of the shuttle or load them into any crates aboard: everything that lies in the crates is sold too (the money for the crates goes to the colony). What is left lying on the floor goes with the shuttle and is lost, unpaid.
rumc-foreman-shipment-none = Nothing is put on.
rumc-foreman-shipment-line = { $good }: { $amount } pieces ({ $owner })
rumc-foreman-expected = Expected takings: { $sum }

# Why not, and what is told
rumc-world-fail-zone = Pick a free landing zone.
rumc-world-fail-load = The shuttle could not be made ready.
rumc-world-fail-flight = The shuttle could not take off. The orders are given back.
rumc-world-fail-passengers = There are people aboard the shuttle: let them go out.
rumc-world-intake-closed = The shuttle does not take cargo now.
rumc-world-intake-no-account = A bank account is needed to get your share.
rumc-world-intake-taken = Taken: { $amount } pieces ({ $good }), the city pays now { $price } for a piece.
rumc-world-paid = The city { $city } has paid for your goods: { $sum }.
rumc-world-payer-colony = the colony
rumc-world-order-refused = An order is not possible now: the shuttle is on a trip, or the cart is full.
rumc-world-order-no-money = Not enough money on the account: { $sum }.

# The console of the people
rumc-world-title = World order
rumc-world-hint = Order the goods of the cities with your own money. They come on the next trip of the trade shuttle (the heads call it), and you can take them when the shuttle stands on the zone. The price has a small fee for the delivery.
rumc-world-city = The shuttle goes to the city: { $city }
rumc-world-goods = What can be ordered
rumc-world-buy = Order { $amount } pieces at { $price }: { $cost }
rumc-world-orders = Your orders
rumc-world-orders-none = There are no orders.
rumc-world-order-line = { $good }: { $amount } pieces, { $cost }

# The goods
rumc-good-steel-name = Steel sheets
rumc-good-wood-name = Planks
rumc-good-glass-name = Glass
rumc-good-cloth-name = Cloth
rumc-good-plastic-name = Plastic
rumc-good-iron-ore-name = Iron ore
rumc-good-copper-ore-name = Copper ore
rumc-good-iron-ingot-name = Iron ingots
rumc-good-copper-ingot-name = Copper ingots
rumc-good-silver-ingot-name = Silver ingots
rumc-good-titanium-ingot-name = Titanium ingots
rumc-good-weapon-steel-ingot-name = Weapon steel ingots
rumc-good-plasteel-ingot-name = Plasteel ingots
rumc-world-crate-owner = Shuttle crates
