"""Offline corpus runner. It does not capture screens or implement Watch Mode."""
import argparse
import base64
import csv
import hashlib
import io
import json
import random
import statistics
import time
from datetime import datetime, timezone
from pathlib import Path
from PIL import Image
from provider import Ollama, Request
from resources import Resources
from metrics import cer, normalize, percentile, compare_pairs
from fixtures import generate

EVENTS = ["dialogue", "quest_update", "death_screen", "pause_menu", "unknown"]
SCHEMA = {"type": "object", "properties": {"event": {"type": "string", "enum": EVENTS},
    "text": {"type": "string"}, "uncertain": {"type": "boolean"}},
    "required": ["event", "text", "uncertain"], "additionalProperties": False}
CONFIG = json.loads(Path(__file__).with_name("config.json").read_text(encoding="utf-8"))


def write_json(path, value):
    Path(path).write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")


def load_corpus(path):
    path = Path(path).resolve()
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    ids = set()
    if data.get("schema_version") != 1 or not data.get("cases"):
        raise ValueError("Expected non-empty schema_version 1 corpus.")
    for case in data["cases"]:
        if case["id"] in ids or case["source"] not in ("synthetic", "gameplay") or case["split"] not in ("development", "holdout"):
            raise ValueError("Invalid identity/source/split.")
        ids.add(case["id"])
        image_path = (path.parent / case["image"]).resolve()
        if not image_path.is_relative_to(path.parent):
            raise ValueError("Corpus image must be inside corpus directory.")
        with Image.open(image_path) as image:
            if image.width * image.height > 20_000_000:
                raise ValueError("Image exceeds corpus limit.")
            image.verify()
        if case["expected"]["event"] not in EVENTS or type(case["expected"]["uncertain"]) is not bool:
            raise ValueError("Invalid ground truth.")
        if len(case["expected"].get("transcript", "")) > 10000:
            raise ValueError("Reference transcript too long.")
    return path, data


def prepare(case, root, mode):
    budget = CONFIG["budgets"][mode]
    with Image.open(root / case["image"]) as image:
        image = image.convert("RGB")
        crop = case.get("watch_crop" if mode == "watch_probe" else "ask_crop")
        if crop is not None:
            if len(crop) != 4 or not (0 <= crop[0] < crop[2] <= image.width and 0 <= crop[1] < crop[3] <= image.height):
                raise ValueError("Invalid crop.")
            image = image.crop(crop)
        image.thumbnail((budget["max_image_edge"], budget["max_image_edge"]), Image.Resampling.LANCZOS)
        buffer = io.BytesIO(); image.save(buffer, format="PNG")
        raw = buffer.getvalue()
        dimensions = [image.width, image.height]
    context = case.get("context", "")
    if len(context) > budget["max_context_chars"]:
        raise ValueError("Context exceeds selected budget; curate it, do not silently truncate facts.")
    prompt = case["question"] if mode == "ask" else (
        "Classifique apenas o evento visível como dialogue, quest_update, death_screen, pause_menu ou unknown. "
        "Transcreva o texto legível na ordem visual no campo text; use uncertain=true para informação ambígua ou ilegível. "
        "Responda somente no esquema JSON fornecido. Não deduza morte apenas de vida zero.")
    return Request(prompt, base64.b64encode(raw).decode(), context, budget["max_output_tokens"], budget["timeout_seconds"], SCHEMA if mode == "watch_probe" else None), {
        "image_sha256": hashlib.sha256(raw).hexdigest(), "dimensions": dimensions, "crop": crop, "budget": budget}


def score(case, mode, text):
    truth = case["expected"]
    if mode == "ask":
        terms = truth.get("ask_lexical_terms", [])
        return {"lexical_proxy_fraction": sum(normalize(t) in normalize(text) for t in terms) / len(terms) if terms else None,
                "human_quality": None, "critical_claim_review": "pending"}
    try:
        value = json.loads(text)
        valid = isinstance(value, dict) and set(value) == {"event", "text", "uncertain"} and value["event"] in EVENTS and isinstance(value["text"], str) and type(value["uncertain"]) is bool
        if not valid or len(value["text"]) > 10000:
            raise ValueError()
        return {"structured_valid": True, "predicted_event": value["event"], "expected_event": truth["event"],
                "event_correct": value["event"] == truth["event"], "uncertainty_correct": value["uncertain"] == truth["uncertain"],
                "cer": cer(truth["transcript"], value["text"]) if truth.get("transcript") else None,
                "dialogue_exact": normalize(truth["transcript"]) == normalize(value["text"]) if truth.get("transcript") else None}
    except (ValueError, TypeError, KeyError):
        return {"structured_valid": False, "event_correct": False, "expected_event": truth["event"], "predicted_event": "invalid", "cer": None}


