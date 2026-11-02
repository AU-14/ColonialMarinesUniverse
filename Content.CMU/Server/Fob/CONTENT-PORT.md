# CMSS13 FOB equipment port

Source: [CMSS13 PR 9293](https://github.com/cmss13-devs/cmss13/pull/9293), final head `2cb8bdbb3e2ae072e3a46293856adddceec334d1`, and its linked design proposal. Original FOB artwork is credited to Thwomper. The PR was closed without merging. This content uses the existing default-off `cmu.fob.reserve_enabled` pilot.

| Original equipment | CMU entity | Function |
| --- | --- | --- |
| HVT-7T high-voltage transformer | `CMUFobRelay` | Physical timer-replenishment objective at the selected colony relay sites. Original transformer and capture/overlay frames imported. |
| UE-09 service terminal | `CMUFobControl` | Reserve controls, gun status and guide. Original deployed/off/folded art; wrench folding disables the terminal. |
| UH-02 systems platform | `CMUFobGunMount` | Two physical platforms delivered folded. Wrench deployment and powerloader transport; relocate within the selected LZ deployment area while support is off and the gun is removed. |
| UH-46 heavy sentry gun | `CMUFobSupportGun` | Original directional weapon art with tank-style turret movement; finite 30mm drum, 360-degree coverage, startup and acquisition warnings, repairable wreck. |
| UH-99 smart rocket launcher | `CMUFobRocketModule`, `CMUFobRocketLauncher` | Powerloader-installed alternative on one of the same two platforms. Requires live UE-04 coverage, three-second warning, finite twelve-rocket cassette. |
| UE-92 area illuminator | `CMUFobIlluminator` | Wrench-deployed reserve-powered floodlight; powerloader transport. |
| UE-04 grid sensor | `CMUFobSensor` | Portable sensor; wrench deployment, line-of-sight acquisition, destructible and nonblocking to movement/projectiles. |
| UE-11 generator unit | `CMUFobGenerator` | Deploy with wrench, activate locally for an interruptible one-use reserve contribution of up to 120 seconds. The bank records consumption, so replacement units cannot repeat it. |
| UE-02 deployable fence | `CMUFobFence` | Wrench deployment, cutters to recover while support is off, adjacent fence connections, powered contact shock. Always takes damage. |
| FOB crate and target markers | `CMUFobWeaponryCrate`, `CMUFobSupportCrate`, `CMUFobMonitoringCrate`, `CMUFobTargetWarning`, private relay map overlay | Original crate, animated lock-on warning and HVT minimap art. |

One set of weaponry, support and monitoring crates arrives through the friendly carrier's requisitions elevator. They contain the physical platforms, terminal, loaded gun modules, utilities, three stacks of ten fences, finite spare ammunition and tools. See [the full manifest and deployment instructions](LOGISTICS.md). Transport and assemble them within 40 tiles of the selected LZ center, outside the landing footprint. Either platform accepts the rocket module; it never creates a third active defense. Unbolting a gun preserves its magazine, and changing modules requires support to be off and a fresh activation. Broken replacements are empty.

The PR introduced machinery, not new handheld wrench/cutter/welder models. CMU uses its existing tools and powerloader clamps. Finite magazines are a CMU addition: the original PR weapons did not consume an ammunition supply. Their reused CMU ammunition art is distinct from the imported machinery art.

The rocket's direct payload is 1,100 blunt with 35 armor piercing. A directly hit T3 has a per-rocket damage allowance of its critical threshold plus 10, capped below its death threshold. Normal armor applies first; active shields absorb damage after this allowance is applied. The same allowance covers the queued blast from that rocket, so it cannot turn a healthy T3's direct-hit crit into an immediate death. It does not block other attacks or later rockets, and an already wounded T3 can die. Smaller castes can be killed outright. Blast-only victims receive the original modest explosion damage. Heavy-sentry and fence damage are unchanged. See [current measurements](DAMAGE.md).

Intentional gameplay adaptations retain rotating relay repairs instead of a permanently defended transformer, breachable machinery/fences, and ordinary contested boarding without a hijack lock. Rockets now home on one original sensor-acquired target. Any powered sensor belonging to the same FOB can supply a target within seven tiles and clear sight; the launcher has no separate firing arc, range or sight restriction. Airborne tracking ignores sight but freezes permanently at the last tracked position when the target moves over eleven tiles from its original sensor, enters roofing, becomes concealed/unavailable, or the sensor loses power. Missiles travel at twelve tiles per second, use real collision and explosion damage, and expire after twelve seconds. Terrain can intercept them. Utilities stay within the selected FOB's vicinity. The three starter crates and limited additional purchases use CMU's shipside requisitions and existing trained roles. The port does not recreate the Almayer storage-room map, three-platform allocation, salvage nerfs, or metal-economy changes.

## Asset provenance

See [equipment animation and effects](EFFECTS.md) for the CMU presentation layer and its multiplayer behavior.

All files under `Content.CMU/Resources/Textures/_CMU14/Structures/Fob/` come from the pinned source revision. Every RSI `meta.json` records its exact source file, credit and CC-BY-SA-3.0 license. DMI direction order and animation delays are retained, without redrawing. Imported sets: `backup_generator`, `electric_fence`, `illuminator`, `rocket_launcher`, `sensor`, `sentry`, `service_terminal`, `weapons_platform`, `transformer`, plus the relevant `Targeted`, `map_blips` and `crates` states. The source declares its asset license in its README and `LICENSE-CC-BY-SA-3.0.txt`.

The conversion verification compares all 168 selected source frames with exported RGBA pixels. Folded/deployed/off variants and all sixteen fence connections are retained. The original file has no damaged fence sprite; broken fences are removed as a breach instead of referencing a nonexistent state.
