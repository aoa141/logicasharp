using LogicaSharp.Ast;
using LogicaSharp.Parsing;

namespace LogicaSharp.Tests;

public class ParserTests
{
    private static Program Parse(string source)
    {
        var lexer = new Lexer(source);
        var parser = new Parser(lexer.Tokenize());
        return parser.Parse();
    }

    [Fact]
    public void Parse_EmptyProgram_ReturnsEmptyStatements()
    {
        var program = Parse("");
        Assert.Empty(program.Statements);
    }

    [Fact]
    public void Parse_SimpleAnnotation_ReturnsAnnotation()
    {
        var program = Parse("@Engine(\"mssql\");");

        Assert.Single(program.Statements);
        var annotation = Assert.IsType<Annotation>(program.Statements[0]);
        Assert.Equal("Engine", annotation.Name);
        Assert.NotNull(annotation.Arguments);
        Assert.Single(annotation.Arguments.Fields);
        var value = Assert.IsType<StringLiteral>(annotation.Arguments.Fields[0].Value);
        Assert.Equal("mssql", value.Value);
    }

    [Fact]
    public void Parse_SimpleFact_ReturnsRule()
    {
        var program = Parse("Person(\"Alice\", 30);");

        Assert.Single(program.Statements);
        var rule = Assert.IsType<Rule>(program.Statements[0]);
        Assert.Equal("Person", rule.Head.PredicateName);
        Assert.Null(rule.Body);
        Assert.Equal(2, rule.Head.Arguments.Fields.Count);
    }

    [Fact]
    public void Parse_FactWithNamedFields_ReturnsCorrectFields()
    {
        var program = Parse("Person(name: \"Alice\", age: 30);");

        var rule = Assert.IsType<Rule>(program.Statements[0]);
        Assert.Equal("name", rule.Head.Arguments.Fields[0].Field);
        Assert.Equal("age", rule.Head.Arguments.Fields[1].Field);

        var name = Assert.IsType<StringLiteral>(rule.Head.Arguments.Fields[0].Value);
        Assert.Equal("Alice", name.Value);

        var age = Assert.IsType<NumberLiteral>(rule.Head.Arguments.Fields[1].Value);
        Assert.Equal(30, age.Value);
    }

    [Fact]
    public void Parse_RuleWithBody_ReturnsRuleWithConjunction()
    {
        var program = Parse("Adult(name:) :- Person(name:, age:), age >= 18;");

        var rule = Assert.IsType<Rule>(program.Statements[0]);
        Assert.Equal("Adult", rule.Head.PredicateName);
        Assert.NotNull(rule.Body);

        var conjunction = Assert.IsType<Conjunction>(rule.Body);
        Assert.Equal(2, conjunction.Conjuncts.Count);
    }

    [Fact]
    public void Parse_RuleWithDisjunction_ReturnsRuleWithDisjunction()
    {
        var program = Parse("Result(x) :- A(x) | B(x);");

        var rule = Assert.IsType<Rule>(program.Statements[0]);
        var disjunction = Assert.IsType<Disjunction>(rule.Body);
        Assert.Equal(2, disjunction.Disjuncts.Count);
    }

    [Fact]
    public void Parse_RuleWithNegation_ReturnsRuleWithNegation()
    {
        var program = Parse("NotA(x) :- B(x), ~A(x);");

        var rule = Assert.IsType<Rule>(program.Statements[0]);
        var conjunction = Assert.IsType<Conjunction>(rule.Body);
        Assert.Equal(2, conjunction.Conjuncts.Count);

        var negation = Assert.IsType<Negation>(conjunction.Conjuncts[1]);
        var call = Assert.IsType<BodyCall>(negation.Body);
        Assert.Equal("A", call.Call.PredicateName);
    }

    [Fact]
    public void Parse_FunctionDefinition_ReturnsFunctionRule()
    {
        var program = Parse("Double(x) = x * 2;");

        var func = Assert.IsType<FunctionRule>(program.Statements[0]);
        Assert.Equal("Double", func.Head.PredicateName);

        var binOp = Assert.IsType<BinaryOp>(func.Value);
        Assert.Equal("*", binOp.Operator);
    }

    [Fact]
    public void Parse_BinaryExpressions_ReturnsCorrectOperators()
    {
        var program = Parse("Test() = 1 + 2 * 3;");

        var func = Assert.IsType<FunctionRule>(program.Statements[0]);
        var add = Assert.IsType<BinaryOp>(func.Value);
        Assert.Equal("+", add.Operator);

        // Due to operator precedence, * should be nested
        var mul = Assert.IsType<BinaryOp>(add.Right);
        Assert.Equal("*", mul.Operator);
    }

    [Fact]
    public void Parse_UnaryExpression_ReturnsUnaryOp()
    {
        var program = Parse("Neg(x) = -x;");

        var func = Assert.IsType<FunctionRule>(program.Statements[0]);
        var unary = Assert.IsType<UnaryOp>(func.Value);
        Assert.Equal("-", unary.Operator);
    }

    [Fact]
    public void Parse_ListLiteral_ReturnsListLiteral()
    {
        var program = Parse("Numbers() = [1, 2, 3];");

        var func = Assert.IsType<FunctionRule>(program.Statements[0]);
        var list = Assert.IsType<ListLiteral>(func.Value);
        Assert.Equal(3, list.Elements.Count);
    }

    [Fact]
    public void Parse_RecordLiteral_ReturnsRecord()
    {
        var program = Parse("Data() = {name: \"test\", value: 42};");

        var func = Assert.IsType<FunctionRule>(program.Statements[0]);
        var record = Assert.IsType<Ast.Record>(func.Value);
        Assert.Equal(2, record.Fields.Count);
        Assert.Equal("name", record.Fields[0].Field);
        Assert.Equal("value", record.Fields[1].Field);
    }

