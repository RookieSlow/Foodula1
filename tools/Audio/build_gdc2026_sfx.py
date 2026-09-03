"""Build Foodula1's compact SFX pack from the Sonniss GDC 2026 source bundle.

The source bundle is read-only. Outputs are deterministic 48 kHz, stereo,
16-bit PCM WAV files normalized to a -3 dBFS ceiling.
"""

from __future__ import annotations

import argparse
import json
import math
import wave
from pathlib import Path

import numpy as np


TARGET_RATE = 48_000
PEAK = 10 ** (-3.0 / 20.0)

SOURCES = {
    "ui": "UIClick_UI Button Analog Vintage Double Click Neutral Dry Press 11_ESM_BG.wav",
    "card": "PAPRHndl_Game Play Cards Dry Show Flip Toss Disgard Near 12_ESM_BG.wav",
    "deal": "GAMECas_Dealing 3_344 Audio_Casino Cards Vol 1.wav",
    "pickup": "GAMECas_Pick Up Multiple Cards At Once 5_344 Audio_Casino Cards Vol 1.wav",
    "shuffle": "GAMECas_Automatic Shuffler, Shuffling 1_344 Audio_Casino Cards Vol 1.wav",
    "dial": "VEHInt_CLIMATE CONTROL SYSTEM, FAN SPEED DIAL, FAST_344 Audio_Car Foley Vol 1.wav",
    "latch": "MECHLtch_Click Deep Mechanism Latch Button Nearfield Thunk 02_ESM_HDLM.wav",
    "spray": "OBJMisc_Spray Bottle, Spray 1_344 Audio_Barbershop Vol 1.wav",
    "arc": "ELECArc_ArcPowerUpDesign04_InMotionAudio_Arc.wav",
    "wind": "WINDDsgn_Wind, Rush, Whoosh, Long x5 01_344 Audio_Elemental Palette Designed Vol 1.wav",
    "metal_whoosh": "METLMisc_Metal, Slow Whoosh, Rattle, Pass By x4 01_344 Audio_Elemental Palette Designed Vol 1.wav",
    "wire_car": "WIR006.wav",
    "crowd": "CRWDCheer_Small Club, 50 People, Crowd, Spanish, Cheering and Applause-Surround_KSL_KS015.wav",
}


def locate(root: Path, filename: str) -> Path:
    matches = list(root.rglob(filename))
    if len(matches) != 1:
        raise RuntimeError(f"Expected exactly one source named {filename!r}, found {len(matches)}")
    return matches[0]


def read_pcm(path: Path) -> tuple[int, np.ndarray]:
    with wave.open(str(path), "rb") as stream:
        channels = stream.getnchannels()
        width = stream.getsampwidth()
        rate = stream.getframerate()
        frames = stream.readframes(stream.getnframes())

    if width == 2:
        values = np.frombuffer(frames, dtype="<i2").astype(np.float32) / 32768.0
    elif width == 3:
        raw = np.frombuffer(frames, dtype=np.uint8).reshape(-1, 3)
        integers = (raw[:, 0].astype(np.int32)
                    | (raw[:, 1].astype(np.int32) << 8)
                    | (raw[:, 2].astype(np.int32) << 16))
        integers = np.where(integers & 0x800000, integers - 0x1000000, integers)
        values = integers.astype(np.float32) / 8_388_608.0
    elif width == 4:
        values = np.frombuffer(frames, dtype="<i4").astype(np.float32) / 2_147_483_648.0
    else:
        raise RuntimeError(f"Unsupported sample width {width} in {path}")

    values = values.reshape(-1, channels)
    if channels == 1:
        values = np.repeat(values, 2, axis=1)
    elif channels > 2:
        values = np.column_stack((values[:, 0::2].mean(axis=1), values[:, 1::2].mean(axis=1)))
    return rate, values[:, :2]


def resample(data: np.ndarray, source_rate: int, target_rate: int = TARGET_RATE) -> np.ndarray:
    if source_rate == target_rate or len(data) < 2:
        return data.copy()
    count = max(1, round(len(data) * target_rate / source_rate))
    old = np.linspace(0.0, 1.0, len(data), endpoint=False)
    new = np.linspace(0.0, 1.0, count, endpoint=False)
    return np.column_stack([np.interp(new, old, data[:, channel]) for channel in range(2)]).astype(np.float32)


