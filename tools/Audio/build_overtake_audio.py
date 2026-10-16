"""Build a short overtake cue from Joseph Sardin's CC0 roadside car recording.

Standard library only; no downloads, generated noise, pitch shifting, or Assets writes.
"""
import array
import hashlib
import json
import math
from pathlib import Path
import sys
import wave

ROOT = Path(__file__).resolve().parents[2]
STAGING = ROOT / 'tools/Audio/overtake-staging'
SOURCE = STAGING / 'source/bigsoundbank_0556_bwf.wav'
EXPECTED = '6afee85df5e2f3c417a7e743fc14006ea3a0a1fdfb0b74ba01ee3ce8db8b5703'
RATE = 48000


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build():
    assert digest(SOURCE) == EXPECTED, 'Source hash mismatch'
    with wave.open(str(SOURCE), 'rb') as src:
        assert (src.getnchannels(), src.getsampwidth(), src.getframerate()) == (2, 2, RATE)
        source_frames = src.getnframes()
        start = 5.0
        duration = .76
        src.setpos(round(start * RATE))
        pcm = array.array('h', src.readframes(round(duration * RATE)))
        if sys.byteorder != 'little':
            pcm.byteswap()
    mono = [(pcm[i] + pcm[i + 1]) / 65536 for i in range(0, len(pcm), 2)]
    # A gentle one-pole rumble cut and low pass retain recorded engine/air texture.
    hp_a = math.exp(-2 * math.pi * 70 / RATE)
    lp_a = 1 - math.exp(-2 * math.pi * 6500 / RATE)
    previous_input = previous_hp = low = 0.0
    filtered = []
    for i, value in enumerate(mono):
        high = hp_a * (previous_hp + value - previous_input)
        previous_input, previous_hp = value, high
        low += lp_a * (high - low)
        edge = min(1, i / (.035 * RATE), (len(mono) - 1 - i) / (.100 * RATE))
        filtered.append(low * (.5 - .5 * math.cos(math.pi * edge)))
    rms = math.sqrt(sum(x*x for x in filtered) / len(filtered))
    peak = max(map(abs, filtered))
    gain = min(10**(-22/20) / rms, 10**(-9/20) / peak)
    result = [round(x * gain * 32767) for x in filtered]
    assert result[0] == result[-1] == 0
    assert max(map(abs, result)) < 32767
    output = STAGING / 'overtake_cc0.wav'
    data = array.array('h', result)
    if sys.byteorder != 'little':
        data.byteswap()
    with wave.open(str(output), 'wb') as dst:
        dst.setnchannels(1)
        dst.setsampwidth(2)
        dst.setframerate(RATE)
        dst.writeframes(data.tobytes())
    # A/B reference: first car, 4.0-7.0 seconds, then 0.5s silence and cue.
    # Preserve the reference stereo waveform; only attenuate to the same peak ceiling.
    with wave.open(str(SOURCE), 'rb') as src:
        src.setpos(4 * RATE)
        reference = array.array('h', src.readframes(3 * RATE))
        if sys.byteorder != 'little':
            reference.byteswap()
    ref_gain = min(1, 32767 * 10**(-9/20) / max(map(abs, reference)))
    comparison = array.array('h', [round(x * ref_gain) for x in reference])
    comparison.extend([0] * RATE)
    comparison.extend(x for sample in result for x in (sample, sample))
    if sys.byteorder != 'little':
        comparison.byteswap()
    review = STAGING / 'overtake_source_then_cue.wav'
    with wave.open(str(review), 'wb') as dst:
        dst.setnchannels(2)
        dst.setsampwidth(2)
        dst.setframerate(RATE)
        dst.writeframes(comparison.tobytes())
    envelope = []
    window = RATE // 10 * 2
    for i in range(30):
        values = reference[i*window:(i+1)*window]
        power = math.sqrt(sum(x*x for x in values) / len(values)) / 32768
        envelope.append({'source_start_seconds': round(4+i*.1, 1), 'rms_dbfs': 20*math.log10(max(power, 1e-12))})
    manifest = {
        'source_page': 'https://bigsoundbank.com/cars-passages-s0556.html',
        'download_url': 'https://bigsoundbank.com/UPLOAD/bwf-en/0556.wav',
        'download_date': '2026-10-15', 'author': 'Joseph SARDIN', 'license': 'CC0-1.0',
        'license_url': 'https://creativecommons.org/publicdomain/zero/1.0/legalcode.en',
        'source': str(SOURCE.relative_to(ROOT)).replace('\\', '/'),
        'source_sha256': EXPECTED, 'source_frames': source_frames,
        'source_duration_seconds': source_frames / RATE,
        'source_format': '48000 Hz / 16-bit PCM / stereo BWF',
        'excerpt_start_seconds': start, 'excerpt_end_seconds': start + duration,
        'processing': 'L+R mean; one-pole HP70Hz LP6500Hz; cosine fade in35ms out100ms; RMS target -22 dBFS capped peak -9 dBFS; no time/pitch shift or synthetic layers',
        'output': str(output.relative_to(ROOT)).replace('\\', '/'),
        'output_sha256': digest(output), 'output_format': '48000 Hz / 16-bit PCM / mono',
        'frames': len(result), 'duration_seconds': len(result)/RATE,
        'peak_dbfs': 20*math.log10(max(map(abs, result))/32768),
        'rms_dbfs': 20*math.log10(math.sqrt(sum(x*x for x in result)/len(result))/32768),
        'full_scale_samples': sum(abs(x) >= 32767 for x in result),
        'first_sample': result[0], 'last_sample': result[-1],
        'subjective_listening': 'Pending; objective PCM checks do not establish perceptual quality',
        'excerpt_selection': 'First of six passages; 100ms RMS maximum 5.4-5.5s (-11.92dBFS); crop includes approach rise 5.0s (-16.26), apex and initial departure 5.7s (-15.66), then designed fade. Does not retain entire distant approach/recession.',
        'source_100ms_envelope_4_to_7_seconds': envelope,
        'review': {'path': str(review.relative_to(ROOT)).replace('\\', '/'), 'sha256': digest(review),
                   'duration_seconds': 4.26, 'layout': '0-3s: source4-7s stereo peak-attenuated; 3-3.5s silence; 3.5-4.26s mono cue duplicated L/R',
                   'reference_gain': ref_gain},
    }
    (STAGING / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(manifest, indent=2))


if __name__ == '__main__':
    build()
