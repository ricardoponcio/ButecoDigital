"""Gera as músicas da roda de pagode (procedurais) e os efeitos sonoros do buteco.

Uso: python3 Tools/gen_audio.py
Saída: Assets/_Buteco/Audio/
Nada aqui é sample de terceiros: tudo é sintetizado.
"""
import os
import wave

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "_Buteco", "Audio")
os.makedirs(OUT, exist_ok=True)

SR = 44100
rng = np.random.default_rng(42)


def save(name, data, stereo=False):
    data = np.clip(data, -1, 1)
    pcm = (data * 32000).astype(np.int16)
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(2 if stereo else 1)
        w.setsampwidth(2)
        w.setframerate(SR)
        if stereo:
            pcm = pcm.reshape(-1)
        w.writeframes(pcm.tobytes())


def env(n, attack=0.002, decay=0.2):
    t = np.arange(n) / SR
    a = np.clip(t / max(attack, 1e-6), 0, 1)
    return a * np.exp(-t / decay)


def noise(n):
    return rng.uniform(-1, 1, n)


def bandpass(x, lo, hi):
    f = np.fft.rfft(x)
    freqs = np.fft.rfftfreq(len(x), 1 / SR)
    f[(freqs < lo) | (freqs > hi)] = 0
    return np.fft.irfft(f, len(x))


# ---------------------------------------------------------------- instrumentos

def surdo(strong=True):
    n = int(SR * 0.6)
    t = np.arange(n) / SR
    freq = 55 + 40 * np.exp(-t / 0.04)
    ph = 2 * np.pi * np.cumsum(freq) / SR
    s = np.sin(ph) * env(n, 0.003, 0.25)
    s += 0.15 * bandpass(noise(n), 80, 400) * env(n, 0.001, 0.03)
    return s * (1.0 if strong else 0.55)


def pandeiro(accent):
    n = int(SR * 0.12)
    s = bandpass(noise(n), 4000, 12000) * env(n, 0.001, 0.035)
    # platinelas: ressonâncias metálicas
    t = np.arange(n) / SR
    for f in (6200, 7900, 9300):
        s += 0.2 * np.sin(2 * np.pi * f * t) * env(n, 0.001, 0.05)
    return s * (0.55 if accent else 0.25)


def pandeiro_grave():
    n = int(SR * 0.25)
    t = np.arange(n) / SR
    s = np.sin(2 * np.pi * (140 + 60 * np.exp(-t / 0.02)) * t) * env(n, 0.002, 0.08)
    return s * 0.5


def tamborim():
    n = int(SR * 0.1)
    t = np.arange(n) / SR
    s = np.sin(2 * np.pi * 820 * t) * env(n, 0.001, 0.03)
    s += 0.5 * bandpass(noise(n), 1500, 6000) * env(n, 0.001, 0.015)
    return s * 0.35


def ganza(accent):
    n = int(SR * 0.08)
    s = bandpass(noise(n), 5000, 14000) * env(n, 0.01, 0.025)
    return s * (0.22 if accent else 0.12)


def karplus(freq, dur, bright=0.5, damp=0.996):
    n = int(SR * dur)
    period = max(2, int(SR / freq))
    buf = rng.uniform(-1, 1, period) * bright + rng.uniform(-1, 1, period) * (1 - bright) * 0.3
    out = np.zeros(n)
    idx = 0
    for i in range(n):
        out[i] = buf[idx]
        nxt = (idx + 1) % period
        buf[idx] = damp * 0.5 * (buf[idx] + buf[nxt])
        idx = nxt
    return out * env(n, 0.001, dur * 0.6)


def bass_note(freq, dur):
    n = int(SR * dur)
    t = np.arange(n) / SR
    s = np.sin(2 * np.pi * freq * t) + 0.3 * np.sin(4 * np.pi * freq * t) + 0.1 * np.sin(6 * np.pi * freq * t)
    return s * env(n, 0.005, dur * 0.5) * 0.5