def summarize(rows):
    summaries = {}
    for mode in ("watch_probe", "ask"):
        group = [r for r in rows if r["mode"] == mode]
        if not group:
            continue
        ok = [r for r in group if "error" not in r]
        latencies = [r["end_to_end_seconds"] for r in ok]
        summary = {"attempts": len(group), "completed": len(ok), "failures": len(group) - len(ok),
                   "p50_seconds": percentile(latencies, .5), "p95_seconds": percentile(latencies, .95),
                   "latency_excludes_failed_requests": True, "ask_human_quality": None}
        if mode == "watch_probe":
            summary["structured_valid_fraction"] = sum(r.get("score", {}).get("structured_valid", False) for r in group) / len(group)
            f1s = []
            for label in sorted({r["expected_event"] for r in group}):
                tp = sum(r["expected_event"] == label and r.get("score", {}).get("predicted_event") == label for r in group)
                fp = sum(r["expected_event"] != label and r.get("score", {}).get("predicted_event") == label for r in group)
                fn = sum(r["expected_event"] == label and r.get("score", {}).get("predicted_event") != label for r in group)
                f1s.append(2 * tp / (2 * tp + fp + fn))
            summary["event_macro_f1"] = statistics.mean(f1s)
            values = [r["score"]["cer"] for r in ok if r["score"].get("cer") is not None]
            summary["mean_cer_valid_outputs_only"] = statistics.mean(values) if values else None
        summaries[mode] = summary
    return summaries


