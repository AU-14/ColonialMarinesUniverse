# Yautja Dropship Moving-Pad Landing Design

## Goal

When the Hunter Shuttle returns through `Landing Pad A` or `Landing Pad B`, it must land on the pad's current position and orientation. The result must match the red landing footprint shown in the gameplay report, even if the Hunter Ship upper-deck grid moves while the shuttle is in FTL.

## Corrected Root Cause

The A/B destination markers are children of the dynamic Hunter Ship upper-deck grid. The current `DropshipSystem.FlyTo` special case converts a Yautja destination from grid-local coordinates to map coordinates at departure. This freezes the marker's old world pose in the `FTLComponent`.

While the shuttle is travelling, the upper-deck grid can move. Arrival therefore uses the old map position instead of the landing pad's current position. The existing regression test hides this bug because it snapshots each destination's world pose before departure and asserts against that stale pose.

The hull-center correction itself remains valid: the A/B beacon denotes the desired center of the 7 by 13 Hunter Shuttle footprint, while the shuttle transform denotes the grid origin.

## Selected Design

Keep Yautja destinations grid-relative throughout the FTL trip.

1. `DropshipSystem.FlyTo` passes the destination's mover coordinates and local rotation to `FTLToCoordinates` without converting Yautja targets to the destination map.
2. At arrival, the existing no-docking-config Yautja path resolves those grid-local coordinates through the destination grid's current world transform.
3. The path computes the current destination world rotation, rotates the shuttle `LocalAABB.Center`, and subtracts it from the current destination position to obtain the shuttle grid origin.
4. The arriving shuttle remains parented to the map rather than nested under the Hunter Ship grid.
5. The landed shuttle remains static, on-ground, and fixed-rotation, matching the round-start shuttle physics state.

Ordinary dropship destinations retain the existing proximity fallback. The Hangar destination continues to use the same grid-relative Yautja arrival path; this correction does not change its map marker.

## Alternatives Rejected

- Freezing all Hunter Ship grids would broaden the change into Z-level and station physics and could break unrelated movement or synchronization behavior.
- A joint or continuous post-landing synchronization between the Hunter Ship and shuttle grids would add lifecycle and cleanup complexity that is unnecessary for resolving a destination during FTL.
- Moving A/B map markers would encode a runtime movement bug into static map geometry and would not solve stale coordinates when the parent grid moves.

## Verification

The integration regression will exercise the real FTL path for both A and B:

1. Start the shuttle flight and verify the stored FTL target remains relative to the Hunter Ship grid rather than the map entity.
2. Move and rotate the Hunter Ship grid after departure but before arrival.
3. Wait for FTL cooldown.
4. Assert that the arrived shuttle hull center matches the destination marker's current world position within `0.01` tile.
5. Assert that the shuttle uses the destination's current world orientation and remains a map-parented static/fixed-rotation grid.

The destination-isolation tests continue to verify that non-Yautja consoles cannot use these landing points.

## Non-goals

This change does not make the landed shuttle continuously follow later Hunter Ship movement, alter Z-level synchronization, change map marker placement, or modify ordinary dropship landing behavior.