def midi(m):
    return 440.0 * 2 ** ((m - 69) / 12)


# ---------------------------------------------------------------- música

def mix_in(track, sound, start):
    i = int(start * SR)
    if i < 0:
        sound = sound[-i:]
        i = 0
    if i >= len(track):
        return
    end = min(len(track), i + len(sound))
    track[i:end] += sound[: end - i]


PROG_1 = [
    ("C", [48, 52, 55, 60]), ("A7", [45, 49, 52, 55]), ("Dm", [50, 53, 57, 62]), ("G7", [43, 47, 50, 53]),
    ("C", [48, 52, 55, 60]), ("E7", [40, 44, 47, 50]), ("Am", [45, 48, 52, 57]), ("D7", [50, 54, 57, 60]),
    ("Dm", [50, 53, 57, 62]), ("G7", [43, 47, 50, 53]), ("Em", [52, 55, 59, 64]), ("A7", [45, 49, 52, 55]),
    ("Dm", [50, 53, 57, 62]), ("G7", [43, 47, 50, 53]), ("C", [48, 52, 55, 60]), ("G7", [43, 47, 50, 53]),
]

CHORDS = {
    "C": [48, 52, 55, 60], "A7": [45, 49, 52, 55], "Dm": [50, 53, 57, 62], "G7": [43, 47, 50, 53],
    "E7": [40, 44, 47, 50], "Am": [45, 48, 52, 57], "D7": [50, 54, 57, 60], "Em": [52, 55, 59, 64],
    "F": [53, 57, 60, 65], "Fm": [53, 56, 60, 65],
}
# "Fim de tarde na laje": verso (16) + refrão (8) + refrão (8)
VERSO_2 = ["C", "C", "A7", "A7", "Dm", "Dm", "G7", "G7", "Em", "A7", "Dm", "G7", "C", "A7", "D7", "G7"]
REFRAO_2 = ["F", "G7", "Em", "A7", "Dm", "G7", "C", "C"]
PROG_2 = [(c, CHORDS[c]) for c in VERSO_2 + REFRAO_2 + REFRAO_2]


