# Yautja Dropship Landing Alignment Design

## Goal

When a Hunter Shuttle returns to the Yautja Hunter Ship, its hull must occupy the landing pad exactly as it did at round start. This applies to landing pad A, landing pad B, and the hangar destination.

## Current Behavior and Root Cause

The Yautja destination marker represents the geometric center of the shuttle landing area. The Hunter Shuttle transform, however, represents the origin of its grid. The shuttle grid is 7 by 13 tiles, so its local bounding-box center is `(3.5, 6.5)`.

At round start, pad A spawns the shuttle with its grid origin offset from the marker so that the hull center is aligned with the pad. The current FTL arrival code places the grid origin directly on the destination marker. This shifts the entire hull by its local center even though the transform technically reaches the selected marker.

## Selected Design

Treat a Yautja dropship destination as the desired world position of the shuttle hull center.

During exact Yautja arrival:

1. Convert the destination to map coordinates.
2. Resolve the destination's world rotation.
3. Read the arriving shuttle grid's `LocalAABB.Center`.
4. Rotate that local center by the destination world rotation.
5. Subtract the rotated center from the destination position to obtain the shuttle grid origin.
6. Parent the shuttle grid directly to the destination map and apply the resolved world rotation.

This keeps the grid out of the Hunter Ship grid hierarchy while aligning the visible hull rather than its internal origin.

## Alternatives Rejected

- Moving map markers to the shuttle grid origin would make them dependent on this shuttle's dimensions and would no longer describe the center of a landing pad.
- Adding per-marker landing offsets and angles would duplicate values that can be derived from the shuttle grid and destination transform.

## Verification

The integration test will exercise the real FTL path and verify:

- the existing implementation fails because the post-arrival hull center does not match the selected marker;
- after the fix, the world-space center of the Hunter Shuttle matches each A, B, and Hangar marker within `0.01` tile;
- the shuttle is parented to the Hunter Ship map rather than nested under its grid;
- on pad A, the shuttle grid origin and world rotation match the round-start placement;
- non-Yautja destinations continue to use the existing proximity path.

## Non-goals

This change does not alter ordinary dropship landing behavior, docking-port selection, destination visibility, shuttle map geometry, or the placement of Hunter Ship markers.
