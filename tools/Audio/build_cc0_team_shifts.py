"""Reproducible CC0-only shift cues from the 18 checked-in engine recording layers.

Standard library only. Writes staging output, never Unity Assets or old shift files.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import struct
import wave

ROOT = Path(__file__).resolve().parents[2]
TEAMS = {"uk": .22, "de": .18, "it": .20, "us": .24, "cn": .16, "jp": .18}
LICENSE = "https://creativecommons.org/publicdomain/zero/1.0/"


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_layer(path):
    with wave.open(str(path), "rb") as source:
        assert (source.getnchannels(), source.getsampwidth(), source.getframerate()) == (1, 2, 48000), path
        raw = source.readframes(source.getnframes())
    return [x[0] / 32768.0 for x in struct.iter_unpack("<h", raw)]


def interpolate(layer, position):
    start = int(position) % len(layer)
    fraction = position - int(position)
    return layer[start] * (1 - fraction) + layer[(start + 1) % len(layer)] * fraction


def db(value):
    return 20 * math.log10(max(value, 1e-12))


def build(output):
    output = output.resolve()
    if output == ROOT / "Assets" or (ROOT / "Assets") in output.parents:
        raise ValueError("This tool writes staging only; integration is a separate reviewed action")
    source_manifest = json.loads((ROOT / "Assets/Resources/Audio/team_vehicle_profiles.json").read_text(encoding="utf-8-sig"))
    expected = {entry["path"]: entry["sha256"] for entry in source_manifest["generation"]["outputs"]}
    origins = {entry["team"].lower(): entry for entry in source_manifest["generation"]["sources"]}
    layers = {}
    input_rows = []
    for team in TEAMS:
        origin = origins[team]
        assert origin["license"] == "CC0-1.0", team
        for suffix in ("", "_mid", "_high"):
            relative = "Assets/Resources/Audio/Engines/team_engine_" + team + suffix + ".wav"
            path = ROOT / relative
            digest = sha256(path)
            assert digest == expected[relative], "Existing CC0 engine layer hash drift: " + relative
            layers[(team, suffix)] = read_layer(path)
            input_rows.append({"path": relative, "sha256": digest, "license": "CC0-1.0",
                               "license_url": LICENSE, "source_page": origin["source_page"],
                               "original_download": origin["download"], "author": origin["author"],
                               "original_recording": origin["file"], "original_sha256": origin["sha256"]})
    output.mkdir(parents=True, exist_ok=True)
    generated = []
    for team_index, (team, duration) in enumerate(TEAMS.items()):
        for direction in ("up", "down"):
            if team == "cn":
                pair = ("", "_mid") if direction == "up" else ("_mid", "")
                speeds = (.88, 1.20) if direction == "up" else (1.10, .80)
            else:
                pair = ("_high", "_mid") if direction == "up" else ("_mid", "_high")
                speeds = (1.12, .83) if direction == "up" else (.88, 1.18)
            count = round(duration * 48000)
            phase = 48000 * (.04 + team_index * .017)
            start_phase = phase
            samples = []
            for index in range(count):
                t = index / (count - 1)
                blend = .5 - .5 * math.cos(math.pi * t)
                sample = ((1 - blend) * interpolate(layers[(team, pair[0])], phase)
                          + blend * interpolate(layers[(team, pair[1])], phase))
                edge = min(1.0, index / (48000 * .018), (count - 1 - index) / (48000 * .022))
                fade = .5 - .5 * math.cos(math.pi * edge)
                samples.append(sample * fade)
                phase += speeds[0] + (speeds[1] - speeds[0]) * blend
            peak = max(abs(x) for x in samples)
            rms = math.sqrt(sum(x * x for x in samples) / count)
            gain = min(10 ** (-22 / 20) / max(rms, 1e-12), 10 ** (-9 / 20) / max(peak, 1e-12))
            pcm = [max(-32767, min(32767, round(x * gain * 32767))) for x in samples]
            name = "team_shift_cc0_" + team + "_" + direction + ".wav"
            path = output / name
            with wave.open(str(path), "wb") as destination:
                destination.setnchannels(1)
                destination.setsampwidth(2)
                destination.setframerate(48000)
                destination.writeframes(struct.pack("<" + "h" * count, *pcm))
            peak = max(abs(x) for x in pcm) / 32768
            rms = math.sqrt(sum(x * x for x in pcm) / count) / 32768
            assert pcm[0] == 0 and pcm[-1] == 0
            assert peak < .36 and rms > .01
            generated.append({"file": name, "resource_path": "Audio/SFX/" + path.stem,
                              "team": team.upper(), "direction": direction, "sha256": sha256(path),
                              "frames": count, "duration_seconds": duration, "sample_rate": 48000,
                              "sample_width_bits": 16, "channels": 1, "peak_dbfs": round(db(peak), 6),
                              "rms_dbfs": round(db(rms), 6), "first_sample": pcm[0], "last_sample": pcm[-1],
                              "clipped_samples": sum(abs(x) >= 32767 for x in pcm),
                              "maximum_adjacent_delta": max(abs(a - b) for a, b in zip(pcm, pcm[1:])) / 32768,
                              "source_layers": ["Assets/Resources/Audio/Engines/team_engine_" + team + x + ".wav" for x in pair],
                              "source_phase_frames": start_phase, "resample_rate_start_end": list(speeds),
                              "processing": "Linear resampling + cosine layer crossfade; 18ms/22ms cosine edge fades; RMS -22dBFS with -9dBFS peak ceiling",
                              "license": "CC0-1.0", "license_url": LICENSE,
                              "source_page": origins[team]["source_page"], "author": origins[team]["author"]})
    assert len(generated) == 12 and len({entry["sha256"] for entry in generated}) == 12
    manifest = {"schema_version": 1, "script": "tools/Audio/build_cc0_team_shifts.py",
                "generation_date": "2026-10-15", "source_policy": "Only the 18 hash-verified CC0 engine layers; no mechanical/old shift/Sonniss input",
                "source_retrieval": "Reuses existing local derivatives; no new download in this build",
                "inputs": input_rows, "outputs": generated}
    (output / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"outputs": len(generated), "inputs_verified": len(input_rows),
                      "manifest": str(output / "manifest.json"),
                      "peak_dbfs_range": [min(x["peak_dbfs"] for x in generated), max(x["peak_dbfs"] for x in generated)],
                      "rms_dbfs_range": [min(x["rms_dbfs"] for x in generated), max(x["rms_dbfs"] for x in generated)]}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "tools/Audio/cc0-shift-staging")
    build(parser.parse_args().output)
