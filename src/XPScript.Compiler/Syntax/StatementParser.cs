namespace XPScript.Compiler.Syntax;

public sealed class StatementParser
{
    private readonly SyntaxToken[] _tokens;
    private readonly List<SyntaxDiagnostic> _diagnostics = [];
    private int _position;

    public StatementParser(string text, int baseOffset = 0)
    {
        var lexer = new Lexer(text, baseOffset);
        _tokens = lexer.Lex().ToArray();
        foreach (var diagnostic in lexer.Diagnostics)
            _diagnostics.Add(new SyntaxDiagnostic(diagnostic.Code, diagnostic.Message, diagnostic.Span));
    }

    public IReadOnlyList<SyntaxDiagnostic> Diagnostics => _diagnostics;

    public StatementSyntax ParseStatement()
    {
        var statement = ParseCurrentStatement();

        if (Current.Kind == SyntaxKind.NewLineToken)
            NextToken();

        if (Current.Kind != SyntaxKind.EndOfFileToken)
            _diagnostics.Add(new SyntaxDiagnostic(
                "XPS1012",
                $"Unexpected token {Current.Kind} after statement.",
                Current.Span));

        return statement;
    }

    private StatementSyntax ParseCurrentStatement()
    {
        if (Current.Kind == SyntaxKind.IfKeyword)
            return ParseIfStatement();
        if (Current.Kind == SyntaxKind.SetKeyword)
            return ParseSetStatement();
        if (Current.Kind == SyntaxKind.DimKeyword)
            return ParseDimStatement();
        if (Current.Kind == SyntaxKind.WhileKeyword)
            return ParseWhileStatement();
        if (Current.Kind == SyntaxKind.ForKeyword)
            return ParseForStatement();
        if (Current.Kind == SyntaxKind.ForAllKeyword)
            return ParseForAllStatement();
        if (Current.Kind == SyntaxKind.DoKeyword)
            return ParseDoStatement();
        if (Current.Kind == SyntaxKind.SelectKeyword)
            return ParseSelectStatement();
        if (Current.Kind == SyntaxKind.CallKeyword)
            return ParseCallStatement();
        if (Current.Kind == SyntaxKind.OpenKeyword)
            return ParseOpenStatement();
        if (Current.Kind == SyntaxKind.CloseKeyword)
            return ParseCloseStatement();
        if ((IsIdentifier("Print") || IsIdentifier("Write")) && PeekKind(1) == SyntaxKind.HashToken)
            return ParseFileOutputStatement();
        if (IsIdentifier("Print"))
            return ParseRuntimeFileStatement();
        if (IsIdentifier("Randomize") || IsIdentifier("Beep"))
            return ParseRuntimeFileStatement();
        if (Current.Kind == SyntaxKind.InputKeyword || (IsIdentifier("Line") && PeekKind(1) == SyntaxKind.InputKeyword))
            return ParseFileInputStatement();
        if (IsIdentifier("Seek") && PeekKind(1) == SyntaxKind.HashToken)
            return ParseSeekStatement();
        if (IsIdentifier("Put") || IsIdentifier("Get"))
            return ParseBinaryFileStatement();
        if (IsIdentifier("Lock") || IsIdentifier("Unlock"))
            return ParseFileLockStatement();
        if (IsRuntimeFileCommand(Current))
            return ParseRuntimeFileStatement();
        if (IsIdentifier("Name"))
            return ParseRenameFileStatement();
        if (Current.Kind == SyntaxKind.OnKeyword && PeekKind(1) == SyntaxKind.EventKeyword)
            return ParseOnEventStatement();
        if (Current.Kind == SyntaxKind.OnKeyword)
            return ParseOnErrorStatement();
        if (Current.Kind == SyntaxKind.ResumeKeyword)
            return ParseResumeStatement();
        if (Current.Kind == SyntaxKind.ErrorKeyword)
            return ParseErrorStatement();

        var equalsIndex = FindTopLevelEqualsIndex(_position);
        return equalsIndex >= 0
            ? ParseAssignmentStatement(equalsIndex)
            : ParseExpressionStatement();
    }

    private StatementSyntax ParseIfStatement()
    {
        var ifKeyword = NextToken();
        var thenIndex = FindThenIndex();
        if (thenIndex < 0)
        {
            _diagnostics.Add(new SyntaxDiagnostic(
                "XPS1012",
                "Expected ThenKeyword in If statement.",
                new TextSpan(Current.Span.Start, 0)));
            return new ExpressionStatementSyntax(ParseExpressionUntilLineEnd());
        }

        var condition = ParseExpressionRange(_position, thenIndex, _tokens[thenIndex].Span.Start);
        _position = thenIndex;
        var thenKeyword = NextToken();

        if (Current.Kind != SyntaxKind.NewLineToken)
            return new IfStatementSyntax(ifKeyword, condition, thenKeyword, ParseExpressionStatement());

        NextToken();

        var thenStatements = ParseBranchStatements();
        var elseIfClauses = new List<ElseIfClauseSyntax>();
        while (Current.Kind == SyntaxKind.ElseIfKeyword)
            elseIfClauses.Add(ParseElseIfClause());

        var elseStatements = new List<StatementSyntax>();
        if (Current.Kind == SyntaxKind.ElseKeyword)
        {
            NextToken();
            ConsumeRequiredNewLine("Else");
            elseStatements.AddRange(ParseBranchStatements());
        }

        SyntaxToken? endKeyword = null;
        SyntaxToken? endIfKeyword = null;
        if (Current.Kind == SyntaxKind.EndKeyword)
        {
            endKeyword = NextToken();
            endIfKeyword = Match(SyntaxKind.IfKeyword);
        }
        else
        {
            _diagnostics.Add(new SyntaxDiagnostic(
                "XPS1012",
                "Expected 'End If' to close block If statement.",
                new TextSpan(Current.Span.Start, 0)));
        }

        return new IfStatementSyntax(
            ifKeyword,
            condition,
            thenKeyword,
            thenStatements,
            elseIfClauses,
            elseStatements,
            endKeyword,
            endIfKeyword);
    }

