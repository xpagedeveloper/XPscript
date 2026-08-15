from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "docs"
REFERENCE = DOCS / "command-reference.md"

errors: list[str] = []


def fail(message: str) -> None:
    errors.append(message)


# Validate all local Markdown links in docs. Ignore web URLs and anchors.
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

    # Repository sample references should be clickable, not bare code-only paths.
    for match in re.finditer(r"`(samples/[^`]+)`", text):
        fail(f"{doc.relative_to(ROOT)}: sample reference must be clickable: {match.group(1)}")

if not REFERENCE.exists():
    fail("docs/command-reference.md is missing")
else:
    lines = REFERENCE.read_text(encoding="utf-8").splitlines()
    header_seen = False
    data_rows = 0
    for lineno, line in enumerate(lines, 1):
        if line.strip() == "| Command | Syntax | Parameters | Description | Example |" or line.strip() == "| Command/property | Syntax | Parameters | Description | Example |" or line.strip() == "| Command/type | Syntax | Parameters | Description | Example |" or line.strip() == "| Command/option | Syntax | Parameters | Description | Example |":
            header_seen = True
            continue
        if not line.startswith("|") or line.startswith("|---"):
            continue
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if len(cells) != 5:
            # Other Markdown tables are allowed only if they are not command-reference rows.
            continue
        if cells[0] in {"Command", "Command/property", "Command/type", "Command/option"}:
            continue
        data_rows += 1
        labels = ("command", "syntax", "parameters", "description", "example")
        for label, cell in zip(labels, cells):
            if not cell:
                fail(f"docs/command-reference.md:{lineno}: empty {label} field")
        if "[" not in cells[4] or "](" not in cells[4]:
            fail(f"docs/command-reference.md:{lineno}: example must be a clickable docs/sample link")

    if not header_seen:
        fail("docs/command-reference.md: no command reference table header found")
    if data_rows < 75:
        fail(f"docs/command-reference.md: expected broad command coverage, found only {data_rows} rows")

if errors:
    print("Documentation validation failed:")
    for error in errors:
        print(f"- {error}")
    raise SystemExit(1)

print("DOCS-REFERENCE=OK")
