"""Rebuild the approved MANPAD acquisition, then the recording-based fighter mix.

Acquisition layers the licensed CM13 mechanism with original pressure synthesis.
Run fetch-audio-sources.py first; NumPy and soundfile are required for the full mix.
"""

from pathlib import Path
import wave

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Content.CMU/Resources/Audio/CMU14/Fighter"
RATE = 44100
RNG = np.random.default_rng(140923)
MECHANICAL_RNG = np.random.default_rng(140924)


def noise(seconds, low, high, rng=RNG):
    count = round(seconds * RATE)
    spectrum = np.fft.rfft(rng.normal(size=count))
    frequencies = np.fft.rfftfreq(count, 1 / RATE)
    weights = (frequencies / max(1, low)) ** 2 / (1 + (frequencies / max(1, low)) ** 2)
    weights /= 1 + (frequencies / high) ** 4
    result = np.fft.irfft(spectrum * weights, count)
    return result / max(.01, np.std(result))


def layer(destination, sound, offset=0, gain=1):
    start = round(offset * RATE)
    count = min(len(sound), len(destination) - start)
    destination[start:start + count] += sound[:count] * gain


def excerpt(samples, rate, start, duration, pitch=1):
    clip = samples[round(start * rate):round((start + duration) * rate)]
    result = np.interp(np.arange(round(len(clip) / rate / pitch * RATE)) / RATE * pitch,
                       np.arange(len(clip)) / rate, clip)
    edge = min(441, len(result) // 4)
    result[:edge] *= np.linspace(0, 1, edge)
    result[-edge:] *= np.linspace(1, 0, edge)
    return result


def save(name, samples, gain=None):
    samples = np.tanh(samples * 1.15)
    samples *= gain if gain is not None else .89 / max(.001, np.max(np.abs(samples)))
    edge = min(441, len(samples) // 4)
    samples[:edge] *= np.linspace(0, 1, edge)
    samples[-edge:] *= np.linspace(1, 0, edge)
    assert np.all(np.isfinite(samples)) and np.max(np.abs(samples)) <= .9
    with wave.open(str(OUTPUT / name), "wb") as wav:
        wav.setparams((1, 2, RATE, len(samples), "NONE", "not compressed"))
        wav.writeframes(np.round(samples * 32767).astype("<i2").tobytes())
    print(f"{name}: {len(samples) / RATE:.2f}s, mono PCM16, peak {np.max(np.abs(samples)):.3f}")


OUTPUT.mkdir(parents=True, exist_ok=True)

# A recorded mechanism winding up and pressure venting before ignition.
t = np.arange(round(1.2 * RATE)) / RATE
lock = np.zeros_like(t)
with wave.open(str(ROOT / "Content.CMU/Resources/Audio/CMU14/Blackfoot/mechanical.wav"), "rb") as wav:
    assert wav.getsampwidth() == 2
    mechanism = np.frombuffer(wav.readframes(wav.getnframes()), dtype="<i2").astype(float) / 32768
    mechanism = mechanism.reshape(-1, wav.getnchannels()).mean(axis=1)
    layer(lock, excerpt(mechanism, wav.getframerate(), .10, .72, .86), .08, .75)
    layer(lock, excerpt(mechanism, wav.getframerate(), 1.87, .16, .88), .97, .55)
pressure = np.clip((t - .12) / .9, 0, 1) ** 1.4 * np.clip((1.18 - t) / .14, 0, 1)
lock += noise(1.2, 500, 4600, MECHANICAL_RNG) * pressure * .065
lock += noise(1.2, 45, 430, MECHANICAL_RNG) * pressure * .045
# Preserve the remaining mix's volume after removing the two opening clinks.
save("manpad-lock.wav", lock, gain=1.5388853410297243)


# Rebuild the recording-based set without restoring the obsolete synthetic mix.
import runpy
runpy.run_path(str(ROOT / "Tools/fighter/generate-fighter-audio.py"), run_name="__main__")
