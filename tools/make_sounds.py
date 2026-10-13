#!/usr/bin/env python3
"""Synthesises AVA1942's sound effects and tunes into ../src/AVA1942/Sounds as 16-bit mono WAV.

Nothing is sampled from any arcade machine: every effect is built here from square, triangle and
noise voices, and the tunes are new compositions. Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVA1942", "Sounds")
RNG = random.Random(1985)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "pulse":
        return 1.0 if p < 0.25 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    if kind == "sine":
        return math.sin(2 * math.pi * p)
    return RNG.uniform(-1, 1)


def tone(seconds, f0, f1=None, kind="square", attack=0.004, release=1.0, vibrato=0.0, vib_rate=8.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        phase += f / RATE
        if kind == "noise":
            # Noise re-sampled at the voice's rate: a lower 'pitch' sounds rougher.
            if int(phase) != int(phase - f / RATE):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def tune(bpm, lead, bass, kind="pulse"):
    """Two voices: a lead and a bass line, as (note, beats) lists; '-' is a rest."""
    beat = 60.0 / bpm

    def line(notes, k, gain, release):
        out = []
        for note, beats in notes:
            d = beats * beat
            out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=k, release=release, gain=gain))
        return out

    return mix(line(lead, kind, 0.55, 0.6), line(bass, "tri", 0.6, 0.3))


def main():
    os.makedirs(OUT, exist_ok=True)
    write("Shoot.wav", tone(0.05, 1500, 900, "pulse", release=1.4), 0.18)
    write("Hit.wav", tone(0.05, 900, 500, "square", release=1.2), 0.25)
    write("Kill.wav", mix(tone(0.25, 700, 60, "noise", release=0.8), tone(0.18, 260, 70, "square", release=1.0)), 0.4)
    write("Explode.wav", mix(tone(1.2, 350, 30, "noise", release=0.5), tone(1.0, 110, 30, "square", release=0.6, gain=0.6)), 0.6)
    write("Die.wav", cat(mix(tone(0.6, 500, 40, "noise", release=0.5), tone(0.6, 700, 60, "saw", release=0.5)),
                         *[tone(0.15, hz(n), kind="square", release=0.6) for n in ["G4", "E4", "C4"]]), 0.55)
    write("Roll.wav", mix(tone(0.9, 200, 800, "saw", release=0.2, vibrato=0.1, vib_rate=6), tone(0.9, 900, 200, "tri", release=0.4, gain=0.5)), 0.38)
    write("PowAppear.wav", cat(*[tone(0.05, hz(n), kind="pulse", release=0.4) for n in ["E6", "C6", "E6", "C6"]]), 0.35)
    write("PowUp.wav", cat(*[tone(0.07, hz(n), kind="tri", release=0.4) for n in ["C5", "E5", "G5", "C6", "E6", "G6"]]), 0.45)
    write("BossAlarm.wav", cat(*[tone(0.25, f0, f1, "square", release=0.2) for f0, f1 in [(500, 800), (500, 800), (500, 800)]]), 0.36)
    write("BossHit.wav", mix(tone(0.07, 180, 120, "square", release=1.0), tone(0.05, 1000, 300, "noise", release=1.0)), 0.3)
    write("ExtraLife.wav", cat(*[tone(0.08, hz(n), kind="pulse", release=0.3) for n in ["C6", "G5", "C6", "G5", "C6", "E6", "G6"]]), 0.4)
    write("Tally.wav", cat(*[tone(0.04, 1800 + 120 * (i % 5), kind="square", release=0.5) for i in range(18)]), 0.28)

    # Flying: a brisk march in F over a bass that steps along, with an engine drone beneath (new). Loops.
    lead = [("F5", 0.5), ("F5", 0.25), ("G5", 0.25), ("A5", 0.5), ("F5", 0.5), ("C6", 1), ("A5", 1),
            ("Bb5", 0.5), ("A5", 0.5), ("G5", 0.5), ("F5", 0.5), ("G5", 1), ("C5", 1),
            ("F5", 0.5), ("F5", 0.25), ("G5", 0.25), ("A5", 0.5), ("Bb5", 0.5), ("C6", 1), ("D6", 1),
            ("C6", 0.5), ("Bb5", 0.5), ("A5", 0.5), ("G5", 0.5), ("F5", 2)]
    bass = [(n, 1) for n in ["F2", "C3", "F2", "C3", "Bb2", "C3", "C2", "C3", "F2", "C3", "Bb2", "D3", "C3", "C2", "F2", "F2"]]
    song = tune(132, lead, bass)
    engine = tone(len(song) / RATE, 62, kind="saw", attack=0.001, release=0.0, vibrato=0.015, vib_rate=9, gain=0.16)
    write("Music.wav", mix(song, engine), 0.42)

    write("TitleTune.wav", tune(112,
        [("C5", 0.5), ("F5", 0.5), ("A5", 0.5), ("C6", 1.5), ("A5", 0.5), ("F5", 0.5), ("G5", 2),
         ("A5", 0.5), ("Bb5", 0.5), ("A5", 0.5), ("G5", 0.5), ("F5", 0.5), ("E5", 0.5), ("F5", 2),
         ("D5", 0.5), ("F5", 0.5), ("Bb5", 0.5), ("D6", 1.5), ("C6", 0.5), ("Bb5", 0.5), ("A5", 2),
         ("G5", 0.5), ("A5", 0.5), ("Bb5", 0.5), ("G5", 0.5), ("C6", 1), ("E5", 1), ("F5", 2)],
        [(n, 2) for n in ["F2", "F2", "C3", "F2", "Bb2", "Bb2", "C3", "F2"]]), 0.55)
    write("StartTune.wav", tune(150,
        [("C5", 0.5), ("F5", 0.5), ("A5", 0.5), ("C6", 0.5), ("A5", 0.5), ("C6", 1.5)],
        [("F2", 1), ("C3", 1), ("F2", 2)]), 0.55)
    write("StageClear.wav", tune(140,
        [("F5", 0.5), ("A5", 0.5), ("C6", 0.5), ("F6", 1), ("E6", 0.5), ("D6", 0.5), ("C6", 0.5), ("Bb5", 0.5), ("A5", 0.5), ("G5", 0.5), ("F5", 2)],
        [("F2", 2), ("C3", 2), ("Bb2", 1), ("C3", 1), ("F2", 2)]), 0.55)
    write("GameOver.wav", tune(80,
        [("C5", 1), ("A4", 1), ("F4", 1), ("G4", 0.5), ("E4", 0.5), ("F4", 3)],
        [("F2", 2), ("C2", 2), ("F2", 3)], kind="tri"), 0.55)


if __name__ == "__main__":
    main()
