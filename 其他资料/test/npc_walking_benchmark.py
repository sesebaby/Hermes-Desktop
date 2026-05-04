from __future__ import annotations

import argparse
import json
import time
from dataclasses import dataclass
from typing import Any

from openai import APIConnectionError, OpenAI

DEFAULT_BASE_URL = "http://127.0.0.1:1234/v1"
DEFAULT_MODEL = "qwen3.5-0.8b"

SYSTEM_PROMPT = (
    "You are a tile-based NPC movement controller. "
    "Given current position, target position, and blocked tiles, "
    "output the NEXT step direction: up/down/left/right or wait if stuck. "
    "Walk around obstacles. Do not chat. Always output valid JSON."
)

# ── JSON Schema (structured output) ──────────────────────────────
TILE_STEP_SCHEMA = {
    "type": "json_schema",
    "json_schema": {
        "name": "tile_step",
        "strict": True,
        "schema": {
            "type": "object",
            "properties": {
                "direction": {
                    "type": "string",
                    "enum": ["up", "down", "left", "right", "wait"],
                },
                "reason": {
                    "type": "string",
                    "description": "One sentence why this direction",
                },
            },
            "required": ["direction", "reason"],
            "additionalProperties": False,
        },
    },
}

MOVE_STEP_TOOL = {
    "type": "function",
    "function": {
        "name": "move_npc_step",
        "description": "Move NPC one tile in a direction. Call only with a valid direction.",
        "parameters": {
            "type": "object",
            "properties": {
                "direction": {
                    "type": "string",
                    "enum": ["up", "down", "left", "right", "wait"],
                },
            },
            "required": ["direction"],
            "additionalProperties": False,
        },
    },
}


# ── 场景构建 ─────────────────────────────────────────────────────

def _grid_view(current: tuple[int, int], target: tuple[int, int], blocked: set[tuple[int, int]]) -> str:
    """生成 ASCII 地图，让模型能看见空间关系"""
    all_cells = {current, target} | blocked
    min_x = min(x for x, y in all_cells) - 2
    max_x = max(x for x, y in all_cells) + 2
    min_y = min(y for x, y in all_cells) - 2
    max_y = max(y for x, y in all_cells) + 2
    rows = []
    for y in range(min_y, max_y + 1):
        row = []
        for x in range(min_x, max_x + 1):
            pos = (x, y)
            if pos == current:
                row.append("N")
            elif pos == target:
                row.append("T")
            elif pos in blocked:
                row.append("#")
            else:
                row.append(".")
        rows.append(f"  y={y:>2d} " + " ".join(row))
    return "\n".join(
        [
            f"(x from {min_x} to {max_x})",
            *rows,
            f"N=NPC(current), T=Target, #=Blocked, .=Open",
        ]
    )


def _tile_scenario(
    name: str,
    approach: str,
    current: tuple[int, int],
    target: tuple[int, int],
    blocked: set[tuple[int, int]],
    expected_directions: set[str],
    must_not: set[str],
    description: str,
    rounds: int = 3,
) -> dict[str, Any]:
    """构建一个 tile 导航测试场景"""
    grid = _grid_view(current, target, blocked)
    user_content = (
        f"Current NPC position: ({current[0]}, {current[1]})\n"
        f"Target position: ({target[0]}, {target[1]})\n"
        f"Blocked tiles (cannot walk into): {sorted(blocked)}\n\n"
        "Map:\n" + grid + "\n\n"
        "Output the NEXT single step direction to move toward the target. Avoid blocked tiles."
    )
    messages = [
        {"role": "system", "content": SYSTEM_PROMPT},
        {"role": "user", "content": user_content},
    ]

    scenario: dict[str, Any] = {
        "name": name,
        "description": description,
        "approach": approach,
        "messages": messages,
        "rounds": rounds,
        "expected_directions": expected_directions,
        "must_not_direction": must_not,
    }

    if approach == "json_schema":
        scenario["response_format"] = TILE_STEP_SCHEMA
    else:
        scenario["tools"] = [MOVE_STEP_TOOL]
        scenario["tool_choice"] = "required"

    return scenario