    private ElseIfClauseSyntax ParseElseIfClause()
    {
        var elseIfKeyword = NextToken();
        var thenIndex = FindThenIndex();
        if (thenIndex < 0)
        {
            _diagnostics.Add(new SyntaxDiagnostic(
                "XPS1012",
                "Expected ThenKeyword in ElseIf clause.",
                new TextSpan(Current.Span.Start, 0)));
            var missingThen = new SyntaxToken(
                SyntaxKind.ThenKeyword,
                string.Empty,
                null,
                new TextSpan(Current.Span.Start, 0));
            return new ElseIfClauseSyntax(
                elseIfKeyword,
                ParseExpressionUntilLineEnd(),
                missingThen,
                []);
        }

        var condition = ParseExpressionRange(_position, thenIndex, _tokens[thenIndex].Span.Start);
        _position = thenIndex;
        var thenKeyword = NextToken();
        ConsumeRequiredNewLine("ElseIf");
        var statements = ParseBranchStatements();

        return new ElseIfClauseSyntax(elseIfKeyword, condition, thenKeyword, statements);
    }

    private List<StatementSyntax> ParseBranchStatements()
    {
        var statements = new List<StatementSyntax>();
        while (Current.Kind is not SyntaxKind.ElseIfKeyword
               and not SyntaxKind.ElseKeyword
               and not SyntaxKind.EndKeyword
               and not SyntaxKind.EndOfFileToken)
        {
            if (Current.Kind == SyntaxKind.NewLineToken)
            {
                NextToken();
                continue;
            }

            statements.Add(ParseCurrentStatement());
            if (Current.Kind == SyntaxKind.NewLineToken)
                NextToken();
        }

        return statements;
    }

    private StatementSyntax ParseSelectStatement()
    {
        var selectKeyword = NextToken();
        var caseKeyword = Match(SyntaxKind.CaseKeyword);
        var expression = ParseExpressionUntilLineEnd();
        ConsumeRequiredNewLine("Select Case");

        var cases = new List<CaseClauseSyntax>();
        while (Current.Kind != SyntaxKind.EndOfFileToken)
        {
            if (Current.Kind == SyntaxKind.EndKeyword
                && PeekKind(1) == SyntaxKind.SelectKeyword)
                break;

            if (Current.Kind == SyntaxKind.NewLineToken)
            {
                NextToken();
                continue;
            }

            if (Current.Kind != SyntaxKind.CaseKeyword)
            {
                _diagnostics.Add(new SyntaxDiagnostic(
                    "XPS1012",
                    $"Expected CaseKeyword in Select statement but found {Current.Kind}.",
                    Current.Span));
                ParseExpressionUntilLineEnd();
                if (Current.Kind == SyntaxKind.NewLineToken)
                    NextToken();
                continue;
            }

            cases.Add(ParseCaseClause());
        }

        var endKeyword = Match(SyntaxKind.EndKeyword);
        var endSelectKeyword = Match(SyntaxKind.SelectKeyword);
        return new SelectStatementSyntax(
            selectKeyword,
            caseKeyword,
            expression,
            cases,
            endKeyword,
            endSelectKeyword);
    }

    private CaseClauseSyntax ParseCaseClause()
    {
        var caseKeyword = NextToken();

        SelectCaseKind caseKind;
        SyntaxToken? isKeyword = null;
        SyntaxToken? operatorToken = null;
        ExpressionSyntax? lowerExpression = null;
        SyntaxToken? toKeyword = null;
        ExpressionSyntax? upperExpression = null;
        SyntaxToken? elseKeyword = null;

        if (Current.Kind == SyntaxKind.ElseKeyword)
        {
            caseKind = SelectCaseKind.Else;
            elseKeyword = NextToken();
        }
        else if (Current.Kind == SyntaxKind.IsKeyword)
        {
            caseKind = SelectCaseKind.Relational;
            isKeyword = NextToken();
            operatorToken = Current.Kind is SyntaxKind.EqualsToken
                or SyntaxKind.LessToken
                or SyntaxKind.LessOrEqualsToken
                or SyntaxKind.GreaterToken
                or SyntaxKind.GreaterOrEqualsToken
                or SyntaxKind.LessGreaterToken
                    ? NextToken()
                    : Match(SyntaxKind.EqualsToken);

            var lineEnd = FindLineEndIndex(_position);
            lowerExpression = ParseExpressionRange(_position, lineEnd, _tokens[lineEnd].Span.Start);
            _position = lineEnd;
        }
        else
        {
            var toIndex = FindTokenOnCurrentLine(SyntaxKind.ToKeyword);
            var lineEnd = FindLineEndIndex(_position);
            if (toIndex >= 0)
            {
                caseKind = SelectCaseKind.Range;
                lowerExpression = ParseExpressionRange(_position, toIndex, _tokens[toIndex].Span.Start);
                _position = toIndex;
                toKeyword = NextToken();
                lineEnd = FindLineEndIndex(_position);
                upperExpression = ParseExpressionRange(_position, lineEnd, _tokens[lineEnd].Span.Start);
                _position = lineEnd;
            }
            else
            {
                caseKind = SelectCaseKind.Value;
                lowerExpression = ParseExpressionRange(_position, lineEnd, _tokens[lineEnd].Span.Start);
                _position = lineEnd;
            }
        }

        ConsumeRequiredNewLine("Case");

        var statements = new List<StatementSyntax>();
        while (Current.Kind != SyntaxKind.EndOfFileToken)
        {
            if (Current.Kind == SyntaxKind.CaseKeyword)
                break;
            if (Current.Kind == SyntaxKind.EndKeyword
                && PeekKind(1) == SyntaxKind.SelectKeyword)
                break;

            if (Current.Kind == SyntaxKind.NewLineToken)
            {
                NextToken();
                continue;
            }

            statements.Add(ParseCurrentStatement());
            if (Current.Kind == SyntaxKind.NewLineToken)
                NextToken();
        }

        return new CaseClauseSyntax(
            caseKeyword,
            caseKind,
            isKeyword,
            operatorToken,
            lowerExpression,
            toKeyword,
            upperExpression,
            elseKeyword,
            statements);
    }

