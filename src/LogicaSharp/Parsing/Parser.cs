using LogicaSharp.Ast;

namespace LogicaSharp.Parsing;

/// <summary>
/// Parser for the Logica programming language.
/// Converts a stream of tokens into an AST.
/// </summary>
public class Parser
{
    private readonly List<Token> _tokens;
    private int _position;
    private int _fieldCounter;

    public Parser(IEnumerable<Token> tokens)
    {
        _tokens = tokens.ToList();
    }

    public Program Parse()
    {
        var statements = new List<IStatement>();

        while (!IsAtEnd())
        {
            var statement = ParseStatement();
            if (statement != null)
            {
                statements.Add(statement);
            }
        }

        return new Program(statements);
    }

    private IStatement? ParseStatement()
    {
        SkipSemicolons();
        if (IsAtEnd()) return null;

        // Check for annotation @...
        if (Check(TokenType.At))
        {
            return ParseAnnotation();
        }

        // Check for import
        if (CheckIdentifier("import"))
        {
            return ParseImport();
        }

        // Parse rule or function definition
        return ParseRuleOrFunction();
    }

    private Annotation ParseAnnotation()
    {
        Expect(TokenType.At, "Expected '@'");
        var name = Expect(TokenType.Identifier, "Expected annotation name").Value;

        Record? args = null;
        if (Match(TokenType.LeftParen))
        {
            args = ParseRecord();
            Expect(TokenType.RightParen, "Expected ')' after annotation arguments");
        }

        Expect(TokenType.Semicolon, "Expected ';' after annotation");
        return new Annotation(name, args);
    }

    private Import ParseImport()
    {
        Advance(); // consume 'import'
        var path = ParseImportPath();

        string? predicateName = null;
        if (Match(TokenType.Dot))
        {
            predicateName = Expect(TokenType.Identifier, "Expected predicate name after '.'").Value;
        }

        string? alias = null;
        if (CheckIdentifier("as"))
        {
            Advance(); // consume 'as'
            alias = Expect(TokenType.Identifier, "Expected alias name").Value;
        }

        Expect(TokenType.Semicolon, "Expected ';' after import");
        return new Import(path, predicateName, alias);
    }

    private string ParseImportPath()
    {
        if (Check(TokenType.String))
        {
            return Advance().Value;
        }

        // Handle dotted path
        var parts = new List<string>();
        parts.Add(Expect(TokenType.Identifier, "Expected import path").Value);

        while (Match(TokenType.Dot) && !CheckIdentifier("as"))
        {
            if (Check(TokenType.Identifier))
            {
                parts.Add(Advance().Value);
            }
            else
            {
                break;
            }
        }

        return string.Join(".", parts);
    }

    private IStatement ParseRuleOrFunction()
    {
        // Parse the head (predicate call)
        var head = ParsePredicateCall();

        // Check what comes next
        if (Match(TokenType.ColonDash))
        {
            // Standard rule: Head :- Body;
            var body = ParseBody();
            Expect(TokenType.Semicolon, "Expected ';' after rule");
            return new Rule(head, body);
        }
        else if (Match(TokenType.ColonEquals))
        {
            // Functor rule: Pred := Functor(args);
            var functorName = Expect(TokenType.Identifier, "Expected functor name").Value;
            Expect(TokenType.LeftParen, "Expected '(' after functor name");
            var args = ParseRecord();
            Expect(TokenType.RightParen, "Expected ')' after functor arguments");
            Expect(TokenType.Semicolon, "Expected ';' after functor rule");
            return new FunctorRule(head.PredicateName, functorName, args);
        }
        else if (Match(TokenType.Assign))
        {
            // Function definition: F(x) = expression;
            var value = ParseExpression();
            Expect(TokenType.Semicolon, "Expected ';' after function definition");
            return new FunctionRule(head, value);
        }
        else if (Match(TokenType.PlusEquals))
        {
            // Aggregation rule: Sum(x) += value :- Body;
            var value = ParseExpression();
            IBody? body = null;
            if (Match(TokenType.ColonDash))
            {
                body = ParseBody();
            }
            Expect(TokenType.Semicolon, "Expected ';' after aggregation rule");

            // Transform into a rule with aggregation
            var aggField = new FieldValue("col0", value, AggregationType.Sum);
            var newHead = new PredicateCall(head.PredicateName, new Record([.. head.Arguments.Fields, aggField]));
            return new Rule(newHead, body);
        }
        else
        {
            // Fact (rule without body)
            Expect(TokenType.Semicolon, "Expected ';' after fact");
            return new Rule(head, null);
        }
    }

