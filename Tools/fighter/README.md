# Two-seat fighter and MANPAD

The fighter provides ground attack and reconnaissance through editable entry/exit
passes and holding points. It does not use unrestricted flight across the map.
The pilot and weapons officer occupy actual tandem seats inside the aircraft.

## Ground operation

Both GOVFOR and OPFOR platoon supply catalogs offer the fighter with a folded VTOL
pad. The delivered aircraft and pad retain their faction ownership; hostile crews
cannot board the fighter or use its landing pad. Unfold the pad with a wrench.

While seated on the ground, the pilot uses W/S to taxi and A/D to steer. Releasing
the controls brakes. The compact upper-right console stays inside the game viewport.
Its Take off / Unbuckle buttons become Land / Eject in flight. No taxi buttons or
Form up on pad button are shown. The aircraft returns to its takeoff position.

Takeoff lasts eight seconds and descent six seconds, with dust, downwash, lights,
heat shimmer and recorded engine sounds. Crew remain attached to the airframe
through the transition. An empty grounded fighter can be stored and retrieved on
the vehicle supply lift without losing its equipment or ammunition.
Vehicles unloading from the Mohawk's lowered ramp are placed three tiles farther
out, clear of the ramp center.
The airborne cockpit and Mohawk cabin each contain an `AU14VehicleRadioSet`.
FTL backgrounds share a steady scrolling phase across decks; their motion no longer
depends on replicated grid movement or which lower deck is visible.

## Flight and weapons

Use Plan route to set an entry and exit, then start the approach. Low / Medium /
High altitude and Near stall / Cruise / Afterburner are segmented controls.
The independent targeting camera can move, follow a friendly flare, or hold a
position within sensor range. Greater altitude increases range. Clouds obstruct
normal optics; thermals penetrate them for up to 120 seconds before a 360-second
cooldown. Night vision has no heat penalty. The exterior follows the aircraft
and its heading, independently of the north-up targeting lock; a cloud bank reveals and covers
the AO continuously during approach and departure.

Six external pylons accept missiles directly. Rockets require a rocket pod, and
the internal GAU is fixed. Both crew can fire missiles, but all weapon release
requires an approach/pass inside the AO. Holding, outside-AO approaches and returns
keep weapons safe, including sector interceptors. The pilot fires GAU and rockets
during an aligned run. Selecting a flare offers an
editable approach assist and a queue for one shot or burst when lined up.

Service the grounded fighter with a power loader: click a mount with ammunition
or a rocket pod in the claw. Activate a mount with an empty claw to remove its
ammunition, then its removable pod. The internal cannon stays installed.

Spawn `CMUFighterGroundLoaded` for the **Fully equipped, mixed missiles** variant.
Its six pylons carry one Widowmaker, Keeper II, Harpoon II, Napalm, Banshee and
Dragon's Breath, with 400 PGU-100 rounds in the internal GAU. These are ordinary,
finite ammunition items that can be unloaded and replaced with a power loader.
The loadout is applied only when the aircraft is first created; landing or storing
it does not replenish ammunition. The standard supply-lift fighter starts empty.

Friendly signal flares appear in the target list; green marks are strikeable.
The camera can also create a visible red ground laser for ten seconds. Missile
release requires a two-second designation lock and warns the impact site.

Air coverage is sector based. A hostile fighter entering a covered sector can
trigger an interceptor. The pilot gets a central flare response prompt; one hit
forces a retreat. Ejection requires confirmation. Pilot ejection launches both
crew seats, while WSO ejection launches only that seat. The existing parachute
system handles descent to the ground.
Ejecting at a holding point chooses a random valid landing area inside that AO.
Both crew share the selected area with normal parachute scatter; drops never
scatter onto a different planet map.

Observers following the pilot, weapons officer or aircraft see the exterior,
cockpit effects and that seat's targeting camera. The presentation is read-only;
switching Plan route / Targeting changes only the observer's display.

## MANPAD requisitions

Every GOVFOR/OPFOR platoon requisitions catalog includes an Air Defense kit:

- One unloaded EMBLR MANPAD.
- A universal rocket backpack containing three existing EMBLR 70mm HE rounds.
- Cost: 3,000 supply points. Starting/max stock: one kit. Restock: one per 900 seconds.

Load an EMBLR 70mm round using the normal six-second ammunition interaction.
Wield the launcher, then toggle Aim at airspace. It automatically acquires hostile
aircraft crossing the operator's current sector. The operator's authenticated
faction supplies its IFF. Unwielding, dropping, changing hands or becoming unable
to act cancels acquisition. Each committed launch consumes one round, lowers the
empty launcher, and requires another physical reload; the existing 15-second
launch interval also remains. There is no manual fire control.

Wielding a MANPAD also shows a compact map of the AO, the operator's sector and
airborne contacts with headings and altitudes. A ring identifies the current
acquisition. The terrain chart is transmitted once when the display is acquired;
contact updates go only to the wielding operator.

The separate spawn prototype `CMUFighterManpadNoIFF` bypasses IFF and engages any
crewed aircraft, including friendlies. It retains the same aiming and reloading
requirements. Requisition kits continue to supply the normal IFF-protected launcher.

## Boiler air defense

Boilers have a **Look at airspace** toggle. While active, the airspace map shows
aircraft and the boiler automatically covers its current sector. A passing jet
requires two seconds of stationary acquisition under open sky, then receives an
ascending plasma shot. Moving, losing the target, becoming unable to act or
turning the ability off cancels acquisition. Each launch spends 200 plasma and
starts a fifteen-second cooldown.

Charging and launching have green ground effects and organic sounds; the pilot
sees an incoming-plasma warning and bolt. The existing countermeasure window and
one-hit forced retreat also apply to plasma.

See [AUDIO.md](AUDIO.md) for selected recordings and reproducible sound edits.
Original fighter and pilot-seat artwork: **nzzy on Discord**, credited by the
contributor. The supplied RSI license fields were unspecified and remain unchanged.
The folded-wing adaptation is documented in [folded-wings.md](folded-wings.md).

## Lobby shows

Each server CVar independently enables its Party Time variant:

```text
cvar cmu.lobby_party_time true
cvar cmu.lobby_party_time_flyby true
cvar cmu.lobby_party_time_parade true
```

The flyby uses the readied roster in a cosmetic battlefield with GAU strafes,
missiles, ground combat and counter-battery effects. The parade marches the roster
with vehicles, actual flag sprites, wheels and comic routines. These are cosmetic
lobby presentations. Clients can hide a show or mute its effects.

## Optional local demonstration

`start-fighter.ps1` starts an isolated Trijent development session using separate
build outputs. It leaves other development servers running. `-Clients 1` opens a
pilot only; the default opens both seats. Use `-SkipBuild` with an existing output.

For a recording of actual weapon releases and ground impacts, use:

```powershell
./Tools/fighter/start-fighter.ps1 -WeaponsTrial -EffectsTrial -RecordPreview
```

This opt-in demonstration restores manual control after the scripted pass and
saves screenshots under the clients' user-data Screenshots folder. The recording
CVar `fighter.record_preview` defaults to false. It records up to ten frames per second
around GAU and missile cues for the PR GIF; it is not a benchmark or a unit test.
Other existing opt-in modes cover ground transitions, lasers and air combat.
