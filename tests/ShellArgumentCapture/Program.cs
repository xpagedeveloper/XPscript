if (args.Length < 1)
    throw new ArgumentException("ShellArgumentCapture requires an output path.");

var outputPath = Path.GetFullPath(args[0]);
var values = args.Skip(1).ToArray();
var lines = new List<string> { "COUNT=" + values.Length };
for (var i = 0; i < values.Length; i++)
    lines.Add("ARG" + i + "=" + values[i]);

File.WriteAllLines(outputPath, lines, new System.Text.UTF8Encoding(false));