    private PredicateCall ParsePredicateCall()
    {
        var name = Expect(TokenType.Identifier, "Expected predicate name").Value;

        if (!Match(TokenType.LeftParen))
        {
            return new PredicateCall(name, new Record([]));
        }

        var args = ParseRecord();
        Expect(TokenType.RightParen, "Expected ')' after predicate arguments");

        return new PredicateCall(name, args);
    }

    private Record ParseRecord()
    {
        var fields = new List<FieldValue>();
        IExpression? spread = null;

        if (Check(TokenType.RightParen) || Check(TokenType.RightBrace))
        {
            return new Record(fields);
        }

        do
        {
            // Check for spread operator ...
            if (Match(TokenType.Ellipsis))
            {
                spread = ParseExpression();
                continue;
            }

            var field = ParseFieldValue();
            fields.Add(field);
        } while (Match(TokenType.Comma) && !Check(TokenType.RightParen) && !Check(TokenType.RightBrace));

        return new Record(fields, spread);
    }

    private FieldValue ParseFieldValue()
    {
        AggregationType? aggregation = null;

        // Check for positional argument (no field name)
        if (!Check(TokenType.Identifier) ||
            (_position + 1 < _tokens.Count &&
             _tokens[_position + 1].Type != TokenType.Colon &&
             _tokens[_position + 1].Type != TokenType.Question))
        {
            var expr = ParseExpression();
            return new FieldValue("col" + _fieldCounter++, expr);
        }

        var fieldName = Advance().Value;

        // Check for aggregation marker ?
        if (Match(TokenType.Question))
        {
            aggregation = AggregationType.Collect;
            if (!Match(TokenType.Colon))
            {
                return new FieldValue(fieldName, null, aggregation);
            }
        }
        else if (!Match(TokenType.Colon))
        {
            // This was actually an expression, not a field name
            _position--;
            var expr = ParseExpression();
            return new FieldValue("col" + _fieldCounter++, expr);
        }

        // Check for shorthand (field:) - field name used as variable
        if (Check(TokenType.Comma) || Check(TokenType.RightParen) || Check(TokenType.RightBrace))
        {
            return new FieldValue(fieldName, new Variable(fieldName), aggregation);
        }

        var value = ParseExpression();
        return new FieldValue(fieldName, value, aggregation);
    }

    private IBody ParseBody()
    {
        return ParseDisjunction();
    }

    private IBody ParseDisjunction()
    {
        var left = ParseConjunction();

        var disjuncts = new List<IBody> { left };
        while (Match(TokenType.Pipe))
        {
            disjuncts.Add(ParseConjunction());
        }

        if (disjuncts.Count == 1)
        {
            return disjuncts[0];
        }

        return new Disjunction(disjuncts);
    }

    private IBody ParseConjunction()
    {
        var left = ParseBodyElement();

        var conjuncts = new List<IBody> { left };
        while (Match(TokenType.Comma) && !Check(TokenType.Pipe) && !Check(TokenType.Semicolon) && !Check(TokenType.RightParen))
        {
            conjuncts.Add(ParseBodyElement());
        }

        if (conjuncts.Count == 1)
        {
            return conjuncts[0];
        }

        return new Conjunction(conjuncts);
    }