def samba(prog, bpm, radio=False, melody=None, palmas_from=None, choir_from=None):
    """Base de pagode (pandeiro, ganzá, tamborim, surdo, cavaco, 7 cordas) + melodia opcional."""
    beat = 60 / bpm
    bars = len(prog)
    total = bars * 4 * beat  # 2/4 escrito em 4/4 para simplificar
    track = np.zeros(int(total * SR) + SR)

    pand = [pandeiro(True), pandeiro(False)]
    gz = [ganza(True), ganza(False)]
    tb = tamborim()
    sd_strong, sd_weak = surdo(True), surdo(False)
    pg = pandeiro_grave()
    clap = palma() * 0.5

    # padrão do tamborim (16 semicolcheias por 2 tempos) — "teleco-teco" simplificado
    tamb_pattern = [1, 0, 1, 1, 0, 1, 0, 1, 1, 0, 1, 1, 0, 1, 0, 1]
    # batida do cavaco (palhetada) em semicolcheias: 1 = ataque para baixo forte
    cav_pattern = [1, 0, 1, 1, 0, 1, 1, 0, 1, 0, 1, 1, 0, 1, 1, 0]

    # cache de palhetadas do cavaco: 3 variações por acorde
    strums = {}
    for name, notes in prog:
        if name in strums:
            continue
        vs = []
        for _ in range(3):
            strum = np.zeros(int(SR * 0.4))
            for k, m in enumerate(notes):
                s = karplus(midi(m + 12), 0.35, bright=0.7, damp=0.994)
                off = int(k * 0.006 * SR)
                strum[off: off + len(s)] += s[: len(strum) - off]
            vs.append(strum)
        strums[name] = vs

    sixteenth = beat / 4
    for bar in range(bars):
        name, notes = prog[bar]
        t0 = bar * 4 * beat
        for step in range(16):
            t = t0 + step * sixteenth
            human = rng.normal(0, 0.004)
            # pandeiro e ganzá em todas as semicolcheias
            mix_in(track, pand[0 if step % 4 == 0 else 1], t + human)
            mix_in(track, gz[0 if step % 2 == 0 else 1], t + human * 0.5)
            if step % 4 == 2:
                mix_in(track, pg * 0.6, t)
            if tamb_pattern[step] and bar % 2 == 1:
                mix_in(track, tb, t + human)
            # cavaco
            if cav_pattern[step]:
                strum = strums[name][int(rng.integers(0, 3))]
                mix_in(track, strum * (0.18 if step % 4 == 0 else 0.11), t + human)
        # surdo: marcação no 1 e (mais forte) no 2 — de dois em dois tempos
        for b in range(4):
            mix_in(track, sd_weak if b % 2 == 0 else sd_strong, t0 + b * beat)
        # palmas da roda no refrão: no tempo e no contratempo do 2
        if palmas_from is not None and bar >= palmas_from:
            for b in range(4):
                mix_in(track, clap, t0 + b * beat + rng.normal(0, 0.006))
                if b % 2 == 1:
                    mix_in(track, clap * 0.6, t0 + (b + 0.75) * beat + rng.normal(0, 0.006))
        # baixo / violão 7 cordas: fundamental e quinta
        root = notes[0] - 12
        line = [root, root + 7, root, root + 7]
        for b in range(4):
            mix_in(track, bass_note(midi(line[b]), beat * 0.9) * 0.7, t0 + b * beat)
        # baixaria no último compasso de cada frase
        if bar % 4 == 3:
            walk = [root + 12, root + 11, root + 9, root + 7]
            for i, m in enumerate(walk):
                mix_in(track, bass_note(midi(m), beat * 0.45) * 0.5, t0 + 2 * beat + i * beat * 0.5)

    if melody:
        for bar, step, m, dur in melody:
            t = bar * 4 * beat + step * sixteenth + rng.normal(0, 0.003)
            d = dur * sixteenth
            # cavaco solando a melodia uma oitava acima
            mix_in(track, karplus(midi(m + 12), max(0.18, d * 1.3), bright=0.85, damp=0.997) * 0.32, t)
            # a roda cantando junto no refrão ("laiá, laiá")
            if choir_from is not None and bar >= choir_from:
                mix_in(track, coro(midi(m), d * 0.95) * 0.16, t)

    track = track[: int(total * SR)]
    if radio:
        # "rádio de buteco": corta graves e agudos extremos e satura levemente
        track = bandpass(track, 70, 9000)
        track = np.tanh(track * 1.4)
    else:
        track = np.tanh(track * 1.1)
    track /= np.max(np.abs(track)) + 1e-9
    track *= 0.85
    fade = int(0.05 * SR)
    track[:fade] *= np.linspace(0, 1, fade)
    track[-fade:] *= np.linspace(1, 0, fade)
    left = track
    right = np.roll(track, int(0.012 * SR)) * 0.95
    return np.stack([left, right], axis=1)


def musica():
    return samba(PROG_1, 100, radio=True)


# células rítmicas de samba (início, duração em semicolcheias) dentro de um compasso de 16
CELLS = [
    [(1, 2), (3, 3), (6, 2), (9, 3), (12, 4)],
    [(0, 2), (2, 2), (5, 3), (8, 2), (11, 5)],
    [(1, 3), (4, 2), (7, 3), (10, 2), (12, 3)],
    [(2, 4), (6, 2), (9, 7)],
    [(0, 3), (3, 3), (6, 4), (11, 5)],
]


