# XPScript documentation site

This directory contains the static documentation site for the Markdown sources in `../docs`.

The site is intentionally separate from the compiler source tree. Existing Markdown remains authoritative while the documentation is migrated to structured frontmatter and the new hierarchy.

## Commands

```text
npm install
npm run docs:audit
npm run dev
npm run build
npm run docs:links
npm run preview
```

Use JSON output when a machine-readable inventory is useful:

```text
npm run docs:audit -- --json
```

`docs:links` must be run after `npm run build`. It validates the generated HTML instead of only inspecting Markdown source links.

It checks:

- all internal documentation URLs are relative from the current generated page
- internal documentation targets under the configured GitHub Pages base
- generated `#fragment` anchors
- repository-local links rewritten to GitHub, including `samples/` and `demo/` files
- structured function example links

The command exits with an error if any checked link is broken or if a generated internal link is root-relative or an absolute GitHub Pages URL. Third-party HTTP(S) URLs are counted separately and are not used as a deterministic CI dependency.

## Migration model

The first phase supports all existing Markdown without requiring frontmatter.

A migrated document can add structured metadata such as:

```yaml
---
id: string-substring
title: Substring
type: function
category: string
shortDescription: Returns part of a string.
returnType: String
migration: complete
---
```

Structured pages get their page title and short description from frontmatter. Legacy pages continue to render their existing Markdown headings.

The long-term documentation model uses the following document types:

- `overview`
- `index`
- `language`
- `function-category`
- `function`
- `object`
- `property`

Property access values are stored as `Read` or `ReadWrite`. The UI renders `ReadWrite` as `Read/Write`.

## Migration rules

1. Do not bulk-rewrite the existing documentation.
2. Migrate one coherent section at a time.
3. Keep XPScript member casing in displayed names.
4. Normalize generated URL segments to lowercase.
5. Generate internal documentation paths relative to the current page.
6. Use stable documentation IDs for cross references.
7. Do not invent API metadata when migrating existing reference tables.
8. Keep the documentation audit informational until a section has been fully migrated and can be validated strictly.
9. Require the generated-site link verifier to pass before merging documentation changes.
