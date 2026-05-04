from __future__ import annotations

import argparse
import json
import time
from typing import Any

from openai import APIConnectionError, OpenAI

DEFAULT_BASE_URL = "http://127.0.0.1:1234/v1"
MODELS = ["qwen3.5-0.8b", "qwen3.5-2b"]

SYSTEM_PROMPT = (
    "You are an NPC decision-maker in Stardew Valley. "
    "You receive observation facts: current location, available destinations, and schedule entries. "
    "Your job: pick ONE destination_id. Do not chat. Always output valid JSON."
)

DEST_SCHEMA = {
    "type": "json_schema",
    "json_schema": {
        "name": "destination_choice",
        "strict": True,
        "schema": {
            "type": "object",
            "properties": {
                "destination_id": {
                    "type": "string",
                    "description": "The chosen destination ID, or 'none' to stay",
                },
                "why": {
                    "type": "string",
                    "enum": [
                        "nearest_available",
                        "following_schedule",
                        "personal_interest",
                        "alternative_fallback",
                        "no_good_option",
                    ],
                },
            },
            "required": ["destination_id", "why"],
            "additionalProperties": False,
        },
    },
}

MOVE_TOOL = {
    "type": "function",
    "function": {
        "name": "stardew_move",
        "description": "Move NPC to a destination.",
        "parameters": {
            "type": "object",
            "properties": {
                "destination_id": {"type": "string"},
                "why": {"type": "string"},
            },
            "required": ["destination_id", "why"],
            "additionalProperties": False,
        },
    },
}


# ── 模拟真实游戏 observation 格式 ─────────────────────────────────
# 对应 StardewQueryService.BuildStatusFacts() 输出

def _make_observation(
    npc: str,
    location: str,
    tile: tuple[int, int],
    time_str: str,
    destinations: list[dict[str, Any]],
    schedule: list[dict[str, Any]] | None = None,
    extra: str = "",
) -> str:
    lines = [
        f"NPC: {npc}",
        f"Current location: {location}, tile=({tile[0]},{tile[1]})",
        f"Game time: {time_str}",
        "",
    ]
    for i, d in enumerate(destinations):
        lines.append(
            f"destination[{i}].id={d['id']}\n"
            f"destination[{i}].label={d['label']}\n"
            f"destination[{i}].area={d['area']}\n"
            f"destination[{i}].distance={d['distance']}\n"
            f"destination[{i}].availability={d['availability']}"
        )
    if schedule:
        lines.append("")
        for i, s in enumerate(schedule):
            lines.append(
                f"schedule_entry[{i}].time={s['time']}\n"
                f"schedule_entry[{i}].destination={s['destination']}\n"
                f"schedule_entry[{i}].label={s['label']}"
            )
    if extra:
        lines.append("")
        lines.append(extra)
    return "\n".join(lines)


