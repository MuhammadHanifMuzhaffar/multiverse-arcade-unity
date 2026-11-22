"""
Dragon Hunter - generator audio orisinal (SFX + musik) untuk Sprint 2.

Semua suara disintesis dari nol (osilator, noise, filter, envelope); tidak ada sampel pihak ketiga.
Hasil deterministik (seed tetap) sehingga bisa dibangkitkan ulang kapan saja:

    python generate_audio.py

Output: ../Audio/SFX/*.ogg dan ../Audio/Music/*.ogg (Ogg Vorbis, sesuai aturan README).
Folder `Tools~` diabaikan oleh Unity, jadi skrip ini tidak ikut di-import/di-build.
Butuh: numpy, scipy, soundfile.
"""

import math
import os
import sys

import numpy as np
import soundfile as sf
from scipy import signal

SR = 44100
RNG = np.random.default_rng(20261122)
HERE = os.path.dirname(os.path.abspath(__file__))
OUT_SFX = os.path.normpath(os.path.join(HERE, "..", "Audio", "SFX"))
OUT_MUSIC = os.path.normpath(os.path.join(HERE, "..", "Audio", "Music"))


# ----------------------------------------------------------------------------- dasar
def t_axis(seconds):
    return np.arange(int(seconds * SR)) / SR


def noise(seconds):
    return RNG.uniform(-1.0, 1.0, int(seconds * SR))


def brown(seconds):
    x = np.cumsum(noise(seconds))
    x = signal.sosfilt(signal.butter(1, 20, "hp", fs=SR, output="sos"), x)
    return x / (np.max(np.abs(x)) + 1e-9)


def env_exp(seconds, decay, attack=0.002):
    t = t_axis(seconds)
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    return a * np.exp(-t / decay)


def env_adsr(seconds, a, d, s, r):
    n = int(seconds * SR)
    e = np.zeros(n)
    na, nd, nr = int(a * SR), int(d * SR), int(r * SR)
    ns = max(0, n - na - nd - nr)
    seg = [np.linspace(0, 1, na, endpoint=False), np.linspace(1, s, nd, endpoint=False),
           np.full(ns, s), np.linspace(s, 0, nr)]
    e = np.concatenate(seg)[:n]
    return np.pad(e, (0, n - len(e)))


def lp(x, cutoff, order=2):
    return signal.sosfilt(signal.butter(order, min(cutoff, SR * 0.45), "lp", fs=SR, output="sos"), x)


def hp(x, cutoff, order=2):
    return signal.sosfilt(signal.butter(order, cutoff, "hp", fs=SR, output="sos"), x)


def bp(x, lo, hi, order=2):
    return signal.sosfilt(signal.butter(order, [lo, min(hi, SR * 0.45)], "bp", fs=SR, output="sos"), x)


def svf_sweep(x, f_start, f_end, q=2.0, mode="bp"):
    """State-variable filter dengan frekuensi tengah yang bergeser eksponensial (whoosh)."""
    n = len(x)
    freqs = f_start * (f_end / f_start) ** (np.arange(n) / max(n - 1, 1))
    low = band = 0.0
    out = np.empty(n)
    damp = 1.0 / q
    for i in range(n):
        f = 2.0 * math.sin(math.pi * min(freqs[i], SR / 6) / SR)
        high = x[i] - low - damp * band
        band += f * high
        low += f * band
        out[i] = band if mode == "bp" else low
    return out


def osc_saw(freq_curve):
    phase = np.cumsum(freq_curve) / SR
    return 2.0 * (phase - np.floor(phase + 0.5))


def osc_sine(freq_curve):
    return np.sin(2 * np.pi * np.cumsum(freq_curve) / SR)


def glide(seconds, f0, f1, curve="exp"):
    n = int(seconds * SR)
    k = np.arange(n) / max(n - 1, 1)
    return f0 * (f1 / f0) ** k if curve == "exp" else f0 + (f1 - f0) * k


def pluck(freq, seconds, damping=0.996):
    """Karplus-Strong: senar dipetik (tali busur)."""
    n = int(seconds * SR)
    period = int(SR / freq)
    buf = RNG.uniform(-1, 1, period)
    out = np.empty(n)
    for i in range(n):
        out[i] = buf[i % period]
        buf[i % period] = damping * 0.5 * (buf[i % period] + buf[(i + 1) % period])
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[:len(p)] += p
    return out


def place(dst, src, start_sec, gain=1.0):
    i = int(start_sec * SR)
    if i >= len(dst):
        return
    j = min(len(dst), i + len(src))
    dst[i:j] += src[:j - i] * gain


