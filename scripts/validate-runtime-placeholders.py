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
METHOD_HEAD = re.compile(
    r"public\s+(?P<static>static\s+)?[A-Za-z_][\w?<>,.\[\] ]*\s+(?P<name>[A-Za-z_]\w*)\s*\([^;{}]*\)\s*(?P<body>=>[^;]+;|\{)",
    re.MULTILINE,
)
CONSTANT_GETTER = re.compile(
    r"get\s*\{\s*(?:Ensure(?:Linked)?Alive\(\);\s*)?return\s+(?:true|false|0|null|\"\"|string\.Empty|LSOperatorArrayRuntime\.CreateArray\(Array\.Empty<object\?>\(\)\)|new\s+XPScriptNotesColorObject\([^;]+,\s*0\))\s*;\s*\}",
    re.DOTALL,
)
EMPTY_SETTER = re.compile(
    r"set\s*\{\s*(?:Ensure(?:Linked)?Alive\(\);\s*)?\}",
    re.DOTALL,
)
UNSUPPORTED_METHOD = re.compile(
    r"(?:throw\s+(?:RichTextStructuralWriteNotSupported|UnsupportedWrite)\s*\(|=>\s*throw\s+(?:RichTextStructuralWriteNotSupported|UnsupportedWrite)\s*\()",
    re.DOTALL,
)
RICH_TEXT_FILE = re.compile(r"NotesRichText|NotesEmbeddedObject")


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


def block_body(text: str, brace_index: int) -> str | None:
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


def allow_or_violate(relative: str, name: str, reason: str, line: int, allowlist: dict[str, str], seen: set[str], violations: list[str]) -> None:
    key = f"{relative}::{name}"
    if key in allowlist:
        seen.add(key)
        return
    violations.append(f"{relative}:{line}: {name}: {reason}")


def main() -> int:
    allowlist = load_allowlist()
    seen_allowlist: set[str] = set()
    violations: list[str] = []

    for path in sorted(SOURCE_ROOT.rglob("*.cs")):
        text = path.read_text(encoding="utf-8")
        relative = path.relative_to(ROOT).as_posix()
        is_rich_text = RICH_TEXT_FILE.search(path.name) is not None

        for match in PROPERTY_HEAD.finditer(text):
            body = block_body(text, text.find("{", match.start()))
            if body is None:
                continue
            reasons: list[str] = []
            if EMPTY_SETTER.search(body):
                reasons.append("empty setter")
            if CONSTANT_GETTER.search(body):
                reasons.append("constant/empty getter")
            if match.group(0).startswith("public static") and is_rich_text:
                reasons.append("public static rich-text property")
            if not reasons:
                continue
            line = text.count("\n", 0, match.start()) + 1
            allow_or_violate(relative, match.group("name"), ", ".join(reasons), line, allowlist, seen_allowlist, violations)

        if not is_rich_text:
            continue
        for match in METHOD_HEAD.finditer(text):
            body_start = match.start("body")
            if match.group("body") == "{":
                body = block_body(text, body_start)
            else:
                semicolon = text.find(";", body_start)
                body = text[body_start : semicolon + 1] if semicolon >= 0 else None
            if body is None:
                continue
            reasons: list[str] = []
            if match.group("static"):
                reasons.append("public static rich-text method")
            if UNSUPPORTED_METHOD.search(body):
                reasons.append("public method only reports unsupported")
            if not reasons:
                continue
            line = text.count("\n", 0, match.start()) + 1
            allow_or_violate(relative, match.group("name"), ", ".join(reasons), line, allowlist, seen_allowlist, violations)

    stale = sorted(set(allowlist) - seen_allowlist)
    if stale:
        violations.extend(f"stale allowlist entry: {entry}" for entry in stale)

    if violations:
        print("Runtime placeholder guard failed:")
        for violation in violations:
            print(f"  - {violation}")
        print("\nImplement/remove the member or add a narrowly-scoped allowlist entry with a concrete compatibility reason.")
        return 1

    print(f"Runtime placeholder guard passed ({len(seen_allowlist)} explicit allowlist entries checked).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
