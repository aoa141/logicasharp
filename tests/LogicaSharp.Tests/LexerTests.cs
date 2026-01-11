using LogicaSharp.Parsing;

namespace LogicaSharp.Tests;

public class LexerTests
{
    [Fact]
    public void Tokenize_EmptyString_ReturnsEof()
    {
        var lexer = new Lexer("");
        var tokens = lexer.Tokenize().ToList();

        Assert.Single(tokens);
        Assert.Equal(TokenType.Eof, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_SimpleIdentifiers_ReturnsCorrectTokens()
    {
        var lexer = new Lexer("Person Parent Ancestor");
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(4, tokens.Count); // 3 identifiers + EOF
        Assert.Equal(TokenType.Identifier, tokens[0].Type);
        Assert.Equal("Person", tokens[0].Value);
        Assert.Equal(TokenType.Identifier, tokens[1].Type);
        Assert.Equal("Parent", tokens[1].Value);
        Assert.Equal(TokenType.Identifier, tokens[2].Type);
        Assert.Equal("Ancestor", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_Numbers_ReturnsCorrectTokens()
    {
        var lexer = new Lexer("42 3.14 1e10");
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(4, tokens.Count);
        Assert.Equal(TokenType.Number, tokens[0].Type);
        Assert.Equal("42", tokens[0].Value);
        Assert.Equal(TokenType.Number, tokens[1].Type);
        Assert.Equal("3.14", tokens[1].Value);
        Assert.Equal(TokenType.Number, tokens[2].Type);
        Assert.Equal("1e10", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_Strings_ReturnsCorrectTokens()
    {
        var lexer = new Lexer("\"hello\" 'world'");
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(3, tokens.Count);
        Assert.Equal(TokenType.String, tokens[0].Type);
        Assert.Equal("hello", tokens[0].Value);
        Assert.Equal(TokenType.String, tokens[1].Type);
        Assert.Equal("world", tokens[1].Value);
    }

    [Fact]
    public void Tokenize_StringWithEscapes_ReturnsCorrectValue()
    {
        var lexer = new Lexer("\"hello\\nworld\"");
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(2, tokens.Count);
        Assert.Equal(TokenType.String, tokens[0].Type);
        Assert.Equal("hello\nworld", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_Operators_ReturnsCorrectTokens()
    {
        var lexer = new Lexer(":- := == != <= >= && || ++ +=");
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.ColonDash, tokens[0].Type);
        Assert.Equal(TokenType.ColonEquals, tokens[1].Type);
        Assert.Equal(TokenType.Equal, tokens[2].Type);
        Assert.Equal(TokenType.NotEqual, tokens[3].Type);
        Assert.Equal(TokenType.LessOrEqual, tokens[4].Type);
        Assert.Equal(TokenType.GreaterOrEqual, tokens[5].Type);
        Assert.Equal(TokenType.And, tokens[6].Type);
        Assert.Equal(TokenType.Or, tokens[7].Type);
        Assert.Equal(TokenType.PlusPlus, tokens[8].Type);
        Assert.Equal(TokenType.PlusEquals, tokens[9].Type);
    }

    [Fact]
    public void Tokenize_Delimiters_ReturnsCorrectTokens()
    {
        var lexer = new Lexer("() {} [] , : ; . @ ?");
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.LeftParen, tokens[0].Type);
        Assert.Equal(TokenType.RightParen, tokens[1].Type);
        Assert.Equal(TokenType.LeftBrace, tokens[2].Type);
        Assert.Equal(TokenType.RightBrace, tokens[3].Type);
        Assert.Equal(TokenType.LeftBracket, tokens[4].Type);
        Assert.Equal(TokenType.RightBracket, tokens[5].Type);
        Assert.Equal(TokenType.Comma, tokens[6].Type);
        Assert.Equal(TokenType.Colon, tokens[7].Type);
        Assert.Equal(TokenType.Semicolon, tokens[8].Type);
        Assert.Equal(TokenType.Dot, tokens[9].Type);
        Assert.Equal(TokenType.At, tokens[10].Type);
        Assert.Equal(TokenType.Question, tokens[11].Type);
    }

    [Fact]
    public void Tokenize_Keywords_ReturnsCorrectTokens()
    {
        var lexer = new Lexer("true false in is null");
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.True, tokens[0].Type);
        Assert.Equal(TokenType.False, tokens[1].Type);
        Assert.Equal(TokenType.In, tokens[2].Type);
        Assert.Equal(TokenType.Is, tokens[3].Type);
        Assert.Equal(TokenType.Null, tokens[4].Type);
    }

    [Fact]
    public void Tokenize_Arrow_ReturnsCorrectToken()
    {
        var lexer = new Lexer("a -> b");
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.Identifier, tokens[0].Type);
        Assert.Equal(TokenType.Arrow, tokens[1].Type);
        Assert.Equal(TokenType.Identifier, tokens[2].Type);
    }

    [Fact]
    public void Tokenize_Ellipsis_ReturnsCorrectToken()
    {
        var lexer = new Lexer("...rest");
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.Ellipsis, tokens[0].Type);
        Assert.Equal(TokenType.Identifier, tokens[1].Type);
        Assert.Equal("rest", tokens[1].Value);
    }

    [Fact]
    public void Tokenize_LineComment_SkipsComment()
    {
        var lexer = new Lexer("a # this is a comment\nb");
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(3, tokens.Count);
        Assert.Equal("a", tokens[0].Value);
        Assert.Equal("b", tokens[1].Value);
    }

    [Fact]
    public void Tokenize_BlockComment_SkipsComment()
    {
        var lexer = new Lexer("a /* this is\na block comment */ b");
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(3, tokens.Count);
        Assert.Equal("a", tokens[0].Value);
        Assert.Equal("b", tokens[1].Value);
    }

    [Fact]
    public void Tokenize_CompleteRule_ReturnsCorrectSequence()
    {
        var lexer = new Lexer("Person(name: \"Alice\", age: 30);");
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.Identifier, tokens[0].Type);
        Assert.Equal("Person", tokens[0].Value);
        Assert.Equal(TokenType.LeftParen, tokens[1].Type);
        Assert.Equal(TokenType.Identifier, tokens[2].Type);
        Assert.Equal("name", tokens[2].Value);
        Assert.Equal(TokenType.Colon, tokens[3].Type);
        Assert.Equal(TokenType.String, tokens[4].Type);
        Assert.Equal("Alice", tokens[4].Value);
        Assert.Equal(TokenType.Comma, tokens[5].Type);
        Assert.Equal(TokenType.Identifier, tokens[6].Type);
        Assert.Equal("age", tokens[6].Value);
        Assert.Equal(TokenType.Colon, tokens[7].Type);
        Assert.Equal(TokenType.Number, tokens[8].Type);
        Assert.Equal("30", tokens[8].Value);
        Assert.Equal(TokenType.RightParen, tokens[9].Type);
        Assert.Equal(TokenType.Semicolon, tokens[10].Type);
    }

    [Fact]
    public void Tokenize_Annotation_ReturnsCorrectSequence()
    {
        var lexer = new Lexer("@Engine(\"mssql\");");
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.At, tokens[0].Type);
        Assert.Equal(TokenType.Identifier, tokens[1].Type);
        Assert.Equal("Engine", tokens[1].Value);
        Assert.Equal(TokenType.LeftParen, tokens[2].Type);
        Assert.Equal(TokenType.String, tokens[3].Type);
        Assert.Equal("mssql", tokens[3].Value);
        Assert.Equal(TokenType.RightParen, tokens[4].Type);
        Assert.Equal(TokenType.Semicolon, tokens[5].Type);
    }

    [Fact]
    public void Tokenize_BacktickIdentifier_ReturnsCorrectToken()
    {
        var lexer = new Lexer("`my table`");
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(TokenType.Identifier, tokens[0].Type);
        Assert.Equal("`my table`", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_TracksLineAndColumn()
    {
        var lexer = new Lexer("a\nb c");
        var tokens = lexer.Tokenize().ToList();

        Assert.Equal(1, tokens[0].Line);
        Assert.Equal(1, tokens[0].Column);
        Assert.Equal(2, tokens[1].Line);
        Assert.Equal(1, tokens[1].Column);
        Assert.Equal(2, tokens[2].Line);
        Assert.Equal(3, tokens[2].Column);
    }
}
