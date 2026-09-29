# Mohawk flight audio

Takeoff uses xilith.com's **Alien ship takeoff**, with mono conversion and short
edge fades. In flight, **Plasma Engine FX** is layered over **Rustbound Reactor 02**
at 12 dB below the plasma layer. The sustained plasma section is crossfaded into
a thirty-second loop. Flight ambience starts on departure and stops at touchdown.
All eight Mohawk cabin maps configure these sounds; other dropships retain their
existing startup and flight audio.

The source credits, licenses, hashes and mix settings are in `flight_audio.json`.
Rebuild with the dependencies listed below:

```sh
python Tools/mohawk/build_flight_audio.py path/to/takeoff.mp3 path/to/plasma.mp3 path/to/reactor.mp3
```

The ten-second arrival cue uses **Extended spaceship lift-off** by pointparkcinema
from 0:15 to 0:25, with short fades. It starts with the landing approach and ends
at touchdown, playing aboard the dropship and at the landing zone. Both new cues
are CC0; the output is mono Ogg Vorbis at 48 kHz.

Source links, licenses, exact cuts and hashes are recorded in `landing_audio.json`.
With numpy, soundfile and ffmpeg (or imageio-ffmpeg) installed, rebuild using:

```sh
python Tools/mohawk/build_landing_audio.py path/to/extended-spaceship-lift-off.mp3
```

Keep the original downloads outside the repository. Credits are also included
in the audio directory's `attributions.yml`.

`MohawkLandingAudioTest` checks takeoff, the departure sound tail, looping flight
ambience, arrival timing and playback at both locations, cleanup at touchdown,
and unchanged stock dropship playback.