def melodia(prog, refrao_from):
    """Melodia em notas do acorde (com passagens), andando por graus; refrão repete o motivo."""
    mel = []
    cur = 67  # G4
    motif = {}
    for bar, (name, notes) in enumerate(prog):
        refrao = bar >= refrao_from
        key = (bar - refrao_from) % 8 if refrao else None
        if refrao and key in motif:
            mel.extend((bar, st, m, d) for st, m, d in motif[key])
            continue
        cell = CELLS[(bar * 3 + (1 if refrao else 0)) % len(CELLS)] if not refrao else CELLS[[3, 0, 3, 1, 2, 0, 4, 3][key]]
        pitches = sorted({n + o for n in notes for o in (12, 24) if 60 <= n + o <= 77})
        out = []
        for i, (st, d) in enumerate(cell):
            # escolhe a nota do acorde mais próxima, com um passo pra cima/baixo de vez em quando
            target = cur + int(rng.choice([-3, -2, -1, 1, 2, 3, 4], p=[0.12, 0.2, 0.13, 0.13, 0.2, 0.12, 0.1]))
            m = min(pitches, key=lambda p: abs(p - target))
            if i == len(cell) - 1 and bar % 4 == 3:
                m = min(pitches, key=lambda p: abs(p - notes[0] - 12 * round((cur - notes[0]) / 12)))
            m = int(np.clip(m, 60, 77))
            out.append((st, m, d))
            cur = m
        if refrao:
            motif[key] = out
        mel.extend((bar, st, m, d) for st, m, d in out)
    return mel


def musica_roda_2():
    mel = melodia(PROG_2, 16)
    return samba(PROG_2, 96, melody=mel, palmas_from=16, choir_from=16)


def musica_roda_1():
    # o mesmo samba do rádio, agora ao vivo, com palmas na segunda metade
    return samba(PROG_1 + PROG_1, 100, palmas_from=16)


def coro(f0, dur):
    """Vozes cantando "a": dente-de-serra com vibrato, moldado por formantes no espectro."""
    n = int(SR * dur)
    t = np.arange(n) / SR
    out = np.zeros(n)
    for v in range(4):
        det = 1 + rng.uniform(-0.008, 0.008)
        vib = 1 + 0.012 * np.sin(2 * np.pi * (5.2 + v * 0.3) * t + rng.uniform(0, 6))
        ph = 2 * np.pi * np.cumsum(f0 * det * vib) / SR
        saw = 2 * ((ph / (2 * np.pi)) % 1.0) - 1
        out += saw
    spec = np.fft.rfft(out)
    freqs = np.fft.rfftfreq(n, 1 / SR)
    shape = np.zeros_like(freqs)
    for fc, bw, g in ((800, 110, 1.0), (1150, 130, 0.6), (2900, 220, 0.25), (3300, 250, 0.15)):
        shape += g * np.exp(-0.5 * ((freqs - fc) / bw) ** 2)
    out = np.fft.irfft(spec * shape, n)
    a = np.clip(t / 0.06, 0, 1) * np.clip((dur - t) / 0.08, 0, 1)
    return out * a / (np.max(np.abs(out)) + 1e-9)


def palma():
    n = int(SR * 0.09)
    s = bandpass(noise(n), 800, 5000) * env(n, 0.0008, 0.012)
    s += 0.4 * bandpass(noise(n), 300, 1200) * env(n, 0.0005, 0.006)
    return s / (np.max(np.abs(s)) + 1e-9) * 0.8


def aplausos():
    """Aplauso da roda no fim da música + um assobio."""
    dur = 4.5
    n = int(SR * dur)
    out = np.zeros(n)
    t = 0.0
    while t < dur - 0.1:
        rate = 45 * np.exp(-((t - 0.8) / 1.6) ** 2) + 3
        t += rng.exponential(1 / rate)
        c = palma() * rng.uniform(0.15, 0.5)
        f = rng.uniform(0.9, 1.15)
        idx = (np.arange(int(len(c) / f)) * f).astype(int)
        c = c[idx[idx < len(c)]]
        mix_in(out, c, t)
    # assobio
    wn = int(SR * 0.6)
    wt = np.arange(wn) / SR
    fr = 2200 + 900 * np.sin(np.pi * wt / 0.6)
    w = np.sin(2 * np.pi * np.cumsum(fr) / SR) * np.sin(np.pi * wt / 0.6) * 0.12
    out[int(0.6 * SR): int(0.6 * SR) + wn] += w
    out[-int(0.8 * SR):] *= np.linspace(1, 0, int(0.8 * SR))
    return out / (np.max(np.abs(out)) + 1e-9) * 0.8


