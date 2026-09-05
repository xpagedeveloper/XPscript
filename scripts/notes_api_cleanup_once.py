#!/usr/bin/env python3
from pathlib import Path
import re

# FTSearchScore has no reliable native score source in the current view-entry
# pipeline. Remove it rather than expose a value that can look authoritative.
v2 = Path('src/XPScript.Compiler/NotesViewNavigationV2PostProcessor.cs')
text = v2.read_text(encoding='utf-8')
needle = r'\n    public int FTSearchScore { get { EnsureAlive(); return 0; } }'
if needle not in text:
    raise SystemExit('FTSearchScore V2 staging property not found')
v2.write_text(text.replace(needle, ''), encoding='utf-8')

v3 = Path('src/XPScript.Compiler/NotesViewNavigationV3PostProcessor.cs')
text = v3.read_text(encoding='utf-8')
old_block = '''        source = ReplaceRequired(
            source,
            "    public int FTSearchScore { get { EnsureAlive(); return 0; } }",
            "    public int FTSearchScore { get { EnsureAlive(); return Row.FTSearchScore; } }\\n    public bool GetRead() => GetRead(Session.Username);\\n    public bool GetRead(object? userNameValue)\\n    {\\n        EnsureAlive();\\n        return !Row.IsDocument || !Session.Api.IsDocumentUnread(Database.Handle, Row.NoteId, XPScriptRuntime.CStr(userNameValue));\\n    }",
            "entry-read");

        source = ReplaceRequired(
            source,
            "    internal XPScriptNotesViewEntryType Type { get; }\\n    internal bool IsDocument => Type == XPScriptNotesViewEntryType.Document;",
            "    internal XPScriptNotesViewEntryType Type { get; }\\n    internal int FTSearchScore { get; set; }\\n    internal bool IsDocument => Type == XPScriptNotesViewEntryType.Document;",
            "row-score");

'''
new_block = '''        source = ReplaceRequired(
            source,
            "    public bool IsConflict { get { EnsureAlive(); return false; } }",
            "    public bool IsConflict { get { EnsureAlive(); return false; } }\\n    public bool GetRead() => GetRead(Session.Username);\\n    public bool GetRead(object? userNameValue)\\n    {\\n        EnsureAlive();\\n        return !Row.IsDocument || !Session.Api.IsDocumentUnread(Database.Handle, Row.NoteId, XPScriptRuntime.CStr(userNameValue));\\n    }",
            "entry-read");

'''
if old_block not in text:
    raise SystemExit('FTSearchScore V3 replacement block not found')
v3.write_text(text.replace(old_block, new_block), encoding='utf-8')

p = Path('samples/notes-view-navigation-v3-domino-runtime-test.xps')
text = p.read_text(encoding='utf-8')
text = re.sub(r'^\s*If entry\.FTSearchScore < 0 Then failures = failures \+ 1\s*\n', '', text, flags=re.M)
p.write_text(text, encoding='utf-8')

p = Path('samples/notes-full-domino-runtime-test.xps')
text = p.read_text(encoding='utf-8')
text = re.sub(r'^\s*Print "FTSearchScore=" & CStr\(entry\.FTSearchScore\)\s*\n', '', text, flags=re.M)
text = re.sub(r'^\s*Print "AgentTarget=" & CStr\(agent\.Target\)\s*\n', '', text, flags=re.M)
p.write_text(text, encoding='utf-8')

p = Path('docs/notes-c-api.md')
text = p.read_text(encoding='utf-8')
old = '`ParameterDocID`, `IsNotesAgent`, `IsPublic`, `IsWebAgent`, `IsActivatable`,\n`HasRunSinceModified`, `ProhibitDesignUpdate`, `IsEnabled`, `Trigger`, `Target`,\n`NotesURL`, `HttpURL`, and `OnBehalfOf` are exposed.'
new = '`ParameterDocID`, `IsNotesAgent`, `IsPublic`, `HasRunSinceModified`, `IsEnabled`,\n`Trigger`, `NotesURL`, and `OnBehalfOf` are exposed.'
if old not in text:
    raise SystemExit('old NotesAgent property list not found in docs')
text = text.replace(old, new)
text = re.sub(r'^.*`UnLock\(\)`.*\n', '', text, flags=re.M)
for name in ('IsWebAgent', 'IsActivatable', 'ProhibitDesignUpdate', 'Target', 'HttpURL', 'FTSearchScore'):
    if name in text:
        raise SystemExit(f'{name} still present in docs/notes-c-api.md')
p.write_text(text, encoding='utf-8')

p = Path('scripts/runtime-placeholder-allowlist.txt')
lines = []
for line in p.read_text(encoding='utf-8').splitlines():
    if any(f'|{name}|' in line for name in ('IsWebAgent','IsActivatable','ProhibitDesignUpdate','Target','HttpURL','FTSearchScore')):
        continue
    lines.append(line)
p.write_text('\n'.join(lines) + '\n', encoding='utf-8')

agent = Path('src/XPScript.Compiler/NotesAgentPostProcessor.cs').read_text(encoding='utf-8')
for name in ('IsWebAgent','IsActivatable','ProhibitDesignUpdate','Target','HttpURL','UnLock'):
    if re.search(r'public\s+[^\n]+\b' + re.escape(name) + r'\b', agent):
        raise SystemExit(f'{name} still exposed in NotesAgentPostProcessor.cs')

# Self-clean so neither the temporary script nor workflow remains in the PR.
Path('.github/workflows/notes-api-cleanup-once.yml').unlink(missing_ok=True)
Path('scripts/notes_api_cleanup_once.py').unlink(missing_ok=True)
