# Mohawk flight audio

Takeoff uses steshystesh's **Spaceship Whoosh 2**, with mono conversion and short
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

The ten-second arrival cue starts at 0:06 of Vospi's **super specific landing**,
then crossfades for 250 ms into **Creaky Thunderous SciFi SpaceShip Landing** by
Kalpesh Ajugia (kalsstockmedia) at approximately 1.9 seconds. It plays both aboard
the dropship and at the landing zone. The output is mono Ogg Vorbis at 48 kHz.

Source links, licenses, exact cuts and hashes are recorded in `landing_audio.json`.
With numpy, soundfile and ffmpeg (or imageio-ffmpeg) installed, rebuild using:

```sh
python Tools/mohawk/build_landing_audio.py path/to/super-specific-landing.mp3 path/to/creaky-thunderous.mp3
```

Keep the original downloads outside the repository. The edited cue's combined
license and author credits are in the audio directory's `attributions.yml`.

`MohawkLandingAudioTest` checks takeoff, the departure sound tail, looping flight
ambience, arrival timing and playback at both locations, cleanup at touchdown,
and unchanged stock dropship playback.
