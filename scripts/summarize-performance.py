"""Summarize actual alternating observations; never manufacture missing runs."""
import argparse
import json
import math
import statistics
from collections import defaultdict
from datetime import datetime
from pathlib import Path


def load(path):
    def invalid_constant(value):
        raise ValueError(f"Non-finite JSON number rejected: {value}")
    def finite_float(value):
        result = float(value)
        if not math.isfinite(result):
            raise ValueError("Overflowing JSON number rejected")
        return result
    return json.loads(path.read_text(encoding="utf-8-sig"), parse_constant=invalid_constant, parse_float=finite_float)


def timestamp(value):
    result = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if result.tzinfo is None:
        raise ValueError("Observation timestamps must include a time zone")
    return result


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


def summarize(directory, additional_directories=(), exclude_before=None, require_complete=False):
    execution = load(directory / "execution.json")
    groups = defaultdict(list)
    excluded = []
    executions = []
    seen = set()
    for source in (directory, *additional_directories):
        current = load(source / "execution.json")
        for field in ("schemaVersion", "seconds", "warmupSeconds"):
            if current[field] != execution[field]:
                raise ValueError(f"Incompatible observation protocol: {field}")
        executions.append(current)
        for run in current["runs"]:
            relative = Path(run["relativeReport"])
            folder = (source / relative).resolve()
            if relative.is_absolute() or not folder.is_relative_to(source.resolve()):
                raise ValueError("Observation evidence escapes its report directory")
            if folder in seen:
                raise ValueError("The same evidence cannot count as two trials")
            seen.add(folder)
            metrics = load(folder / "metrics.json")
            rows = [r for r in metrics["samples"] if r["phase"] == "stable"]
            if not metrics.get("passed") or len(rows) < current["seconds"] - 1:
                raise ValueError("Failed or incomplete observations cannot be summarized as passed")
            if metrics["mode"] != run["mode"] or metrics["runtime"] != run["runtime"]:
                raise ValueError("Measurement identity differs from its execution record")
            reason = None
            if exclude_before and timestamp(metrics["started"]) < timestamp(exclude_before):
                reason = "Before the operator confirmed the machine was idle; physical input may overlap the automation"
            elif run.get("contaminatedByPointerMotion"):
                reason = "Pointer-position observation changed during intended idle/disabled observation; its input source was not recorded"
            if any(r.get("pointerAvailable") is False for r in rows):
                reason = "Interactive cursor position unavailable during stable observation; locked/changed desktops cannot prove this workload"
            driver = load(folder / "driver.json") if (folder / "driver.json").exists() else None
            if run["mode"] in {"comet", "minimal", "neon", "stardust", "target120", "power30", "fast-click", "comet-no-paint", "comet-no-render"} and not driver:
                raise ValueError("An active workload requires its completed independent input-driver evidence")
            if driver and not driver.get("passed"):
                raise ValueError("Cancelled input drivers cannot supply accepted evidence")
            if driver and driver.get("observedMoves", 0) < 5:
                reason = "Input target did not receive enough actual movement; requested synthetic events are not proof of input delivery"
            if driver and run["mode"] == "fast-click" and driver.get("observedClicks", 0) < 1:
                reason = "High-frequency click workload did not deliver any clicks to its owned input target"
            if reason:
                excluded.append({"target": run["target"], "mode": run["mode"], "round": run["round"],
                                 "report": source.name + "/" + run["relativeReport"], "reason": reason})
                continue
            groups[run["mode"], run["target"]].append((run, metrics, rows, driver))
    expected_modes = {run["mode"] for current in executions for run in current["runs"]}
    expected_targets = {run["target"] for current in executions for run in current["runs"]}
    expected_groups = {(mode, target) for mode in expected_modes for target in expected_targets}
    insufficient = [{"mode": mode, "target": target, "acceptedRounds": len(groups[mode, target])}
                    for mode, target in sorted(expected_groups) if len(groups[mode, target]) < execution["rounds"]]
    if require_complete and (not all(e.get("completed", False) for e in executions) or insufficient):
        raise ValueError("Requested complete evidence, but some executions or accepted observation groups are incomplete")
    measurements = []
    fields = ["cpuOneCorePercent", "cpuMachinePercent", "machineBusyPercent", "workingSet", "privateBytes",
              "gdi", "user", "threads", "allocatedBytesPerSecond", "samplerHz", "renderHz", "paintHz", "paintWindowMeanMs"]
    for (mode, target), trials in sorted(groups.items()):
        if not trials:
            continue
        if len({run["applicationSHA256"] for run, _, _, _ in trials}) != 1:
            raise ValueError("Different application builds cannot share a mode/version summary")
        all_rows = [row for _, _, rows, _ in trials for row in rows]
        warm_rows = [row for _, metrics, _, _ in trials for row in metrics["samples"] if row["phase"] == "warmup"]
        drivers = [driver for _, _, _, driver in trials if driver]
        measurements.append({"mode": mode, "target": target, "completedRounds": len(trials),
                             "stableObservations": len(all_rows),
                             "applicationSHA256": sorted({run["applicationSHA256"] for run, _, _, _ in trials}),
                             "roundCpuMeans": [statistics.mean(r["cpuOneCorePercent"] for r in rows) for _, _, rows, _ in trials],
                             "roundCpuMeanDistribution": distribution([statistics.mean(r["cpuOneCorePercent"] for r in rows) for _, _, rows, _ in trials]),
                             "observations": {field: distribution([r.get(field) for r in all_rows]) for field in fields},
                             "warmupObservations": {field: distribution([r.get(field) for r in warm_rows]) for field in fields},
                             "roundResourceChanges": [{field: rows[-1][field] - rows[0][field]
                                                       for field in ("workingSet", "privateBytes", "gdi", "user", "threads")
                                                       if field in rows[0] and field in rows[-1]}
                                                      for _, _, rows, _ in trials],
                             "initialPanelsCreated": [metrics.get("initialPanel") for _, metrics, _, _ in trials],
                             "interventions": sorted({metrics.get("intervention", "none") for _, metrics, _, _ in trials}),
                             "managedMainEpochExplicit": all(metrics.get("managedMainEpochExplicit", False) for _, metrics, _, _ in trials),
                             "gcCollections": {str(g): sum(r["gen" + str(g)] for r in all_rows) for g in range(3)},
                             "driverOneCoreCPU": distribution([d["cpuOneCorePercent"] for d in drivers]),
                             "controllerOneCoreCPU": distribution([run["controllerCPUOneCorePercent"] for run, _, _, _ in trials]),
                             "inputConditions": [{"input": d["input"], "events": d["events"], "observedClicks": d["observedClicks"],
                                                  "observedMoves": d.get("observedMoves"), "seconds": d["seconds"]} for d in drivers],
                             "markers": {key: distribution([m["markers"].get(key) for _, m, _, _ in trials])
                                         for key in sorted({k for _, m, _, _ in trials for k in m["markers"]})}})
    return {"schemaVersion": 1, "executionCompleted": all(e.get("completed", False) for e in executions),
            "started": min((e["started"] for e in executions), key=timestamp),
            "ended": max((e.get("ended", e.get("updated", e["started"])) for e in executions), key=timestamp),
            "requestedRounds": execution["rounds"], "stableSecondsPerTrial": execution["seconds"],
            "warmupSeconds": execution["warmupSeconds"], "excludedBefore": exclude_before,
            "insufficientGroups": insufficient, "excludedRuns": excluded, "measurements": measurements,
            "notes": "P95 uses actual 1-second observations; round CPU means are shown separately. Windows CPU accounting is quantized. Paint statistics are sampling-window CPU means, not per-frame/GPU P95. Missing counters remain null. Profiling and natural-input observations must remain separate from this timed automation."}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("directory", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--additional-directory", type=Path, action="append", default=[])
    parser.add_argument("--exclude-before", help="Explicit operator-confirmed UTC/time-zone cutoff; earlier evidence stays on disk but is excluded")
    parser.add_argument("--require-complete", action="store_true")
    arguments = parser.parse_args()
    result = summarize(arguments.directory, arguments.additional_directory, arguments.exclude_before, arguments.require_complete)
    output = arguments.output or arguments.directory / "summary.json"
    output.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"Summarized {len(result['measurements'])} actual mode/version groups; excluded {len(result['excludedRuns'])} contaminated runs")
