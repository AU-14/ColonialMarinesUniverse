# Explosion limb-loss comparison

The original blast-only baseline produced no limb loss in 1,280 controlled cases. The changes increase explosion severance accumulation on arms, legs, hands, and feet by a configurable factor of four. They do not multiply ordinary health damage or change head/torso severance rules.

A separate damage-cap bug made structural-heavy tank blasts unusually weak against people: structural damage counted against the cap even though the biological damage container discarded it. The cap now counts only supported damage types. This correction increases actual tank-blast damage as well as its ability to sever limbs.

## Method

- Measurements were collected in the September 26 development checkout before porting these fixes to current `master`. The CSVs retain those measured results; they are not a fresh measurement of the PR branch.
- Real authored explosion profiles, processed by the server's explosion queue and flood fill.
- Sixteen profiles: HEDP and HEFA grenades, C4, two IEDs, three tank rounds, five rockets, and three dropship missiles.
- Fresh humans, either unarmored or wearing `AU14ArmorM3JungleOne` and `ArmorHelmetM10` through the inventory system.
- Distances of 0, 1, 2, 4, and 6 tiles, on separate open-floor areas with no cover.
- Eight cases per armor/distance combination: four cardinal facing directions, repeated twice. The random seed resets to 5397 for each weapon profile.
- Results are counts from these scenarios, not universal live-round probabilities. Limb loss includes hands/feet; arms/legs and hands/feet also have separate CSV columns. Losing a parent limb can remove its attached hand/foot.
- The test measures the authored explosion and its medical processing. It does not launch the weapon, include separate physical fragmentation projectiles or direct projectile impact, or measure ongoing fire. In particular, HEFA fragments and direct rocket/tank hits can add injuries beyond this table.
- Floor breaking and vacuum creation are disabled so terrain destruction does not confound the comparison.

The complete results are in [explosion-delimb-before.csv](explosion-delimb-before.csv) and [explosion-delimb-after.csv](explosion-delimb-after.csv). Both runs completed successfully: 160 rows and 1,280 cases per run. `MeanDamage` is the aggregate explosion damage observed at receipt; `MaxSeveranceFraction` describes limbs still attached at that point, so it is not a measure of already-severed limbs.

## Results

Cases with limb loss increased from **0/1,280 to 320/1,280**. This is a comparison across the chosen distance/armor cases, not a 25% per-explosion chance. The after run lost 792 arms/legs in total. At point-blank range, HEDP removed an average of two arms/legs from unarmored humans; M3-armored humans retained their limbs in those cases.

Each table entry lists **cases with any limb loss out of eight**, at **0 / 1 / 2 tiles** respectively. Every corresponding before entry was zero.

| Explosion profile | Unarmored: 0 / 1 / 2 tiles | M3 + M10: 0 / 1 / 2 tiles |
| --- | --- | --- |
| HEDP grenade | 8 / 4 / 0 | 0 / 0 / 0 |
| HEFA grenade, blast only | 0 / 0 / 0 | 0 / 0 / 0 |
| C4 | 8 / 8 / 4 | 0 / 4 / 0 |
| IED | 8 / 4 / 4 | 0 / 0 / 0 |
| Large IED | 8 / 4 / 4 | 0 / 0 / 0 |
| Tank HE | 8 / 4 / 0 | 0 / 0 / 0 |
| Tank HEAT | 0 / 0 / 0 | 0 / 0 / 0 |
| Tank napalm, blast only | 0 / 0 / 0 | 0 / 0 / 0 |
| 84 mm HE rocket | 8 / 8 / 0 | 8 / 4 / 0 |
| 84 mm anti-armor rocket | 0 / 0 / 0 | 0 / 0 / 0 |
| 70 mm HE rocket | 8 / 8 / 0 | 8 / 4 / 0 |
| HJRA-12 HE rocket | 8 / 8 / 0 | 8 / 4 / 0 |
| HJRA-12 anti-tank rocket | 0 / 0 / 0 | 0 / 0 / 0 |
| Widowmaker missile | 8 / 8 / 8 | 8 / 8 / 8 |
| Keeper missile | 8 / 8 / 8 | 8 / 8 / 8 |
| Harpoon missile | 8 / 8 / 8 | 8 / 4 / 4 |

At four tiles, Widowmaker caused loss in 8/8 unarmored and 4/8 armored cases; Harpoon caused loss in 4/8 unarmored cases. At six tiles, only Widowmaker caused loss, in 4/8 unarmored cases. All other four- and six-tile cases retained their limbs.

The medical model distributes an epicenter hit over the body, while an off-center hit can concentrate damage on particular limbs. Results therefore need not decrease monotonically between zero and one tile, as the armored C4 cases illustrate. The zeroes for HEFA, anti-armor weapons, and napalm here do not include their separate fragments, direct hits, or continued burning.

Only the three structural-heavy tank profiles changed ordinary blast damage by more than 0.1 between these runs. Point-blank mean damage changed as follows; other weapons received the limb-severance adjustment without a general health-damage multiplier.

| Tank blast | Unarmored damage, before → after | M3 + M10 damage, before → after |
| --- | --- | --- |
| HE | 36.40 → 486.00 | 27.06 → 221.37 |
| HEAT | 36.40 → 291.60 | 27.05 → 132.80 |
| Napalm | 36.40 → 145.80 | 27.05 → 66.39 |

## Reproduction

Explicitly select `Content.IntegrationTests.CMU14.Medical.Anatomy.BodyParts.ExplosionDelimbBaselineTest.MeasureAuthoredExplosions` in `Content.IntegrationTests/Content.IntegrationTests.csproj`. A broad fixture filter skips this opt-in balance experiment. The test prints CSV records prefixed with `BLAST,`.

The severance tuning is `cmu.medical.severance.explosion_limb_multiplier`, default `4`. Setting it to `1` restores the old limb accumulation rate, but does not undo the separate damage-cap correction.