def fade(x, fin=0.003, fout=0.02):
    x = x.copy()
    a, b = int(fin * SR), int(fout * SR)
    if a:
        x[:a] *= np.linspace(0, 1, a)
    if b:
        x[-b:] *= np.linspace(1, 0, b)
    return x


def normalize(x, peak_db=-1.0):
    peak = np.max(np.abs(x)) + 1e-9
    return x * (10 ** (peak_db / 20) / peak)


def match_rms(x, target_db, peak_cap_db=-1.0):
    rms = np.sqrt(np.mean(x ** 2)) + 1e-9
    y = x * (10 ** (target_db / 20) / rms)
    peak = np.max(np.abs(y))
    cap = 10 ** (peak_cap_db / 20)
    return y * (cap / peak) if peak > cap else y


def drive(x, amount):
    return np.tanh(x * amount) / np.tanh(amount)


# ----------------------------------------------------------------------------- SFX
def sfx_sword_swing(up=True):
    d = 0.3
    w = svf_sweep(noise(d), 500 if up else 3200, 3200 if up else 500, q=3.0)
    e = env_adsr(d, 0.09, 0.08, 0.5, 0.13)
    return fade(w * e)


def sfx_sword_hit():
    d = 0.4
    thump = osc_sine(glide(d, 140, 50)) * env_exp(d, 0.07)
    crack = hp(noise(d), 1500) * env_exp(d, 0.012)
    ring = sum(np.sin(2 * np.pi * f * t_axis(d)) * env_exp(d, dec) * g
               for f, dec, g in [(1240, 0.18, 0.5), (1873, 0.12, 0.35), (2650, 0.09, 0.25), (3310, 0.06, 0.2)])
    return fade(mix(thump * 1.0, crack * 0.6, ring * 0.35))


def sfx_heavy_hit():
    d = 0.7
    thump = osc_sine(glide(d, 100, 36)) * env_exp(d, 0.16)
    crunch = lp(noise(d), 2200) * env_exp(d, 0.06)
    ring = np.sin(2 * np.pi * 980 * t_axis(d)) * env_exp(d, 0.25) * 0.25
    return fade(drive(mix(thump * 1.2, crunch * 0.8, ring), 2.0))


def sfx_arrow_shoot():
    d = 0.45
    string = pluck(165, d, 0.993) * env_exp(d, 0.15)
    whoosh = svf_sweep(noise(d), 2500, 900, q=2.5) * env_adsr(d, 0.01, 0.1, 0.2, 0.2)
    return fade(mix(string * 0.9, whoosh * 0.4))


def sfx_arrow_hit():
    d = 0.22
    thunk = osc_sine(glide(d, 260, 90)) * env_exp(d, 0.04)
    click = bp(noise(d), 1500, 5000) * env_exp(d, 0.006)
    return fade(mix(thunk, click * 0.7))


def sfx_dodge():
    d = 0.42
    whoosh = svf_sweep(noise(d), 280, 1100, q=1.5, mode="lp") * env_adsr(d, 0.08, 0.1, 0.6, 0.2)
    thud = np.zeros(int(d * SR))
    place(thud, osc_sine(glide(0.15, 90, 45)) * env_exp(0.15, 0.05), 0.27)
    return fade(mix(whoosh * 0.7, thud * 0.8))


def sfx_hunter_hurt():
    d = 0.45
    thud = osc_sine(glide(d, 120, 45)) * env_exp(d, 0.08)
    grunt = bp(osc_saw(glide(d, 210, 130)), 300, 1400) * env_adsr(d, 0.01, 0.1, 0.4, 0.2)
    grit = lp(noise(d), 1800) * env_exp(d, 0.05)
    return fade(drive(mix(thud, grunt * 0.6, grit * 0.5), 1.8))


def sfx_potion():
    d = 1.0
    out = np.zeros(int(d * SR))
    for i, start in enumerate([0.05, 0.36, 0.66]):
        g = osc_sine(glide(0.16, 230 + i * 25, 420 + i * 30)) * env_adsr(0.16, 0.01, 0.05, 0.6, 0.08)
        place(out, g, start, 0.7)
    for _ in range(14):
        b = osc_sine(glide(0.04, RNG.uniform(900, 1500), RNG.uniform(1600, 2400))) * env_exp(0.04, 0.012)
        place(out, b, RNG.uniform(0.0, 0.9), 0.25)
    return fade(out)


