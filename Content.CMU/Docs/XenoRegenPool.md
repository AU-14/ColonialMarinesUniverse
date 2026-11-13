# Xeno regeneration pool

Xenos and neomorphs start with three passive regeneration charges. Each crossing
below half of the critical-health threshold consumes one charge. Crossings within
five seconds of the last counted crossing are ignored. At zero charges, passive
health regeneration is reduced by 25% on friendly weeds and stops elsewhere;
plasma regeneration and other healing are unchanged.

Resting within six tiles of a living friendly queen or overmind, or a friendly
hive cluster, hive core, or recovery node, restores one charge every ten seconds.
Leaving range or stopping rest interrupts the current charge. A successful healer
drone or healer haze salve or sacrifice heal fully refills the pool. Evolution
and devolution retain the number of spent charges.

The `CMUXenoRegenPool` component on `CMXenoBase` defines `maxCharges`,
`healthThreshold`, `gracePeriod`, `rechargeTime`, `rechargeRange`, and
`depletedWeedsHealingMultiplier`. Individual
caste prototypes can override these fields. Admins with VarEdit can toggle
`cmu.xeno.regen_pool_enabled`; changing it resets existing pools.

The regeneration alert displays remaining charges, and xenos can examine other
xenos or neomorphs to see their charges. A warning appears when the pool empties.