# ---------------------------------------------------------------- efeitos

def sfx_ball_click():
    n = int(SR * 0.12)
    t = np.arange(n) / SR
    s = 0.6 * np.sin(2 * np.pi * 2900 * t) * env(n, 0.0003, 0.012)
    s += 0.4 * np.sin(2 * np.pi * 4700 * t) * env(n, 0.0003, 0.008)
    s += 0.4 * bandpass(noise(n), 2000, 9000) * env(n, 0.0002, 0.004)
    return s * 0.9


def sfx_cushion():
    n = int(SR * 0.2)
    t = np.arange(n) / SR
    s = np.sin(2 * np.pi * 180 * t) * env(n, 0.002, 0.04)
    s += 0.5 * bandpass(noise(n), 100, 1200) * env(n, 0.001, 0.03)
    return s * 0.8


def sfx_cue():
    n = int(SR * 0.15)
    t = np.arange(n) / SR
    s = 0.7 * np.sin(2 * np.pi * 1400 * t) * env(n, 0.0003, 0.015)
    s += 0.5 * bandpass(noise(n), 600, 5000) * env(n, 0.0002, 0.01)
    s += 0.3 * np.sin(2 * np.pi * 380 * t) * env(n, 0.001, 0.03)
    return s * 0.9


def sfx_pocket():
    n = int(SR * 0.7)
    s = np.zeros(n)
    t = np.arange(n) / SR
    s += np.sin(2 * np.pi * 120 * t) * env(n, 0.002, 0.08) * 0.8
    # bola rolando pelo trilho de retorno
    for k in range(6):
        start = int((0.08 + k * 0.07 + rng.uniform(0, 0.02)) * SR)
        c = sfx_cushion()[: n - start] * (0.35 * (0.8 ** k))
        s[start: start + len(c)] += c
    return s


def sfx_chalk():
    n = int(SR * 0.3)
    s = bandpass(noise(n), 1500, 6000)
    am = (np.sin(2 * np.pi * 28 * np.arange(n) / SR) > 0).astype(float)
    return s * am * env(n, 0.02, 0.12) * 0.4


def ambiente_viela():
    """Ruído de fundo da rua: murmúrio, uns latidos distantes, moto longe."""
    dur = 30
    n = SR * dur
    base = bandpass(noise(n), 150, 1800) * 0.05
    # murmúrio: ruído modulado lentamente
    lfo = 0.6 + 0.4 * np.sin(2 * np.pi * 0.13 * np.arange(n) / SR)
    base *= lfo
    # moto passando ao longe
    t = np.arange(int(SR * 4)) / SR
    moto = np.sin(2 * np.pi * (70 + 15 * np.sin(2 * np.pi * 3 * t)) * t) + 0.5 * np.sign(np.sin(2 * np.pi * 140 * t))
    moto = bandpass(moto, 60, 900) * np.sin(np.pi * t / 4) ** 2 * 0.05
    i = int(SR * 11)
    base[i: i + len(moto)] += moto
    # cachorro latindo longe
    for start in (4.0, 4.35, 21.2, 21.5, 21.9):
        bn = int(SR * 0.18)
        bt = np.arange(bn) / SR
        bark = np.sin(2 * np.pi * (500 - 900 * bt) * bt) + 0.4 * bandpass(noise(bn), 400, 2000)
        bark = bandpass(bark, 300, 2500) * env(bn, 0.01, 0.06) * 0.06
        j = int(start * SR)
        base[j: j + bn] += bark
    fade = int(0.5 * SR)
    base[:fade] *= np.linspace(0, 1, fade)
    base[-fade:] *= np.linspace(1, 0, fade)
    return base


