# Force on Force

## Role roll

The character editor has a Force on Force tab with one Roles list. Role priorities apply to equivalent GOVFOR and OPFOR roles, while the preferred side and fallback policy determine allowed sides. Existing side-specific priorities appear in the shared list; editing a role replaces its old preferences on both sides. Side and job are assigned in one roll, with team sizes differing by at most one. Role capacity, bans, playtime, whitelists, synthetic eligibility and character allegiance still apply. Late joins cannot join the larger side.

Players select a preferred side (or either side) and one fallback policy:

| Policy | Allowed assignment |
| --- | --- |
| Return to lobby | Selected roles on the selected side only |
| Allow other side, keep role | Also allow the equivalent role on the opposite side |
| Allow rifleman fallback, keep side | Also allow rifleman on the selected side |
| Allow both | Also allow either side and rifleman |

The roll maximizes balanced assignments, then prefers higher role priorities and the preferred side. A later overflow pass cannot change these limits. Direct late joins keep the selected job and return to the lobby if that side is full, rather than substituting a different role. Preferences are saved per character, with SQLite and PostgreSQL migrations.

## Dropships

Dropship part fabricators share a budget within their carrier's faction, including linked decks. GOVFOR and OPFOR have separate accounts for purchases, refunds, passive income and faction tech rewards.

In FoF, squad leaders, acting squad leaders, officers and command personnel can hack an opposing dropship's navigation console. The hack takes 60 seconds and breaks on movement, damage or resting. Existing launch and hijack timing limits apply. Authorization is tied to the user and console and expires after one minute.

Humans and xenos have a single **Initiate hijack** action. The server chooses a valid destination at random when pressed. FoF humans only target the opposing faction's carrier: GOVFOR attacks OPFOR and OPFOR attacks GOVFOR. The human FoF route uses the queen-style crash sequence. Xenos retain the existing valid carrier set and queen decline option. Legacy human intel hijacks outside FoF retain their landing-zone behavior with random selection.

Ordinary navigation respects both carrier ownership (including connected decks) and landing-zone faction whitelists. A hostile passenger on a launched dropship triggers the owning faction's ship announcement, including passengers on other dropship decks.

When a FoF hijack flight starts, living attackers on the ground or their own carrier receive a **Join / Stay** popup offering transport aboard the hijacked dropship. Living defenders on the ground or the attackers' carrier receive the same choice to return to their own carrier's spawn area. Players already aboard the hijacking dropship or defending carrier are not prompted. Closing the popup counts as staying. The offer expires at landing, on death, or when the player changes body, faction or eligible location; acceptance is validated on the server and requires a clear floor tile.

Admin commands support faction recipients:

```text
aresannounce govfor "Hostile personnel detected aboard the dropship."
aresannounce opfor "Security teams report to the hangar."
announcepreset MarineCommand "Incoming hostiles." --target=Govfor
announcepreset MarineCommand "Incoming hostiles." --target=Opfor
```

## Orbital bombardment

Officers and command personnel use their faction's command console, select one of four presentations and confirm the request. Each faction has a five-minute cooldown. A global siren and announcement precede the effects by eight seconds. Living ground players on every faction also receive a large red warning over their character.

Four passes cover opposing ground units. Each effect is placed 6–14 tiles from a target and at least six tiles from every unit, including friendlies, neutrals, unconscious people and corpses. Positions are checked at launch and again before impact. If someone steps into a descending effect's landing site, that impact is cancelled. Crowded areas with no safe site are skipped. The effects contain no damage, explosions, projectiles, fires or terrain destruction.

Each type has four internal arrangements, drawn in shuffled order, with irregular burst gaps, approach angles and pauses:

- **Rolling thunder:** raking shell salvos, staggered doubles, scattered shell pops and dense rolling barrages.
- **Laser curtain:** sustained lances, stuttering pulses, sweeping fans and crossing lances. Only this type creates laser beams or laser sounds.
- **Meteor strike:** heavy descending fireballs, paired fireballs, fragment showers and sporadic meteor rain, with incandescent trails and debris.
- **Shock and awe:** heavy concussion, paired pressure waves, rippling blasts and rolling detonations with large expanding pressure fronts.

Bombardment never spawns jets or plays jet flyby audio. The selected main type remains constant across all four passes. Each target batch and each impact-completion step is capped at eight; each target arrangement schedules at most four pulses. Settings live in `Resources/Prototypes/CMU14/ForceOnForce/bombardment.yml` under the CMU resource tree.

There are four audio variants each for sirens, lasers and impacts. Previously prepared flyby assets remain available but are not used by bombardment. Preview reels are generated in `bin/ForceOnForceAudioSources/preview-{siren,flyby,laser,impact}.wav`; each reel plays variants 1–4. Rebuild them with `Tools/force-on-force/generate-bombardment-audio.py` (Python, NumPy and SoundFile).

The selected alarms are audition S2/S3 (Alarm Loop 00/01), repeated for the eight-second warning. The selected lasers are L1/L2 (Space Laser/Laser 05). Slots 1/2 preserve the selected sounds' pitch; slots 3/4 use lower-pitched versions of the same recordings, with subtle echoes on the lasers.

Sources and licenses:

