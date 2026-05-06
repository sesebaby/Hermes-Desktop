# Skill Content Cleanup Manual Review

## Scope
- Manual one-by-one review of bundled skills exposed in the Desktop Skills UI.
- Record only user-approved decisions.
- Do not delete files until a later execution pass.

## Status
- Review in progress.
- File/system changes for skill deletion: not started.

## Reviewed Decisions

| Order | Skill | Decision | Reason |
|---|---|---|---|
| 1 | `algorithmic-art` | Delete approved | Generative coding art skill; not part of NPC life/runtime foundation. |
| 2 | `apple-notes` | Delete approved | Apple Notes/macOS integration; external personal productivity surface. |
| 3 | `apple-reminders` | Delete approved | Apple Reminders/macOS integration; external reminder app, not retained internal todo/cron runtime. |
| 4 | `arxiv` | Delete approved | Academic paper research assistant skill; outside current game runtime goal. |
| 5 | `ascii-art` | Delete approved | Terminal character-art/novelty skill; not a retained NPC/media foundation. |
| 6 | `ascii-video` | Retain approved | User explicitly wants to keep it for future use. |
| 7 | `audiocraft-audio-generation` | Retain approved | Audio generation can support future NPC voice / sound generation use. |
| 8 | `claude-code` | Delete approved | External coding-agent delegation skill; not part of retained NPC runtime. |
| 9 | `axolotl` | Delete approved | Fine-tuning/training workflow skill; not part of current NPC runtime surface. |
| 10 | `blogwatcher` | Delete approved | External RSS/blog monitoring skill; outside current game runtime goal. |
| 11 | `canvas-design` | Retain approved | Can be used to generate NPC cards and small images for players. |
| 12 | `code-review` | Delete approved | Development-only code review workflow; not part of retained NPC runtime. |
| 13 | `commit` | Delete approved | Development-only git commit workflow; not part of retained NPC runtime. |
| 14 | `commit-push-pr` | Delete approved | Development-only commit/push/PR workflow; not part of retained NPC runtime. |
| 15 | `codex` | Delete approved | External coding-agent delegation skill; not part of retained NPC runtime. |
| 16 | `documentation` | Delete approved | Development-only documentation workflow; not part of retained NPC runtime. |
| 17 | `docx` | Delete approved | Office document workflow; not part of retained NPC runtime. |
| 18 | `dogfood` | Delete approved | Web application QA testing workflow; not part of retained NPC runtime. |
| 19 | `himalaya` | Delete approved | External email workflow; not part of retained NPC runtime. |
| 20 | `minecraft-modpack-server` | Delete approved | Minecraft server setup workflow; game-adjacent but not part of retained NPC runtime. |
| 21 | `pokemon-player` | Defer approved | Game-internal agent behavior template; not current target, but closer to future NPC runtime than generic external tools. |
| 22 | `github-auth` | Delete approved | GitHub authentication workflow; development infrastructure, not part of retained NPC runtime. |
| 23 | `github-code-review` | Delete approved | GitHub PR/code review workflow; development infrastructure, not part of retained NPC runtime. |
| 24 | `github-issues` | Delete approved | GitHub issue/project-management workflow; development infrastructure, not part of retained NPC runtime. |
| 25 | `github-pr-workflow` | Delete approved | GitHub PR/CI/merge workflow; development infrastructure, not part of retained NPC runtime. |
| 26 | `github-repo-management` | Delete approved | GitHub repository/release/workflow management; development infrastructure, not part of retained NPC runtime. |
| 27 | `google-workspace` | Delete approved | External Google productivity suite integration; not part of retained NPC runtime. |
| 28 | `linear` | Delete approved | Linear issue/project management workflow; development infrastructure, not part of retained NPC runtime. |
| 29 | `llama-cpp` | Delete approved | Local LLM inference/runtime path, but user explicitly does not want NPCs operating this kind of model-serving/inference skill directly. |
| 30 | `obliteratus` | Delete approved | Open-weight model guardrail/refusal removal and model surgery workflow; not part of retained NPC runtime foundation. |
| 31 | `outlines` | Retain approved | Structured generation can help NPC/agent output stable, parseable JSON/state/action results for runtime use. |
| 32 | `findmy` | Delete approved | Apple device/AirTag location tracking via FindMy; real-world Apple utility, not part of retained NPC runtime. |
| 33 | `imessage` | Delete approved | Apple Messages/iMessage/SMS integration on macOS; external real-world messaging channel, not part of retained NPC runtime. |
| 34 | `hermes-agent` | Retain approved | Core Hermes agent operating/extension guide covering multi-agent, memory, cron, tools, MCP, profiles, and gateway capabilities aligned with retained runtime goals. |
| 35 | `opencode` | Defer approved | External coding-agent integration is not current runtime core, but may later be adapted as a delegated sub-agent path for certain NPC tasks. |
| 36 | `frontend-design` | Delete approved | General frontend/UI development workflow for coding tasks; development-time helper, not part of retained NPC runtime foundation. |
| 37 | `mcp-builder` | Retain approved | MCP server construction guidance is closely tied to the retained MCP framework and future runtime/tool capability expansion. |
| 38 | `pdf` | Delete approved | Generic PDF document manipulation workflow; office/document utility, not part of retained NPC runtime. |
| 39 | `plan` | Delete approved | Development planning workflow; useful for implementation prep, but not part of retained NPC runtime itself. |
| 40 | `pptx` | Delete approved | PowerPoint presentation creation/editing workflow; office/deck utility, not part of retained NPC runtime. |
| 41 | `refactor` | Delete approved | Safe code refactoring workflow; development-time engineering aid, not part of retained NPC runtime. |
| 42 | `security-audit` | Delete approved | Development-time security review workflow; not part of retained NPC runtime. |
| 43 | `simplify` | Delete approved | Code simplification/reuse review workflow; development-time engineering aid, not part of retained NPC runtime. |
| 44 | `skill-creator` | Retain approved | User considers skill creation part of the self-evolution core: the system must be able to accumulate and author new reusable skills over time. |
| 45 | `systematic-debugging` | Delete approved | Structured bug investigation workflow for development; not part of retained NPC runtime. |
| 46 | `test-driven-development` | Delete approved | TDD development workflow; coding-process aid, not part of retained NPC runtime. |
| 47 | `webapp-testing` | Delete approved | Web application automated testing workflow; development-time QA aid, not part of retained NPC runtime. |
| 48 | `xlsx` | Delete approved | Spreadsheet/Excel manipulation workflow; office/data-table utility, not part of retained NPC runtime. |
| 49 | `excalidraw` | Delete approved | Diagram/flowchart creation workflow; documentation/communication aid, not part of retained NPC runtime. |
| 50 | `songwriting-and-ai-music` | Defer approved | Music/song creation is adjacent to retained audio expression, but not clearly part of the current NPC runtime core. |
| 51 | `jupyter-live-kernel` | Delete approved | Stateful Jupyter/Python experimentation workflow; research/development tool, not part of retained NPC runtime. |
| 52 | `webhook-subscriptions` | Defer approved | External event push can become a bridge from game/backend systems into agent activation, but it is not yet confirmed as core runtime. |
| 53 | `codebase-inspection` | Delete approved | Repository size/LOC/language breakdown analysis workflow; development analytics aid, not part of retained NPC runtime. |
| 54 | `find-nearby` | Delete approved | Nearby-place discovery workflow; location utility, not part of retained NPC runtime. |
| 55 | `mcporter` | Defer approved | MCP CLI bridge is close to the retained MCP framework, but it is an operator/tooling layer rather than core runtime itself. |
| 56 | `native-mcp` | Retain approved | Built-in MCP client is part of the retained MCP framework/core external capability injection path. |
| 57 | `gif-search` | Delete approved | GIF search/download utility for chat/media reactions; peripheral media helper, not part of retained NPC runtime. |
| 58 | `heartmula` | Defer approved | Full-song/music generation is adjacent to retained audio expression, but not clearly part of the current NPC runtime core. |
| 59 | `songsee` | Delete approved | Audio spectrogram/feature-visualization workflow; audio analysis/debugging aid, not part of retained NPC runtime core. |
| 60 | `youtube-content` | Delete approved | YouTube transcript extraction and content repackaging workflow; external content-processing utility, not part of retained NPC runtime. |
| 61 | `modal-serverless-gpu` | Delete approved | Cloud serverless GPU deployment path; optional infrastructure backend, not part of retained NPC runtime core. |
| 62 | `evaluating-llms-harness` | Delete approved | Academic/engineering LLM benchmark evaluation workflow; model assessment tool, not part of retained NPC runtime. |
| 63 | `weights-and-biases` | Delete approved | ML experiment tracking / model training operations platform; research/training infrastructure, not part of retained NPC runtime. |
| 64 | `huggingface-hub` | Delete approved | Model/dataset hub operations workflow; external model-repository tooling, not part of retained NPC runtime core. |
| 65 | `gguf-quantization` | Delete approved | Local-model quantization/packing workflow tied to deployment preparation, not part of retained NPC runtime itself. |
| 66 | `guidance` | Defer approved | Constrained generation / structured-control framework is close to runtime output control and overlaps partly with retained structured-generation needs. |
| 67 | `serving-llms-vllm` | Delete approved | High-throughput LLM service deployment workflow; model-serving infrastructure, not part of retained NPC runtime itself. |
| 68 | `clip` | Delete approved | Vision-language model workflow, but user chose not to retain this specific visual-model path as part of current NPC runtime. |
| 69 | `segment-anything-model` | Defer approved | Image segmentation is a concrete visual-operation path that may matter later for runtime perception, but is not yet confirmed as core. |
| 70 | `stable-diffusion-image-generation` | Retain approved | User wants to keep direct image generation capability for NPC/player-facing visual output. |
| 71 | `whisper` | Retain approved | Speech-to-text / listening capability is directly relevant to future NPC voice interaction and runtime perception. |
| 72 | `dspy` | Delete approved | Declarative LM/RAG/agent engineering framework; research/build methodology, not part of the retained NPC runtime core. |
| 73 | `grpo-rl-training` | Delete approved | RL/GRPO post-training workflow for model improvement; training pipeline capability, not part of retained NPC runtime. |
| 74 | `peft-fine-tuning` | Delete approved | Parameter-efficient fine-tuning workflow; model training/adaptation capability, not part of retained NPC runtime itself. |
| 75 | `pytorch-fsdp` | Delete approved | Distributed large-model training infrastructure; training backend, not part of retained NPC runtime itself. |
| 76 | `fine-tuning-with-trl` | Delete approved | Full TRL post-training/RLHF workflow; model alignment/training pipeline, not part of retained NPC runtime itself. |
| 77 | `unsloth` | Delete approved | Fast fine-tuning/training acceleration workflow; training optimization tooling, not part of retained NPC runtime itself. |
| 78 | `obsidian` | Delete approved | External Obsidian vault note-taking integration; personal knowledge-base tooling, not the retained in-runtime memory/soul system itself. |
| 79 | `notion` | Delete approved | External Notion workspace/page/database integration; productivity/knowledge-base tooling, not the retained in-runtime memory/soul system itself. |
| 80 | `nano-pdf` | Delete approved | Natural-language PDF editing utility for office/document changes; generic document tooling, not part of retained NPC runtime capabilities. |
| 81 | `ocr-and-documents` | Delete approved | PDF/OCR/document text extraction workflow for real-world documents; adjacent to perception, but positioned as general document-processing tooling rather than retained NPC runtime core. |
| 82 | `powerpoint` | Delete approved | Full PowerPoint/PPTX creation, editing, and visual QA workflow; office/presentation tooling, not part of retained NPC runtime capabilities. |
| 83 | `openhue` | Delete approved | Philips Hue smart-home light control integration for real-world devices; external IoT/home automation, not part of retained NPC runtime capabilities. |
| 84 | `polymarket` | Delete approved | External prediction-market data lookup for real-world event probabilities; finance/news-adjacent query tooling, not part of retained NPC runtime capabilities. |
| 85 | `subagent-driven-development` | Retain approved | Fresh-subagent-per-task orchestration with two-stage review; directly aligned with the explicitly preserved subagent/AgentTool coordination capability. |
| 86 | `godmode` | Retain approved | User explicitly chose to keep this capability. |
| 87 | `writing-plans` | Retain approved | Upstream planning skill that pairs directly with retained subagent-driven-development; preserves the plan-then-delegate workflow chain. |
| 88 | `requesting-code-review` | Delete approved | Development-time review/quality-gate workflow; useful for engineering governance, but not part of retained NPC runtime capabilities. |
| 89 | `research-paper-writing` | Delete approved | End-to-end ML/AI research paper production pipeline; academic writing and submission workflow, not part of retained NPC runtime capabilities. |
| 90 | `xitter` | Delete approved | External X/Twitter API integration; social-media tooling, not part of retained NPC runtime capabilities. |

## Execution Rule
- No file deletion until the user finishes manual review and explicitly asks for execution.
