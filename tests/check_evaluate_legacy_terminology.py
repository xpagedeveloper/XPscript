from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]

SCAN_ROOTS = [
    ROOT / "src",
    ROOT / "docs",
    ROOT / "samples",
    ROOT / "examples",
]
SCAN_FILES = [
    ROOT / "README.md",
]

PATTERNS = [
    (re.compile(r"\bformula[ -]engine\b", re.IGNORECASE), "legacy formula-engine terminology"),
    (re.compile(r"\bDataTable\s*\.\s*Compute\b", re.IGNORECASE), "obsolete DataTable.Compute evaluator"),
    (re.compile(r"\bSystem\s*\.\s*Data\s*\.\s*DataTable\b", re.IGNORECASE), "obsolete System.Data.DataTable evaluator"),
]

TEXT_SUFFIXES = {
    ".cs", ".csproj", ".props", ".targets", ".md", ".txt", ".xps", ".json", ".xml", ".yml", ".yaml"
}


def iter_files():
    for root in SCAN_ROOTS:
        if not root.exists():
            continue
        for path in root.rglob("*"):
            if path.is_file() and path.suffix.lower() in TEXT_SUFFIXES:
                yield path
    for path in SCAN_FILES:
        if path.is_file():
            yield path


def main() -> int:
    violations = []
    for path in iter_files():
        try:
            text = path.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            continue
        for line_number, line in enumerate(text.splitlines(), start=1):
            for pattern, description in PATTERNS:
                if pattern.search(line):
                    violations.append((path.relative_to(ROOT), line_number, description))

    if violations:
        print("Legacy Evaluate terminology/evaluator references found:")
        for path, line_number, description in violations:
            print(f"  {path}:{line_number}: {description}")
        return 1

    print("EVALUATE-LEGACY-TERMINOLOGY=OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