    private StatementSyntax ParseOnEventStatement()
    {
        var onKeyword = NextToken();
        var eventKeyword = Match(SyntaxKind.EventKeyword);
        var eventName = Match(SyntaxKind.IdentifierToken);
        var fromKeyword = Match(SyntaxKind.FromKeyword);

        var actionIndex = FindTokenOnCurrentLine(SyntaxKind.CallKeyword);
        var removeIndex = FindTokenOnCurrentLine(SyntaxKind.RemoveKeyword);
        if (actionIndex < 0 || (removeIndex >= 0 && removeIndex < actionIndex))
            actionIndex = removeIndex;

        if (actionIndex < 0)
        {
            _diagnostics.Add(new SyntaxDiagnostic(
                "XPS1012",
                "Expected CallKeyword or RemoveKeyword in On Event statement.",
                new TextSpan(Current.Span.Start, 0)));
            var source = ParseExpressionUntilLineEnd();
            var missingAction = new SyntaxToken(
                SyntaxKind.CallKeyword, string.Empty, null, new TextSpan(source.Span.End, 0));
            return new OnEventStatementSyntax(
                onKeyword, eventKeyword, eventName, fromKeyword, source, missingAction, null);
        }

        var sourceExpression = ParseExpressionRange(
            _position, actionIndex, _tokens[actionIndex].Span.Start);
        _position = actionIndex;
        var actionKeyword = NextToken();

        SyntaxToken? handlerToken = null;
        if (actionKeyword.Kind == SyntaxKind.CallKeyword)
            handlerToken = Match(SyntaxKind.IdentifierToken);
        else if (Current.Kind == SyntaxKind.IdentifierToken)
            handlerToken = NextToken();

        return new OnEventStatementSyntax(
            onKeyword, eventKeyword, eventName, fromKeyword,
            sourceExpression, actionKeyword, handlerToken);
    }

    private StatementSyntax ParseDoStatement()
    {
        var doKeyword = NextToken();

        SyntaxToken? conditionKeyword = null;
        ExpressionSyntax? condition = null;
        var isPostTest = false;

        if (Current.Kind is SyntaxKind.WhileKeyword or SyntaxKind.UntilKeyword)
        {
            conditionKeyword = NextToken();
            condition = ParseExpressionUntilLineEnd();
        }

        ConsumeRequiredNewLine("Do");

        var statements = new List<StatementSyntax>();
        while (Current.Kind is not SyntaxKind.LoopKeyword and not SyntaxKind.EndOfFileToken)
        {
            if (Current.Kind == SyntaxKind.NewLineToken)
            {
                NextToken();
                continue;
            }

            statements.Add(ParseCurrentStatement());
            if (Current.Kind == SyntaxKind.NewLineToken)
                NextToken();
        }

        var loopKeyword = Match(SyntaxKind.LoopKeyword);

        if (conditionKeyword is null
            && Current.Kind is SyntaxKind.WhileKeyword or SyntaxKind.UntilKeyword)
        {
            conditionKeyword = NextToken();
            condition = ParseExpressionUntilLineEnd();
            isPostTest = true;
        }

        return new DoStatementSyntax(
            doKeyword,
            conditionKeyword,
            condition,
            isPostTest,
            statements,
            loopKeyword);
    }

    private StatementSyntax ParseForAllStatement()
    {
        var forAllKeyword = NextToken();
        var identifier = Match(SyntaxKind.IdentifierToken);
        var inKeyword = Match(SyntaxKind.InKeyword);

        var lineEnd = FindLineEndIndex(_position);
        var collectionExpression = ParseExpressionRange(
            _position,
            lineEnd,
            _tokens[lineEnd].Span.Start);
        _position = lineEnd;
        ConsumeRequiredNewLine("ForAll");

        var statements = new List<StatementSyntax>();
        while (Current.Kind != SyntaxKind.EndOfFileToken)
        {
            if (Current.Kind == SyntaxKind.EndKeyword
                && PeekKind(1) == SyntaxKind.ForAllKeyword)
                break;

            if (Current.Kind == SyntaxKind.NewLineToken)
            {
                NextToken();
                continue;
            }

            statements.Add(ParseCurrentStatement());
            if (Current.Kind == SyntaxKind.NewLineToken)
                NextToken();
        }

        var endKeyword = Match(SyntaxKind.EndKeyword);
        var endForAllKeyword = Match(SyntaxKind.ForAllKeyword);
        return new ForAllStatementSyntax(
            forAllKeyword,
            identifier,
            inKeyword,
            collectionExpression,
            statements,
            endKeyword,
            endForAllKeyword);
    }

