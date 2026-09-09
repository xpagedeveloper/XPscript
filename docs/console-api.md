# Console API

XPScript console applications can use the `Console` object for interactive terminal output.

## Output

```vb
Console.Write("Working...")
Console.WriteLine("done")
Console.WriteError("error message")
```

`Console.Write` writes without a newline. `Console.WriteLine` appends a newline. `Console.WriteError` writes a line to stderr.

## Screen and cursor

```vb
Console.Clear()
Console.ClearLine()
Console.SetCursorPosition(0, 5)
Console.CursorLeft = 10
Console.CursorTop = 5
Console.CursorVisible = False
```

Read-only window dimensions are available through `Console.WindowWidth` and `Console.WindowHeight`.

## Colors

```vb
Console.ForegroundColor = "Green"
Console.BackgroundColor = "Black"
Console.WriteLine("Success")
Console.ResetColor()
```

Supported color names are `Black`, `DarkBlue`, `DarkGreen`, `DarkCyan`, `DarkRed`, `DarkMagenta`, `DarkYellow`, `Gray`, `DarkGray`, `Blue`, `Green`, `Cyan`, `Red`, `Magenta`, `Yellow`, and `White`. Color names are case-insensitive.

## Title

```vb
Console.Title = "My XPScript Application"
```

## Keyboard

```vb
Dim key
key = Console.ReadKey(True)
Print key.Key
Print key.Char
Print key.Control
Print key.Alt
Print key.Shift
```

`Console.KeyAvailable` reports whether a key press is waiting to be read.

## Redirection

Use these properties before terminal-specific operations when the application can participate in shell pipelines:

```vb
Console.IsInputRedirected
Console.IsOutputRedirected
Console.IsErrorRedirected
```

## Progress output

Cursor positioning and `Console.Write` can update one line without scrolling:

```vb
Dim row As Integer
row = Console.CursorTop

Dim i As Integer
For i = 0 To 100 Step 10
    Console.SetCursorPosition(0, row)
    Console.ForegroundColor = "Green"
    Console.Write("Progress: " & CStr(i) & "%   ")
    Console.ResetColor()
Next

Console.WriteLine()
```

Console operations are backed by the platform's .NET console implementation. Applications should check redirection before relying on cursor positioning, colors, or interactive keyboard input.