def build_scenarios() -> list[dict[str, Any]]:
    return [
        # ── 1) 简单直走：目标在右，无障碍 ───────────────────────
        _tile_scenario(
            name="simple_direct_path",
            approach="json_schema",
            current=(3, 4),
            target=(7, 4),
            blocked=set(),
            expected_directions={"right"},
            must_not={"wait", "left"},
            description="目标正右方4格，无障碍，应选 right",
        ),
        # ── 2) 简单直走上行
        _tile_scenario(
            name="simple_direct_up",
            approach="json_schema",
            current=(5, 8),
            target=(5, 3),
            blocked=set(),
            expected_directions={"up"},
            must_not={"wait", "down"},
            description="目标正上方5格，无障碍，应选 up",
        ),
        # ── 3) 单障碍绕行：目标在右，正右有障碍 ← ⭐ 核心测试
        _tile_scenario(
            name="obstacle_avoidance",
            approach="json_schema",
            current=(3, 4),
            target=(7, 4),
            blocked={(4, 4), (5, 4)},  # 正右被堵
            expected_directions={"up", "down"},
            must_not={"right", "wait"},
            description="正右方被堵，应选 up/down 绕行",
        ),
        # ── 4) 多障碍绕行：目标右下，直路被挡
        _tile_scenario(
            name="multi_obstacle_navigation",
            approach="json_schema",
            current=(3, 4),
            target=(6, 6),
            blocked={(4, 4), (4, 5), (5, 5)},
            expected_directions={"up", "right"},
            must_not={"wait", "left", "down"},
            description="目标右下角，多障碍，至少不能选 left/down/wait",
        ),
        # ── 5) 完全被围 → wait
        _tile_scenario(
            name="dead_end_surrounded",
            approach="json_schema",
            current=(3, 4),
            target=(7, 4),
            blocked={(3, 3), (3, 5), (2, 4), (4, 4)},  # 上下左右被堵
            expected_directions={"wait"},
            must_not=set(),
            description="四面被堵，唯一安全选择 wait",
        ),
        # ── 6) 一致性：相同输入 5 轮
        _tile_scenario(
            name="multi_round_consistency",
            approach="json_schema",
            current=(3, 4),
            target=(7, 4),
            blocked={(4, 4)},
            expected_directions={"up", "down"},
            must_not={"wait", "left", "right"},
            description="5轮相同输入，方向应稳定(都选up或都选down)",
            rounds=5,
        ),
        # ── 7) tool calling 对比
        _tile_scenario(
            name="tool_calling_basic_nav",
            approach="tool_calling",
            current=(3, 4),
            target=(7, 4),
            blocked={(4, 4)},
            expected_directions={"up", "down"},
            must_not={"wait", "left", "right"},
            description="用 tool calling 做同样避障，对比 json_schema",
        ),
        # ── 8) 斜向目标无直通：必须选右或下
        _tile_scenario(
            name="diagonal_target_no_direct",
            approach="json_schema",
            current=(3, 4),
            target=(6, 7),
            blocked={(4, 4), (3, 5)},
            expected_directions={"right", "up"},
            must_not={"wait", "left"},
            description="目标右下，正右正下都空，需选合理方向",
        ),
    ]


# ── 评估执行 ─────────────────────────────────────────────────────

def create_client(base_url: str) -> OpenAI:
    return OpenAI(base_url=base_url, api_key="lm-studio")