    [Fact]
    public void Parse_PredicateCall_ReturnsPredicateCall()
    {
        var program = Parse("Result(x) = Sum(x);");

        var func = Assert.IsType<FunctionRule>(program.Statements[0]);
        var call = Assert.IsType<PredicateCall>(func.Value);
        Assert.Equal("Sum", call.PredicateName);
    }

    [Fact]
    public void Parse_Subscript_ReturnsSubscript()
    {
        var program = Parse("Field(r) = r.name;");

        var func = Assert.IsType<FunctionRule>(program.Statements[0]);
        var subscript = Assert.IsType<Subscript>(func.Value);
        var target = Assert.IsType<Variable>(subscript.Target);
        Assert.Equal("r", target.Name);
    }

    [Fact]
    public void Parse_InExpression_ReturnsInExpression()
    {
        var program = Parse("Contains(x, list) :- x in list;");

        var rule = Assert.IsType<Rule>(program.Statements[0]);
        var exprCond = Assert.IsType<ExpressionCondition>(rule.Body);
        var inExpr = Assert.IsType<InExpression>(exprCond.Expression);
    }

    [Fact]
    public void Parse_Comparison_ReturnsBinaryOp()
    {
        var program = Parse("Positive(x) :- N(x), x > 0;");

        var rule = Assert.IsType<Rule>(program.Statements[0]);
        var conjunction = Assert.IsType<Conjunction>(rule.Body);

        var exprCond = Assert.IsType<ExpressionCondition>(conjunction.Conjuncts[1]);
        var binOp = Assert.IsType<BinaryOp>(exprCond.Expression);
        Assert.Equal(">", binOp.Operator);
    }

    [Fact]
    public void Parse_BooleanLiterals_ReturnsBooleanLiteral()
    {
        var program = Parse("Bool() = true;");

        var func = Assert.IsType<FunctionRule>(program.Statements[0]);
        var literal = Assert.IsType<BooleanLiteral>(func.Value);
        Assert.True(literal.Value);
    }

    [Fact]
    public void Parse_NullLiteral_ReturnsNullLiteral()
    {
        var program = Parse("Nothing() = null;");

        var func = Assert.IsType<FunctionRule>(program.Statements[0]);
        Assert.IsType<NullLiteral>(func.Value);
    }

    [Fact]
    public void Parse_StringConcatenation_ReturnsBinaryOp()
    {
        var program = Parse("Concat(a, b) = a ++ b;");

        var func = Assert.IsType<FunctionRule>(program.Statements[0]);
        var binOp = Assert.IsType<BinaryOp>(func.Value);
        Assert.Equal("++", binOp.Operator);
    }

    [Fact]
    public void Parse_MultipleRules_ReturnsAllRules()
    {
        var source = @"
            @Engine(""mssql"");
            Person(""Alice"", 30);
            Person(""Bob"", 25);
            Adult(name:) :- Person(name:, age:), age >= 18;
        ";

        var program = Parse(source);
        Assert.Equal(4, program.Statements.Count);
        Assert.IsType<Annotation>(program.Statements[0]);
        Assert.IsType<Rule>(program.Statements[1]);
        Assert.IsType<Rule>(program.Statements[2]);
        Assert.IsType<Rule>(program.Statements[3]);
    }

    [Fact]
    public void Parse_NestedParen_ReturnsCorrectExpression()
    {
        var program = Parse("Expr() = (1 + 2) * 3;");

        var func = Assert.IsType<FunctionRule>(program.Statements[0]);
        var mul = Assert.IsType<BinaryOp>(func.Value);
        Assert.Equal("*", mul.Operator);

        var add = Assert.IsType<BinaryOp>(mul.Left);
        Assert.Equal("+", add.Operator);
    }

    [Fact]
    public void Parse_ShorthandFieldSyntax_UsesFieldAsVariable()
    {
        var program = Parse("Result(name:) :- Person(name:, age:);");

        var rule = Assert.IsType<Rule>(program.Statements[0]);

        // Head field should use name as variable
        var headField = rule.Head.Arguments.Fields[0];
        Assert.Equal("name", headField.Field);
        var headVar = Assert.IsType<Variable>(headField.Value);
        Assert.Equal("name", headVar.Name);
    }

    [Fact]
    public void Parse_ArrowExpression_ReturnsBinaryOp()
    {
        var program = Parse("Map() = [\"a\" -> 1, \"b\" -> 2];");

        var func = Assert.IsType<FunctionRule>(program.Statements[0]);
        var list = Assert.IsType<ListLiteral>(func.Value);
        Assert.Equal(2, list.Elements.Count);

        var arrow = Assert.IsType<BinaryOp>(list.Elements[0]);
        Assert.Equal("->", arrow.Operator);
    }

    [Fact]
    public void Parse_ComplexRecursiveRule_ParsesCorrectly()
    {
        var source = @"
            Parent(""Alice"", ""Bob"");
            Ancestor(a, d) :- Parent(a, d);
            Ancestor(a, d) :- Parent(a, c), Ancestor(c, d);
        ";

        var program = Parse(source);
        Assert.Equal(3, program.Statements.Count);

        // Check the recursive rule
        var recursiveRule = Assert.IsType<Rule>(program.Statements[2]);
        Assert.Equal("Ancestor", recursiveRule.Head.PredicateName);

        var conjunction = Assert.IsType<Conjunction>(recursiveRule.Body);
        Assert.Equal(2, conjunction.Conjuncts.Count);

        var ancestorCall = Assert.IsType<BodyCall>(conjunction.Conjuncts[1]);
        Assert.Equal("Ancestor", ancestorCall.Call.PredicateName);
    }
}
