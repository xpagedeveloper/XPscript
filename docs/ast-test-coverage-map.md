# AST test coverage map

The AST FullTest path currently covers lexer and syntax models, expression and statement binding, conversions, control flow, bound C# emission, optional parameters, function-result lowering, scope isolation and the end-to-end `Sub Main` compile probe. These tests compile generated C# and execute selected programs.

Published-main parity fixtures additionally cover the compatibility baseline. GoTo/GoSub, Resume/On Error, implicit `With` members and XPImage behavior have audit fixtures or documented gaps, but they are not complete AST runtime regressions until their bound lowering exists. A feature may be marked migrated only when its focused probe, relevant permanent FullTest and published-main comparison all pass.
