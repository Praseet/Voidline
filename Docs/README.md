# VOIDLINE Documentation Index

Use `VOIDLINE_IMPLEMENTATION_ROADMAP.md` for current phase, next tasks, dependencies, and acceptance gates.

| Document | Authority and purpose | Update rule |
|---|---|---|
| `VOIDLINE_IMPLEMENTATION_ROADMAP.md` | Canonical broad/detailed phases and acceptance criteria | Update when phase scope, order, or exit gates change |
| `VOIDLINE_CREATIVE_BIBLE.md` | Product identity, design pillars, aesthetic, route/creature design intent | Change only when design intent changes; distinguish proposal from implementation |
| `VOIDLINE_GAMEPLAY_SPEC.md` | Current gameplay rules, controls, scoring and provisional tunables | Keep factual; link upcoming route changes to the roadmap |
| `TECHNICAL_ARCHITECTURE.md` | Code responsibilities, asset paths, engineering guardrails and known debt | Update after ownership or runtime architecture changes |
| `ASSET_MANIFEST.md` | Third-party provenance, licenses and project-owned content | Add every new imported/created production asset |
| `VOIDLINE_RESEARCH_PLAYBOOK.md` | External references, rationale, design/technical research and historical observations | Preserve useful sources; do not use it as a task backlog |
| `QA_CHECKLIST.md` | Observed test results, open checks, build/Editor status | Only mark passed when actually observed; date findings |
| `AGENT_SKILLS_WORKFLOW.md` | Installed skill/tool guidance and how it was applied to this project | Update when workflow or relevant skill use changes |

## Documentation rules
- The roadmap is the current execution plan; older dated notes are historical evidence, not permission to keep following superseded priorities.
- The Creative Bible decides what the game should feel like; the Technical Architecture decides which component owns behavior.
- QA status is authoritative for verification. Source edits or successful compilation do not imply runtime/build success.
- Keep root `AGENTS.md` and `CLAUDE.md`: they contain required Unity/KitWright workflow guidance. Keep `Assets/VOIDLINE/Art/Models/README.md`: it describes project-owned art assets.