    private StatementSyntax ParseForStatement()
    {
        var forKeyword = NextToken();
        var identifier = Match(SyntaxKind.IdentifierToken);
        var equalsToken = Match(SyntaxKind.EqualsToken);

        var toIndex = FindTokenOnCurrentLine(SyntaxKind.ToKeyword);
        ExpressionSyntax fromExpression;
        if (toIndex < 0)
        {
            _diagnostics.Add(new SyntaxDiagnostic(
                "XPS1012",
                "Expected ToKeyword in For statement.",
                new TextSpan(Current.Span.Start, 0)));
            fromExpression = ParseExpressionUntilLineEnd();
            var missingTo = new SyntaxToken(SyntaxKind.ToKeyword, string.Empty, null, new TextSpan(fromExpression.Span.End, 0));
            return new ForStatementSyntax(
                forKeyword, identifier, equalsToken, fromExpression, missingTo,
                new NameExpressionSyntax(new SyntaxToken(SyntaxKind.IdentifierToken, string.Empty, null, new TextSpan(fromExpression.Span.End, 0))),
                null, null, [], Match(SyntaxKind.NextKeyword), null);
        }

        fromExpression = ParseExpressionRange(_position, toIndex, _tokens[toIndex].Span.Start);
        _position = toIndex;
        var toKeyword = NextToken();

        var stepIndex = FindTokenOnCurrentLine(SyntaxKind.StepKeyword);
        var lineEnd = FindLineEndIndex(_position);
        ExpressionSyntax toExpression;
        SyntaxToken? stepKeyword = null;
        ExpressionSyntax? stepExpression = null;

        if (stepIndex >= 0)
        {
            toExpression = ParseExpressionRange(_position, stepIndex, _tokens[stepIndex].Span.Start);
            _position = stepIndex;
            stepKeyword = NextToken();
            lineEnd = FindLineEndIndex(_position);
            stepExpression = ParseExpressionRange(_position, lineEnd, _tokens[lineEnd].Span.Start);
            _position = lineEnd;
        }
        else
        {
            toExpression = ParseExpressionRange(_position, lineEnd, _tokens[lineEnd].Span.Start);
            _position = lineEnd;
        }

        ConsumeRequiredNewLine("For");

        var statements = new List<StatementSyntax>();
        while (Current.Kind is not SyntaxKind.NextKeyword and not SyntaxKind.EndOfFileToken)
        {
            if (Current.Kind == SyntaxKind.NewLineToken)
            {
                NextToken();
                continue;
            }

            statements.Add(ParseCurrentStatement());
            if (Current.Kind == SyntaxKind.NewLineToken)
                NextToken();
        }

        var nextKeyword = Match(SyntaxKind.NextKeyword);
        SyntaxToken? nextIdentifier = null;
        if (Current.Kind == SyntaxKind.IdentifierToken)
            nextIdentifier = NextToken();

        if (nextIdentifier is not null
            && !string.Equals(nextIdentifier.Text, identifier.Text, StringComparison.OrdinalIgnoreCase))
        {
            _diagnostics.Add(new SyntaxDiagnostic(
                "XPS1012",
                $"Next identifier '{nextIdentifier.Text}' does not match For identifier '{identifier.Text}'.",
                nextIdentifier.Span));
        }

        return new ForStatementSyntax(
            forKeyword,
            identifier,
            equalsToken,
            fromExpression,
            toKeyword,
            toExpression,
            stepKeyword,
            stepExpression,
            statements,
            nextKeyword,
            nextIdentifier);
    }

    private StatementSyntax ParseWhileStatement()
    {
        var whileKeyword = NextToken();
        var condition = ParseExpressionUntilLineEnd();
        ConsumeRequiredNewLine("While");

        var statements = new List<StatementSyntax>();
        while (Current.Kind is not SyntaxKind.WendKeyword and not SyntaxKind.EndOfFileToken)
        {
            if (Current.Kind == SyntaxKind.NewLineToken)
            {
                NextToken();
                continue;
            }

            statements.Add(ParseCurrentStatement());
            if (Current.Kind == SyntaxKind.NewLineToken)
                NextToken();
        }

        var wendKeyword = Match(SyntaxKind.WendKeyword);
        return new WhileStatementSyntax(whileKeyword, condition, statements, wendKeyword);
    }

    private StatementSyntax ParseDimStatement()
    {
        var dimKeyword = NextToken();
        var identifier = Match(SyntaxKind.IdentifierToken);

        SyntaxToken? asKeyword = null;
        SyntaxToken? typeName = null;
        if (Current.Kind == SyntaxKind.AsKeyword)
        {
            asKeyword = NextToken();
            typeName = Match(SyntaxKind.IdentifierToken);
        }

        SyntaxToken? equalsToken = null;
        ExpressionSyntax? initializer = null;
        if (Current.Kind == SyntaxKind.EqualsToken)
        {
            equalsToken = NextToken();
            initializer = ParseExpressionUntilLineEnd();
        }

        if (Current.Kind is not SyntaxKind.NewLineToken and not SyntaxKind.EndOfFileToken)
        {
            _diagnostics.Add(new SyntaxDiagnostic(
                "XPS1012",
                $"Unexpected token {Current.Kind} in Dim statement.",
                Current.Span));
        }

        return new DimStatementSyntax(
            dimKeyword,
            identifier,
            asKeyword,
            typeName,
            equalsToken,
            initializer);
    }

