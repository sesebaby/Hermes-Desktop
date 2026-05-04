from __future__ import annotations

import importlib.util
import json
import subprocess
import sys
from pathlib import Path


SCRIPT_PATH = Path(__file__).with_name("destination_selector_benchmark.py")


def load_module():
    spec = importlib.util.spec_from_file_location("dest_bench", SCRIPT_PATH)
    module = importlib.util.module_from_spec(spec)
    assert spec is not None
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


def test_dry_run_covers_all_scenario_types() -> None:
    result = subprocess.run(
        [sys.executable, str(SCRIPT_PATH), "--dry-run"],
        capture_output=True, text=True, check=False,
    )
    assert result.returncode == 0, result.stderr
    payload = json.loads(result.stdout)
    names = [s["name"] for s in payload["scenarios"]]
    assert "pick_nearest_available" in names
    assert "schedule_follow_next_entry" in names
    assert "schedule_deviate_with_reason" in names
    assert "fallback_when_blocked" in names
    assert "all_blocked_return_none" in names
    assert "out_of_area_avoid" in names
    assert "multi_round_consistency" in names


def test_build_verdict_ranks_models() -> None:
    module = load_module()
    results = [
        {"model": "qwen3.5-0.8b", "summary": {"destination_pass_rate": 0.85, "conclusion": "ready_as_destination_selector", "avg_latency_s": 0.5}},
        {"model": "qwen3.5-2b", "summary": {"destination_pass_rate": 0.95, "conclusion": "ready_as_destination_selector", "avg_latency_s": 0.4}},
    ]
    verdict = module.build_verdict(results)
    assert verdict["recommendation"] == "both_usable"
    assert verdict["preferred"] == "qwen3.5-2b"
