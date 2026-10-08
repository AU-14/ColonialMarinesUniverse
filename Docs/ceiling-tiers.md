# CMU ceiling tiers

Ceiling restrictions are cumulative. A higher tier blocks everything blocked by
lower tiers. This applies to all area prototypes, including existing maps.

| Tier | Preset | Additional blocked actions |
| --- | --- | --- |
| 0 | `RMCAreaProtectionZero` | None |
| 1 | `RMCAreaProtectionOne` | Mortar placement, laser designation, medevac, paradropping |
| 2 | `RMCAreaProtectionTwo` | Mortar fire, supply drops, Fulton extraction |
| 3 | `RMCAreaProtectionThree` | Close air support |
| 4 | `RMCAreaProtectionFour` | Orbital bombardment |

For new areas, inherit the appropriate protection preset and configure unrelated
properties such as weather, power, and hive construction separately. Use
`mortarPlacement` and `mortarFire`; `mortar` is not a valid Area field.

Legacy areas still specify individual permission flags. When an area initializes,
the strongest disabled permission determines its ceiling tier. For example,
`OB: false` makes the area tier 4 even if it also declares `CAS: true`; CAS and all
lower-tier actions are blocked. To lower a tier, enable every permission in the
tiers above the desired level. Explicitly enabling one weaker action does not
punch a hole through a stronger roof.

Hive cores and pylons apply their additional runtime protection through
`RoofingEntity`. Multi-Z mortar and orbital targeting still resolve the vertical
column; permission to strike does not guarantee impact on the selected floor.
These tiers describe aerial access, not structural tile durability or weather.
