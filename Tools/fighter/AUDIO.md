# Fighter and MANPAD audio

The sound pass uses the selected F-18 low pass, F-16 cruise/flyby and F-16 startup
recordings, plus turbine, missile and recorded EOD blast material.
Public Freesound HQ previews are the source quality;
these are not the lossless original downloads. The approved MANPAD acquisition
mix stays without its opening clinks and has no electronic lock tones.

Sonniss GameAudioGDC was reviewed on 2026-09-24. Its current license allows game
use but restricts publishing the source/modified sounds as assets. The repository
therefore uses CC0 sources and its existing CC-BY-SA-3.0 CM13 recordings instead.
No Sonniss sounds are included or re-licensed. See https://sonniss.com/gdc-bundle-license/.

| Event | Playback and source |
| --- | --- |
| Occupied ground hull / taxi | Quiet recorded turbine idle; stops when vacated or taking off |
| Takeoff | Selected F-16 startup, first eight seconds, faded to match vertical launch; world sound at the pad |
| Descent / touchdown | Turbine envelope, exhaust swell and low strut compression; world sound at the pad |
| Airborne cockpit, including hold | Filtered loop from the selected F-16 recording |
| Aircraft over battlefield | Moving exterior F-16 loop and ground shadow; higher altitude is quieter with a softer shadow |
| Start of low pass | Selected F-18 flyover, eight-second excerpt; no cockpit radar beep |
| Start of medium/high pass | Selected F-16 flyby, eight-second excerpt with quieter playback and softer treble |
| Lobby flyby | F-18 for the two gun runs, F-16 for the higher missile pass |
| GAU | Rebalanced CM13 recordings; ground report starts at release, impact reports at actual dispersed hits |
| Rockets / missiles | Separate release lengths, launch at designation, spatial blast at real impacts |
| MANPAD acquisition | Existing approved mechanical pressure buildup, no initial dinks |
| MANPAD launch | Selected Missile firing fl by NHMWretched, natural pitch and full tail; audible beyond the sprite PVS |
| Incoming missile / laser warning | Five-second turbine approach; existing visual warning and flare timing remain |
| Defensive flares | Three ignition puffs; visible ground smoke/fan burst |
| Missile hit / evasion | Cabin airframe impact or distant airburst; ground flash/smoke at encounter |
| Laser select/lock / repaired | Quiet physical servo movement |
| Thermal overheat | Pressure vent; UI still indicates cooldown |
| Ejection | Short launch pressure cue; existing parachute sequence |
| Cockpit buttons | Existing Blackfoot button recording retained |
| Route changes / holding / return | Engine and UI provide feedback; generic beeps/chimes removed |

Rebuild with Python, NumPy and soundfile:

```powershell
python Tools/fighter/fetch-audio-sources.py
python Tools/fighter/generate-fighter-audio.py
```

Use `--manpad-only` to rebuild the selected [Missile firing fl](https://freesound.org/people/NHMWretched/sounds/151858/)
launch cue without changing the approved mechanical buildup. The source is CC0;
the edit only converts to mono, filters, balances the level and fades the edges.

Use `python Tools/fighter/generate-fighter-audio.py --flight-only` to rebuild
only the five cues from the three selected recordings. This preserves the
approved MANPAD buildup and all other effects. Low passes select the F-18 below
the cloud base (600 m); medium/high passes select the F-16 above it. Existing
flight timing and weapon behavior are unchanged.

Selected source mapping, with original CC0 uploads verified:

| Use | Selected recording | Original upload | Edit |
| --- | --- | --- | --- |
| Low pass | [F-18 fly-over-and-circle above](https://pixabay.com/sound-effects/city-f-18-fly-over-and-circle-above-dr070cut-76968/) | [Ears68](https://freesound.org/people/Ears68/sounds/150328/) | 2.5-10.5 seconds, mono, natural pitch and edge fades |
| Cruise and high pass | [Two Royal Dutch Airforce F-16s](https://pixabay.com/sound-effects/city-2-royal-dutch-airforce-f-16-fighting-falcons-flyby-16990/) | [Rudmer_Rotteveel](https://freesound.org/people/Rudmer_Rotteveel/sounds/343746/) | 37-45.18 seconds crossfaded into cruise loops; 14-22 seconds for high passes |
| Takeoff | [F16 fighter jet start up](https://pixabay.com/sound-effects/city-f16-fighter-jet-start-upaif-14690/) | [ikbenraar](https://freesound.org/people/ikbenraar/sounds/322178/) | First eight seconds, mono, launch envelope, natural pitch |

Downloads stay in `bin/FighterAudioSources`, together with verified source URLs,
license identifiers, SHA256 hashes, and mix duration/peak/RMS checks. The shipped
mono WAV/Vorbis cues and individual credits live in
`Content.CMU/Resources/Audio/CMU14/Fighter/attributions.yml`.
The review reel is `bin/FighterAudioSources/fighter-audio-preview.wav`.
The flight-only reel is `bin/FighterAudioSources/fighter-flight-audio-preview.wav`,
in this order: low pass, high pass, cruise, takeoff. These edits have been checked
for format, length and level; they still need listening in the game mix.
Further candidates are listed in [AUDIO-CANDIDATES.md](AUDIO-CANDIDATES.md).

Ground audio uses a spatial range filter rather than relying on a visible sprite.
The flyby has one small replicated presentation entity and one looping sound per
aircraft over the battlefield, cleaned up on departure/landing/deletion. It adds
no collision body or free flight. Airbursts expire after three seconds. Existing
VTOL downwash and actual-impact dust/sparks/spray remain in the world overlay.

The ground engine is attached spatially to the occupied hull and bypasses
self-occlusion. The ordinary ambient loop returned NaN occlusion while seated
inside the airframe in the native preview, causing repeated OpenAL InvalidValue
errors. Idle stops for takeoff or empty seats and restarts after touchdown.