    private StatementSyntax ParseAssignmentStatement(int equalsIndex)
    {
        var target = ParseExpressionRange(_position, equalsIndex, _tokens[equalsIndex].Span.Start);
        _position = equalsIndex;
        var equalsToken = NextToken();
        var expression = ParseExpressionUntilLineEnd();

        if (target.Kind is not SyntaxKind.NameExpression
            and not SyntaxKind.MemberAccessExpression
            and not SyntaxKind.IndexExpression)
        {
            _diagnostics.Add(new SyntaxDiagnostic(
                "XPS1012",
                "Invalid assignment target.",
                target.Span));
        }

        return new AssignmentStatementSyntax(target, equalsToken, expression);
    }

    private StatementSyntax ParseSetStatement()
    {
        var setKeyword = NextToken();
        var equalsIndex = FindTopLevelEqualsIndex(_position);
        if (equalsIndex < 0)
        {
            _diagnostics.Add(new SyntaxDiagnostic(
                "XPS1012",
                "Expected '=' in Set statement.",
                new TextSpan(Current.Span.Start, 0)));
            var target = ParseExpressionUntilLineEnd();
            var missingEquals = new SyntaxToken(
                SyntaxKind.EqualsToken,
                string.Empty,
                null,
                new TextSpan(target.Span.End, 0));
            var missingExpression = new NameExpressionSyntax(new SyntaxToken(
                SyntaxKind.IdentifierToken,
                string.Empty,
                null,
                new TextSpan(target.Span.End, 0)));
            return new SetStatementSyntax(setKeyword, target, missingEquals, missingExpression);
        }

        var targetExpression = ParseExpressionRange(_position, equalsIndex, _tokens[equalsIndex].Span.Start);
        _position = equalsIndex;
        var equalsToken = NextToken();
        var expression = ParseExpressionUntilLineEnd();

        if (targetExpression.Kind is not SyntaxKind.NameExpression
            and not SyntaxKind.MemberAccessExpression
            and not SyntaxKind.IndexExpression)
        {
            _diagnostics.Add(new SyntaxDiagnostic(
                "XPS1012",
                "Invalid Set target.",
                targetExpression.Span));
        }

        return new SetStatementSyntax(setKeyword, targetExpression, equalsToken, expression);
    }

    private int FindTopLevelEqualsIndex(int start)
    {
        var parenDepth = 0;
        var bracketDepth = 0;

        for (var i = start; i < _tokens.Length; i++)
        {
            switch (_tokens[i].Kind)
            {
                case SyntaxKind.NewLineToken:
                case SyntaxKind.EndOfFileToken:
                    return -1;
                case SyntaxKind.OpenParenToken:
                    parenDepth++;
                    break;
                case SyntaxKind.CloseParenToken:
                    if (parenDepth > 0)
                        parenDepth--;
                    break;
                case SyntaxKind.OpenBracketToken:
                    bracketDepth++;
                    break;
                case SyntaxKind.CloseBracketToken:
                    if (bracketDepth > 0)
                        bracketDepth--;
                    break;
                case SyntaxKind.EqualsToken when parenDepth == 0 && bracketDepth == 0:
                    return i;
            }
        }

        return -1;
    }

    private void ConsumeRequiredNewLine(string context)
    {
        if (Current.Kind == SyntaxKind.NewLineToken)
        {
            NextToken();
            return;
        }

        _diagnostics.Add(new SyntaxDiagnostic(
            "XPS1012",
            $"Expected newline after {context}.",
            new TextSpan(Current.Span.Start, 0)));
    }

    private StatementSyntax ParseOnErrorStatement()
    {
        var onKeyword = NextToken();
        var errorKeyword = Match(SyntaxKind.ErrorKeyword);
        if (Current.Kind == SyntaxKind.GoToKeyword)
        {
            var goToKeyword = NextToken();
            SyntaxToken target;
            if (Current.Kind is SyntaxKind.IdentifierToken or SyntaxKind.NumberToken)
                target = NextToken();
            else
                target = Match(SyntaxKind.IdentifierToken);
            return new OnErrorStatementSyntax(onKeyword, errorKeyword, goToKeyword, target);
        }

        var resumeKeyword = Match(SyntaxKind.ResumeKeyword);
        var nextKeyword = Match(SyntaxKind.NextKeyword);
        return new OnErrorStatementSyntax(onKeyword, errorKeyword, resumeKeyword, nextKeyword);
    }

    private StatementSyntax ParseResumeStatement()
    {
        var resumeKeyword = NextToken();
        SyntaxToken? target = null;
        if (Current.Kind is SyntaxKind.NextKeyword or SyntaxKind.IdentifierToken)
            target = NextToken();
        return new ResumeStatementSyntax(resumeKeyword, target);
    }

