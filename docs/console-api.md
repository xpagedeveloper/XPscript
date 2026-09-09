---
id: console-api
title: Console API
type: object
shortDescription: Interactive terminal output, colors, cursor control, keyboard input, redirection detection, and console metadata for XPScript console applications.
order: 45
migration: complete
---

# Console API

XPScript console applications can use the `Console` object for interactive terminal output. The same Markdown file is loaded directly by the Astro documentation site, so this page is available in both the repository documentation and generated Astro docs.

## Output

```vb
Console.Write("Working...")
Console.WriteLine("done")
Console.WriteError("error message")
```

`Console.Write` writes without a newline. `Console.WriteLine` appends a newline. `Console.WriteError` writes a line to stderr.

Compatibility members `Console.Read`, `Console.ReadLine`, `Console.In`, `Console.Out`, and `Console.Error` are also available to generated runtime code and delegate to the platform console implementation.

## Screen and cursor

```vb
Console.Clear()
Console.ClearLine()
Console.SetCursorPosition(0, 5)
Console.CursorLeft = 10
Console.CursorTop = 5
Console.CursorVisible = False
```

`Console.CursorLeft` and `Console.CursorTop` are read/write. `Console.CursorVisible` can be used by progress displays and other interactive terminal interfaces.

Read-only window dimensions are available through `Console.WindowWidth` and `Console.WindowHeight`.

## Colors

```vb
Console.ForegroundColor = "Green"
Console.BackgroundColor = "Black"
Console.WriteLine("Success")
Console.ResetColor()
```

Supported color names are `Black`, `DarkBlue`, `DarkGreen`, `DarkCyan`, `DarkRed`, `DarkMagenta`, `DarkYellow`, `Gray`, `DarkGray`, `Blue`, `Green`, `Cyan`, `Red`, `Magenta`, `Yellow`, and `White`. Color names are case-insensitive.

Invalid color names raise an XPScript runtime error.

## Title

```vb
Console.Title = "My XPScript Application"
Print Console.Title
```

`Console.Title` is read/write.

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

`Console.ReadKey(True)` reads a key without echoing it to the terminal. The returned value exposes `Key`, `Char`, `Control`, `Alt`, and `Shift`.

`Console.KeyAvailable` reports whether a key press is waiting to be read.

## Redirection

Use these properties before terminal-specific operations when the application can participate in shell pipelines:

```vb
Console.IsInputRedirected
Console.IsOutputRedirected
Console.IsErrorRedirected
```

`Console.ClearLine()` returns without modifying the terminal when standard output is redirected.

## Beep

```vb
Console.Beep()
Console.Beep(800, 150)
```

The parameterized form accepts frequency and duration values supported by the current platform. Console capabilities can vary by operating system and terminal.

## Progress output

Cursor positioning, `Console.ClearLine`, and `Console.Write` can update one line without scrolling. This example advances by 10 percent every 0.5 seconds and reaches 100 percent in approximately five seconds:

```vb
Dim row As Integer
row = Console.CursorTop

Dim i As Integer
For i = 0 To 100 Step 10
    Console.SetCursorPosition(0, row)
    Console.ClearLine()
    Console.SetCursorPosition(0, row)
    Console.ForegroundColor = "Green"
    Console.Write("Progress: " & CStr(i) & "%")
    Console.ResetColor()

    If i < 100 Then
        Call Sleep(0.5)
    End If
Next

Console.SetCursorPosition(0, row + 1)
Console.WriteLine("Progress complete")
```

`Sleep`, `Sleep(...)`, and `Call Sleep(...)` statement forms are supported. The sleep value is expressed in seconds.

## Complete test sample

See [`samples/console-api.xps`](../samples/console-api.xps) for an interactive test covering output, stderr, colors, cursor positioning, clearing, cursor visibility, window dimensions, title, redirection, progress output, keyboard input, and beep calls.

Console operations are backed by the platform's .NET console implementation. Applications should check redirection before relying on cursor positioning, colors, or interactive keyboard input. Some terminal operations are platform-dependent.