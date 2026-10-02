"""Sampled process RAM and Windows WDDM allocation, including runtime children.
Missing readings remain null; these are not instantaneous hardware peak counters.
"""
import json
import os
import subprocess
import threading
import time
import psutil


class Resources:
    def __init__(self, root_pid=None):
        self.root = psutil.Process(root_pid) if root_pid else None
        self.samples = []
        self.stop_event = threading.Event()
        self.thread = threading.Thread(target=self.loop, daemon=True)

    def start(self):
        self.started = time.perf_counter()
        self.thread.start()

    def finish(self):
        self.stop_event.set()
        self.thread.join(timeout=10)
        return {"samples": self.samples,
            "ram_metric": "sum of process USS/private working sets for explicit runtime PID and descendants",
            "gpu_metric": "Windows WDDM per-process dedicated/shared allocations; not Ollama file size",
            "limitations": "Sampled every ~2 seconds plus query time; short spikes may be missed. Null is unavailable. Runtime PID must be correct. Run identical monitoring in baseline and treatment."}

    def loop(self):
        while not self.stop_event.is_set():
            sample = {"seconds": time.perf_counter() - self.started, "runtime_uss_bytes": None,
                      "gpu_dedicated_bytes": None, "gpu_shared_bytes": None,
                      "system_available_bytes": psutil.virtual_memory().available}
            try:
                processes = [self.root, *self.root.children(recursive=True)] if self.root and self.root.is_running() else []
                if processes:
                    sample["pids"] = [p.pid for p in processes]
                    sample["runtime_uss_bytes"] = sum(p.memory_full_info().uss for p in processes)
                    if os.name == "nt":
                        # Only validated integer PIDs are interpolated; no paths or user strings.
                        ids = ",".join(str(int(p.pid)) for p in processes)
                        command = "$ids=@(" + ids + r"); $v=@(Get-CimInstance Win32_PerfFormattedData_GPUPerformanceCounters_GPUProcessMemory -ErrorAction Stop | Where-Object { $_.Name -match '^pid_(\d+)_' -and $ids -contains [int]$Matches[1] }); if($v.Count -gt 0){[pscustomobject]@{dedicated=($v|Measure-Object DedicatedUsage -Sum).Sum; shared=($v|Measure-Object SharedUsage -Sum).Sum}|ConvertTo-Json -Compress}"
                        result = subprocess.run(["powershell", "-NoProfile", "-Command", command], capture_output=True, text=True, timeout=6, creationflags=subprocess.CREATE_NO_WINDOW)
                        if result.returncode == 0 and result.stdout.strip():
                            gpu = json.loads(result.stdout)
                            sample["gpu_dedicated_bytes"], sample["gpu_shared_bytes"] = gpu["dedicated"], gpu["shared"]
            except (psutil.Error, OSError, subprocess.SubprocessError, ValueError, AttributeError) as error:
                sample["error"] = type(error).__name__
            self.samples.append(sample)
            self.stop_event.wait(2)