def run_json_schema_round(client: OpenAI, model: str, scenario: dict[str, Any]) -> dict[str, Any]:
    start = time.perf_counter()
    response = client.chat.completions.create(
        model=model,
        temperature=0,
        messages=scenario["messages"],
        response_format=scenario["response_format"],
        max_tokens=128,
    )
    elapsed = time.perf_counter() - start
    text = response.choices[0].message.content or ""
    try:
        parsed = json.loads(text)
        valid_json = True
    except json.JSONDecodeError:
        parsed = {}
        valid_json = False
    return {
        "valid_json": valid_json,
        "direction": parsed.get("direction", "").strip().lower(),
        "reason": parsed.get("reason", ""),
        "raw": text,
        "finish_reason": response.choices[0].finish_reason,
        "elapsed_s": round(elapsed, 2),
    }


def run_tool_calling_round(client: OpenAI, model: str, scenario: dict[str, Any]) -> dict[str, Any]:
    start = time.perf_counter()
    response = client.chat.completions.create(
        model=model,
        temperature=0,
        messages=scenario["messages"],
        tools=scenario.get("tools", []),
        tool_choice=scenario.get("tool_choice", "auto"),
        max_tokens=128,
    )
    elapsed = time.perf_counter() - start
    choice = response.choices[0]
    tool_calls = getattr(choice.message, "tool_calls", None) or []
    if tool_calls:
        args_str = tool_calls[0].function.arguments
        try:
            args = json.loads(args_str)
            direction = args.get("direction", "").strip().lower()
        except json.JSONDecodeError:
            direction = ""
        valid = True
    else:
        direction = ""
        valid = False
    return {
        "valid_json": valid,
        "direction": direction,
        "raw": choice.message.content or "",
        "tool_args": tool_calls[0].function.arguments if tool_calls else "",
        "finish_reason": choice.finish_reason,
        "elapsed_s": round(elapsed, 2),
        "had_tool_call": len(tool_calls) > 0,
    }


def evaluate_round(scenario: dict[str, Any], result: dict[str, Any]) -> bool:
    direction = result.get("direction", "")
    expected = scenario.get("expected_directions", set())
    forbidden = scenario.get("must_not_direction", set())

    # 方向必须在合法集合内
    valid_directions = {"up", "down", "left", "right", "wait"}
    if direction not in valid_directions:
        return False

    # 如果有明确期望值，必须在其中
    if expected and direction in expected:
        return True

    # 至少不能在禁止集合中
    if forbidden and direction in forbidden:
        return False

    # 如果期望集为空（例如 wait 是唯一期望），不在禁止集就算过
    if not expected and forbidden:
        return direction not in forbidden

    return False


def run_scenario(client: OpenAI, model: str, scenario: dict[str, Any]) -> dict[str, Any]:
    run_fn = run_json_schema_round if scenario["approach"] == "json_schema" else run_tool_calling_round
    round_results = []
    pass_count = 0

    for _ in range(scenario["rounds"]):
        result = run_fn(client, model, scenario)
        passed = evaluate_round(scenario, result)
        result["passed"] = passed
        if passed:
            pass_count += 1
        round_results.append(result)

    directions = [r["direction"] for r in round_results]
    unique_directions = set(directions)

    pass_rate = pass_count / scenario["rounds"] if scenario["rounds"] > 0 else 0.0

    # 方向一致性: 所有轮方向相同
    direction_consistent = len(unique_directions) == 1

    return {
        "name": scenario["name"],
        "description": scenario["description"],
        "approach": scenario["approach"],
        "rounds": scenario["rounds"],
        "pass_count": pass_count,
        "pass_rate": round(pass_rate, 2),
        "unique_directions": sorted(unique_directions),
        "direction_consistent_across_rounds": direction_consistent,
        "avg_elapsed_s": round(
            sum(r["elapsed_s"] for r in round_results) / len(round_results), 2
        ),
        "passed": pass_rate >= 0.66,  # 2/3 以上算通过
        "round_details": round_results,
    }


# ── 汇总与结论 ────────────────────────────────────────────────────

