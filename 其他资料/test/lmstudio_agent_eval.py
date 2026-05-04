from __future__ import annotations

import argparse
import json
from typing import Any

from openai import APIConnectionError, OpenAI


DEFAULT_BASE_URL = "http://127.0.0.1:1234/v1"
DEFAULT_MODEL = "qwen3.5-0.8b"


SYSTEM_PROMPT = (
    "You are a local game-control planner. "
    "When a tool is relevant, call the tool instead of chatting. "
    "Be concise and deterministic."
)


TOOLS = [
    {
        "type": "function",
        "function": {
            "name": "plan_npc_route",
            "description": "Plan the next walking action for an NPC in a tile-based game map.",
            "parameters": {
                "type": "object",
                "properties": {
                    "npc_id": {"type": "string"},
                    "current_tile": {
                        "type": "object",
                        "properties": {
                            "x": {"type": "integer"},
                            "y": {"type": "integer"},
                        },
                        "required": ["x", "y"],
                        "additionalProperties": False,
                    },
                    "target_tile": {
                        "type": "object",
                        "properties": {
                            "x": {"type": "integer"},
                            "y": {"type": "integer"},
                        },
                        "required": ["x", "y"],
                        "additionalProperties": False,
                    },
                    "blocked_tiles": {
                        "type": "array",
                        "items": {
                            "type": "object",
                            "properties": {
                                "x": {"type": "integer"},
                                "y": {"type": "integer"},
                            },
                            "required": ["x", "y"],
                            "additionalProperties": False,
                        },
                    },
                },
                "required": ["npc_id", "current_tile", "target_tile", "blocked_tiles"],
                "additionalProperties": False,
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "move_npc_one_step",
            "description": "Execute one step of movement for an NPC. Use after a route decision is known.",
            "parameters": {
                "type": "object",
                "properties": {
                    "npc_id": {"type": "string"},
                    "direction": {
                        "type": "string",
                        "enum": ["up", "down", "left", "right", "wait"],
                    },
                },
                "required": ["npc_id", "direction"],
                "additionalProperties": False,
            },
        },
    },
]


def build_cases() -> list[dict[str, Any]]:
    return [
        {
            "name": "forced_single_tool",
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {
                    "role": "user",
                    "content": (
                        "NPC abigail 当前在 (3,4)，目标是 (7,4)，障碍物在 [(5,4)]。"
                        "请调用工具给出下一步移动规划，不要闲聊。"
                    ),
                },
            ],
            "tool_choice": "required",
            "expected_tool": "plan_npc_route",
        },
        {
            "name": "auto_choose_tool",
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {
                    "role": "user",
                    "content": (
                        "NPC abigail 在 3,4，要去 7,4，5,4 被石头挡住。"
                        "请直接决定现在该怎么走。"
                    ),
                },
            ],
            "tool_choice": "auto",
            "expected_tool": "plan_npc_route",
        },
        {
            "name": "two_step_agent_loop",
            "messages": [
                {
                    "role": "system",
                    "content": (
                        "You control NPC walking. First call plan_npc_route. "
                        "After receiving the planning result, call move_npc_one_step. Do not chat."
                    ),
                },
                {
                    "role": "user",
                    "content": "NPC abigail current=(3,4), target=(4,4), blocked=[]",
                },
            ],
            "tool_choice": "required",
        },
    ]


def create_client(base_url: str) -> OpenAI:
    return OpenAI(base_url=base_url, api_key="lm-studio")


def serialize_tool_calls(message: Any) -> list[dict[str, Any]]:
    tool_calls = getattr(message, "tool_calls", None) or []
    serialized = []
    for tool_call in tool_calls:
        serialized.append(
            {
                "id": tool_call.id,
                "name": tool_call.function.name,
                "arguments": tool_call.function.arguments,
            }
        )
    return serialized


def assistant_tool_message(tool_call: Any) -> dict[str, Any]:
    return {
        "role": "assistant",
        "tool_calls": [
            {
                "id": tool_call.id,
                "type": tool_call.type,
                "function": {
                    "name": tool_call.function.name,
                    "arguments": tool_call.function.arguments,
                },
            }
        ],
    }


def tool_result_message(tool_call_id: str, content: dict[str, Any]) -> dict[str, Any]:
    return {
        "role": "tool",
        "tool_call_id": tool_call_id,
        "content": json.dumps(content, ensure_ascii=False),
    }


