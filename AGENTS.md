# Repository instructions for coding agents

When changing XPscript's user-visible language, built-ins, runtime classes, constructors, object lifecycle, function signatures, filesystem behavior, XPDB/XPAI APIs, or recommended coding idioms, review `skills/xpscript-programming/SKILL.md` in the same change.

If the change affects how an LLM should write XPscript programs, update that skill in the same PR together with the authoritative documentation and executable regression/sample coverage.

Treat `docs/language-reference.md`, `docs/api-reference.md`, `docs/file-io-reference.md`, database/AI-specific docs, and executable `samples/*.xps` / `demo/**/*.xps` as the source of truth. Do not update the skill with speculative or unimplemented syntax.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

When the user types `/graphify`, invoke the `skill` tool with `skill: "graphify"` before doing anything else.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- Dirty graphify-out/ files are expected after hooks or incremental updates; dirty graph files are not a reason to skip graphify. Only skip graphify if the task is about stale or incorrect graph output, or the user explicitly says not to use it.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