def build_scenarios() -> list[dict[str, Any]]:
    return [
        # ── 1) 最近可用 ──────────────────────────────────────
        {
            "name": "pick_nearest_available",
            "approach": "json_schema",
            "expected_ids": {"town.fountain"},
            "forbidden_ids": {"none"},
            "rounds": 3,
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": _make_observation(
                    npc="Haley", location="Town", tile=(30, 55), time_str="11:30",
                    destinations=[
                        {"id": "town.fountain", "label": "Town fountain", "area": "Town", "distance": "tiles_15", "availability": "available"},
                        {"id": "town.pierre_shop", "label": "Pierre's shop", "area": "Town", "distance": "tiles_40", "availability": "available"},
                        {"id": "beach.tide_pool", "label": "Tide pool", "area": "Beach", "distance": "tiles_120", "availability": "available"},
                        {"id": "haley_house.kitchen", "label": "Kitchen", "area": "HaleyHouse", "distance": "tiles_80", "availability": "available"},
                    ],
                    extra="Haley wants to go somewhere nearby. Pick the best destination.",
                )},
            ],
        },
        # ── 2) 跟日程 ──────────────────────────────────────
        {
            "name": "schedule_follow_next_entry",
            "approach": "json_schema",
            "expected_ids": {"town.fountain"},
            "forbidden_ids": {"none"},
            "rounds": 3,
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": _make_observation(
                    npc="Haley", location="Town", tile=(30, 55), time_str="12:50",
                    destinations=[
                        {"id": "town.fountain", "label": "Town fountain", "area": "Town", "distance": "tiles_15", "availability": "available"},
                        {"id": "beach.tide_pool", "label": "Tide pool", "area": "Beach", "distance": "tiles_120", "availability": "available"},
                    ],
                    schedule=[
                        {"time": "10:00", "destination": "haley_house.kitchen", "label": "Kitchen"},
                        {"time": "13:00", "destination": "town.fountain", "label": "Town fountain"},
                        {"time": "18:00", "destination": "haley_house.bedroom", "label": "Bedroom"},
                    ],
                    extra="The next schedule entry is at 13:00 (town.fountain). It's almost time. Pick the best destination.",
                )},
            ],
        },
        # ── 3) 偏离日程有理由 ───────────────────────────────
        {
            "name": "schedule_deviate_with_reason",
            "approach": "json_schema",
            "expected_ids": {"beach.tide_pool", "haley_house.kitchen"},
            "forbidden_ids": {"none"},
            "rounds": 3,
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": _make_observation(
                    npc="Haley", location="Beach", tile=(70, 15), time_str="14:30",
                    destinations=[
                        {"id": "beach.tide_pool", "label": "Tide pool", "area": "Beach", "distance": "tiles_5", "availability": "available"},
                        {"id": "haley_house.kitchen", "label": "Kitchen", "area": "HaleyHouse", "distance": "tiles_130", "availability": "available"},
                    ],
                    schedule=[
                        {"time": "10:00", "destination": "haley_house.kitchen", "label": "Kitchen"},
                        {"time": "13:00", "destination": "town.fountain", "label": "Town fountain"},
                        {"time": "18:00", "destination": "haley_house.bedroom", "label": "Bedroom"},
                    ],
                    extra="It's 14:30 — Haley already missed the 13:00 fountain. She's at the beach already. The tide pool is right next to her. Pick the best destination. She does NOT have to follow the schedule.",
                )},
            ],
        },
        # ── 4) 首选被挡选备选 ──────────────────────────────
        {
            "name": "fallback_when_blocked",
            "approach": "json_schema",
            "expected_ids": {"town.pierre_shop", "town.community_center"},
            "forbidden_ids": {"town.fountain", "none"},
            "rounds": 3,
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": _make_observation(
                    npc="Haley", location="Town", tile=(30, 55), time_str="11:00",
                    destinations=[
                        {"id": "town.fountain", "label": "Town fountain", "area": "Town", "distance": "tiles_15", "availability": "blocked_by_npc"},
                        {"id": "town.pierre_shop", "label": "Pierre's shop", "area": "Town", "distance": "tiles_40", "availability": "available"},
                        {"id": "town.community_center", "label": "Community center", "area": "Town", "distance": "tiles_55", "availability": "available"},
                    ],
                    extra="The fountain is blocked. Pick a reasonable alternative. Do NOT pick the blocked destination.",
                )},
            ],
        },
        # ── 5) 全堵了 → none ──────────────────────────────
        {
            "name": "all_blocked_return_none",
            "approach": "json_schema",
            "expected_ids": {"none"},
            "forbidden_ids": set(),
            "rounds": 3,
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": _make_observation(
                    npc="Haley", location="Town", tile=(30, 55), time_str="19:00",
                    destinations=[
                        {"id": "town.fountain", "label": "Town fountain", "area": "Town", "distance": "tiles_15", "availability": "blocked_by_npc"},
                        {"id": "town.pierre_shop", "label": "Pierre's shop", "area": "Town", "distance": "tiles_40", "availability": "closed"},
                    ],
                    extra="All destinations are unavailable. Pick 'none' to stay in place.",
                )},
            ],
        },
        # ── 6) 跨地图远距离应该回避 ────────────────────────
        {
            "name": "out_of_area_avoid",
            "approach": "json_schema",
            "expected_ids": {"town.fountain", "town.pierre_shop"},
            "forbidden_ids": {"beach.tide_pool"},
            "rounds": 3,
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": _make_observation(
                    npc="Haley", location="Town", tile=(30, 55), time_str="10:30",
                    destinations=[
                        {"id": "town.fountain", "label": "Town fountain", "area": "Town", "distance": "tiles_15", "availability": "available"},
                        {"id": "town.pierre_shop", "label": "Pierre's shop", "area": "Town", "distance": "tiles_40", "availability": "available"},
                        {"id": "beach.tide_pool", "label": "Tide pool", "area": "Beach", "distance": "tiles_800", "availability": "available"},
                    ],
                    extra="Haley is in Town. The beach is very far (800 tiles). Pick the best destination.",
                )},
            ],
        },
        # ── 7) 一致性 ─────────────────────────────────────
        {
            "name": "multi_round_consistency",
            "approach": "json_schema",
            "expected_ids": {"town.fountain"},
            "forbidden_ids": {"none"},
            "rounds": 5,
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": _make_observation(
                    npc="Haley", location="Town", tile=(30, 55), time_str="12:50",
                    destinations=[
                        {"id": "town.fountain", "label": "Town fountain", "area": "Town", "distance": "tiles_15", "availability": "available"},
                        {"id": "beach.tide_pool", "label": "Tide pool", "area": "Beach", "distance": "tiles_120", "availability": "available"},
                    ],
                    schedule=[
                        {"time": "13:00", "destination": "town.fountain", "label": "Town fountain"},
                    ],
                    extra="It's almost 13:00, schedule says fountain. Pick the best destination.",
                )},
            ],
        },
        # ── 8) tool calling 对比 ──────────────────────────
        {
            "name": "tool_calling_schedule_pick",
            "approach": "tool_calling",
            "tools": [MOVE_TOOL],
            "tool_choice": "required",
            "expected_ids": {"town.fountain"},
            "forbidden_ids": {"none"},
            "rounds": 3,
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": _make_observation(
                    npc="Haley", location="Town", tile=(30, 55), time_str="12:50",
                    destinations=[
                        {"id": "town.fountain", "label": "Town fountain", "area": "Town", "distance": "tiles_15", "availability": "available"},
                        {"id": "beach.tide_pool", "label": "Tide pool", "area": "Beach", "distance": "tiles_120", "availability": "available"},
                    ],
                    schedule=[
                        {"time": "13:00", "destination": "town.fountain", "label": "Town fountain"},
                    ],
                    extra="Pick a destination and call the move tool.",
                )},
            ],
        },
    ]


