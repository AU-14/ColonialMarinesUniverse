# Force on Force

## Role roll

The character editor has a Force on Force tab with separate GOVFOR and OPFOR role lists. Side and job are assigned in one roll, with team sizes differing by at most one. Role capacity, bans, playtime, whitelists, synthetic eligibility and character allegiance still apply. Late joins cannot join the larger side.

Players select a preferred side (or either side) and one fallback policy:

| Policy | Allowed assignment |
| --- | --- |
| Return to lobby | Selected roles on the selected side only |
| Allow other side, keep role | Also allow the equivalent role on the opposite side |
| Allow rifleman fallback, keep side | Also allow rifleman on the selected side |
| Allow both | Also allow either side and rifleman |

The roll maximizes balanced assignments, then prefers higher role priorities and the preferred side. A later overflow pass cannot change these limits. Direct late joins keep the selected job and return to the lobby if that side is full, rather than substituting a different role. Preferences are saved per character, with SQLite and PostgreSQL migrations.

## Dropships

In FoF, squad leaders, acting squad leaders, officers and command personnel can hack an opposing dropship's navigation console. The hack takes 60 seconds and breaks on movement, damage or resting. Existing launch and hijack timing limits apply. Authorization is tied to the user and console and expires after one minute.

Humans and xenos have a single **Initiate hijack** action. The server chooses a valid destination at random when pressed. FoF humans only target the opposing faction's carrier: GOVFOR attacks OPFOR and OPFOR attacks GOVFOR. The human FoF route uses the queen-style crash sequence. Xenos retain the existing valid carrier set and queen decline option. Legacy human intel hijacks outside FoF retain their landing-zone behavior with random selection.

Ordinary navigation respects both carrier ownership (including connected decks) and landing-zone faction whitelists. A hostile passenger on a launched dropship triggers the owning faction's ship announcement, including passengers on other dropship decks.

Admin commands support faction recipients:

```text
aresannounce govfor "Hostile personnel detected aboard the dropship."
aresannounce opfor "Security teams report to the hangar."
announcepreset MarineCommand "Incoming hostiles." --target=Govfor
announcepreset MarineCommand "Incoming hostiles." --target=Opfor
```

## Orbital bombardment

Officers and command personnel use their faction's command console, select one of four presentations and confirm the request. Each faction has a five-minute cooldown. A global siren and announcement precede the effects by eight seconds.

Four passes cover opposing ground units. Each effect is placed 6–14 tiles from a target and at least six tiles from every unit, including friendlies, neutrals, unconscious people and corpses. Positions are checked again during execution. Crowded areas with no safe site are skipped. The effects contain no damage, explosions, projectiles, fires or terrain destruction.

Presentation combines fighter shadows, colored laser passes, airbursts, descending strike visuals, ground shockwaves, local camera shake and spatial audio. Work is bounded to eight targets per 0.75-second interval. Settings live in `Resources/Prototypes/CMU14/ForceOnForce/bombardment.yml` under the CMU resource tree.

There are four audio variants each for sirens, flybys, lasers and impacts. The four preview reels are generated in `bin/ForceOnForceAudioSources/preview-{siren,flyby,laser,impact}.wav`; each reel plays variants 1–4. Rebuild them with `Tools/force-on-force/generate-bombardment-audio.py` (Python, NumPy and SoundFile).

The selected alarms are audition S2/S3 (Alarm Loop 00/01), repeated for the eight-second warning. The selected lasers are L1/L2 (Space Laser/Laser 05). Slots 1/2 preserve the selected sounds' pitch; slots 3/4 use lower-pitched versions of the same recordings, with subtle echoes on the lasers.

Sources and licenses:

- [Little Robot Sound Factory Sci-Fi Sound Effects Library](https://opengameart.org/content/sci-fi-sound-effects-library): CC BY 3.0; Alarm Loop 00/01 supply all four sirens and Laser 05 supplies laser variants 2/4. Credit Little Robot Sound Factory and https://www.littlerobotsoundfactory.com.
- [bart's Space Laser](https://opengameart.org/content/space-laser): CC0; supplies laser variants 1/3.
- [Ears68 F-18](https://freesound.org/people/Ears68/sounds/150328/) and [Rudmer Rotteveel F-16](https://freesound.org/people/Rudmer_Rotteveel/sounds/343746/): existing CC0 fighter recordings, edited into four flybys.
- [qubodup Fire Explosion](https://freesound.org/people/qubodup/sounds/855898/): existing CC0 fighter impact recording, edited into four impacts.

Asset-level attribution is in `Resources/Audio/CMU14/ForceOnForce/attributions.yml` under the CMU resource tree.

## Identification and respawn

The marine HUD replaces faction, role, squad and fireteam markers with a large, thick red X with a black outline when someone is missing a recognized uniform. The whitelist follows their issued platoon, plus their issued specialist uniform. Wearing an opposing uniform does not impersonate that faction. Recognition updates when equipment changes.

FoF respawning waits five minutes from death, tracked by account through ghosting and reconnecting. Revival cancels that death's eligibility; a subsequent death starts a new five-minute wait. The round reset clears the history.

## Fighter crashes

The lifetime limit of 2–4 hits, or a forced return to an obstructed recovery site, starts an eight-second crash and ejection window. The aircraft approaches nose-first with a burning smoke trail and embers. Impact produces a large fireball, expanding dust ring, glowing fragments and eight seconds of drifting smoke. These particles are cosmetic; the existing crash damage applies to crew who remain aboard.

The hull, cockpit frame, glass, seats and pylons share a moderate scorched tint that preserves the airframe's details. Flickering flames, glowing cores, embers and drifting smoke continue around the engines and fuselage for as long as the wreck remains, including for players arriving after the impact. These effects are cosmetic. Crashed aircraft remain grounded and cannot resume flight.

## Validation

Focused coverage is in `ForceOnForceAssignmentTest`, `ForceOnForceTest`, `ForceOnForceGameplayTest`, `StationJobsMergeRegressionTest`, `ServerDbSqliteTests.ForceOnForce`, `MohawkFlightFeaturesTest` and `FighterCrashPresentationTest`. These cover assignment against exhaustive small cases, all fallback policies, persistence, carrier restrictions and random candidate pools, command authorization, safe bombardment, uniform swaps, the respawn timer, nose orientation and charred cockpit attachments.

Run the unit project with `--filter FullyQualifiedName~ForceOnForce`. For integration coverage, filter on the fixture names above and `TestNoPendingDatabaseChanges`. The PostgreSQL schema check requires a configured test database. The audio generator checks every exported asset for finite samples without clipping; the checked-in assets also retain source and license attribution.

For a multiplayer playtest, check the full human hack/crash sequence in both directions, the xeno button and decline path, arrival warnings heard by the correct faction, HUD appearance from another player's view, and the combined visual/audio intensity with a large ground population. These visual and multiplayer checks are separate from automated regression coverage.

For a local visual demonstration with one connected admin, open the console with `~`, run `scsi`, paste the entire contents of `Tools/force-on-force/show-orbital-bombardment.csx` or `show-fighter-crash.csx`, and press Ctrl+Enter. Close the scripting window and console before the 15-second delay finishes. Run one demonstration at a time; each creates a separate floor map and moves the admin's observer there. Paste the source directly rather than using `#load`. The bombardment demo marks its map as a planetary surface, protects its dummy humans from the test map's vacuum, and resets the faction cooldown for replays.
