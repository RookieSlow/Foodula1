"""Build team vehicle audio from licensed original recordings; no synthesis.

The five combustion identities use different original muted.io field recordings.
CN uses a real electric motor recording by Joseph Sardin. Gear contacts reuse the
project's licensed Sonniss vehicle dial/latch recording, with recorded rev layers.
Nothing downloads from Freesound or uses compressed public previews.

Rebuild: --source-dir <original-WAV-cache>. --fetch-sources optionally retrieves
only the selected original ZIP members and the publisher's original BWF WAV.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
import tempfile
import urllib.request
import uuid
import wave
import zlib
from pathlib import Path

import numpy as np

RATE = 48_000
PACK_PAGE = "https://muted.io/performance-cars/"
PACK_URL = "https://mutedio.s3.us-east-2.amazonaws.com/sounds/sample-packs/performance-cars-free-sample-pack-mutedio.zip"
MOTOR_PAGE = "https://bigsoundbank.com/car-antenna-exit-s1263.html"
MOTOR_URL = "https://bigsoundbank.com/UPLOAD/bwf-en/1263.wav"
TEAMS = {
    "uk": ("015-performance-cars-mutedio.wav", "Warm recorded engine with vintage mechanical texture", 10, .78, 38, 4100),
    "de": ("038-performance-cars-mutedio.wav", "Smooth precise recorded performance engine", 11, .74, 45, 5200),
    "it": ("042-performance-cars-mutedio.wav", "Bright recorded high-rev performance engine", 10, .72, 60, 6500),
    "us": ("007-performance-cars-mutedio.wav", "Deep uneven recorded combustion/exhaust texture", 10, .84, 32, 3300),
    "cn": ("bigsoundbank_1263.wav", "Recorded automotive antenna motor whine; no combustion layer or certified drivetrain", 11, .70, 260, 3000),
    "jp": ("033-performance-cars-mutedio.wav", "Agile recorded performance engine with upper air texture", 10, .76, 65, 7000),
}
REV_PITCH = {
    "uk": (.92, 1.44), "de": (.94, 1.46), "it": (.96, 1.52),
    "us": (.88, 1.36), "cn": (.94, 1.44), "jp": (.94, 1.52),
}

def fetch_range(url: str, first: int, last: int) -> bytes:
    req = urllib.request.Request(url, headers={"Range": f"bytes={first}-{last}"})
    with urllib.request.urlopen(req, timeout=60) as response:
        if response.status != 206:
            raise RuntimeError("Original archive server must support byte ranges")
        return response.read()

def fetch_sources(cache: Path) -> None:
    """Read public original archive members, validate ZIP CRC, never preview URLs."""
    cache.mkdir(parents=True, exist_ok=True)
    with urllib.request.urlopen(urllib.request.Request(PACK_URL, method="HEAD"), timeout=30) as response:
        archive_size = int(response.headers["Content-Length"])
    tail = fetch_range(PACK_URL, max(0, archive_size - 65536), archive_size - 1)
    eocd_at = tail.rfind(b"PK\x05\x06")
    if eocd_at < 0:
        raise RuntimeError("Missing ZIP directory")
    eocd = struct.unpack_from("<4s4H2LH", tail, eocd_at)
    directory_size, directory_offset = eocd[5:7]
    directory = fetch_range(PACK_URL, directory_offset, directory_offset + directory_size - 1)
    wanted = {v[0] for k, v in TEAMS.items() if k != "cn"}
    position = 0
    found = set()
    while directory[position:position + 4] == b"PK\x01\x02":
        fields = struct.unpack_from("<4s6H3L5H2L", directory, position)
        name_size, extra_size, comment_size = fields[10:13]
        name = directory[position + 46:position + 46 + name_size].decode("utf-8")
        basename = Path(name).name
        if not name.startswith("__MACOSX/") and basename in wanted:
            target = cache / basename
            crc, compressed_size, size, offset, method = fields[7], fields[8], fields[9], fields[16], fields[4]
            if not target.exists() or zlib.crc32(target.read_bytes()) != crc:
                header = struct.unpack("<4s5H3L2H", fetch_range(PACK_URL, offset, offset + 29))
                start = offset + 30 + header[9] + header[10]
                compressed = fetch_range(PACK_URL, start, start + compressed_size - 1)
                values = zlib.decompress(compressed, -15) if method == 8 else compressed
                if len(values) != size or zlib.crc32(values) != crc:
                    raise RuntimeError("Original archive member failed CRC: " + basename)
                target.write_bytes(values)
            found.add(basename)
        position += 46 + name_size + extra_size + comment_size
    if found != wanted:
        raise RuntimeError("Original archive does not contain the selected recordings")
    motor = cache / TEAMS["cn"][0]
    if not motor.exists():
        with urllib.request.urlopen(MOTOR_URL, timeout=60) as response:
            values = response.read()
        if values[:4] != b"RIFF" or values[8:12] != b"WAVE":
            raise RuntimeError("Motor publisher did not return the original WAV")
        motor.write_bytes(values)

def read_pcm(path: Path) -> tuple[int, np.ndarray]:
    with wave.open(str(path), "rb") as source:
        width, channels, rate = source.getsampwidth(), source.getnchannels(), source.getframerate()
        raw = source.readframes(source.getnframes())
    if width == 2:
        values = np.frombuffer(raw, "<i2").astype(np.float64) / 32768.
    elif width == 3:
        b = np.frombuffer(raw, np.uint8).reshape(-1, 3)
        signed = b[:, 0].astype(np.int32) | (b[:, 1].astype(np.int32) << 8) | (b[:, 2].astype(np.int32) << 16)
        signed = np.where(signed & 0x800000, signed - 0x1000000, signed)
        values = signed.astype(np.float64) / 8388608.
    elif width == 4:
        values = np.frombuffer(raw, "<i4").astype(np.float64) / 2147483648.
    else:
        raise RuntimeError(f"Unsupported original PCM width: {path}")
    return rate, values.reshape(-1, channels).mean(axis=1)

def resample(values: np.ndarray, rate: int) -> np.ndarray:
    if rate == RATE:
        return values.copy()
    # FFT resampling removes above-Nyquist energy when reducing 96k field recordings.
    count = round(len(values) * RATE / rate)
    spectrum = np.fft.rfft(values)
    target = np.zeros(count // 2 + 1, complex)
    copied = min(len(spectrum), len(target))
    target[:copied] = spectrum[:copied]
    if count % 2 == 0 and count < len(values):
        target[-1] *= 2
    return np.fft.irfft(target, n=count) * count / len(values)

def gentle_eq(values: np.ndarray, highpass: float, lowpass: float) -> np.ndarray:
    frequency = np.fft.rfftfreq(len(values), 1 / RATE)
    hp = 1 / np.sqrt(1 + (highpass / np.maximum(frequency, 1)) ** 4)
    lp = 1 / np.sqrt(1 + (frequency / lowpass) ** 4)
    return np.fft.irfft(np.fft.rfft(values - values.mean()) * hp * lp, n=len(values))

def rpm_windows(values: np.ndarray, electric: bool) -> list[dict]:
    size, hop = round(RATE * 1.18), round(RATE * .12)
    candidates = []
    for start in range(0, len(values) - size, hop):
        if electric and (start < RATE * 1.4 or start + size > len(values) - RATE * 1.1):
            continue  # Keep the antenna motor's steady run, away from engagement/end-stop contacts.
        part = values[start:start + size]
        rms = float(np.sqrt(np.mean(part ** 2)))
        if rms < .0003:
            continue
        blocks = part[:len(part) // 12 * 12].reshape(12, -1)
        block_rms = np.sqrt(np.mean(blocks ** 2, axis=1))
        if abs(part).max() / rms > 6.25 or block_rms.std() / max(block_rms.mean(), 1e-12) > .4:
            continue  # Reject contacts, isolated transients and sharp drive-by envelope changes.
        probe = part[::4]
        spectrum = np.abs(np.fft.rfft(probe * np.hanning(len(probe)))) ** 2
        frequencies = np.fft.rfftfreq(len(probe), 4 / RATE)
        mask = (frequencies > (300 if electric else 40)) & (frequencies < (2500 if electric else 800))
        peak = float(frequencies[np.argmax(spectrum * mask)])
        candidates.append({"start": start, "rms": rms, "tone_hz": peak})
    if len(candidates) < 3:
        raise RuntimeError("Recording lacks three distinct usable windows")
    loudness_floor = max(c["rms"] for c in candidates) * (.20 if electric else .32)
    candidates = [c for c in candidates if c["rms"] >= loudness_floor]
    candidates.sort(key=lambda c: (c["tone_hz"], -c["rms"]))
    selected = []
    for quantile in (.12, .52, .90):
        target = candidates[round((len(candidates) - 1) * quantile)]["tone_hz"]
        options = [c for c in candidates if all(abs(c["start"] - p["start"]) >= RATE * .45 for p in selected)]
        if not options:
            raise RuntimeError("Recorded layers would duplicate the same excerpt")
        chosen = min(options, key=lambda c: (abs(c["tone_hz"] - target), -c["rms"], c["start"]))
        selected.append(chosen)
    selected.sort(key=lambda c: c["tone_hz"])
    return selected

def seam_loop(part: np.ndarray) -> np.ndarray:
    overlap = round(RATE * .055)
    result = part[:-overlap].copy()
    fade = np.linspace(0, 1, overlap)
    result[:overlap] = part[-overlap:] * (1 - fade) + part[:overlap] * fade
    result -= result.mean()
    result[-1] = result[0]
    return result

def fade_event(values: np.ndarray) -> np.ndarray:
    values = values.copy()
    attack = min(round(RATE * .004), len(values) // 4)
    release = min(round(RATE * .022), len(values) // 3)
    values[:attack] *= np.linspace(0, 1, attack)
    values[-release:] *= np.linspace(1, 0, release)
    values[0] = values[-1] = 0
    return values

def play_loop(loop: np.ndarray, frames: int, pitch=1.) -> np.ndarray:
    if np.isscalar(pitch):
        positions = np.arange(frames) * pitch
    else:
        positions = np.cumsum(pitch)
    return np.interp(positions % len(loop), np.arange(len(loop)), loop)

def normalized(values: np.ndarray, rms_db: float, peak_db: float) -> np.ndarray:
    rms = max(float(np.sqrt(np.mean(values ** 2))), 1e-12)
    scale = min(10 ** (rms_db / 20) / rms, 10 ** (peak_db / 20) / max(abs(values).max(), 1e-12))
    return values * scale

def shift_cue(team: str, layers: list[np.ndarray], mechanical: np.ndarray, up: bool) -> np.ndarray:
    duration = .27 if up else .33
    count = round(RATE * duration)
    t = np.linspace(0, 1, count)
    if team == "cn":
        # Go engages and rises; Recover disengages and winds down, with a real relay-like contact.
        blend = t if up else 1 - t
    else:
        # Upshift briefly drops revs; downshift has a short recorded throttle blip.
        blend = 1 - .65 * np.minimum(t * 2.6, 1) if up else .2 + .8 * np.sin(np.pi * t) ** 1.4
    low = play_loop(layers[0], count, .99)
    mid = play_loop(layers[1], count, 1.)
    high = play_loop(layers[2], count, 1.03)
    rev = np.where(blend < .5, low * np.cos(blend * np.pi) + mid * np.sin(blend * np.pi),
                   mid * np.cos((blend - .5) * np.pi) + high * np.sin((blend - .5) * np.pi))
    rev *= np.sin(np.pi * t) ** .8
    contact_pitch = {"uk": .93, "de": 1.08, "it": 1.12, "us": .86, "cn": 1.04, "jp": 1.13}[team]
    # All impacts are recorded dial/latch samples, never noise oscillators.
    contact = play_loop(mechanical, min(count, round(RATE * (.17 if team in ("de", "cn", "jp") else .22))), contact_pitch)
    contact = fade_event(contact)
    contact = normalized(contact, -23., -14.)
    result = rev * (.42 if team == "cn" else .65)
    result[:len(contact)] += contact
    if team == "uk":
        delay = round(RATE * .085)
        length = min(len(contact), len(result) - delay)
        result[delay:delay + length] += contact[:length] * .32
    return fade_event(result)

def meta(path: Path, project: Path) -> None:
    target = Path(str(path) + ".meta")
    if target.exists():
        return
    guid = uuid.uuid5(uuid.NAMESPACE_URL, "foodula1-team-audio:" + path.relative_to(project).as_posix()).hex
    target.write_text(f"fileFormatVersion: 2\nguid: {guid}\nAudioImporter:\n"
        "  externalObjects: {}\n  serializedVersion: 7\n  defaultSettings:\n"
        "    serializedVersion: 2\n    loadType: 0\n    sampleRateSetting: 0\n"
        "    sampleRateOverride: 48000\n    compressionFormat: 0\n    quality: 1\n"
        "    conversionMode: 0\n    preloadAudioData: 1\n  platformSettingOverrides: {}\n"
        "  forceToMono: 1\n  normalize: 0\n  loadInBackground: 0\n  ambisonic: 0\n  3D: 0\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")

def write_audio(path: Path, values: np.ndarray, project: Path, looping: bool, peak_db=-10.) -> dict:
    path.parent.mkdir(parents=True, exist_ok=True)
    values = normalized(values, -21. if looping else -20., peak_db)
    pcm = np.round(np.clip(values, -1, 1) * 32767).astype("<i2")
    if looping:
        pcm[-1] = pcm[0]
    else:
        pcm[0] = pcm[-1] = 0
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(pcm.tobytes())
    if path.is_relative_to(project / "Assets"):
        meta(path, project)
    floating = pcm.astype(float) / 32767
    return {"path": path.relative_to(project).as_posix(), "frames": len(pcm),
            "duration_seconds": len(pcm) / RATE,
            "peak_dbfs": round(20 * math.log10(max(abs(floating).max(), 1e-12)), 4),
            "rms_dbfs": round(20 * math.log10(max(float(np.sqrt(np.mean(floating ** 2))), 1e-12)), 4),
            "looping": looping, "boundary_sample_delta": abs(int(pcm[-1]) - int(pcm[0])),
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}

def build(project: Path, source_dir: Path) -> None:
    audio = project / "Assets/Resources/Audio"
    mechanical_path = audio / "SFX/gear_shift.wav"
    mechanical_rate, mechanical = read_pcm(mechanical_path)
    mechanical = resample(mechanical, mechanical_rate)
    profiles, sources, outputs, comparison = [], [], [], []
    for team, (filename, identity, ramp, gain, hp, lp) in TEAMS.items():
        original_path = source_dir / filename
        source_rate, values = read_pcm(original_path)
        values = gentle_eq(resample(values, source_rate), hp, lp)
        windows = rpm_windows(values, team == "cn")
        layers = [normalized(seam_loop(values[w["start"]:w["start"] + round(RATE * 1.18)]), -21., -10.) for w in windows]
        paths = [f"Audio/Engines/team_engine_{team}", f"Audio/Engines/team_engine_{team}_mid", f"Audio/Engines/team_engine_{team}_high"]
        for index, (path, layer) in enumerate(zip(paths, layers)):
            record = write_audio(project / "Assets/Resources" / (path + ".wav"), layer, project, True)
            record["source_file"] = filename
            record["source_start_seconds"] = round(windows[index]["start"] / RATE, 4)
            record["source_tonal_peak_hz"] = round(windows[index]["tone_hz"], 2)
            outputs.append(record)
        shifts = []
        for up, direction in ((True, "up"), (False, "down")):
            cue = shift_cue(team, layers, mechanical, up)
            outputs.append(write_audio(audio / "SFX" / f"team_shift_{team}_{direction}.wav", cue, project, False, -8.))
            shifts.append(normalized(cue, -20., -8.))
        profiles.append({"team": team.upper(), "soundIdentity": identity,
            "engineResourcePath": paths[0], "midEngineResourcePath": paths[1], "highEngineResourcePath": paths[2],
            "upShiftResourcePath": f"Audio/SFX/team_shift_{team}_up",
            "downShiftResourcePath": f"Audio/SFX/team_shift_{team}_down",
            "lowPitch": REV_PITCH[team][0], "highPitch": REV_PITCH[team][1], "cellsToMax": ramp,
            "engineGain": gain, "shiftGain": .65, "pitchResponse": 7, "fadeOutSeconds": .09})
        sources.append({"team": team.upper(), "file": filename,
            "sha256": hashlib.sha256(original_path.read_bytes()).hexdigest(),
            "source_page": MOTOR_PAGE if team == "cn" else PACK_PAGE,
            "download": MOTOR_URL if team == "cn" else PACK_URL,
            "author": "Joseph Sardin" if team == "cn" else "Sebastien / muted.io",
            "license": "CC0-1.0", "brand_certified": False,
            "source_sample_rate": source_rate, "eq_highpass_hz": hp, "eq_lowpass_hz": lp})
        count = round(RATE * 2.5)
        intensity = np.linspace(0, 1, count)
        angle = np.where(intensity < .5, intensity * np.pi, (intensity - .5) * np.pi)
        weights = np.vstack((np.where(intensity < .5, np.cos(angle), 0),
                             np.where(intensity < .5, np.sin(angle), np.cos(angle)),
                             np.where(intensity < .5, 0, np.sin(angle))))
        low_pitch, high_pitch = REV_PITCH[team]
        ramp_sound = sum(play_loop(layer, count, low_pitch + (high_pitch - low_pitch) * intensity) * weight
                         for layer, weight in zip(layers, weights)) * gain
        comparison.extend([fade_event(ramp_sound), np.zeros(round(RATE * .15)),
                           shifts[0] * .65, np.zeros(round(RATE * .1)), shifts[1] * .65, np.zeros(round(RATE * .5))])
    document = {"schemaVersion": 2, "profiles": profiles, "generation": {
        "source": "Original real field recordings with excerpt selection, gentle EQ and seam overlap; no synthesized engine or mechanical content",
        "script": "tools/Audio/build_team_vehicle_audio.py",
        "format": {"sample_rate": RATE, "channels": 1, "sample_width_bits": 16},
        "sources": sources, "shift_contact": {
            "file": "Assets/Resources/Audio/SFX/gear_shift.wav",
            "sha256": hashlib.sha256(mechanical_path.read_bytes()).hexdigest(),
            "source": "Sonniss GDC 2026: 344 Audio Car Foley dial + Epic Stock Media mechanism latch",
            "license_record": "docs/audio/gdc-2026-sfx-provenance.md"},
        "loop_contract": "55ms recorded tail/head overlap, matching first/last PCM samples",
        "outputs": outputs}}
    (audio / "team_vehicle_profiles.json").write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    review = write_audio(project / "tools/Audio/team_vehicle_comparison.wav", np.concatenate(comparison), project, False, -8.)
    print(json.dumps({"runtime_assets": len(outputs), "recorded_loops": 18, "recorded_shifts": 12,
        "maximum_peak_dbfs": max(o["peak_dbfs"] for o in outputs),
        "equal_loop_boundaries": all(o["boundary_sample_delta"] == 0 for o in outputs if o["looping"]),
        "review": review, "review_order": list(TEAMS)}, indent=2))

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--source-dir", type=Path, default=Path(tempfile.gettempdir()) / "foodula1_real_vehicle_audio")
    parser.add_argument("--fetch-sources", action="store_true")
    args = parser.parse_args()
    if args.fetch_sources:
        fetch_sources(args.source_dir)
    build(args.project.resolve(), args.source_dir.resolve())