# ── 执行 ─────────────────────────────────────────────────────────

def create_client(base_url: str) -> OpenAI:
    return OpenAI(base_url=base_url, api_key="lm-studio")


def run_json_round(client: OpenAI, model: str, scenario: dict[str, Any]) -> dict[str, Any]:
    start = time.perf_counter()
    response = client.chat.completions.create(
        model=model, temperature=0,
        messages=scenario["messages"],
        response_format=DEST_SCHEMA,
        max_tokens=128,
    )
    elapsed = time.perf_counter() - start
    text = response.choices[0].message.content or ""
    try:
        parsed = json.loads(text)
        ok = True
    except json.JSONDecodeError:
        parsed = {}
        ok = False
    return {
        "valid_json": ok,
        "destination_id": parsed.get("destination_id", "").strip(),
        "why": parsed.get("why", ""),
        "elapsed_s": round(elapsed, 2),
        "finish_reason": response.choices[0].finish_reason,
    }


def run_tool_round(client: OpenAI, model: str, scenario: dict[str, Any]) -> dict[str, Any]:
    start = time.perf_counter()
    response = client.chat.completions.create(
        model=model, temperature=0,
        messages=scenario["messages"],
        tools=scenario.get("tools", []),
        tool_choice=scenario.get("tool_choice", "auto"),
        max_tokens=128,
    )
    elapsed = time.perf_counter() - start
    choice = response.choices[0]
    tool_calls = getattr(choice.message, "tool_calls", None) or []
    if tool_calls:
        try:
            args = json.loads(tool_calls[0].function.arguments)
            dest_id = args.get("destination_id", "").strip()
        except json.JSONDecodeError:
            dest_id = ""
        ok = True
    else:
        dest_id = ""
        ok = False
    return {
        "valid_json": ok,
        "destination_id": dest_id,
        "why": "",
        "elapsed_s": round(elapsed, 2),
        "finish_reason": choice.finish_reason,
        "had_tool_call": len(tool_calls) > 0,
    }


def evaluate(scenario: dict[str, Any], result: dict[str, Any]) -> bool:
    dest = result.get("destination_id", "")
    if scenario.get("expected_ids") and dest in scenario["expected_ids"]:
        return True
    if scenario.get("forbidden_ids") and dest in scenario["forbidden_ids"]:
        return False
    return False


