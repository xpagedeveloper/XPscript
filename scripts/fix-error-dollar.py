from pathlib import Path
p=Path('src/XPScript.Compiler/CoreCompatibilityTranspiler.cs')
s=p.read_text()
old='''        line = Regex.Replace(line, @"(?<![\\w.])Error\\$?\\b(?!\\s*\\()", "XPScriptErrorRuntime.Error()", RegexOptions.IgnoreCase);'''
new='''        line = Regex.Replace(line, @"(?<![\\w.])(?:Error\\$(?![\\w])|Error\\b)(?!\\s*\\()", "XPScriptErrorRuntime.Error()", RegexOptions.IgnoreCase);'''
if old in s:
    s=s.replace(old,new,1)
elif new not in s:
    raise SystemExit('Error$ rewrite marker not found')
p.write_text(s)
print('Error$ rewrite fixed')
