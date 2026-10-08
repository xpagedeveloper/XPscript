# Regex rewrite inventory

The AST bridge still uses regular expressions for source normalization before parsing. The rewrites cover request attributes, `Static`, line continuations, constants, utility and file commands, `ReDim`, selected encoding calls and XPImage calls. They are safe only when they preserve the original expression boundaries and runtime contract.

The following rewrites are semantic migrations and must not remain regex-only: labels and GoTo/GoSub/Resume, `On Error`, implicit `With` members, and XPImage construction/loading. Their replacement must be represented in syntax, bound nodes and emitted runtime code, with source spans and published-main output comparisons. This inventory is intentionally kept beside the preprocessor code so a new rewrite cannot silently bypass the migration checklist.
