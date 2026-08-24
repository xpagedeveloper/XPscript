# Repository instructions for coding agents

When changing XPscript's user-visible language, built-ins, runtime classes, constructors, object lifecycle, function signatures, filesystem behavior, XPDB/XPAI APIs, or recommended coding idioms, review `skills/xpscript-programming/SKILL.md` in the same change.

If the change affects how an LLM should write XPscript programs, update that skill in the same PR together with the authoritative documentation and executable regression/sample coverage.

Treat `docs/language-reference.md`, `docs/api-reference.md`, `docs/file-io-reference.md`, database/AI-specific docs, and executable `samples/*.xps` / `demo/**/*.xps` as the source of truth. Do not update the skill with speculative or unimplemented syntax.
