ent-RUMCTradeParcel = parcel
    .desc = A sealed cardboard parcel. Whatever is packed in it is sold with it on a trade platform. Tear it open to take the things out.
ent-RUMCTradePacker = parcel packer
    .desc = Takes cardboard and whatever is put into it, and when it is switched on it seals the things in a parcel. One sheet of cardboard seals eight things. Click it with an empty hand to pack.
ent-RUMCTradePlatform = trade platform
    .desc = A landing pad for the drones of the trade. Put the contracts of the companies into it (nine at most): they open what the drones buy and sell. A parcel put on the pad can be sold, and what is bought is left on it. A drone comes ten seconds after the call, and only to a pad under the open sky.
ent-RUMCTradeDrone = trade drone
    .desc = A drone of the trade. It comes for a parcel or brings one.

au14-craft-recipe-tradepacker = Parcel packer
au14-craft-recipe-tradeplatform = Trade platform
au14-craft-recipe-cardboard = Cardboard

rumc-trade-packer-cardboard-loaded = You load the cardboard. Sheets in the packer: { $count }.
rumc-trade-packer-cardboard-full = The packer cannot take more cardboard.
rumc-trade-packer-full = There is no more room in the packer.
rumc-trade-packer-cannot-take = The packer does not take { $item }.
rumc-trade-packer-item-loaded = You put { $item } into the packer. Things inside: { $count }.
rumc-trade-packer-empty = There is nothing in the packer.
rumc-trade-packer-no-cardboard = Not enough cardboard: { $need } sheets are needed, { $have } loaded.
rumc-trade-packer-started = The packer starts working.
rumc-trade-packer-done = The packer seals a parcel.
rumc-trade-packer-take-out = Take everything out of the packer
rumc-trade-packer-examine = Cardboard in the packer: { $cardboard } sheets, things inside: { $items }. They take { $need } sheets.

rumc-trade-parcel-open = Tear the parcel open
rumc-trade-parcel-opened = You tear the parcel open.
rumc-trade-parcel-examine = Things in the parcel: { $count }.

rumc-trade-busy = A drone is already called.
rumc-trade-no-sky = The platform is not under the open sky: a drone cannot come.
rumc-trade-no-sky-arrival = The platform was covered from the sky, the drone did not come. The money is returned.
rumc-trade-no-parcel = There is no parcel on the platform.
rumc-trade-no-card = Your ID card with a bank account is needed.
rumc-trade-no-money = There is not enough money on your card.
rumc-trade-called = A drone is called and comes in { $seconds } s.
rumc-trade-sold = The drone took the parcels: { $worth }, tax of the colony { $tax }, to the seller { $payout }.
rumc-trade-delivered = The drone left a parcel.

rumc-trade-ui-title = Trade platform
rumc-trade-ui-sell-section = Selling
rumc-trade-ui-sell-hint = Put a sealed parcel on the platform.
rumc-trade-ui-sell = Sell the parcels
rumc-trade-ui-parcels = Parcels on the platform: { $count }
rumc-trade-ui-value = Worth: { $value }
rumc-trade-ui-tax = Tax of the colony: { $percent }% ({ $tax })
rumc-trade-ui-payout = You will get: { $payout }
rumc-trade-ui-buy-section = Buying
rumc-trade-ui-balance = On your card: { $money }
rumc-trade-ui-no-card = No ID card with a bank account.
rumc-trade-ui-price = { $price }
rumc-trade-ui-buy = Buy
rumc-trade-ui-busy = A drone is already called.
rumc-trade-ui-sky-open = The platform is under the open sky.
rumc-trade-ui-sky-closed = The platform is covered from the sky: a drone will not come.


rumc-trade-company-ores = Heavy Stone Mining Co.

rumc-trade-contracts-full = There is no more room for contracts in the platform.
rumc-trade-contract-present = A contract with this company is already in the platform.
rumc-trade-contract-refused = The contract cannot be put in.
rumc-trade-contract-installed = You put the contract of { $company } into the platform.
rumc-trade-nothing-wanted = None of the contracts in the platform buys what lies on it.
rumc-trade-locked = This good opens at a higher level of the contract.
rumc-trade-platform-examine = Contracts in the platform: { $count } of { $max }.
rumc-trade-contract-examine-next = To the next level: { $next } points.

rumc-trade-ui-contracts-section = Contracts
rumc-trade-ui-contracts-count = Contracts: { $count } of { $max }
rumc-trade-ui-no-contracts = No contracts: the platform buys and sells nothing.
rumc-trade-ui-contract-standing = Level { $level }, points { $points } of { $next }
rumc-trade-ui-contract-standing-max = Level { $level } (maximum), points { $points }
rumc-trade-ui-eject = Take out
rumc-trade-ui-needs-level = Needs level { $level }

ent-RUMCTradeContractMetals = ores contract
    .desc = A contract with a mining company for the ores and the crystals. Put it into a trade platform to sell it ore and crystals, to order ready ingots of it ten to a lot, and the crystals.
ent-RUMCTradeContractGems = gems contract
    .desc = A contract with a mining company for the gems. Put it into a trade platform to sell it raw gems and to buy them of it.
ent-RUMCTradeContractFuel = fuel contract
    .desc = A contract with a mining company for the fuel. Put it into a trade platform to sell it coke and to buy it of it.
ent-RUMCCrateTradeStart = trade kit crate
    .desc = A sealed crate with the start of the trade, a parcel packer with cardboard, a trade platform and four contracts. Open it with a crowbar.

rumc-trade-company-label = { $company } - { $category }
rumc-trade-category-metals = ores and crystals
rumc-trade-category-gems = gems
rumc-trade-category-fuel = fuel
rumc-trade-contract-metals-desc = All the metal ores and the crystals of the mines, from the most common to the rarest, and three alloys: brass, weapon steel and plasteel. The company buys the ore, and ready ingots of the same metal are ordered of it ten to a lot. The ingots of the alloys, which have no ore, and the crystals are bought and ordered as they are.
rumc-trade-contract-gems-desc = The raw gems of the mines. A drone pays for a gem a tenth of the price the company asks for it.
rumc-trade-contract-fuel-desc = The coke of the mines. A drone pays for it a tenth of the price the company asks for it.
rumc-trade-contract-examine = Company: { $company }, category: { $category }. Level { $level } of 3, points of trade: { $points }.
rumc-trade-ui-contracts-hint = To put a contract in, hit the platform with it.
rumc-trade-ui-category = { $name } ({ $count })
rumc-trade-ui-no-goods = Nothing to order: put a contract in.

ent-RUMCTradeContractSheets = sheets contract
    .desc = A contract with a mining company for the sheets of metal. Put it into a trade platform to sell it sheets and to order sheets of it, ten to a lot.
rumc-trade-category-sheets = sheets
rumc-trade-contract-sheets-desc = The sheets of all the metals and alloys, from iron to uranium. A sheet is pressed of an ingot, and an ingot is four ores: a drone pays four times the price of the ore for a sheet, and ten sheets are ordered for the ten ingots with an addition for the pressing.
