"""Summarize actual alternating observations; never manufacture missing runs."""
import argparse
import json
import math
import statistics
from collections import defaultdict
from pathlib import Path


def load(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def percentile(values, fraction):
    values = sorted(v for v in values if v is not None and math.isfinite(v))
    if not values:
        return None
    position = (len(values) - 1) * fraction
    lower = math.floor(position)
    upper = math.ceil(position)
    return values[lower] + (values[upper] - values[lower]) * (position - lower)


def distribution(values):
    values = [v for v in values if v is not None and math.isfinite(v)]
    return {"count": len(values), "median": percentile(values, .5),
            "p95": percentile(values, .95), "mean": statistics.mean(values) if values else None,
            "minimum": min(values) if values else None, "maximum": max(values) if values else None}


def summarize(directory):
    execution = load(directory / "execution.json")
    groups = defaultdict(list)
    excluded = []
    for run in execution["runs"]:
        metrics = load(directory / run["relativeReport"] / "metrics.json")
        rows = [r for r in metrics["samples"] if r["phase"] == "stable"]
        if run.get("contaminatedByPointerMotion"):
            excluded.append({"target": run["target"], "mode": run["mode"], "round": run["round"],
                             "reason": "Pointer motion observed during intended idle/disabled observation"})
            continue
        groups[run["mode"], run["target"]].append((run, metrics, rows))
    measurements = []
    fields = ["cpuOneCorePercent", "cpuMachinePercent", "machineBusyPercent", "workingSet", "privateBytes",
              "gdi", "user", "threads", "allocatedBytesPerSecond", "samplerHz", "renderHz", "paintHz", "paintWindowMeanMs"]
    for (mode, target), trials in sorted(groups.items()):
        all_rows = [row for _, _, rows in trials for row in rows]
        drivers = []
        for run, _, _ in trials:
            path = directory / run["relativeReport"] / "driver.json"
            if path.exists():
                drivers.append(load(path))
        measurements.append({"mode": mode, "target": target, "completedRounds": len(trials),
                             "stableObservations": len(all_rows),
                             "roundCpuMeans": [statistics.mean(r["cpuOneCorePercent"] for r in rows) for _, _, rows in trials],
                             "roundCpuMeanDistribution": distribution([statistics.mean(r["cpuOneCorePercent"] for r in rows) for _, _, rows in trials]),
                             "observations": {field: distribution([r.get(field) for r in all_rows]) for field in fields},
                             "gcCollections": {str(g): sum(r["gen" + str(g)] for r in all_rows) for g in range(3)},
                             "driverOneCoreCPU": distribution([d["cpuOneCorePercent"] for d in drivers]),
                             "controllerOneCoreCPU": distribution([run["controllerCPUOneCorePercent"] for run, _, _ in trials]),
                             "inputConditions": [{"input": d["input"], "events": d["events"], "observedClicks": d["observedClicks"],
                                                  "observedMoves": d.get("observedMoves"), "seconds": d["seconds"]} for d in drivers],
                             "markers": {key: distribution([m["markers"].get(key) for _, m, _ in trials])
                                         for key in sorted({k for _, m, _ in trials for k in m["markers"]})}})
    return {"schemaVersion": 1, "executionCompleted": execution.get("completed", False),
            "started": execution["started"], "ended": execution.get("ended", execution.get("updated")),
            "requestedRounds": execution["rounds"], "stableSecondsPerTrial": execution["seconds"],
            "warmupSeconds": execution["warmupSeconds"], "excludedRuns": excluded, "measurements": measurements,
            "notes": "P95 uses actual 1-second observations; round CPU means are shown separately. Windows CPU accounting is quantized. Paint statistics are sampling-window CPU means, not per-frame/GPU P95. Missing counters remain null. Profiling and natural-input observations must remain separate from this timed automation."}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("directory", type=Path)
    parser.add_argument("--output", type=Path)
    arguments = parser.parse_args()
    result = summarize(arguments.directory)
    output = arguments.output or arguments.directory / "summary.json"
    output.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"Summarized {len(result['measurements'])} actual mode/version groups; excluded {len(result['excludedRuns'])} contaminated runs")
