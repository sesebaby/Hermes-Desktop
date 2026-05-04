from __future__ import annotations

import importlib.util
import json
import subprocess
import sys
from pathlib import Path


SCRIPT_PATH = Path(__file__).with_name("lmstudio_agent_eval.py")


def load_module():
    spec = importlib.util.spec_from_file_location("lmstudio_agent_eval", SCRIPT_PATH)
    module = importlib.util.module_from_spec(spec)
    assert spec is not None
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


def test_dry_run_outputs_recommendation_json() -> None:
    result = subprocess.run(
        [sys.executable, str(SCRIPT_PATH), "--dry-run"],
        capture_output=True,
        text=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    payload = json.loads(result.stdout)
    assert payload["recommendation"] in {
        "usable_for_npc_walking",
        "tool_call_only_not_agentic",
        "not_ready_for_npc_walking",
    }
    assert payload["cases"]


def test_unreachable_server_returns_diagnostic_json() -> None:
    result = subprocess.run(
        [
            sys.executable,
            str(SCRIPT_PATH),
            "--base-url",
            "http://127.0.0.1:1/v1",
        ],
        capture_output=True,
        text=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    payload = json.loads(result.stdout)
    assert payload["recommendation"] == "server_unreachable"
    assert "无法连接" in payload["error"]


def test_build_recommendation_accepts_sequential_agent_loop() -> None:
    module = load_module()

    recommendation = module.build_recommendation(
        [
            {"name": "forced_single_tool", "passed": True, "tool_calls": [{"name": "plan_npc_route"}]},
            {"name": "auto_choose_tool", "passed": True, "tool_calls": [{"name": "plan_npc_route"}]},
            {
                "name": "two_step_agent_loop",
                "passed": True,
                "tool_calls": [{"name": "move_npc_one_step"}],
                "plan_called": True,
                "move_called": True,
            },
        ]
    )

    assert recommendation == "usable_for_npc_walking"
