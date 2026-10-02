import csv
import math
import statistics
import unicodedata


def normalize(text):
    return " ".join(unicodedata.normalize("NFC", text).casefold().split())


def cer(expected, actual):
    a, b = normalize(expected), normalize(actual)
    if not a:
        return 0.0 if not b else 1.0
    previous = list(range(len(b) + 1))
    for i, x in enumerate(a, 1):
        row = [i]
        for j, y in enumerate(b, 1):
            row.append(min(row[-1] + 1, previous[j] + 1, previous[j - 1] + (x != y)))
        previous = row
    return previous[-1] / len(a)


def percentile(values, fraction):
    if not values:
        return None
    ordered = sorted(values)
    position = (len(ordered) - 1) * fraction
    lo, hi = math.floor(position), math.ceil(position)
    return ordered[lo] + (ordered[hi] - ordered[lo]) * (position - lo)


def frame_stats(path, column, process_id, swapchain):
    values, streams, invalid = [], set(), 0
    with open(path, encoding="utf-8-sig", newline="") as stream:
        rows = csv.DictReader(stream)
        for row in rows:
            fields = {key.lower(): value for key, value in row.items() if key}
            if "processid" not in fields or "swapchainaddress" not in fields:
                raise ValueError("CSV must identify ProcessID and SwapChainAddress; do not mix rendering streams.")
            if fields["processid"] != str(process_id):
                continue
            if swapchain and fields["swapchainaddress"] != swapchain:
                continue
            streams.add(fields["swapchainaddress"])
            try:
                value = float(fields[column.lower()])
                if not math.isfinite(value) or value <= 0:
                    raise ValueError()
                values.append(value)
            except (ValueError, KeyError):
                invalid += 1
    if len(streams) != 1 or len(values) < 100 or invalid > max(1, len(values) * .01):
        raise ValueError("Select one swapchain with >=100 valid frames and <=1% invalid rows.")
    slowest = sorted(values, reverse=True)[:max(1, math.ceil(len(values) * .01))]
    return {
        "frames": len(values), "duration_seconds": sum(values) / 1000,
        "average_fps": 1000 / statistics.mean(values),
        "one_percent_low_fps": 1000 / statistics.mean(slowest),
        "p95_frame_ms": percentile(values, .95), "p99_frame_ms": percentile(values, .99),
        "invalid_rows": invalid, "swapchain": next(iter(streams)), "frame_metric": column,
        "definition": "1% low = 1000 / mean(slowest ceil(1% N) frame intervals). CPU/present interval; not necessarily displayed FPS."
    }


def compare_pairs(manifest, base_dir):
    pairs = []
    for pair in manifest["pairs"]:
        baseline = frame_stats(base_dir / pair["baseline"], manifest["column"], pair["baseline_pid"], pair.get("baseline_swapchain"))
        treatment = frame_stats(base_dir / pair["treatment"], manifest["column"], pair["treatment_pid"], pair.get("treatment_swapchain"))
        pairs.append({"id": pair["id"], "baseline": baseline, "treatment": treatment,
            "fps_drop_percent": (1 - treatment["average_fps"] / baseline["average_fps"]) * 100,
            "low_drop_percent": (1 - treatment["one_percent_low_fps"] / baseline["one_percent_low_fps"]) * 100})
    baselines = [p["baseline"]["average_fps"] for p in pairs]
    noise = (max(baselines) - min(baselines)) / statistics.mean(baselines) * 100 if baselines else None
    complete = len(pairs) >= 3 and all(min(p["baseline"]["duration_seconds"], p["treatment"]["duration_seconds"]) >= 120 for p in pairs)
    within = bool(pairs) and all(p["fps_drop_percent"] <= 2 and p["low_drop_percent"] <= 3 for p in pairs)
    return {"pairs": pairs, "baseline_fps_spread_percent": noise,
        "status": "insufficient-data" if not complete else "inconclusive-scene-variance" if noise > 2 else "within-proposed-limits" if within else "performance-failed",
        "model_selection": "not-decided; human quality, memory headroom and reproducibility review required"}
