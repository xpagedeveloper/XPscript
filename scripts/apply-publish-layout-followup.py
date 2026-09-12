from pathlib import Path


def replace(path, old, new):
    p = Path(path)
    text = p.read_text(encoding='utf-8')
    if text.count(old) != 1:
        raise SystemExit(f'{path}: expected exactly one match')
    p.write_text(text.replace(old, new, 1), encoding='utf-8')

replace(
    'src/XPScript.Compiler/CompilePublishLayoutContext.cs',
'''    private sealed class Scope(Settings? previous) : IDisposable
    {
        private Settings? previousValue = previous;

        public void Dispose()
        {
            if (previousValue is null && CurrentSettings.Value is null) return;
            CurrentSettings.Value = previousValue;
            previousValue = null;
        }
    }
''',
'''    private sealed class Scope(Settings? previous) : IDisposable
    {
        private readonly Settings? previousValue = previous;
        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            CurrentSettings.Value = previousValue;
        }
    }
''')

path = 'src/XPScript.Compiler/CompilerDriver.cs'
replace(path,
'''                publishSingleFile: CompilePublishLayoutContext.IsConfigured ? CompilePublishLayoutContext.SingleFile : true,
                usesMimeKit: source.Contains("NotesMIMEEntity", StringComparison.Ordinal));''',
'''                publishSingleFile: CompilePublishLayoutContext.IsConfigured ? CompilePublishLayoutContext.SingleFile : true,
                usesMimeKit: source.Contains("NotesMIMEEntity", StringComparison.Ordinal),
                assemblyName: OutputAssemblyName(outputPath));''')
replace(path,
'''                publishSingleFile: false,
                usesMimeKit: source.Contains("NotesMIMEEntity", StringComparison.Ordinal));''',
'''                publishSingleFile: false,
                usesMimeKit: source.Contains("NotesMIMEEntity", StringComparison.Ordinal),
                assemblyName: "Generated");''')
replace(path,
'''        IReadOnlyList<StagedManagedReference> references,
        bool publishSingleFile,
        bool usesMimeKit)
''',
'''        IReadOnlyList<StagedManagedReference> references,
        bool publishSingleFile,
        bool usesMimeKit,
        string assemblyName)
''')
replace(path,
'''    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>''',
'''    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>{EscapeXml(assemblyName)}</AssemblyName>
    <ImplicitUsings>enable</ImplicitUsings>''')
replace(path,
'''    private static string EscapeXml(string value) => value
''',
'''    private static string OutputAssemblyName(string outputPath)
    {
        var name = Path.GetFileNameWithoutExtension(outputPath);
        return string.IsNullOrWhiteSpace(name) ? "XPScriptApp" : name;
    }

    private static string EscapeXml(string value) => value
''')

print('Publish layout follow-up applied.')
