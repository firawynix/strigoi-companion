import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
from types import SimpleNamespace
import bench
from fixtures import generate
from metrics import cer, frame_stats, compare_pairs
from provider import Ollama, Response


class BenchmarkTests(unittest.TestCase):
    def test_cer_and_unicode(self):
        self.assertEqual(cer("Não abra", "NÃO   abra"), 0)
        self.assertGreater(cer("Não abra", "Abra"), .1)

    def test_ground_truth_not_in_request(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = generate(tmp)
            root, corpus = bench.load_corpus(path)
            case = corpus["cases"][0]
            case["expected"]["transcript"] = "SECRET ANSWER"
            watch, wm = bench.prepare(case, root.parent, "watch_probe")
            ask, am = bench.prepare(case, root.parent, "ask")
            self.assertNotIn("SECRET ANSWER", watch.prompt + ask.prompt)
            self.assertLessEqual(max(wm["dimensions"]), 512)
            self.assertLessEqual(max(am["dimensions"]), 1280)
            self.assertIsNotNone(watch.schema)
            self.assertIsNone(ask.schema)

    def test_invalid_structure_fails(self):
        case = {"expected": {"event": "unknown", "uncertain": True}}
        self.assertFalse(bench.score(case, "watch_probe", '{"event":"unknown","text":"","uncertain": "false"}')["structured_valid"])
        self.assertFalse(bench.score(case, "watch_probe", "```json\n{}\n```")["structured_valid"])

    def test_ask_never_auto_approved(self):
        result = bench.score({"expected": {"ask_lexical_terms": ["esperar"]}}, "ask", "Você deve esperar")
        self.assertEqual(result["lexical_proxy_fraction"], 1)
        self.assertIsNone(result["human_quality"])

    def test_failed_event_reduces_f1(self):
        rows = [{"mode": "watch_probe", "expected_event": "dialogue", "error": "timeout"}]
        self.assertEqual(bench.summarize(rows)["watch_probe"]["event_macro_f1"], 0)
        self.assertIsNone(bench.summarize(rows)["watch_probe"]["p95_seconds"])

    def test_local_only(self):
        for endpoint in ("https://example.com", "http://192.168.0.2:11434", "http://localhost/redirect"):
            with self.assertRaises(ValueError):
                Ollama(endpoint, "candidate", 8192)

    def test_one_resident(self):
        provider = Ollama("http://127.0.0.1:11434", "candidate", 8192)
        provider.api = lambda *args: {"models": [{"name": "other"}]}
        with self.assertRaises(RuntimeError):
            provider.residency()

    def test_fixed_context_same_model_across_modes(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = generate(tmp)
            root, corpus = bench.load_corpus(path)
            provider = Ollama("http://localhost:11434", "candidate", 8192)
            calls = []
            def api(path, body=None, timeout=5):
                if path == "/api/ps":
                    return {"models": [{"name": "candidate"}]}
                calls.append(body)
                return {"done": True, "message": {"content": "ok"}}
            provider.api = api
            for mode in ("watch_probe", "ask"):
                provider.infer(bench.prepare(corpus["cases"][0], root.parent, mode)[0])
            self.assertEqual({c["model"] for c in calls}, {"candidate"})
            self.assertEqual({c["options"]["num_ctx"] for c in calls}, {8192})
            self.assertEqual({c["keep_alive"] for c in calls}, {-1})
            self.assertNotEqual(calls[0]["options"]["num_predict"], calls[1]["options"]["num_predict"])

    def test_frames_and_pair_gate(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp)
            for name, duration in (("baseline.csv", 10), ("slow.csv", 12)):
                (path / name).write_text("ProcessID,SwapChainAddress,FrameTime\n" + f"10,0x1,{duration}\n" * 13000)
            stats = frame_stats(path / "baseline.csv", "FrameTime", 10, None)
            self.assertEqual(stats["average_fps"], 100)
            self.assertEqual(stats["one_percent_low_fps"], 100)
            pairs = [{"id": str(i), "baseline": "baseline.csv", "treatment": "slow.csv", "baseline_pid": 10, "treatment_pid": 10} for i in range(3)]
            result = compare_pairs({"column": "FrameTime", "pairs": pairs}, path)
            self.assertEqual(result["status"], "performance-failed")
            result = compare_pairs({"column": "FrameTime", "pairs": pairs[:1]}, path)
            self.assertEqual(result["status"], "insufficient-data")

    def test_mixed_swapchains_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "frames.csv"
            path.write_text("ProcessID,SwapChainAddress,FrameTime\n" + "10,a,10\n10,b,10\n" * 100)
            with self.assertRaises(ValueError):
                frame_stats(path, "FrameTime", 10, None)

    def test_runner_mock_is_not_real_model_result(self):
        class Fake:
            def __init__(self, *args): pass
            def api(self, path, *args):
                if path == "/api/tags": return {"models": [{"name": "fake", "size": 200_000_000}]}
                if path == "/api/show": return {"capabilities": ["vision"]}
                return {"version": "TEST DOUBLE"}
            def residency(self): return [{"name": "fake", "digest": "TEST DOUBLE"}]
            def infer(self, request): return Response('{"event":"unknown","text":"","uncertain":true}', .1, {})
        with tempfile.TemporaryDirectory() as tmp:
            path = generate(Path(tmp) / "corpus")
            output = Path(tmp) / "out"
            with patch.object(bench, "Ollama", Fake):
                bench.run(SimpleNamespace(corpus=path, output=output, endpoint="http://localhost:11434", model="fake", runtime_pid=None, repetitions=1, mode="both", interval=0))
            result = json.loads((output / "results.json").read_text())
            self.assertEqual(len(result["rows"]), 26)
            self.assertEqual(result["metadata"]["model_selection"], "not-decided")
            self.assertEqual(result["metadata"]["real_cases"], 0)
            self.assertEqual(result["status"], "completed-not-qualified")

    def test_timeout_aborts_without_next_request(self):
        calls = []
        class TimeoutProvider:
            def __init__(self, *args): pass
            def api(self, path, *args):
                if path == "/api/tags": return {"models": [{"name": "fake", "size": 200_000_000}]}
                if path == "/api/show": return {"capabilities": ["vision"]}
                return {"version": "TEST DOUBLE"}
            def residency(self): return [{"name": "fake", "digest": "TEST DOUBLE"}]
            def infer(self, request):
                calls.append(request)
                if len(calls) > 1: raise TimeoutError("test timeout")
                return Response("warmup", .1, {})
        with tempfile.TemporaryDirectory() as tmp:
            path = generate(Path(tmp) / "corpus")
            output = Path(tmp) / "out"
            with patch.object(bench, "Ollama", TimeoutProvider), self.assertRaises(RuntimeError):
                bench.run(SimpleNamespace(corpus=path, output=output, endpoint="http://localhost:11434", model="fake", runtime_pid=None, repetitions=3, mode="both", interval=0))
            self.assertEqual(len(calls), 2)
            result = json.loads((output / "results.json").read_text())
            self.assertEqual(result["status"], "aborted")
            self.assertEqual(len(result["rows"]), 1)


if __name__ == "__main__":
    unittest.main()