def build_summary(model: str, scenarios: list[dict[str, Any]]) -> dict[str, Any]:
    json_scenarios = [s for s in scenarios if s["approach"] == "json_schema"]
    tool_scenarios = [s for s in scenarios if s["approach"] == "tool_calling"]

    json_rates = [s["pass_rate"] for s in json_scenarios]
    tool_rates = [s["pass_rate"] for s in tool_scenarios]

    json_avg = sum(json_rates) / len(json_rates) if json_rates else 0.0
    tool_avg = sum(tool_rates) / len(tool_rates) if tool_rates else 0.0

    json_elapsed = [s.get("avg_elapsed_s", 0) for s in json_scenarios]
    avg_latency = round(sum(json_elapsed) / len(json_elapsed), 2) if json_elapsed else 0.0

    total_passed = sum(1 for s in scenarios if s["passed"])

    # 关键指标
    obstacles = [s for s in scenarios if "obstacle" in s["name"] or "multi_obstacle" in s["name"]]
    dead_end_ok = any(s["name"] == "dead_end_surrounded" and s["passed"] for s in scenarios)
    consistency_ok = any(
        s["name"] == "multi_round_consistency" and s.get("direction_consistent_across_rounds")
        for s in scenarios
    )

    # 结论分层
    if json_avg >= 0.9 and dead_end_ok and consistency_ok:
        tier = "tile_navigation_ready"
        recommendation = "模型可稳定做 tile 级导航+避障，建议接入游戏实测"
    elif json_avg >= 0.7:
        tier = "usable_with_validation"
        recommendation = "模型基本可用，建议加方向合法性校验+fallback 到 wait"
    elif json_avg >= 0.5:
        tier = "marginal_need_guardrails"
        recommendation = "模型有基本方向感但不够稳定，必须加 guardrail（每步校验可达性、超时 fallback）"
    else:
        tier = "unreliable"
        recommendation = "当前模型能力不足以做 tile 导航，建议换更大模型或用纯算法"

    return {
        "model": model,
        "total_scenarios": len(scenarios),
        "passed_scenarios": total_passed,
        "json_schema_avg_pass_rate": round(json_avg, 2),
        "tool_calling_pass_rate": round(tool_avg, 2),
        "avg_latency_s": avg_latency,
        "obstacle_avoidance_pass": all(s["passed"] for s in obstacles) if obstacles else None,
        "dead_end_pass": dead_end_ok,
        "consistency_pass": consistency_ok,
        "conclusion": tier,
        "recommendation": recommendation,
    }


# ── Dry-run ───────────────────────────────────────────────────────

def dry_run_payload() -> dict[str, Any]:
    scenarios_data = []
    for s in build_scenarios():
        scenarios_data.append({
            "name": s["name"],
            "description": s["description"],
            "approach": s["approach"],
            "rounds": s["rounds"],
            "pass_count": s["rounds"],
            "pass_rate": 1.0,
            "unique_directions": sorted(s["expected_directions"]) if s["expected_directions"] else ["up"],
            "direction_consistent_across_rounds": True,
            "avg_elapsed_s": 1.0,
            "passed": True,
            "round_details": [],
        })
    return {
        "model": DEFAULT_MODEL,
        "base_url": DEFAULT_BASE_URL,
        "summary": build_summary(DEFAULT_MODEL, scenarios_data),
        "scenarios": scenarios_data,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL)
    parser.add_argument("--model", default=DEFAULT_MODEL)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    if args.dry_run:
        print(json.dumps(dry_run_payload(), ensure_ascii=False, indent=2))
        return 0

    client = create_client(args.base_url)
    scenarios = build_scenarios()

    try:
        results = [run_scenario(client, args.model, s) for s in scenarios]
    except APIConnectionError:
        print(json.dumps({
            "error": "无法连接到 LM Studio 本地服务，请先确认 Local Server 已启动",
            "base_url": args.base_url,
            "model": args.model,
        }, ensure_ascii=False, indent=2))
        return 0

    summary = build_summary(args.model, results)
    print(json.dumps({
        "model": args.model,
        "base_url": args.base_url,
        "summary": summary,
        "scenarios": results,
    }, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