def run(args):
    path, corpus = load_corpus(args.corpus)
    output = Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=False)
    provider = Ollama(args.endpoint, args.model, CONFIG["runtime_context_tokens"])
    rows = []
    monitor = Resources(args.runtime_pid)
    metadata = {"created_at": datetime.now(timezone.utc).isoformat(), "model": args.model,
        "config": CONFIG, "corpus_sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        "corpus_id": corpus.get("dataset"), "repetitions": args.repetitions,
        "model_selection": "not-decided", "human_review": "pending", "game_impact": "not-measured",
        "synthetic_cases": sum(c["source"] == "synthetic" for c in corpus["cases"]),
        "real_cases": sum(c["source"] == "gameplay" for c in corpus["cases"]),
        "holdout_cases": sum(c["split"] == "holdout" for c in corpus["cases"]),
        "residency_policy": "one model, one request at a time, fixed allocated context across modes; keep_alive=-1; no unload/swap/download"}
    failure = None
    try:
        metadata["runtime_version"] = provider.api("/api/version")
        installed = provider.api("/api/tags").get("models", [])
        if not any(m.get("name") == args.model and m.get("size", 0) > 100_000_000 for m in installed):
            raise RuntimeError("Candidate local weights are not installed. No automatic download.")
        details = provider.api("/api/show", {"model": args.model})
        if "cloud" in args.model.lower() or details.get("remote_host") or details.get("remote_model") or "vision" not in details.get("capabilities", []):
            raise RuntimeError("Candidate must be a local vision model; remote/cloud models are not allowed.")
        metadata["model_details"] = {k: details.get(k) for k in ("details", "capabilities", "model_info")}
        metadata["resident_before"] = provider.residency()
        monitor.start()
        modes = ["watch_probe", "ask"] if args.mode == "both" else [args.mode]
        warm_request, _ = prepare(corpus["cases"][0], path.parent, modes[0])
        warm = provider.infer(warm_request)
        metadata["warmup"] = {"seconds": warm.seconds, "metrics": warm.transport_metrics, "excluded_from_quality": True}
        resident = provider.residency()
        if len(resident) != 1:
            raise RuntimeError("Warmup did not leave exactly one model resident.")
        metadata["resident_after_warmup"] = resident
        jobs = [(case, mode, repeat) for repeat in range(args.repetitions) for case in corpus["cases"] for mode in modes]
        random.Random(42).shuffle(jobs)
        for case, mode, repeat in jobs:
            row = {"case": case["id"], "mode": mode, "repeat": repeat, "source": case["source"], "split": case["split"],
                   "category": case["category"], "expected_event": case["expected"]["event"], "started_at": datetime.now(timezone.utc).isoformat()}
            started = time.perf_counter()
            try:
                request, details = prepare(case, path.parent, mode)
                response = provider.infer(request)
                row.update(details, answer=response.text, inference_seconds=response.seconds, end_to_end_seconds=time.perf_counter() - started,
                           metrics=response.transport_metrics, score=score(case, mode, response.text))
                resident_now = provider.residency()
                if len(resident_now) != 1 or resident_now[0].get("digest") != resident[0].get("digest"):
                    raise RuntimeError("Model residency changed during run.")
                row["runtime_reported_resident"] = resident_now
            except Exception as error:
                row.update(error=type(error).__name__ + ": " + str(error), elapsed_before_failure=time.perf_counter() - started)
                rows.append(row)
                # In particular, a client timeout may leave work on the server: never enqueue the next request.
                raise
            rows.append(row)
            with (output / "responses.jsonl").open("a", encoding="utf-8") as stream:
                stream.write(json.dumps(row, ensure_ascii=False) + "\n")
            time.sleep(args.interval)
    except KeyboardInterrupt:
        failure = "interrupted-by-user"
    except Exception as error:
        failure = type(error).__name__ + ": " + str(error)
    finally:
        resources = monitor.finish() if monitor.thread.is_alive() else {"samples": [], "status": "runtime-not-started"}
        resources["sampled_peaks"] = {key: max((s[key] for s in resources["samples"] if s.get(key) is not None), default=None)
            for key in ("runtime_uss_bytes", "gpu_dedicated_bytes", "gpu_shared_bytes")}
        write_json(output / "resources.json", resources)
        write_json(output / "results.json", {"metadata": metadata, "status": "aborted" if failure else "completed-not-qualified", "failure": failure, "summary": summarize(rows), "rows": rows})
        with (output / "human-review.csv").open("w", encoding="utf-8-sig", newline="") as stream:
            writer = csv.writer(stream)
            writer.writerow(["case", "mode", "repeat", "visual_fidelity_0_4", "reading_0_4", "usefulness_0_4", "portuguese_0_4", "uncertainty_0_4", "unsupported_critical_claim", "notes"])
            for row in rows:
                writer.writerow([row["case"], row["mode"], row["repeat"], "", "", "", "", "", "", ""])
    if failure:
        raise RuntimeError(failure)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    fixtures = commands.add_parser("fixtures"); fixtures.add_argument("output")
    validate = commands.add_parser("validate"); validate.add_argument("corpus")
    benchmark = commands.add_parser("run"); benchmark.add_argument("corpus"); benchmark.add_argument("output")
    benchmark.add_argument("--model", default=CONFIG["first_candidate"])
    benchmark.add_argument("--endpoint", default="http://127.0.0.1:11434")
    benchmark.add_argument("--mode", choices=["both", "ask", "watch_probe"], default="both")
    benchmark.add_argument("--repetitions", type=int, choices=range(1, 11), default=3)
    benchmark.add_argument("--interval", type=float, default=2)
    benchmark.add_argument("--runtime-pid", type=int)
    compare = commands.add_parser("compare-game"); compare.add_argument("manifest"); compare.add_argument("output")
    monitor = commands.add_parser("monitor"); monitor.add_argument("output"); monitor.add_argument("--runtime-pid", type=int)
    monitor.add_argument("--seconds", type=int, choices=range(1, 1801), default=180)
    args = parser.parse_args()
    if args.command == "fixtures":
        print(generate(args.output))
    elif args.command == "validate":
        _, data = load_corpus(args.corpus)
        print(json.dumps({"valid": True, "cases": len(data["cases"]), "selection": "not-decided"}))
    elif args.command == "run":
        if not 0 <= args.interval <= 60:
            parser.error("interval must be 0..60 seconds")
        run(args)
    elif args.command == "compare-game":
        path = Path(args.manifest).resolve()
        write_json(args.output, compare_pairs(json.loads(path.read_text(encoding="utf-8-sig")), path.parent))
    elif args.command == "monitor":
        monitor = Resources(args.runtime_pid); monitor.start()
        try:
            time.sleep(args.seconds)
        finally:
            write_json(args.output, monitor.finish())


if __name__ == "__main__":
    main()