    private IBody ParseBodyElement()
    {
        // Check for negation
        if (Match(TokenType.Not) || (Check(TokenType.Identifier) && Current().Value == "~"))
        {
            if (Current().Value == "~") Advance();

            if (Match(TokenType.LeftParen))
            {
                var body = ParseBody();
                Expect(TokenType.RightParen, "Expected ')' after negated body");
                return new Negation(body);
            }
            else
            {
                var call = ParsePredicateCall();
                return new Negation(new BodyCall(call));
            }
        }

        // Check for grouped body
        if (Match(TokenType.LeftParen))
        {
            var body = ParseBody();
            Expect(TokenType.RightParen, "Expected ')'");
            return body;
        }

        // Check if this looks like a predicate call
        if (Check(TokenType.Identifier) && IsPredicateName(Current().Value))
        {
            // Could be predicate call
            int startPos = _position;
            var call = ParsePredicateCall();

            // Check if it's actually a comparison or other expression
            if (Check(TokenType.Equal) || Check(TokenType.NotEqual) ||
                Check(TokenType.LessThan) || Check(TokenType.LessOrEqual) ||
                Check(TokenType.GreaterThan) || Check(TokenType.GreaterOrEqual))
            {
                // Rewind and parse as expression
                _position = startPos;
                var expr = ParseExpression();
                return new ExpressionCondition(expr);
            }

            return new BodyCall(call);
        }

        // Otherwise, it's an expression condition
        var expression = ParseExpression();
        return new ExpressionCondition(expression);
    }

    private IExpression ParseExpression()
    {
        return ParseOr();
    }

    private IExpression ParseOr()
    {
        var left = ParseAnd();

        while (Match(TokenType.Or))
        {
            var right = ParseAnd();
            left = new BinaryOp(left, "||", right);
        }

        return left;
    }

    private IExpression ParseAnd()
    {
        var left = ParseEquality();

        while (Match(TokenType.And))
        {
            var right = ParseEquality();
            left = new BinaryOp(left, "&&", right);
        }

        return left;
    }