def crop(data: np.ndarray, start: float, duration: float | None = None) -> np.ndarray:
    first = max(0, round(start * TARGET_RATE))
    last = len(data) if duration is None else min(len(data), first + round(duration * TARGET_RATE))
    return data[first:last].copy()


def active_excerpt(data: np.ndarray, duration: float, offset: float = 0.0) -> np.ndarray:
    """Pick a deterministic high-energy excerpt, with a little pre-roll."""
    size = max(1, round(duration * TARGET_RATE))
    if len(data) <= size:
        return data.copy()
    mono = np.abs(data).mean(axis=1)
    hop = max(1, TARGET_RATE // 100)
    blocks = np.add.reduceat(mono, np.arange(0, len(mono), hop))
    window_blocks = max(1, size // hop)
    energy = np.convolve(blocks, np.ones(window_blocks), mode="valid")
    center = int(np.argmax(energy)) * hop
    first = max(0, min(len(data) - size, center - round(offset * TARGET_RATE)))
    return data[first:first + size].copy()


def speed(data: np.ndarray, factor: float) -> np.ndarray:
    count = max(1, round(len(data) / factor))
    source_positions = np.linspace(0, len(data) - 1, count)
    original = np.arange(len(data))
    return np.column_stack([np.interp(source_positions, original, data[:, c]) for c in range(2)]).astype(np.float32)


def soften(data: np.ndarray, taps: int = 7) -> np.ndarray:
    taps = max(1, taps | 1)
    kernel = np.ones(taps, dtype=np.float32) / taps
    return np.column_stack([np.convolve(data[:, c], kernel, mode="same") for c in range(2)]).astype(np.float32)


def fade(data: np.ndarray, fade_in: float = 0.008, fade_out: float = 0.025) -> np.ndarray:
    result = data.copy()
    in_count = min(len(result), round(fade_in * TARGET_RATE))
    out_count = min(len(result), round(fade_out * TARGET_RATE))
    if in_count:
        result[:in_count] *= np.linspace(0.0, 1.0, in_count)[:, None]
    if out_count:
        result[-out_count:] *= np.linspace(1.0, 0.0, out_count)[:, None]
    return result


def layer(*parts: tuple[np.ndarray, float, float]) -> np.ndarray:
    """Mix (audio, gain, delay_seconds) tuples."""
    length = max(round(delay * TARGET_RATE) + len(audio) for audio, _, delay in parts)
    mixed = np.zeros((length, 2), dtype=np.float32)
    for audio, gain, delay in parts:
        first = round(delay * TARGET_RATE)
        mixed[first:first + len(audio)] += audio * gain
    return mixed


def normalize(data: np.ndarray, gain: float = 1.0, target_rms_db: float = -20.0) -> np.ndarray:
    result = data * gain
    rms = float(np.sqrt(np.mean(result * result))) if len(result) else 0.0
    target_rms = 10 ** (target_rms_db / 20.0)
    if rms > 1e-6:
        result *= target_rms / rms
    peak = float(np.max(np.abs(result))) if len(result) else 0.0
    if peak > PEAK:
        result *= PEAK / peak
    return np.clip(result, -1.0, 1.0)


def write_wav(path: Path, data: np.ndarray, target_rms_db: float) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    pcm = np.round(normalize(fade(data), target_rms_db=target_rms_db) * 32767.0).astype("<i2")
    with wave.open(str(path), "wb") as stream:
        stream.setnchannels(2)
        stream.setsampwidth(2)
        stream.setframerate(TARGET_RATE)
        stream.writeframes(pcm.tobytes())


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source_root", type=Path)
    parser.add_argument("output_root", type=Path)
    args = parser.parse_args()

    source_paths = {key: locate(args.source_root, name) for key, name in SOURCES.items()}
    audio = {}
    for key, path in source_paths.items():
        source_rate, source_audio = read_pcm(path)
        audio[key] = resample(source_audio, source_rate)

    ui = active_excerpt(audio["ui"], 0.16, 0.01)
    card = active_excerpt(audio["card"], 0.17, 0.01)
    latch = active_excerpt(audio["latch"], 0.32, 0.015)
    deal = active_excerpt(audio["deal"], 0.28, 0.02)
    pickup = active_excerpt(audio["pickup"], 0.36, 0.02)
    dial = active_excerpt(audio["dial"], 0.42, 0.02)
    spray = active_excerpt(audio["spray"], 0.50, 0.03)
    arc = active_excerpt(audio["arc"], 0.62, 0.04)
    wind = active_excerpt(audio["wind"], 0.84, 0.04)
    metal = active_excerpt(audio["metal_whoosh"], 0.70, 0.04)
    wire = active_excerpt(audio["wire_car"], 0.44, 0.025)
    shuffle = active_excerpt(audio["shuffle"], 0.78, 0.04)
    crowd = active_excerpt(audio["crowd"], 1.80, 0.10)

    short_hop = soften(active_excerpt(audio["wire_car"], 0.14, 0.015), 17)
    short_trigger = active_excerpt(metal, 0.24, 0.02)
    warning_pulse = soften(active_excerpt(latch, 0.16, 0.01), 13)
    spin_mechanical = layer(
        (speed(active_excerpt(metal, 0.52, 0.02), 1.15), 0.72, 0.0),
        (soften(latch, 11), 0.36, 0.08))

    outputs = {
        "ui_hover.wav": (soften(speed(ui, 1.10), 9), -25.0),
        "ui_confirm.wav": (ui, -20.0),
        "ui_back.wav": (speed(ui, 0.82), -21.0),
        "ui_error.wav": (layer((warning_pulse, 0.8, 0.0), (speed(warning_pulse, 0.86), 0.55, 0.14)), -23.0),
        "card_select.wav": (card, -20.0),
        "card_deselect.wav": (speed(card, 0.86), -22.0),
        "card_play.wav": (layer((card, 0.76, 0.0), (latch, 0.26, 0.08)), -18.0),
        "card_discard.wav": (pickup, -19.0),
        "card_draw.wav": (deal, -20.0),
        "deck_shuffle.wav": (shuffle, -21.0),
        "gear_shift.wav": (layer((dial, 0.78, 0.0), (latch, 0.30, 0.08)), -19.0),
        "gear_failure.wav": (layer((speed(latch, 0.76), 0.82, 0.0), (warning_pulse, 0.42, 0.10)), -18.0),
        "heat_pay.wav": (layer((soften(arc, 21), 0.48, 0.0), (speed(latch, 0.82), 0.72, 0.04)), -20.0),
        "heat_cool.wav": (layer((spray, 0.72, 0.0), (speed(active_excerpt(wind, 0.34), 1.32), 0.42, 0.06)), -20.0),
        "heat_warning.wav": (layer((warning_pulse, 0.72, 0.0), (warning_pulse, 0.50, 0.18)), -24.0),
        "car_hop.wav": (short_hop, -26.0),
        "slipstream_trigger.wav": (layer((short_trigger, 0.68, 0.0), (speed(short_trigger, 1.22), 0.38, 0.02)), -18.0),
        "slipstream_move.wav": (wind, -21.0),
        "corner_safe.wav": (layer((wire, 0.54, 0.0), (ui, 0.24, 0.18)), -21.0),
        "corner_over.wav": (layer((speed(metal, 1.20), 0.68, 0.0), (wire, 0.32, 0.08)), -19.0),
        "spin_out.wav": (spin_mechanical, -18.0),
        "lap_cross.wav": (layer((speed(ui, 1.16), 0.74, 0.0), (short_trigger, 0.34, 0.05)), -21.0),
        "final_lap.wav": (layer((warning_pulse, 0.72, 0.0), (warning_pulse, 0.58, 0.18), (ui, 0.48, 0.34)), -19.0),
        "finish.wav": (layer((crowd, 0.72, 0.0), (ui, 0.36, 0.04)), -20.0),
    }

    for name, (data, target_rms_db) in outputs.items():
        write_wav(args.output_root / name, data, target_rms_db)

    manifest = {
        "format": {"sample_rate": TARGET_RATE, "channels": 2, "sample_width_bits": 16, "peak_dbfs_max": -3},
        "source_bundle": args.source_root.name,
        "sources": {key: str(path.relative_to(args.source_root)) for key, path in source_paths.items()},
        "outputs": sorted(outputs),
    }
    (args.output_root / "build-manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Built {len(outputs)} SFX in {args.output_root}")


if __name__ == "__main__":
    main()