def sfx_garrafa():
    """Abrindo a garrafa: tsss + tampinha caindo no balcão."""
    n = int(SR * 0.9)
    s = np.zeros(n)
    hiss = bandpass(noise(int(SR * 0.35)), 3000, 12000) * env(int(SR * 0.35), 0.002, 0.12) * 0.6
    s[: len(hiss)] += hiss
    t = np.arange(int(SR * 0.15)) / SR
    for k, at in enumerate((0.38, 0.5, 0.57)):
        ping = np.sin(2 * np.pi * (3100 + k * 250) * t) * np.exp(-t / 0.02) * (0.35 * 0.6 ** k)
        i = int(at * SR)
        s[i: i + len(ping)] += ping
    return s


def sfx_servir():
    """Cerveja enchendo o copo americano: o tom sobe conforme enche."""
    dur = 1.4
    n = int(SR * dur)
    t = np.arange(n) / SR
    out = np.zeros(n)
    base = noise(n)
    hop = 2048
    for i in range(0, n - hop, hop // 2):
        seg = base[i: i + hop] * np.hanning(hop)
        fc = 500 + 1300 * (i / n)
        spec = np.fft.rfft(seg)
        fr = np.fft.rfftfreq(hop, 1 / SR)
        spec *= np.exp(-0.5 * ((fr - fc) / 180) ** 2) + 0.15 * np.exp(-0.5 * ((fr - 6000) / 2500) ** 2)
        out[i: i + hop] += np.fft.irfft(spec, hop)
    a = np.clip(t / 0.08, 0, 1) * np.clip((dur - t) / 0.25, 0, 1)
    out *= a
    return out / (np.max(np.abs(out)) + 1e-9) * 0.45


def sfx_gole():
    n = int(SR * 0.35)
    t = np.arange(n) / SR
    fr = 260 - 140 * t / 0.35
    s = np.sin(2 * np.pi * np.cumsum(fr) / SR) * env(n, 0.01, 0.07)
    s += 0.2 * bandpass(noise(n), 200, 900) * env(n, 0.005, 0.04)
    return s * 0.6


def sfx_mordida():
    n = int(SR * 0.3)
    s = np.zeros(n)
    for k in range(5):
        bn = int(SR * 0.03)
        b = bandpass(noise(bn), 800, 6000) * env(bn, 0.001, 0.008)
        i = int((0.01 + k * 0.035 + rng.uniform(0, 0.01)) * SR)
        s[i: i + bn] += b * (0.8 ** k)
    return s * 0.6


def sfx_passo():
    n = int(SR * 0.18)
    t = np.arange(n) / SR
    s = np.sin(2 * np.pi * (110 + 60 * np.exp(-t / 0.01)) * t) * env(n, 0.002, 0.03)
    s += 0.35 * bandpass(noise(n), 300, 3500) * env(n, 0.001, 0.02)
    return s * 0.5


def main():
    save("musica_pagode_radio.wav", musica(), stereo=True)
    save("sfx_bola.wav", sfx_ball_click())
    save("sfx_tabela.wav", sfx_cushion())
    save("sfx_taco.wav", sfx_cue())
    save("sfx_cacapa.wav", sfx_pocket())
    save("sfx_giz.wav", sfx_chalk())
    save("amb_viela.wav", ambiente_viela())
    save("roda_samba_1.wav", musica_roda_1(), stereo=True)
    save("roda_samba_2.wav", musica_roda_2(), stereo=True)
    save("roda_aplausos.wav", aplausos())
    save("sfx_palma.wav", palma())
    save("sfx_garrafa.wav", sfx_garrafa())
    save("sfx_servir.wav", sfx_servir())
    save("sfx_gole.wav", sfx_gole())
    save("sfx_mordida.wav", sfx_mordida())
    save("sfx_passo.wav", sfx_passo())
    print("ok", OUT)


if __name__ == "__main__":
    main()
