# XPScript documentation site

This directory contains the static documentation site for the Markdown sources in `../docs`.

The site is intentionally separate from the compiler source tree. Existing Markdown remains authoritative while the documentation is migrated to structured frontmatter and the new hierarchy.

## Commands

```text
npm install
npm run docs:audit
npm run dev
npm run build
npm run preview
```

Use JSON output when a machine-readable inventory is useful:

```text
npm run docs:audit -- --json
```

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
5. Use stable documentation IDs for cross references.
6. Do not invent API metadata when migrating existing reference tables.
7. Keep the documentation audit informational until a section has been fully migrated and can be validated strictly.