    private StatementSyntax ParseErrorStatement()
    {
        var errorKeyword = NextToken();
        var commaIndex = FindTokenOnCurrentLine(SyntaxKind.CommaToken);
        if (commaIndex < 0)
            return new ErrorStatementSyntax(errorKeyword, ParseExpressionUntilLineEnd(), null, null);

        var numberExpression = ParseExpressionRange(_position, commaIndex, _tokens[commaIndex].Span.Start);
        _position = commaIndex;
        var commaToken = NextToken();
        var descriptionExpression = ParseExpressionUntilLineEnd();
        return new ErrorStatementSyntax(errorKeyword, numberExpression, commaToken, descriptionExpression);
    }

    private StatementSyntax ParseOpenStatement()
    {
        var openKeyword = NextToken();
        var forIndex = FindTokenOnCurrentLine(SyntaxKind.ForKeyword);
        if (forIndex < 0)
        {
            var path = ParseExpressionUntilLineEnd();
            return new OpenStatementSyntax(openKeyword, path, Match(SyntaxKind.ForKeyword),
                Match(SyntaxKind.InputKeyword), Match(SyntaxKind.AsKeyword), null, MissingExpression());
        }

        var pathExpression = ParseExpressionRange(_position, forIndex, _tokens[forIndex].Span.Start);
        _position = forIndex;
        var forKeyword = NextToken();
        var modeKeyword = Current.Kind is SyntaxKind.InputKeyword or SyntaxKind.OutputKeyword
            or SyntaxKind.AppendKeyword or SyntaxKind.BinaryKeyword or SyntaxKind.RandomKeyword
            ? NextToken()
            : Match(SyntaxKind.InputKeyword);
        var asKeyword = Match(SyntaxKind.AsKeyword);
        SyntaxToken? hashToken = Current.Kind == SyntaxKind.HashToken ? NextToken() : null;

        var lenIndex = FindTokenOnCurrentLine(SyntaxKind.LenKeyword);
        var lineEnd = FindLineEndIndex(_position);
        var fileEnd = lenIndex >= 0 ? lenIndex : lineEnd;
        var fileNumber = ParseExpressionRange(_position, fileEnd, _tokens[fileEnd].Span.Start);
        _position = fileEnd;

        SyntaxToken? lenKeyword = null;
        SyntaxToken? equalsToken = null;
        ExpressionSyntax? recordLength = null;
        if (Current.Kind == SyntaxKind.LenKeyword)
        {
            lenKeyword = NextToken();
            equalsToken = Match(SyntaxKind.EqualsToken);
            recordLength = ParseExpressionUntilLineEnd();
        }

        return new OpenStatementSyntax(openKeyword, pathExpression, forKeyword, modeKeyword,
            asKeyword, hashToken, fileNumber, lenKeyword, equalsToken, recordLength);
    }

    private StatementSyntax ParseCloseStatement()
    {
        var closeKeyword = NextToken();
        var fileNumbers = new List<ExpressionSyntax>();
        while (Current.Kind is not SyntaxKind.NewLineToken and not SyntaxKind.EndOfFileToken)
        {
            if (Current.Kind == SyntaxKind.HashToken)
                NextToken();
            var commaIndex = FindTokenOnCurrentLine(SyntaxKind.CommaToken);
            var end = commaIndex >= 0 ? commaIndex : FindLineEndIndex(_position);
            fileNumbers.Add(ParseExpressionRange(_position, end, _tokens[end].Span.Start));
            _position = end;
            if (Current.Kind == SyntaxKind.CommaToken)
                NextToken();
        }
        return new CloseStatementSyntax(closeKeyword, fileNumbers);
    }

    private StatementSyntax ParseFileOutputStatement()
    {
        var rawKeyword = NextToken();
        if (Current.Kind == SyntaxKind.HashToken)
            NextToken();
        var keyword = PromoteIdentifier(rawKeyword, string.Equals(rawKeyword.Text, "Write", StringComparison.OrdinalIgnoreCase) ? SyntaxKind.WriteKeyword : SyntaxKind.PrintKeyword);
        var commaIndex = FindTokenOnCurrentLine(SyntaxKind.CommaToken);
        var lineEnd = FindLineEndIndex(_position);
        var fileEnd = commaIndex >= 0 ? commaIndex : lineEnd;
        var fileNumber = ParseExpressionRange(_position, fileEnd, _tokens[fileEnd].Span.Start);
        _position = fileEnd;
        var values = new List<ExpressionSyntax>();
        if (Current.Kind == SyntaxKind.CommaToken)
            NextToken();
        while (Current.Kind is not SyntaxKind.NewLineToken and not SyntaxKind.EndOfFileToken)
        {
            commaIndex = FindTokenOnCurrentLine(SyntaxKind.CommaToken);
            var end = commaIndex >= 0 ? commaIndex : FindLineEndIndex(_position);
            values.Add(ParseExpressionRange(_position, end, _tokens[end].Span.Start));
            _position = end;
            if (Current.Kind == SyntaxKind.CommaToken)
                NextToken();
        }
        return new FileOutputStatementSyntax(keyword, fileNumber, values);
    }