def growl(d, f0, f1, vib_hz=6.0, vib_depth=0.04, breath=0.4, cutoff=900):
    base = glide(d, f0, f1)
    vib = 1.0 + vib_depth * np.sin(2 * np.pi * vib_hz * t_axis(d))
    jitter = 1.0 + 0.02 * lp(noise(d), 30) * 8
    f = base * vib * jitter
    tone = osc_saw(f) + 0.6 * osc_saw(f * 1.007) + 0.4 * osc_saw(f * 0.5)
    formants = bp(tone, 250, 550) * 1.0 + bp(tone, 700, 1200) * 0.6 + lp(tone, cutoff) * 0.5
    air = bp(noise(d), 800, 3000) * breath
    return formants + air


def sfx_dragon_growl():
    d = 0.95
    g = growl(d, 72, 88, vib_hz=5, breath=0.25) * env_adsr(d, 0.25, 0.2, 0.8, 0.3)
    return fade(drive(g, 1.6))


def sfx_dragon_roar():
    d = 2.3
    g = growl(d, 95, 68, vib_hz=7, vib_depth=0.06, breath=0.6, cutoff=1400) * env_adsr(d, 0.35, 0.3, 0.85, 0.9)
    rumble = lp(brown(d), 120) * env_adsr(d, 0.3, 0.5, 0.7, 0.9) * 0.8
    return fade(drive(mix(g, rumble), 2.4), 0.01, 0.2)


def sfx_dragon_hurt():
    d = 0.55
    g = growl(d, 150, 110, vib_hz=11, vib_depth=0.05, breath=0.35) * env_adsr(d, 0.02, 0.12, 0.5, 0.25)
    return fade(drive(g, 2.0))


def sfx_dragon_death():
    d = 2.8
    g = growl(d, 85, 38, vib_hz=4, vib_depth=0.08, breath=0.5) * env_adsr(d, 0.2, 0.6, 0.6, 1.6)
    thud = np.zeros(int(d * SR))
    place(thud, osc_sine(glide(0.6, 70, 30)) * env_exp(0.6, 0.2), 2.0)
    return fade(drive(mix(g, thud * 1.2), 1.8), 0.01, 0.4)


def sfx_bite_snap():
    d = 0.32
    out = np.zeros(int(d * SR))
    for start in (0.0, 0.09):
        place(out, bp(noise(0.05), 1800, 4200) * env_exp(0.05, 0.008), start, 0.9)
    place(out, osc_sine(glide(0.2, 160, 60)) * env_exp(0.2, 0.05), 0.08, 0.9)
    return fade(out)


def sfx_tail_whoosh():
    d = 0.65
    w = svf_sweep(noise(d), 180, 1300, q=1.8) * env_adsr(d, 0.2, 0.15, 0.6, 0.25)
    sub = osc_sine(glide(d, 55, 80)) * env_adsr(d, 0.2, 0.1, 0.5, 0.3) * 0.4
    return fade(mix(w, sub))


def sfx_fire_breath():
    d = 1.7
    body = lp(brown(d), 1600) * 0.9 + bp(noise(d), 300, 2500) * 0.35
    crackle = np.zeros(int(d * SR))
    for _ in range(90):
        place(crackle, hp(noise(0.006), 2500) * env_exp(0.006, 0.0015), RNG.uniform(0, d - 0.01), RNG.uniform(0.3, 0.9))
    roar = lp(osc_saw(glide(d, 62, 58)), 400) * 0.3
    e = env_adsr(d, 0.12, 0.2, 0.85, 0.35)
    return fade(drive(mix(body, crackle * 0.6, roar) * e, 1.5))


def sfx_charge_stomp():
    d = 0.4
    thump = osc_sine(glide(d, 70, 32)) * env_exp(d, 0.09)
    dirt = lp(noise(d), 900) * env_exp(d, 0.05)
    return fade(drive(mix(thump * 1.3, dirt * 0.6), 1.6))


def sfx_nova_blast():
    d = 2.0
    blast = svf_sweep(noise(d), 4500, 160, q=0.9, mode="lp") * env_exp(d, 0.45, attack=0.005)
    boom = osc_sine(glide(d, 60, 28)) * env_exp(d, 0.5)
    sizzle = hp(noise(d), 3000) * env_exp(d, 0.25) * 0.25
    return fade(drive(mix(blast * 1.1, boom * 1.3, sizzle), 2.2), 0.003, 0.3)


def note_hz(midi):
    return 440.0 * 2 ** ((midi - 69) / 12)


def brass(midi, d, gain=1.0):
    f = np.full(int(d * SR), note_hz(midi)) * (1 + 0.004 * np.sin(2 * np.pi * 5.2 * t_axis(d)))
    tone = osc_saw(f) + 0.5 * osc_saw(f * 1.004) + 0.3 * osc_saw(f * 2.0)
    e = env_adsr(d, 0.04, 0.12, 0.75, min(0.3, d * 0.4))
    bright = lp(tone, 900) * (1 - e) + lp(tone, 2600) * e  # makin terang saat keras
    return bright * e * gain


