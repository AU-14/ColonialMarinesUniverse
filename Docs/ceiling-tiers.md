# CMU ceiling tiers

Ceiling restrictions are cumulative. A higher tier blocks everything blocked by
lower tiers. This applies to all area prototypes, including existing maps.

| Tier | Preset | Additional blocked actions |
| --- | --- | --- |
| 0 | `RMCAreaProtectionZero` | None |
| 1 | `RMCAreaProtectionOne` | Mortar placement, laser designation, medevac, paradropping |
| 2 | `RMCAreaProtectionTwo` | Mortar fire, supply drops, Fulton extraction |
| 3 | `RMCAreaProtectionThree` | Close air support |
| 4 | `RMCAreaProtectionFour` | Orbital bombardment, dropship tactical landing |

For new areas, inherit the appropriate protection preset and configure unrelated
properties such as weather, power, and hive construction separately. Use
`mortarPlacement` and `mortarFire`; `mortar` is not a valid Area field.

Legacy areas still specify individual permission flags. When an area map-initializes,
the strongest disabled permission determines its ceiling tier. For example,
`OB: false` makes the area tier 4 even if it also declares `CAS: true`; CAS and all
lower-tier actions are blocked. To lower a tier, enable every permission in the
tiers above the desired level. Explicitly enabling one weaker action does not
punch a hole through a stronger roof.

Hive cores and pylons apply their additional runtime protection through
`RoofingEntity`. Multi-Z mortar and orbital targeting still resolve the vertical
column; permission to strike does not guarantee impact on the selected floor.
Dropship tactical landings additionally require an open z-level column above the
touchdown tile, so solid ceilings block them regardless of the area's tier.
These tiers describe aerial access, not structural tile durability or weather.

## Where the tier comes from

Every tile has a tier, and nothing has to be painted for it. An area that was graded
on purpose (`ceilingGraded`, the default) owns its tier through the flag ladder above.
Any other area, and any tile with no area at all, takes the tier from the world: a map
below the surface is 4, a tile with a sealed sky above it is 3
(`CMUTopDownOrdnanceSystem.IsOpenToSky`, the same opening predicate the roof coverage
derives from), open sky is 0. Geometry only tightens; it never lowers a tier. An
authored denial still holds where the tier is derived. A mapper opts a structure out
with a preset: `RMCAreaProtectionZero` under a real roof keeps it CAS-able. The CMU
planet roots that were never graded carry `ceilingGraded: false` (LV-376, Garrison,
Sheperds' Pride, Ascanius, Fort McNeil).

## Roofs, caves and munitions

A derived roof (tier 3) is opened one fire sequence at a time. The first sequence from
a munition with `breachRadius` above zero detonates on the roof and deletes that many
tiles; later sequences fall through the hole and nowhere else. A roof declared by a
graded area is not carved: it is refused outright, as before.

Caves (tier 4) refuse everything except munitions with `penetratesRock`, which pass
through intact rock on their first sequence, detonate at the target only and never open
anything; depth is still bounded by `zLevelPenetration`. Hive cores and pylons veto
every CAS munition within range. A munition never detonates below its target.
