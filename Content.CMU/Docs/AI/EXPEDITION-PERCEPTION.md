# Expedition perception and contact searches

The expedition controller separates candidate discovery, visibility, recognition, and target selection. Guards accumulate sampled visual exposure before recognizing unfamiliar distant enemies, while immediate nearby threats retain priority. Lost contacts and sounds lead to finite searches of recorded positions, followed by a return to standing orders.

The implementation belongs to the server-side expedition system. Existing native weapons, ammunition, movement, collision, medical actions, cover, squad coordination, and the decision scheduler remain responsible for execution.

## Recognition

1. Reuse the nearby-hostile spatial query and faction filtering. Reject dead entities, other maps, and targets outside detection range before acquisition sight checks.
2. Sort candidates by distance with entity ID as a stable tie-break. Check the current target and two nearest candidates first, then recent shooters and a rotating selection of remaining candidates.
3. Limit acquisition visibility checks to `sightChecksPerThink`, default **8**, clamped to **3–16**. Native firing checks remain independent of this allowance.
4. Successful visibility contributes at most one 150 ms decision interval of exposure. Repeating a decision at the same timestamp adds none. Deferred checks cannot accumulate unseen exposure; an actual occlusion clears partial exposure.
5. Recognize enemies immediately within **5 tiles**, nearby melee threats, visibly observed recent shooters, and the remembered current visual target. Other contacts require sampled exposure, up to the default **0.6 seconds**, multiplied by **1.5** when another target holds attention. Distance scales the requirement down to a minimum factor of 0.25. Acquisition can take longer in wall-clock time under load.
6. Keep at most **32 contact records per agent**. Expire old/deleted entries and recycle older recognized entries before recent partial observations, while retaining the current target. Rotating the candidate checks allows a larger crowd to be serviced over time.
7. Pass recognized, currently visible candidates into existing target scoring, melee response, threat-sector assessment, and target commitment logic.

Recognition memory does not grant visibility or permission to fire. Current sight, smoke, darkness, range, weapon readiness, and friendly-fire geometry still apply. A radio-reported identity cannot authorize an aimed shot before visual recognition. Existing uncertain muzzle-flash and incoming-fire responses keep their own rules.

The candidate and shot-neighbor scratch collections are reused synchronously across decisions. Acquisition skips its firing-lane neighbor query when there are no recognized candidates. In a dense fight, threat-sector assessment reflects the observed subset for that decision.

## Search lifecycle

A search records the observation point, evidence timestamp, source kind, absolute expiry, and inspection deadline. It does not follow an unseen target's current transform.

The normal lost-sight delay still applies. Search expiry defaults to **12 seconds after the evidence was observed**. Route retries and suppression do not renew it; fresh evidence can. Visual/radio searches can extend ordinary contact memory to this deadline. Sound retains its existing ten-second hearing lifetime.

At a reachable point with line of sight to the recorded area, the guard stops and inspects for **1.5 seconds**. The usual inspection radius is **2.5 tiles**; melee contacts retain their standoff distance. A nearby point across a wall is not considered inspected. Sound investigations do not inherit an unrelated previous melee standoff.

Hold orders, recovery duties, significant damage, withdrawal, and heavy suppression prevent advances without renewing the evidence. Emergency and utility actions retain their priority. A recognized visible enemy ends the search. Explicit commands, death, and possession cancel transient search state. Checked or expired evidence releases the contact and returns control to standing orders.

Search cleanup releases only its own investigation route so that a survival or utility controller's replacement route remains intact. Navigation uses existing route-search budgets, reservations, and failed-route backoff.

## Scheduling and diagnostics

The existing 3 ms soft decision budget, oldest-deadline-first scheduling, per-update decision-count limit, and separate weapon execution remain unchanged. No additional per-frame AI loop, task, thread, or world-wide scan is introduced.

These are bounds on specific sources of work, not a bound on total server time. A single decision can exceed the soft time budget. Candidate queries and sorting, native navigation, firing, and other systems retain their own costs.

The new tuning fields are `immediateRecognitionRange`, `recognitionTime`, `sightChecksPerThink`, `contactSearchDuration`, and `contactInspectDuration`. Existing aggression, courage, doctrine, weapon skill, and target commitment settings remain in effect.

