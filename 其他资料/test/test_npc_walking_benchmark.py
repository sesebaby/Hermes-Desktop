from __future__ import annotations

import importlib.util
import json
import subprocess
import sys
from pathlib import Path


SCRIPT_PATH = Path(__file__).with_name("npc_walking_benchmark.py")


def load_module():
    spec = importlib.util.spec_from_file_location("npc_walking_benchmark", SCRIPT_PATH)
    module = importlib.util.module_from_spec(spec)
    assert spec is not None
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


def test_dry_run_has_tile_nav_scenarios() -> None:
    result = subprocess.run(
        [sys.executable, str(SCRIPT_PATH), "--dry-run"],
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode == 0, result.stderr
    payload = json.loads(result.stdout)
    assert "scenarios" in payload
    names = [s["name"] for s in payload["scenarios"]]
    assert "simple_direct_path" in names
    assert "obstacle_avoidance" in names
    assert "multi_obstacle_navigation" in names
    assert "dead_end_surrounded" in names
    assert "multi_round_consistency" in names


def test_build_summary_classifies_correctly() -> None:
    module = load_module()
    scenarios = [
        {"name": "simple_direct_path", "approach": "json_schema", "passed": True, "pass_rate": 1.0, "direction_ok": True},
        {"name": "obstacle_avoidance", "approach": "json_schema", "passed": True, "pass_rate": 1.0, "direction_ok": True},
        {"name": "dead_end", "approach": "json_schema", "passed": True, "pass_rate": 1.0, "direction_ok": True},
        {"name": "multi_round", "approach": "json_schema", "passed": True, "pass_rate": 0.8, "direction_ok": True},
        {"name": "tool_compare", "approach": "tool_calling", "passed": False, "pass_rate": 0.33, "direction_ok": False},
    ]
    summary = module.build_summary("qwen3.5-0.8b", scenarios)
    assert summary["json_schema_avg_pass_rate"] == 0.95
    assert "tile_navigation_ready" in summary["conclusion"] or "usable_with" in summary["conclusion"]
