from pathlib import Path

runtime = Path('todo/runtime-reference-todo.md')
text = runtime.read_text(encoding='utf-8')
ui_marker = '## 18. Cross-platform UI extension inventory'
if ui_marker not in text:
    raise SystemExit('UI section marker not found')
web_section = '''## 18. Web runtime and server\n\n- [ ] Implement only after the existing compiler/language/runtime backlog is complete and stable.\n- [ ] Complete the architecture/security review before production implementation.\n- [ ] Provide shared XPScript web runtime semantics for standalone Kestrel and FastCGI hosting.\n- [ ] Detailed architecture, object model, runtime compilation/cache, routing, FastCGI and security checklist: `todo/web-runtime-server-todo.md`.\n- [ ] Follow dependency-reuse rules in `todo/development-guidelines.md`; prefer ASP.NET Core/.NET and vetted maintained NuGet packages over custom low-level protocol/parser implementations where suitable.\n- [ ] This section must be completed before the cross-platform UI extension work begins.\n\n'''
text = text.replace(ui_marker, web_section + '## 19. Cross-platform UI extension inventory', 1)
head, sep, tail = text.partition('## 19. Cross-platform UI extension inventory')
if not sep:
    raise SystemExit('Renumbered UI section not found')
tail = tail.replace('### 18.', '### 19.')
runtime.write_text(head + sep + tail, encoding='utf-8')

web = Path('todo/web-runtime-server-todo.md')
wtext = web.read_text(encoding='utf-8')
needle = '> **Priority / sequencing:** This is a future major feature and must be implemented only after the existing compiler/language/runtime TODOs are complete and stable. Do not start implementation directly from this document. Perform a dedicated architecture/security review first, refine this specification, then create an implementation plan and regression matrix before writing production code.\n'
addition = needle + '\n> **Dependency policy:** Follow `todo/development-guidelines.md`. Prefer existing .NET/ASP.NET Core functionality and vetted, maintained NuGet packages where they safely satisfy requirements. In particular, investigate suitable maintained FastCGI packages before writing a custom protocol parser.\n'
if needle not in wtext:
    raise SystemExit('Web priority marker not found')
wtext = wtext.replace(needle, addition, 1)
web.write_text(wtext, encoding='utf-8')
