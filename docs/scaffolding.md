# Project scaffolding

`xpscript new` creates a minimal XPscript starter project.

A target directory is always required. Use `.` to generate the starter in the current directory.

```text
xpscript new rest <directory>
xpscript new web <directory>
xpscript new desktop <directory>
```

Examples:

```text
xpscript new rest ./myapi
xpscript new web ./mysite
xpscript new desktop ./myapp
xpscript new rest .
```

If the target directory does not exist, XPscript creates it. The generator never overwrites an existing generated entry file.

## REST

```text
xpscript new rest ./myapi
```

creates `myapi/index.xps` with an anonymous `/api/health` endpoint using the compact REST route syntax.

Run it with:

```text
xpscript web ./myapi
```

## Web

```text
xpscript new web ./mysite
```

creates `mysite/index.xps` with a minimal anonymous GET page.

Run it with:

```text
xpscript web ./mysite
```

## Desktop

```text
xpscript new desktop ./myapp
```

creates `myapp/main.xps` with a minimal `UIForm` desktop application.

Run it with:

```text
xpscript run ./myapp/main.xps
```

## Existing files

REST and web scaffolds refuse to overwrite an existing `index.xps`. Desktop scaffolds refuse to overwrite an existing `main.xps`. Choose another directory or move the existing file before running `xpscript new` again.
