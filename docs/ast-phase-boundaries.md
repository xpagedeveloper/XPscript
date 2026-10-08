# AST phase boundaries

The intended boundaries are: syntax parsers create immutable syntax nodes; binders resolve symbols, overloads, conversions and control-flow targets; emitters translate bound nodes to C# and never parse source text. The experimental bridge currently crosses those boundaries in three places: its compatibility preprocessor recognizes language constructs with regular expressions, `AstExperimentalCompiler` declares compatibility symbols based on source-text scans, and helper rewrites encode runtime behavior before binding.

These crossings are migration risks. New language behavior must be added as syntax, binder and emitter support, then the corresponding preprocessor rewrite must be removed. Keeping the boundary explicit prevents a successful Roslyn build from being mistaken for semantic compatibility with the published compiler.