def evaluate_single_turn_case(client: OpenAI, model: str, case: dict[str, Any]) -> dict[str, Any]:
    response = client.chat.completions.create(
        model=model,
        temperature=0,
        messages=case["messages"],
        tools=TOOLS,
        tool_choice=case["tool_choice"],
    )
    choice = response.choices[0]
    tool_calls = serialize_tool_calls(choice.message)
    called_names = [tool_call["name"] for tool_call in tool_calls]
    passed = case["expected_tool"] in called_names
    return {
        "name": case["name"],
        "finish_reason": choice.finish_reason,
        "content": choice.message.content,
        "tool_calls": tool_calls,
        "passed": passed,
    }


def evaluate_two_step_agent_loop(client: OpenAI, model: str, case: dict[str, Any]) -> dict[str, Any]:
    first = client.chat.completions.create(
        model=model,
        temperature=0,
        messages=case["messages"],
        tools=TOOLS,
        tool_choice=case["tool_choice"],
    )
    first_choice = first.choices[0]
    first_tool_calls = getattr(first_choice.message, "tool_calls", None) or []
    first_serialized = serialize_tool_calls(first_choice.message)
    plan_call = next((call for call in first_tool_calls if call.function.name == "plan_npc_route"), None)

    if plan_call is None:
        return {
            "name": case["name"],
            "finish_reason": first_choice.finish_reason,
            "content": first_choice.message.content,
            "tool_calls": first_serialized,
            "plan_called": False,
            "move_called": False,
            "passed": False,
        }

    second_messages = case["messages"] + [
        assistant_tool_message(plan_call),
        tool_result_message(plan_call.id, {"npc_id": "abigail", "direction": "right"}),
    ]
    second = client.chat.completions.create(
        model=model,
        temperature=0,
        messages=second_messages,
        tools=TOOLS,
        tool_choice="auto",
    )
    second_choice = second.choices[0]
    second_serialized = serialize_tool_calls(second_choice.message)
    move_called = any(tool_call["name"] == "move_npc_one_step" for tool_call in second_serialized)

    return {
        "name": case["name"],
        "first_finish_reason": first_choice.finish_reason,
        "second_finish_reason": second_choice.finish_reason,
        "content": second_choice.message.content,
        "tool_calls": second_serialized,
        "plan_called": True,
        "move_called": move_called,
        "passed": move_called,
    }


def evaluate_case(client: OpenAI, model: str, case: dict[str, Any]) -> dict[str, Any]:
    if case["name"] == "two_step_agent_loop":
        return evaluate_two_step_agent_loop(client, model, case)
    return evaluate_single_turn_case(client, model, case)


def build_recommendation(cases: list[dict[str, Any]]) -> str:
    forced_ok = any(case["name"] == "forced_single_tool" and case["passed"] for case in cases)
    auto_ok = any(case["name"] == "auto_choose_tool" and case["passed"] for case in cases)
    loop_ok = any(
        case["name"] == "two_step_agent_loop" and case.get("plan_called") and case.get("move_called")
        for case in cases
    )

    if forced_ok and auto_ok and loop_ok:
        return "usable_for_npc_walking"
    if forced_ok:
        return "tool_call_only_not_agentic"
    return "not_ready_for_npc_walking"


def dry_run_payload() -> dict[str, Any]:
    cases = [
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
    return {
        "base_url": DEFAULT_BASE_URL,
        "model": DEFAULT_MODEL,
        "recommendation": build_recommendation(cases),
        "cases": cases,
    }


def connection_error_payload(base_url: str, model: str) -> dict[str, Any]:
    return {
        "base_url": base_url,
        "model": model,
        "recommendation": "server_unreachable",
        "error": "无法连接到 LM Studio 本地服务，请先确认 Local Server 已启动且端口可访问。",
        "cases": [],
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
    try:
        cases = [evaluate_case(client, args.model, case) for case in build_cases()]
    except APIConnectionError:
        print(json.dumps(connection_error_payload(args.base_url, args.model), ensure_ascii=False, indent=2))
        return 0

    payload = {
        "base_url": args.base_url,
        "model": args.model,
        "recommendation": build_recommendation(cases),
        "cases": cases,
    }
    print(json.dumps(payload, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