- [Little Robot Sound Factory Sci-Fi Sound Effects Library](https://opengameart.org/content/sci-fi-sound-effects-library): CC BY 3.0; Alarm Loop 00/01 supply all four sirens and Laser 05 supplies laser variants 2/4. Credit Little Robot Sound Factory and https://www.littlerobotsoundfactory.com.
- [bart's Space Laser](https://opengameart.org/content/space-laser): CC0; supplies laser variants 1/3.
- [Ears68 F-18](https://freesound.org/people/Ears68/sounds/150328/) and [Rudmer Rotteveel F-16](https://freesound.org/people/Rudmer_Rotteveel/sounds/343746/): existing CC0 fighter recordings, edited into four flybys.
- [qubodup Fire Explosion](https://freesound.org/people/qubodup/sounds/855898/): existing CC0 fighter impact recording, edited into four impacts.

Asset-level attribution is in `Resources/Audio/CMU14/ForceOnForce/attributions.yml` under the CMU resource tree.

## Identification and respawn

Options → CMU → Identification has a personal "Show question marks for unfamiliar enemy uniforms in FoF" toggle. Its saved client CVar, `cmu.fof.unidentified_marker_enabled`, defaults to `false`. When enabled, the HUD replaces faction, role, squad and fireteam markers with a red question mark with a black outline for other factions wearing uniforms the viewer does not recognize, including missing uniforms. Teammates never receive the question mark, even with no uniform or a foreign uniform. Recognition includes the selected platoon's resolved vendor stock (including shipside and specialist vendors), its explicit uniform list and the viewer's issued uniform. It matches exact uniform prototypes; inheriting another uniform's components does not establish faction identity. Other factions wearing a recognized uniform do not receive the question mark. Applying the setting and changing equipment update recognition without respawning.

The question mark does not require marine HUD gear. Normal role and squad identifiers retain their existing HUD requirements.

Dead bodies display neither the question mark nor marine identification icons. Revived enemies are evaluated normally again.

FoF respawning waits five minutes from death, tracked by account through ghosting and reconnecting. An account's first GOVFOR or OPFOR spawn locks every subsequent life to that side for the rest of the round, including after changing character. The job list, assignment roll and final spawn check enforce the lock. Revival cancels that death's eligibility; a subsequent death starts a new five-minute wait. The round reset clears the timer and side lock. Existing population balance limits still apply; a returning player waits if their original side is larger.

## Fighter crashes

The lifetime limit of 2–4 hits, or a forced return to an obstructed recovery site, starts an eight-second crash and ejection window. The aircraft approaches nose-first with a burning smoke trail and embers. Impact produces a large fireball, expanding dust ring, glowing fragments and eight seconds of drifting smoke. These particles are cosmetic; the existing crash damage applies to crew who remain aboard.

The hull, cockpit frame, glass, seats and pylons share a moderate scorched tint that preserves the airframe's details. Flickering flames, glowing cores, embers and drifting smoke continue around the engines and fuselage for as long as the wreck remains, including for players arriving after the impact. These effects are cosmetic. Crashed aircraft remain grounded and cannot resume flight.

## Validation

Focused coverage is in `ForceOnForceAssignmentTest`, `ForceOnForceTest`, `ForceOnForceGameplayTest`, `ForceOnForceBombardmentTest`, `StationJobsMergeRegressionTest`, `ServerDbSqliteTests.ForceOnForce`, `MohawkFlightFeaturesTest` and `FighterCrashPresentationTest`. These cover assignment against exhaustive small cases, all fallback policies, persistence, carrier restrictions and random candidate pools, command authorization, safe bombardment, red alarm delivery, four patterns per type, no jets, laser exclusivity, cancellation of newly occupied impact sites, uniform swaps, the respawn timer, nose orientation and charred cockpit attachments.

Run the unit project with `--filter FullyQualifiedName~ForceOnForce`. For integration coverage, filter on the fixture names above and `TestNoPendingDatabaseChanges`. The PostgreSQL schema check requires a configured test database. The audio generator checks every exported asset for finite samples without clipping; the checked-in assets also retain source and license attribution.

For a multiplayer playtest, check the full human hack/crash sequence in both directions, the xeno button and decline path, arrival warnings heard by the correct faction, HUD appearance from another player's view, and the combined visual/audio intensity with a large ground population. These visual and multiplayer checks are separate from automated regression coverage.

For a local visual demonstration with one connected admin, open the console with `~`, run `scsi`, paste the entire contents of `Tools/force-on-force/show-orbital-bombardment.csx` or `show-fighter-crash.csx`, and press Ctrl+Enter. Close the scripting window and console before the 15-second delay finishes. Run one demonstration at a time; each creates a separate floor map and moves the admin's observer there. Paste the source directly rather than using `#load`. The bombardment demo marks its map as a planetary surface, protects its dummy humans from the test map's vacuum, and resets the faction cooldown for replays.

To replay around your current character without faction targets or a cooldown, use `Tools/force-on-force/force-bombardment.csx`. Select 0 for shells, 1 for lasers, 2 for meteors or 3 for shockwaves. This calls the same scheduler and alarm as the console and preserves impact safety. Stand on a floor map with enough room for impacts 6–14 tiles away.
