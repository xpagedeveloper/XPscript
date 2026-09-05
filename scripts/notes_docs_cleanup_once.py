#!/usr/bin/env python3
from pathlib import Path
import re

p = Path('docs/notes-c-api.md')
text = p.read_text(encoding='utf-8')
old = '`ParameterDocID`, `IsNotesAgent`, `IsPublic`, `IsWebAgent`, `IsActivatable`,\n`HasRunSinceModified`, `ProhibitDesignUpdate`, `IsEnabled`, `Trigger`, `Target`,\n`NotesURL`, `HttpURL`, and `OnBehalfOf` are exposed.'
new = '`ParameterDocID`, `IsNotesAgent`, `IsPublic`, `HasRunSinceModified`, `IsEnabled`,\n`Trigger`, `NotesURL`, and `OnBehalfOf` are exposed.'
if old in text:
    text = text.replace(old, new)
text = re.sub(r'^.*`UnLock\(\)`.*\n', '', text, flags=re.M)
for name in ('IsWebAgent', 'IsActivatable', 'ProhibitDesignUpdate', 'Target', 'HttpURL', 'FTSearchScore'):
    if name in text:
        raise SystemExit(f'{name} still present in docs/notes-c-api.md')
p.write_text(text, encoding='utf-8')

# Remove temporary files in the same commit.
Path('.github/workflows/notes-docs-cleanup-once.yml').unlink(missing_ok=True)
Path('scripts/notes_docs_cleanup_once.py').unlink(missing_ok=True)
