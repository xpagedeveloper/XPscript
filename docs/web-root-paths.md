# Web root paths

`xpscript web` and `xpscript fastcgi` accept absolute or relative web root directories through `--root`.

Examples:

```text
xpscript web --root ./site
xpscript web --root "D:\VisualStudio\VisualStudio git\xpscript\web"
xpscript web --root "D:\VisualStudio\VisualStudio git\xpscript\web\"
```

A trailing directory separator is optional. The CLI normalizes the root before starting the host.

Shell quoting is supported. Normal command shells remove surrounding quotes before invoking XPScript. The CLI also tolerates literal matching quote characters around the path when an external wrapper passes them through.

If the selected root does not exist, XPScript prints the normalized path. It also checks the parent directory for sibling folders whose names share the requested prefix. When matches exist, the error includes up to three `Did you mean` suggestions.

Example:

```text
error: Web root does not exist: D:\sites\webb
Did you mean: D:\sites\web
```

The suggestion is diagnostic only. XPScript never silently changes the configured web root.