def sfx_victory():
    d = 4.2
    out = np.zeros(int(d * SR))
    seq = [(62, 0.0, 0.35), (66, 0.3, 0.35), (69, 0.6, 0.35), (74, 0.9, 1.2)]
    for m, s, l in seq:
        place(out, brass(m, l), s, 0.6)
    for m in (62, 66, 69, 74, 78):
        place(out, brass(m, 2.4, 0.4), 1.8)
    place(out, osc_sine(glide(1.0, 70, 35)) * env_exp(1.0, 0.3), 1.8, 0.8)
    return fade(out, 0.003, 0.4)


def sfx_defeat():
    d = 3.4
    out = np.zeros(int(d * SR))
    for i, m in enumerate([62, 60, 58, 57]):
        place(out, lp(brass(m, 1.0, 0.7), 1200), i * 0.6)
    place(out, lp(brass(50, 2.0, 0.5), 600), 2.0)
    return fade(out, 0.003, 0.5)


def sfx_ui_click():
    d = 0.08
    return fade(osc_sine(glide(d, 1400, 900)) * env_exp(d, 0.015) + bp(noise(d), 2000, 6000) * env_exp(d, 0.004) * 0.3)


# ----------------------------------------------------------------------------- musik
BPM = 132
BEAT = 60.0 / BPM
BARS = 16
LOOP = BARS * 4 * BEAT
# Dm - Bb - C - A (dua bar per akor, diulang dua kali)
PROGRESSION = [(50, [62, 65, 69]), (46, [62, 65, 70]), (48, [64, 67, 72]), (45, [61, 64, 69])]


def taiko(gain=1.0, pitch=58):
    d = 0.7
    body = osc_sine(glide(d, pitch, pitch * 0.72)) * env_exp(d, 0.22)
    skin = lp(noise(d), 1200) * env_exp(d, 0.03)
    return drive(body * 1.2 + skin * 0.5, 1.5) * gain


def frame_drum(gain=1.0):
    d = 0.25
    return (bp(noise(d), 300, 2500) * env_exp(d, 0.05) + osc_sine(glide(d, 190, 120)) * env_exp(d, 0.06) * 0.6) * gain


def string_note(midi, d, gain=1.0, cutoff=1100):
    f = np.full(int(d * SR), note_hz(midi))
    tone = osc_saw(f) + osc_saw(f * 1.006) * 0.7
    return lp(tone, cutoff) * env_adsr(d, 0.006, 0.05, 0.6, min(0.05, d * 0.3)) * gain


def pad_chord(notes, d, gain=1.0):
    out = np.zeros(int(d * SR))
    for m in notes:
        f = np.full(int(d * SR), note_hz(m))
        out += lp(osc_saw(f) + osc_saw(f * 1.005) * 0.8 + osc_saw(f * 0.997) * 0.8, 1300)
    return out * env_adsr(d, 0.35, 0.3, 0.8, 0.5) * gain / len(notes)


