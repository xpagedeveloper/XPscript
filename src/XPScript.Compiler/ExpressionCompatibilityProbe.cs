namespace XPScript.Compiler;

public static class ExpressionCompatibilityProbe
{
    public static string EmitLegacy(string expression) =>
        new AdvancedXPScriptTranspiler().TransformExpressionForCompatibilityTest(expression);
}
