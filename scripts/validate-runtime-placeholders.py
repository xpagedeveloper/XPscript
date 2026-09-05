#!/usr/bin/env python3
from __future__ import annotations

import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
SOURCE_ROOT = ROOT / "src" / "XPScript.Compiler"
ALLOWLIST = ROOT / "scripts" / "runtime-placeholder-allowlist.txt"

PROPERTY_HEAD = re.compile(
    r"public\s+(?:static\s+)?[A-Za-z_][\w?<>,.\[\] ]*\s+(?P<name>[A-Za-z_]\w*)\s*\{",
    re.MULTILINE,
)
CONSTANT_GETTER = re.compile(
    r"get\s*\{\s*(?:EnsureAlive\(\);\s*)?return\s+(?:true|false|0|null|\"\"|string\.Empty)\s*;\s*\}",
    re.DOTALL,
)
EMPTY_SETTER = re.compile(
    r"set\s*\{\s*(?:EnsureAlive\(\);\s*)?\}",
    re.DOTALL,
)


def load_allowlist() -> dict[str, str]:
    result: dict[str, str] = {}
    for line_number, raw in enumerate(ALLOWLIST.read_text(encoding="utf-8").splitlines(), 1):
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        parts = [part.strip() for part in line.split("|", 2)]
        if len(parts) != 3 or not parts[2]:
            raise SystemExit(f"Invalid allowlist entry at line {line_number}: expected path|member|reason")
        result[f"{parts[0]}::{parts[1]}"] = parts[2]
    return result


def property_body(text: str, brace_index: int) -> str | None:
    depth = 0
    in_string = False
    verbatim = False
    escaped = False
    i = brace_index
    while i < len(text):
        ch = text[i]
        if in_string:
            if verbatim:
                if ch == '"' and i + 1 < len(text) and text[i + 1] == '"':
                    i += 2
                    continue
                if ch == '"':
                    in_string = False
                    verbatim = False
            else:
                if escaped:
                    escaped = False
                elif ch == '\\':
                    escaped = True
                elif ch == '"':
                    in_string = False
            i += 1
            continue
        if ch == '"':
            in_string = True
            verbatim = i > 0 and text[i - 1] == '@'
            i += 1
            continue
        if ch == '{':
            depth += 1
        elif ch == '}':
            depth -= 1
            if depth == 0:
                return text[brace_index : i + 1]
        i += 1
    return None


def main() -> int:
    allowlist = load_allowlist()
    seen_allowlist: set[str] = set()
    violations: list[str] = []

    for path in sorted(SOURCE_ROOT.rglob("*.cs")):
        text = path.read_text(encoding="utf-8")
        relative = path.relative_to(ROOT).as_posix()
        for match in PROPERTY_HEAD.finditer(text):
            body = property_body(text, text.find("{", match.start()))
            if body is None:
                continue
            reasons: list[str] = []
            if EMPTY_SETTER.search(body):
                reasons.append("empty setter")
            if CONSTANT_GETTER.search(body):
                reasons.append("constant getter")
            if not reasons:
                continue
            key = f"{relative}::{match.group('name')}"
            if key in allowlist:
                seen_allowlist.add(key)
                continue
            line = text.count("\n", 0, match.start()) + 1
            violations.append(f"{relative}:{line}: {match.group('name')}: {', '.join(reasons)}")

    stale = sorted(set(allowlist) - seen_allowlist)
    if stale:
        violations.extend(f"stale allowlist entry: {entry}" for entry in stale)

    if violations:
        print("Runtime placeholder guard failed:")
        for violation in violations:
            print(f"  - {violation}")
        print("\nImplement the member or add a narrowly-scoped allowlist entry with a concrete reason.")
        return 1

    print(f"Runtime placeholder guard passed ({len(seen_allowlist)} explicit allowlist entries checked).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