def render_music(intense):
    tail = 2.0
    n = int((LOOP + tail) * SR)
    left = np.zeros(n)
    right = np.zeros(n)

    def put(sig, start, gain, pan=0.0):
        place(left, sig, start, gain * math.cos((pan + 1) * math.pi / 4))
        place(right, sig, start, gain * math.sin((pan + 1) * math.pi / 4))

    for bar in range(BARS):
        root, chord = PROGRESSION[(bar // 2) % 4]
        t0 = bar * 4 * BEAT

        # Perkusi
        kicks = [0, 1.5, 2, 3] if not intense else [0, 0.75, 1.5, 2, 2.5, 3, 3.5]
        for b in kicks:
            put(taiko(1.0 if b in (0, 2) else 0.7), t0 + b * BEAT, 0.9, -0.1)
        for b in ([1, 3] if not intense else [0.5, 1, 1.5, 2.5, 3, 3.5]):
            put(frame_drum(0.6), t0 + b * BEAT, 0.5, 0.35)
        if bar % 4 == 3:
            for k in range(4 if not intense else 8):
                step = 1.0 if not intense else 0.5
                put(taiko(0.8, 70), t0 + (2 + k * step * 0.5) * BEAT, 0.6, -0.3)

        # Ostinato string rendah: 1/8 (normal) atau 1/16 (enraged)
        div = 2 if not intense else 4
        pattern = [0, 0, 12, 0, 7, 0, 10, 7] if not intense else [0, 0, 12, 0, 7, 0, 10, 7, 0, 12, 0, 7, 3, 0, 10, 12]
        for k in range(4 * div):
            midi = root - 12 + pattern[k % len(pattern)]
            put(string_note(midi, BEAT / div * 0.92, 0.55, 900 if not intense else 1500), t0 + k * BEAT / div, 0.5, 0.2)

        # Pad brass/strings tiap 2 bar
        if bar % 2 == 0:
            put(pad_chord(chord, 8 * BEAT, 1.0), t0, 0.45, -0.25)
            put(pad_chord([c + 12 for c in chord], 8 * BEAT, 0.5), t0, 0.25, 0.3)

        # Stab brass
        if intense:
            for b in (0, 1.5, 3):
                put(brass(chord[0] + 12, BEAT * 0.45, 0.6), t0 + b * BEAT, 0.35, 0.1)
        elif bar % 2 == 1:
            put(brass(chord[-1], BEAT * 1.5, 0.5), t0 + 2 * BEAT, 0.3, 0.1)

    # Loop mulus: ekor setelah LOOP dibungkus ke awal.
    end = int(LOOP * SR)
    left[:n - end] += left[end:]
    right[:n - end] += right[end:]
    stereo = np.stack([left[:end], right[:end]], axis=1)
    if intense:
        stereo = drive(stereo, 1.3)
    return stereo * (10 ** (-3 / 20) / (np.max(np.abs(stereo)) + 1e-9))


# ----------------------------------------------------------------------------- tulis
SFX = {
    "DH_Sfx_SwordSwing1": lambda: sfx_sword_swing(True),
    "DH_Sfx_SwordSwing2": lambda: sfx_sword_swing(False),
    "DH_Sfx_SwordHit": sfx_sword_hit,
    "DH_Sfx_HeavyHit": sfx_heavy_hit,
    "DH_Sfx_ArrowShoot": sfx_arrow_shoot,
    "DH_Sfx_ArrowHit": sfx_arrow_hit,
    "DH_Sfx_Dodge": sfx_dodge,
    "DH_Sfx_HunterHurt": sfx_hunter_hurt,
    "DH_Sfx_Potion": sfx_potion,
    "DH_Sfx_DragonGrowl": sfx_dragon_growl,
    "DH_Sfx_DragonRoar": sfx_dragon_roar,
    "DH_Sfx_DragonHurt": sfx_dragon_hurt,
    "DH_Sfx_DragonDeath": sfx_dragon_death,
    "DH_Sfx_BiteSnap": sfx_bite_snap,
    "DH_Sfx_TailWhoosh": sfx_tail_whoosh,
    "DH_Sfx_FireBreath": sfx_fire_breath,
    "DH_Sfx_ChargeStomp": sfx_charge_stomp,
    "DH_Sfx_NovaBlast": sfx_nova_blast,
    "DH_Sfx_Victory": sfx_victory,
    "DH_Sfx_Defeat": sfx_defeat,
    "DH_Sfx_UiClick": sfx_ui_click,
}


def write(path, data):
    # Ditulis per blok: libsndfile 1.2 bisa crash bila buffer Vorbis besar ditulis sekaligus.
    channels = 1 if data.ndim == 1 else data.shape[1]
    with sf.SoundFile(path, "w", SR, channels, format="OGG", subtype="VORBIS") as out:
        for i in range(0, len(data), 4096):
            out.write(data[i:i + 4096])
    info = sf.info(path)
    peak = float(np.max(np.abs(data)))
    print(f"{os.path.basename(path):28s} {info.duration:6.2f}s  {info.channels}ch  peak {20 * math.log10(peak + 1e-9):6.1f} dBFS  {os.path.getsize(path) / 1024:7.1f} KB")


def main():
    os.makedirs(OUT_SFX, exist_ok=True)
    os.makedirs(OUT_MUSIC, exist_ok=True)
    for name, fn in SFX.items():
        data = normalize(fn(), -1.0)
        if not np.all(np.isfinite(data)):
            sys.exit(f"NaN/Inf pada {name}")
        write(os.path.join(OUT_SFX, name + ".ogg"), data)
    # Kedua loop diputar sinkron lalu di-crossfade, jadi kerasnya disamakan lewat RMS
    # (versi Enraged sedikit lebih keras), dengan batas peak -1 dBFS.
    write(os.path.join(OUT_MUSIC, "DH_Music_Battle.ogg"), match_rms(render_music(False), -17.0))
    write(os.path.join(OUT_MUSIC, "DH_Music_BattleEnraged.ogg"), match_rms(render_music(True), -15.5))


if __name__ == "__main__":
    main()