`cmu-expedition-ai-status` displays acquisition sight checks used/allowed, contact-record count, active search status, and remaining search time. Decision history distinguishes approaching, inspecting, holding under pressure, checked, and expired outcomes. Existing timing diagnostics remain available for population tests.

## Verification

The focused fixture `CMUContactPerceptionTest` calls the real sensor and search methods with controlled decision timestamps, native visibility and geometry, and the complete controller's return to an order. It covers:

| Behavior | Regression test |
| --- | --- |
| Sampled exposure, occlusion, and last-known position retention | `DistantRecognitionRequiresExposureAndAnOcclusionBreaksIt` |
| Bounded acquisition and progress through 40 enemies | `RotatingSightBudgetEventuallyRecognizesCrowdAndPrioritizesCloseThreat` |
| Radio identity cannot authorize an unrecognized aimed shot | `RadioIdentityDoesNotAuthorizeAShotBeforeVisualRecognition` |
| Walls prevent inspection; old evidence expires; fresh evidence permits another search | `SearchDoesNotInspectThroughWallAndExpiresWithoutNewEvidence` |
| Sound approaches are independent of old melee standoff | `SoundSearchApproachesWithoutInheritingAnOldMeleeStandoff` |
| A completed search resumes movement toward the standing order | `CheckedContactReturnsToStandingOrderThroughController` |

The broader focused suite includes scheduling, deleted contacts, combat continuity, assault readiness, and incoming-fire movement:

```powershell
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --configuration Debug --filter 'FullyQualifiedName~CMUContactPerceptionTest|FullyQualifiedName~CMUAgentPerformanceTest|FullyQualifiedName~CMUDeletedContactTest|FullyQualifiedName~CMUCombatContinuityTest|FullyQualifiedName~CMUAssaultReadinessTest|FullyQualifiedName~CMUIncomingFireMovementTest'
```

Final PR verification on 2026-10-24: **29 passed, zero failed, zero skipped**. The run built Shared, Server, Client, and the test projects, with existing unrelated compiler/analyzer warnings. It tested the expedition branch independently of the swarm changes.

Local verification runs the same filter through the workspace's isolation wrapper to serialize builds and keep caches and test results separate. Results are retained locally in the ignored `test-20261024-184754-724.log` and its matching TRX report.

For a manual check, use an illuminated expedition scene with a guard, a distant hostile, and intervening cover. Compare first recognition of the distant hostile with a nearby threat; move the hostile out of sight and inspect the guard's recorded search and eventual return to its order. Repeat with a sound behind a wall and a radio-only contact. Observe the acquisition counters through `cmu-expedition-ai-status` while adding more hostiles.

## Isolated observation measurement

Measured on 2026-10-24 using a temporary integration fixture: 12 ordinary expedition guards, 24 hostile humans, an illuminated scene, 20 warmup sweeps, then 100 measured sweeps of the actual `Observe` method. Each sweep services all 12 guards with 150 ms simulated decision increments. Allocation is measured on the executing thread. Baseline and changed versions use the same fixture and Debug configuration; baseline source is `808c518e554dd620a8b4f8c7dfc9c63c17f8f1cd`.

| Observation sweep | Baseline | Changed |
| --- | ---: | ---: |
| Median time | 19.819 ms | 20.056 ms |
| p95 time | 31.994 ms | 27.117 ms |
| Mean time | 21.405 ms | 20.436 ms |
| Allocated bytes | 1,150,968 | 881,023 |

Allocations fell **23.45%** in this isolated comparison. The median was **1.2% higher**; mean and p95 were lower. A single comparison does not establish a general CPU speedup. These measurements bypass the scheduler to isolate observation and are not whole-server tick times.

The temporary benchmark is excluded from the submitted tests, and no timing assertion is committed. Raw local results are retained in the ignored test logs and TRX reports for the 16:31 baseline and 16:47 changed runs on 2026-10-24. No Release population benchmark or live multiplayer load test has been performed for this expedition change; recognition latency, dangerous terrain, sound-heavy scenes, and sustained firefights still need that validation.
