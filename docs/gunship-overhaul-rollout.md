# Gunship overhaul rollout

The overhaul is disabled by default through `cmu.game.enable_gunship_overhaul`. Enable it only on a private canary server after the build and automated regression gates pass.

## Enable the canary

Add this to the canary server configuration and restart the server:

```toml
[cmu.game]
enable_gunship_overhaul = true
```

Keep hub advertising disabled and retain an otherwise identical control server with the CVar set to `false`.

## Required scenarios

Run each scenario once with one gunship and once with two simultaneously active gunships:

1. Enter and leave tactical hover, change altitude in both directions, land, and enter FTL.
2. Accelerate, coast, translate while rotating, reverse thrust, and repeat during an induced propulsion or maneuvering malfunction.
3. Sweep the hull past narrow platform edges and crowded terrain at maximum linear and angular speed. The hull must not tunnel through a blocker.
4. Hit two destructible contacts in the same step, then repeat with one indestructible contact. Results must be stable across repeated runs and insufficient energy must stop the gunship.
5. Start a hull and malfunction repair while landed, then attempt to take off or enter FTL before completion. Airborne work must not start and a state change must cancel completion.
6. Exercise pilot, visor, camera, zoom, night vision, alarms, crash, wreck, and control-release paths. Unequipped users must stop receiving HUD state.
7. Repeat movement under an artificial server stall. Catch-up must remain bounded and collision behavior must not depend on the rendered frame rate.

## Observe

Record server frame time, entity counts, bandwidth, and these Prometheus series for both the control and canary runs:

- `cmu_gunship_collision_spatial_queries`: must remain at one broadphase query per simulation step/probe.
- `cmu_gunship_flight_impacts_total`: compare impact counts with the scenario log.
- `cmu_gunship_impact_contacts`: verify simultaneous contacts are recorded as one batch.
- `cmu_gunship_hud_wearers`: must match the number of equipped, linked wearers and return to zero after cleanup.

Do not widen the pilot PVS scale during the canary. Test one and two gunships in the same populated area and compare visible entity count and outbound bandwidth with the control server.

## Promotion gate

Promote only if all required scenarios pass, no repair-state race or collision tunneling is observed, the spatial-query metric stays within budget, HUD membership returns to baseline after unequip/disconnect, and server frame time/bandwidth remain acceptable for the production capacity target.

## Rollback

Set `cmu.game.enable_gunship_overhaul = false` and restart. This immediately restores the pre-overhaul gunship systems without reverting commits. Preserve the canary logs and metric snapshot before retrying. Vehicle changes outside the gunship flight/integrity systems are not controlled by this CVar and require their own rollback decision.