    private StatementSyntax ParseFileInputStatement()
    {
        SyntaxToken? lineKeyword = null;
        if (IsIdentifier("Line"))
            lineKeyword = PromoteIdentifier(NextToken(), SyntaxKind.LineKeyword);
        var inputKeyword = Match(SyntaxKind.InputKeyword);
        if (Current.Kind == SyntaxKind.HashToken)
            NextToken();
        var commaIndex = FindTokenOnCurrentLine(SyntaxKind.CommaToken);
        var lineEnd = FindLineEndIndex(_position);
        var fileEnd = commaIndex >= 0 ? commaIndex : lineEnd;
        var fileNumber = ParseExpressionRange(_position, fileEnd, _tokens[fileEnd].Span.Start);
        _position = fileEnd;
        var targets = new List<ExpressionSyntax>();
        if (Current.Kind == SyntaxKind.CommaToken)
            NextToken();
        while (Current.Kind is not SyntaxKind.NewLineToken and not SyntaxKind.EndOfFileToken)
        {
            commaIndex = FindTokenOnCurrentLine(SyntaxKind.CommaToken);
            var end = commaIndex >= 0 ? commaIndex : FindLineEndIndex(_position);
            targets.Add(ParseExpressionRange(_position, end, _tokens[end].Span.Start));
            _position = end;
            if (Current.Kind == SyntaxKind.CommaToken)
                NextToken();
        }
        return new FileInputStatementSyntax(inputKeyword, fileNumber, targets, lineKeyword);
    }

    private StatementSyntax ParseSeekStatement()
    {
        var seekKeyword = PromoteIdentifier(NextToken(), SyntaxKind.SeekKeyword);
        if (Current.Kind == SyntaxKind.HashToken)
            NextToken();
        var commaIndex = FindTokenOnCurrentLine(SyntaxKind.CommaToken);
        if (commaIndex < 0)
        {
            var missing = new TextSpan(Current.Span.Start, 0);
            _diagnostics.Add(new SyntaxDiagnostic("XPS1012", "Expected comma and position in Seek statement.", missing));
            var fileNumber = ParseExpressionUntilLineEnd();
            var position = new NameExpressionSyntax(new SyntaxToken(SyntaxKind.IdentifierToken, string.Empty, null, missing));
            return new SeekStatementSyntax(seekKeyword, fileNumber, position);
        }
        var file = ParseExpressionRange(_position, commaIndex, _tokens[commaIndex].Span.Start);
        _position = commaIndex + 1;
        var positionExpression = ParseExpressionUntilLineEnd();
        return new SeekStatementSyntax(seekKeyword, file, positionExpression);
    }

    private StatementSyntax ParseBinaryFileStatement()
    {
        var command = NextToken();
        if (Current.Kind == SyntaxKind.HashToken)
            NextToken();

        var arguments = new List<ExpressionSyntax>();
        while (Current.Kind is not SyntaxKind.NewLineToken and not SyntaxKind.EndOfFileToken)
        {
            var commaIndex = FindTokenOnCurrentLine(SyntaxKind.CommaToken);
            var end = commaIndex >= 0 ? commaIndex : FindLineEndIndex(_position);
            arguments.Add(ParseExpressionRange(_position, end, _tokens[end].Span.Start));
            _position = end;
            if (Current.Kind == SyntaxKind.CommaToken)
                NextToken();
        }

        if (arguments.Count < 3)
            _diagnostics.Add(new SyntaxDiagnostic("XPS1012", $"{command.Text} expects file number, position and value/target.", command.Span));
        return new RuntimeFileStatementSyntax(command, arguments);
    }

    private StatementSyntax ParseFileLockStatement()
    {
        var command = NextToken();
        if (Current.Kind == SyntaxKind.HashToken)
            NextToken();
        var arguments = new List<ExpressionSyntax>();
        var commaIndex = FindTokenOnCurrentLine(SyntaxKind.CommaToken);
        var lineEnd = FindLineEndIndex(_position);
        var fileEnd = commaIndex >= 0 ? commaIndex : lineEnd;
        arguments.Add(ParseExpressionRange(_position, fileEnd, _tokens[fileEnd].Span.Start));
        _position = fileEnd;
        if (Current.Kind == SyntaxKind.CommaToken)
        {
            NextToken();
            var toIndex = FindTokenOnCurrentLine(SyntaxKind.ToKeyword);
            lineEnd = FindLineEndIndex(_position);
            var startEnd = toIndex >= 0 ? toIndex : lineEnd;
            arguments.Add(ParseExpressionRange(_position, startEnd, _tokens[startEnd].Span.Start));
            _position = startEnd;
            if (Current.Kind == SyntaxKind.ToKeyword)
            {
                NextToken();
                arguments.Add(ParseExpressionUntilLineEnd());
            }
        }
        return new RuntimeFileStatementSyntax(command, arguments);
    }

    private StatementSyntax ParseRuntimeFileStatement()
    {
        var command = NextToken();
        var arguments = new List<ExpressionSyntax>();
        while (Current.Kind is not SyntaxKind.NewLineToken and not SyntaxKind.EndOfFileToken)
        {
            var commaIndex = FindTokenOnCurrentLine(SyntaxKind.CommaToken);
            var end = commaIndex >= 0 ? commaIndex : FindLineEndIndex(_position);
            arguments.Add(ParseExpressionRange(_position, end, _tokens[end].Span.Start));
            _position = end;
            if (Current.Kind == SyntaxKind.CommaToken)
                NextToken();
        }
        return new RuntimeFileStatementSyntax(command, arguments);
    }

    private StatementSyntax ParseRenameFileStatement()
    {
        var nameToken = NextToken();
        var asIndex = FindTokenOnCurrentLine(SyntaxKind.AsKeyword);
        if (asIndex < 0)
        {
            var oldPath = ParseExpressionUntilLineEnd();
            var missingAs = Match(SyntaxKind.AsKeyword);
            var missingPath = new NameExpressionSyntax(new SyntaxToken(SyntaxKind.IdentifierToken, string.Empty, null, missingAs.Span));
            return new RenameFileStatementSyntax(nameToken, oldPath, missingAs, missingPath);
        }
        var oldPathExpression = ParseExpressionRange(_position, asIndex, _tokens[asIndex].Span.Start);
        _position = asIndex;
        var asKeyword = NextToken();
        var newPath = ParseExpressionUntilLineEnd();
        return new RenameFileStatementSyntax(nameToken, oldPathExpression, asKeyword, newPath);
    }