def run_model(client: OpenAI, model: str) -> dict[str, Any]:
    scenarios = build_scenarios()
    results = []
    for s in scenarios:
        run_fn = run_json_round if s["approach"] == "json_schema" else run_tool_round
        rounds_out = []
        passes = 0
        for _ in range(s["rounds"]):
            r = run_fn(client, model, s)
            r["passed"] = evaluate(s, r)
            if r["passed"]:
                passes += 1
            rounds_out.append(r)
        rate = passes / s["rounds"] if s["rounds"] else 0.0
        ids = {r["destination_id"] for r in rounds_out}
        results.append({
            "name": s["name"],
            "approach": s["approach"],
            "rounds": s["rounds"],
            "pass_count": passes,
            "pass_rate": round(rate, 2),
            "unique_ids": sorted(ids),
            "avg_elapsed_s": round(sum(r["elapsed_s"] for r in rounds_out) / len(rounds_out), 2),
            "passed": rate >= 0.66,
            "round_details": rounds_out,
        })
    rates = [r["pass_rate"] for r in results if r["approach"] == "json_schema"]
    tool_rates = [r["pass_rate"] for r in results if r["approach"] == "tool_calling"]
    avg = sum(rates) / len(rates) if rates else 0.0
    tool_avg = sum(tool_rates) / len(tool_rates) if tool_rates else 0.0
    passed_count = sum(1 for r in results if r["passed"])
    total = len(results)
    if avg >= 0.85 and tool_avg >= 0.5:
        conclusion = "ready_as_destination_selector"
    elif avg >= 0.7:
        conclusion = "usable_with_validation"
    elif avg >= 0.5:
        conclusion = "marginal"
    else:
        conclusion = "unreliable"
    return {
        "model": model,
        "summary": {
            "model": model,
            "destination_pass_rate": round(avg, 2),
            "tool_calling_pass_rate": round(tool_avg, 2),
            "passed_scenarios": f"{passed_count}/{total}",
            "avg_latency_s": round(sum(r["avg_elapsed_s"] for r in results) / len(results), 2) if results else 0.0,
            "conclusion": conclusion,
        },
        "scenarios": results,
    }


def build_verdict(all_results: list[dict[str, Any]]) -> dict[str, Any]:
    models_info = []
    for r in all_results:
        s = r["summary"]
        models_info.append({
            "model": r["model"],
            "pass_rate": s["destination_pass_rate"],
            "conclusion": s["conclusion"],
            "latency": s["avg_latency_s"],
        })
    ready = [m for m in models_info if m["conclusion"] == "ready_as_destination_selector"]
    if len(ready) >= 2:
        preferred = min(ready, key=lambda m: m["latency"])
        return {
            "recommendation": "both_usable",
            "preferred": preferred["model"],
            "reason": "两个模型都能做目的地选择，选延迟更低的",
            "models": models_info,
        }
    if len(ready) == 1:
        return {
            "recommendation": "use_the_ready_one",
            "preferred": ready[0]["model"],
            "reason": "只有一个模型达标",
            "models": models_info,
        }
    usable = [m for m in models_info if m["conclusion"] == "usable_with_validation"]
    if usable:
        return {
            "recommendation": "usable_with_guard",
            "preferred": usable[0]["model"],
            "reason": "需加校验层，不直接信任输出",
            "models": models_info,
        }
    return {
        "recommendation": "none_ready",
        "preferred": None,
        "reason": "所有模型都不达标，考虑纯算法方案",
        "models": models_info,
    }


def dry_run_payload() -> dict[str, Any]:
    s_data = []
    for s in build_scenarios():
        s_data.append({
            "name": s["name"], "approach": s["approach"],
            "rounds": s["rounds"], "pass_count": s["rounds"],
            "pass_rate": 1.0, "unique_ids": sorted(s["expected_ids"]),
            "avg_elapsed_s": 0.5, "passed": True, "round_details": [],
        })
    return {
        "models_tested": MODELS,
        "scenarios": s_data,
        "verdict": {"recommendation": "both_usable", "preferred": "qwen3.5-2b", "reason": "两个模型都能做目的地选择"},
        "results": [
            {"model": "qwen3.5-0.8b", "summary": {"destination_pass_rate": 0.85, "conclusion": "ready_as_destination_selector", "avg_latency_s": 0.5, "passed_scenarios": "7/8", "tool_calling_pass_rate": 0.5}, "scenarios": s_data},
            {"model": "qwen3.5-2b", "summary": {"destination_pass_rate": 0.95, "conclusion": "ready_as_destination_selector", "avg_latency_s": 0.4, "passed_scenarios": "8/8", "tool_calling_pass_rate": 0.5}, "scenarios": s_data},
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL)
    parser.add_argument("--models", nargs="*", default=None)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    if args.dry_run:
        print(json.dumps(dry_run_payload(), ensure_ascii=False, indent=2))
        return 0

    models = args.models if args.models else MODELS
    client = create_client(args.base_url)

    all_results = []
    for model in models:
        try:
            result = run_model(client, model)
        except APIConnectionError:
            print(json.dumps({"error": "无法连接 LM Studio", "base_url": args.base_url}, ensure_ascii=False, indent=2))
            return 0
        all_results.append(result)

    verdict = build_verdict(all_results)
    print(json.dumps({
        "base_url": args.base_url,
        "verdict": verdict,
        "results": all_results,
    }, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
