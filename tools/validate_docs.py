from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "docs"
REFERENCE = DOCS / "commands.md"
REQUIRED_DOCS = {
    "index.md",
    "getting-started.md",
    "language.md",
    "commands.md",
    "command-examples.md",
    "evaluate.md",
    "classes.md",
    "web.md",
    "uiform.md",
    "documentation-rules.md",
}

errors: list[str] = []


def fail(message: str) -> None:
    errors.append(message)


for required in sorted(REQUIRED_DOCS):
    if not (DOCS / required).exists():
        fail(f"docs/{required} is missing")

link_re = re.compile(r"\[[^\]]+\]\(([^)]+)\)")
for doc in sorted(DOCS.glob("*.md")):
    text = doc.read_text(encoding="utf-8")
    for target in link_re.findall(text):
        if target.startswith(("http://", "https://", "#", "mailto:")):
            continue
        relative = target.split("#", 1)[0]
        if not relative:
            continue
        resolved = (doc.parent / relative).resolve()
        try:
            resolved.relative_to(ROOT.resolve())
        except ValueError:
            fail(f"{doc.relative_to(ROOT)}: link escapes repository: {target}")
            continue
        if not resolved.exists():
            fail(f"{doc.relative_to(ROOT)}: broken local link: {target}")

    for match in re.finditer(r"`(samples/[^`]+)`", text):
        fail(f"{doc.relative_to(ROOT)}: sample reference must be clickable: {match.group(1)}")

if REFERENCE.exists():
    lines = REFERENCE.read_text(encoding="utf-8").splitlines()
    header_seen = False
    data_rows = 0
    accepted_headers = {
        "| Command | Syntax | Parameters | Description | Example |",
        "| Command/property | Syntax | Parameters | Description | Example |",
        "| Command/option | Syntax | Parameters | Description | Example |",
    }
    for lineno, line in enumerate(lines, 1):
        if line.strip() in accepted_headers:
            header_seen = True
            continue
        if not line.startswith("|") or line.startswith("|---"):
            continue
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if len(cells) != 5:
            continue
        if cells[0] in {"Command", "Command/property", "Command/option"}:
            continue
        data_rows += 1
        for label, cell in zip(("command", "syntax", "parameters", "description", "example"), cells):
            if not cell:
                fail(f"docs/commands.md:{lineno}: empty {label} field")
        if "[" not in cells[4] or "](" not in cells[4]:
            fail(f"docs/commands.md:{lineno}: example must be a clickable sample link")

    if not header_seen:
        fail("docs/commands.md: no command reference table header found")
    if data_rows < 75:
        fail(f"docs/commands.md: expected broad command coverage, found only {data_rows} rows")

rules = DOCS / "documentation-rules.md"
if rules.exists():
    text = rules.read_text(encoding="utf-8")
    for required in ("new command", "executable", "English", "commands.md"):
        if required.lower() not in text.lower():
            fail(f"docs/documentation-rules.md: missing documentation policy concept: {required}")

if errors:
    print("Documentation validation failed:")
    for error in errors:
        print(f"- {error}")
    raise SystemExit(1)

print("DOCS-REFERENCE=OK")