    private StatementSyntax ParseCallStatement()
    {
        var callKeyword = NextToken();
        var expression = ParseExpressionUntilLineEnd();
        return new CallStatementSyntax(callKeyword, expression);
    }

    private StatementSyntax ParseExpressionStatement() =>
        new ExpressionStatementSyntax(ParseExpressionUntilLineEnd());

    private ExpressionSyntax ParseExpressionUntilLineEnd()
    {
        var end = _position;
        while (_tokens[end].Kind is not SyntaxKind.NewLineToken and not SyntaxKind.EndOfFileToken)
            end++;

        var eofPosition = _tokens[end].Span.Start;
        var expression = ParseExpressionRange(_position, end, eofPosition);
        _position = end;
        return expression;
    }

    private ExpressionSyntax ParseExpressionRange(int start, int end, int eofPosition)
    {
        if (start == end)
        {
            _diagnostics.Add(new SyntaxDiagnostic(
                "XPS1012",
                "Expected expression.",
                new TextSpan(eofPosition, 0)));
            return new NameExpressionSyntax(new SyntaxToken(
                SyntaxKind.IdentifierToken,
                string.Empty,
                null,
                new TextSpan(eofPosition, 0)));
        }

        var expressionTokens = _tokens[start..end]
            .Append(new SyntaxToken(
                SyntaxKind.EndOfFileToken,
                string.Empty,
                null,
                new TextSpan(eofPosition, 0)));

        var parser = new ExpressionParser(expressionTokens);
        var expression = parser.ParseExpression();
        _diagnostics.AddRange(parser.Diagnostics);
        return expression;
    }

    private int FindTokenOnCurrentLine(SyntaxKind kind)
    {
        for (var i = _position; i < _tokens.Length; i++)
        {
            if (_tokens[i].Kind is SyntaxKind.NewLineToken or SyntaxKind.EndOfFileToken)
                return -1;
            if (_tokens[i].Kind == kind)
                return i;
        }

        return -1;
    }

    private int FindLineEndIndex(int start)
    {
        var i = start;
        while (_tokens[i].Kind is not SyntaxKind.NewLineToken and not SyntaxKind.EndOfFileToken)
            i++;
        return i;
    }

    private int FindThenIndex()
    {
        for (var i = _position; i < _tokens.Length; i++)
        {
            if (_tokens[i].Kind is SyntaxKind.NewLineToken or SyntaxKind.EndOfFileToken)
                return -1;
            if (_tokens[i].Kind == SyntaxKind.ThenKeyword)
                return i;
        }

        return -1;
    }

    private static bool IsRuntimeFileCommand(SyntaxToken token) =>
        token.Kind == SyntaxKind.IdentifierToken
        && token.Text.Equals("FileCopy", StringComparison.OrdinalIgnoreCase)
            || token.Kind == SyntaxKind.IdentifierToken && token.Text.Equals("Kill", StringComparison.OrdinalIgnoreCase)
            || token.Kind == SyntaxKind.IdentifierToken && token.Text.Equals("MkDir", StringComparison.OrdinalIgnoreCase)
            || token.Kind == SyntaxKind.IdentifierToken && token.Text.Equals("RmDir", StringComparison.OrdinalIgnoreCase)
            || token.Kind == SyntaxKind.IdentifierToken && token.Text.Equals("ChDir", StringComparison.OrdinalIgnoreCase)
            || token.Kind == SyntaxKind.IdentifierToken && token.Text.Equals("SetFileAttr", StringComparison.OrdinalIgnoreCase)
        || token.Kind == SyntaxKind.IdentifierToken && token.Text.Equals("ChDrive", StringComparison.OrdinalIgnoreCase);

    private ExpressionSyntax MissingExpression()
    {
        var span = new TextSpan(Current.Span.Start, 0);
        return new NameExpressionSyntax(new SyntaxToken(SyntaxKind.IdentifierToken, string.Empty, null, span));
    }

    private bool IsIdentifier(string text) =>
        Current.Kind == SyntaxKind.IdentifierToken
        && string.Equals(Current.Text, text, StringComparison.OrdinalIgnoreCase);

    private static SyntaxToken PromoteIdentifier(SyntaxToken token, SyntaxKind kind) =>
        new(kind, token.Text, token.Value, token.Span);

    private SyntaxToken Match(SyntaxKind kind)
    {
        if (Current.Kind == kind)
            return NextToken();

        var span = new TextSpan(Current.Span.Start, 0);
        _diagnostics.Add(new SyntaxDiagnostic(
            "XPS1012",
            $"Expected {kind} but found {Current.Kind}.",
            span));
        return new SyntaxToken(kind, string.Empty, null, span);
    }

    private SyntaxToken Current => _tokens[Math.Min(_position, _tokens.Length - 1)];

    private SyntaxKind PeekKind(int offset)
    {
        var index = Math.Min(_position + offset, _tokens.Length - 1);
        return _tokens[index].Kind;
    }

    private SyntaxToken NextToken()
    {
        var current = Current;
        _position++;
        return current;
    }
}
