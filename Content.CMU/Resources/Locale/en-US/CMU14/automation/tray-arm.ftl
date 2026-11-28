ent-CMUTrayArmHarvester = harvester arm
    .desc = Faces left or right and works the 3x3 block of hydroponics trays on that side, up to three tiles away, one tray at a time. It harvests a ripe plant, clears away a dead one, and keeps the produce for an inserter to take. It leaves automatic trays alone. Needs power. Wrench it to anchor or move it; right-click to turn it to face the other way.
ent-CMUTrayArmPlanter = planter arm
    .desc = Faces left or right and works the 3x3 block of hydroponics trays on that side, up to three tiles away, one tray at a time. It plants a seed packet from its seed slots in an empty tray. It leaves automatic trays alone. Load it by hand or with an inserter. Needs power. Wrench it to anchor or move it; right-click to turn it to face the other way.
cmu-tray-arm-examine-harvest = Harvests the 3x3 block of hydroponics trays to the { $side }, up to three tiles away, and keeps the produce for an inserter to take from behind it.
cmu-tray-arm-examine-plant = Plants seed packets in the 3x3 block of hydroponics trays to the { $side }, up to three tiles away.
cmu-tray-arm-examine-filter = It only plants { $count ->
    [one] one species
   *[other] { $count } species
    }.
cmu-tray-arm-verb-filter-add = Plant this species
cmu-tray-arm-verb-filter-clear = Clear the seed filter
cmu-tray-arm-filter-added = The planter will now plant { $seed }.
cmu-tray-arm-filter-cleared = The planter will plant any seed again.
cmu-tray-arm-verb-take-seeds = Take the seed packets