    private IExpression ParseEquality()
    {
        var left = ParseComparison();

        while (true)
        {
            if (Match(TokenType.Equal))
            {
                left = new BinaryOp(left, "==", ParseComparison());
            }
            else if (Match(TokenType.NotEqual))
            {
                left = new BinaryOp(left, "!=", ParseComparison());
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private IExpression ParseComparison()
    {
        var left = ParseIn();

        while (true)
        {
            if (Match(TokenType.LessThan))
            {
                left = new BinaryOp(left, "<", ParseIn());
            }
            else if (Match(TokenType.LessOrEqual))
            {
                left = new BinaryOp(left, "<=", ParseIn());
            }
            else if (Match(TokenType.GreaterThan))
            {
                left = new BinaryOp(left, ">", ParseIn());
            }
            else if (Match(TokenType.GreaterOrEqual))
            {
                left = new BinaryOp(left, ">=", ParseIn());
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private IExpression ParseIn()
    {
        var left = ParseConcat();

        if (Match(TokenType.In))
        {
            var right = ParseConcat();
            return new InExpression(left, right);
        }

        return left;
    }

    private IExpression ParseConcat()
    {
        var left = ParseAdditive();

        while (Match(TokenType.PlusPlus))
        {
            var right = ParseAdditive();
            left = new BinaryOp(left, "++", right);
        }

        return left;
    }

    private IExpression ParseAdditive()
    {
        var left = ParseMultiplicative();

        while (true)
        {
            if (Match(TokenType.Plus))
            {
                left = new BinaryOp(left, "+", ParseMultiplicative());
            }
            else if (Match(TokenType.Minus))
            {
                left = new BinaryOp(left, "-", ParseMultiplicative());
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private IExpression ParseMultiplicative()
    {
        var left = ParseUnary();

        while (true)
        {
            if (Match(TokenType.Star))
            {
                left = new BinaryOp(left, "*", ParseUnary());
            }
            else if (Match(TokenType.Slash))
            {
                left = new BinaryOp(left, "/", ParseUnary());
            }
            else if (Match(TokenType.Percent))
            {
                left = new BinaryOp(left, "%", ParseUnary());
            }
            else if (Match(TokenType.Caret))
            {
                left = new BinaryOp(left, "^", ParseUnary());
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private IExpression ParseUnary()
    {
        if (Match(TokenType.Minus))
        {
            return new UnaryOp("-", ParseUnary());
        }
        if (Match(TokenType.Not))
        {
            return new UnaryOp("!", ParseUnary());
        }

        return ParsePostfix();
    }

    private IExpression ParsePostfix()
    {
        var expr = ParsePrimary();

        while (true)
        {
            if (Match(TokenType.Dot))
            {
                var field = Expect(TokenType.Identifier, "Expected field name after '.'").Value;
                expr = new Subscript(expr, new StringLiteral(field));
            }
            else if (Match(TokenType.LeftBracket))
            {
                var index = ParseExpression();
                Expect(TokenType.RightBracket, "Expected ']'");
                expr = new Subscript(expr, index);
            }
            else if (Match(TokenType.Arrow))
            {
                var value = ParseExpression();
                expr = new BinaryOp(expr, "->", value);
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    private IExpression ParsePrimary()
    {
        // Null
        if (Match(TokenType.Null))
        {
            return new NullLiteral();
        }

        // Boolean literals
        if (Match(TokenType.True))
        {
            return new BooleanLiteral(true);
        }
        if (Match(TokenType.False))
        {
            return new BooleanLiteral(false);
        }

        // Number literal
        if (Check(TokenType.Number))
        {
            var num = Advance().Value;
            return new NumberLiteral(double.Parse(num, System.Globalization.CultureInfo.InvariantCulture));
        }

        // String literal
        if (Check(TokenType.String))
        {
            return new StringLiteral(Advance().Value);
        }

        // List literal
        if (Match(TokenType.LeftBracket))
        {
            return ParseListLiteral();
        }

        // Record literal
        if (Match(TokenType.LeftBrace))
        {
            var record = ParseRecord();
            Expect(TokenType.RightBrace, "Expected '}'");
            return record;
        }

        // Grouped expression
        if (Match(TokenType.LeftParen))
        {
            var expr = ParseExpression();
            Expect(TokenType.RightParen, "Expected ')'");
            return expr;
        }

        // Identifier (variable or predicate call)
        if (Check(TokenType.Identifier))
        {
            var name = Advance().Value;

            // Check if it's a function/predicate call
            if (Match(TokenType.LeftParen))
            {
                var args = ParseRecord();
                Expect(TokenType.RightParen, "Expected ')'");
                return new PredicateCall(name, args);
            }

            // It's a variable
            return new Variable(name);
        }

        throw new ParseException($"Unexpected token: {Current()}", Current().Line, Current().Column);
    }

    private IExpression ParseListLiteral()
    {
        var elements = new List<IExpression>();

        if (!Check(TokenType.RightBracket))
        {
            do
            {
                elements.Add(ParseExpression());
            } while (Match(TokenType.Comma) && !Check(TokenType.RightBracket));
        }

        Expect(TokenType.RightBracket, "Expected ']'");
        return new ListLiteral(elements);
    }

    private bool IsPredicateName(string name)
    {
        // Predicates start with uppercase OR are backtick-quoted external table references
        return !string.IsNullOrEmpty(name) && (char.IsUpper(name[0]) || name.StartsWith('`'));
    }

    private void SkipSemicolons()
    {
        while (Match(TokenType.Semicolon)) { }
    }

    private Token Current() => _tokens[_position];

    private bool IsAtEnd() => _position >= _tokens.Count || Current().Type == TokenType.Eof;

    private bool Check(TokenType type) => !IsAtEnd() && Current().Type == type;

    private bool CheckIdentifier(string value) => Check(TokenType.Identifier) && Current().Value == value;

    private Token Advance()
    {
        if (!IsAtEnd()) _position++;
        return _tokens[_position - 1];
    }

    private bool Match(TokenType type)
    {
        if (Check(type))
        {
            Advance();
            return true;
        }
        return false;
    }

    private Token Expect(TokenType type, string message)
    {
        if (Check(type))
        {
            return Advance();
        }

        var current = Current();
        throw new ParseException($"{message}, got {current.Type}({current.Value})", current.Line, current.Column);
    }
}

/// <summary>
/// Exception thrown when parsing fails.
/// </summary>
public class ParseException : Exception
{
    public int Line { get; }
    public int Column { get; }

    public ParseException(string message, int line, int column)
        : base($"{message} at line {line}, column {column}")
    {
        Line = line;
        Column = column;
    }
}
