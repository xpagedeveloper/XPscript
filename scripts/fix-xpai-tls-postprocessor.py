from pathlib import Path
p=Path('src/XPScript.Compiler/AiSessionRuntimePostProcessor.cs')
s=p.read_text()
old='''        var requestJson = BuildRequest(messagesValue, stream, modelValue);
        var requestText = requestJson.ToJsonString();
        if (System.Text.Encoding.UTF8.GetByteCount(requestText) > MaxRequestBytes)
            throw new XPScriptRuntimeException(5, "XPAi request body exceeds the 8 MiB limit.");

        var cancellation = BeginRequest();
'''
new='''        var requestJson = BuildRequest(messagesValue, stream, modelValue);
        var requestText = requestJson.ToJsonString();
        if (System.Text.Encoding.UTF8.GetByteCount(requestText) > MaxRequestBytes)
            throw new XPScriptRuntimeException(5, "XPAi request body exceeds the 8 MiB limit.");

        _tls.Reset();
        var cancellation = BeginRequest();
'''
if old in s: s=s.replace(old,new,1)
elif new not in s: raise SystemExit('original tool-loop marker missing')
old2='''        var requestJson = BuildRequest(messagesValue, stream, modelValue);
        var cancellation = BeginRequest();
        try
'''
new2='''        var requestJson = BuildRequest(messagesValue, stream, modelValue);
        _tls.Reset();
        var cancellation = BeginRequest();
        try
'''
if old2 in s: s=s.replace(old2,new2,1)
elif new2 not in s: raise SystemExit('replacement tool-loop marker missing')
p.write_text(s)
