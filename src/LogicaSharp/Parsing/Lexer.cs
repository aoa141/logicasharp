using System.Text;

namespace LogicaSharp.Parsing;

/// <summary>
/// Lexer for the Logica programming language.
/// Converts source code into a stream of tokens.
/// </summary>
public class Lexer
{
    private readonly string _source;
    private int _position;
    private int _line = 1;
    private int _column = 1;

    private static readonly Dictionary<string, TokenType> Keywords = new()
    {
        ["true"] = TokenType.True,
        ["false"] = TokenType.False,
        ["in"] = TokenType.In,
        ["is"] = TokenType.Is,
        ["null"] = TokenType.Null,
    };

    public Lexer(string source)
    {
        _source = source;
    }

    public IEnumerable<Token> Tokenize()
    {
        while (!IsAtEnd())
        {
            SkipWhitespaceAndComments();
            if (IsAtEnd()) break;

            var token = NextToken();
            if (token != null)
            {
                yield return token;
            }
        }

        yield return new Token(TokenType.Eof, "", _line, _column);
    }

    private Token? NextToken()
    {
        int startLine = _line;
        int startColumn = _column;
        char c = Advance();

        return c switch
        {
            '(' => new Token(TokenType.LeftParen, "(", startLine, startColumn),
            ')' => new Token(TokenType.RightParen, ")", startLine, startColumn),
            '{' => new Token(TokenType.LeftBrace, "{", startLine, startColumn),
            '}' => new Token(TokenType.RightBrace, "}", startLine, startColumn),
            '[' => new Token(TokenType.LeftBracket, "[", startLine, startColumn),
            ']' => new Token(TokenType.RightBracket, "]", startLine, startColumn),
            ',' => new Token(TokenType.Comma, ",", startLine, startColumn),
            ';' => new Token(TokenType.Semicolon, ";", startLine, startColumn),
            '@' => new Token(TokenType.At, "@", startLine, startColumn),
            '?' => new Token(TokenType.Question, "?", startLine, startColumn),
            '^' => new Token(TokenType.Caret, "^", startLine, startColumn),
            '*' => new Token(TokenType.Star, "*", startLine, startColumn),
            '/' => new Token(TokenType.Slash, "/", startLine, startColumn),
            '%' => new Token(TokenType.Percent, "%", startLine, startColumn),

            ':' => MatchColonOperator(startLine, startColumn),
            '.' => MatchDotOperator(startLine, startColumn),
            '-' => MatchMinusOperator(startLine, startColumn),
            '+' => MatchPlusOperator(startLine, startColumn),
            '=' => MatchEqualsOperator(startLine, startColumn),
            '!' => MatchNotOperator(startLine, startColumn),
            '<' => MatchLessOperator(startLine, startColumn),
            '>' => MatchGreaterOperator(startLine, startColumn),
            '&' => MatchAndOperator(startLine, startColumn),
            '|' => MatchPipeOperator(startLine, startColumn),
            '~' => new Token(TokenType.Not, "~", startLine, startColumn),

            '"' => ScanString('"', startLine, startColumn),
            '\'' => ScanString('\'', startLine, startColumn),
            '`' => ScanBacktickString(startLine, startColumn),

            _ when char.IsDigit(c) => ScanNumber(c, startLine, startColumn),
            _ when IsIdentifierStart(c) => ScanIdentifier(c, startLine, startColumn),

            _ => new Token(TokenType.Error, c.ToString(), startLine, startColumn)
        };
    }

    private Token MatchColonOperator(int line, int col)
    {
        if (Match('-')) return new Token(TokenType.ColonDash, ":-", line, col);
        if (Match('=')) return new Token(TokenType.ColonEquals, ":=", line, col);
        return new Token(TokenType.Colon, ":", line, col);
    }

    private Token MatchDotOperator(int line, int col)
    {
        if (Match('.') && Match('.')) return new Token(TokenType.Ellipsis, "...", line, col);
        return new Token(TokenType.Dot, ".", line, col);
    }

    private Token MatchMinusOperator(int line, int col)
    {
        if (Match('>')) return new Token(TokenType.Arrow, "->", line, col);
        return new Token(TokenType.Minus, "-", line, col);
    }

    private Token MatchPlusOperator(int line, int col)
    {
        if (Match('+')) return new Token(TokenType.PlusPlus, "++", line, col);
        if (Match('=')) return new Token(TokenType.PlusEquals, "+=", line, col);
        return new Token(TokenType.Plus, "+", line, col);
    }

    private Token MatchEqualsOperator(int line, int col)
    {
        if (Match('=')) return new Token(TokenType.Equal, "==", line, col);
        return new Token(TokenType.Assign, "=", line, col);
    }

    private Token MatchNotOperator(int line, int col)
    {
        if (Match('=')) return new Token(TokenType.NotEqual, "!=", line, col);
        return new Token(TokenType.Not, "!", line, col);
    }

    private Token MatchLessOperator(int line, int col)
    {
        if (Match('=')) return new Token(TokenType.LessOrEqual, "<=", line, col);
        return new Token(TokenType.LessThan, "<", line, col);
    }

    private Token MatchGreaterOperator(int line, int col)
    {
        if (Match('=')) return new Token(TokenType.GreaterOrEqual, ">=", line, col);
        return new Token(TokenType.GreaterThan, ">", line, col);
    }

    private Token MatchAndOperator(int line, int col)
    {
        if (Match('&')) return new Token(TokenType.And, "&&", line, col);
        return new Token(TokenType.Error, "&", line, col);
    }

    private Token MatchPipeOperator(int line, int col)
    {
        if (Match('|')) return new Token(TokenType.Or, "||", line, col);
        return new Token(TokenType.Pipe, "|", line, col);
    }

    private Token ScanString(char quote, int line, int col)
    {
        var sb = new StringBuilder();
        while (!IsAtEnd() && Peek() != quote)
        {
            if (Peek() == '\\' && _position + 1 < _source.Length)
            {
                Advance(); // consume backslash
                char escaped = Advance();
                sb.Append(escaped switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    '\\' => '\\',
                    '"' => '"',
                    '\'' => '\'',
                    _ => escaped
                });
            }
            else if (Peek() == '\n')
            {
                sb.Append(Advance());
            }
            else
            {
                sb.Append(Advance());
            }
        }

        if (IsAtEnd())
        {
            return new Token(TokenType.Error, "Unterminated string", line, col);
        }

        Advance(); // consume closing quote
        return new Token(TokenType.String, sb.ToString(), line, col);
    }

    private Token ScanBacktickString(int line, int col)
    {
        var sb = new StringBuilder();
        sb.Append('`');
        while (!IsAtEnd() && Peek() != '`')
        {
            sb.Append(Advance());
        }

        if (IsAtEnd())
        {
            return new Token(TokenType.Error, "Unterminated backtick string", line, col);
        }

        sb.Append(Advance()); // consume closing backtick
        return new Token(TokenType.Identifier, sb.ToString(), line, col);
    }

    private Token ScanNumber(char first, int line, int col)
    {
        var sb = new StringBuilder();
        sb.Append(first);

        while (!IsAtEnd() && (char.IsDigit(Peek()) || Peek() == '.'))
        {
            if (Peek() == '.' && (_position + 1 >= _source.Length || !char.IsDigit(_source[_position + 1])))
            {
                break; // Don't consume dot if not followed by digit
            }
            sb.Append(Advance());
        }

        // Handle scientific notation
        if (!IsAtEnd() && (Peek() == 'e' || Peek() == 'E'))
        {
            sb.Append(Advance());
            if (!IsAtEnd() && (Peek() == '+' || Peek() == '-'))
            {
                sb.Append(Advance());
            }
            while (!IsAtEnd() && char.IsDigit(Peek()))
            {
                sb.Append(Advance());
            }
        }

        return new Token(TokenType.Number, sb.ToString(), line, col);
    }

    private Token ScanIdentifier(char first, int line, int col)
    {
        var sb = new StringBuilder();
        sb.Append(first);

        while (!IsAtEnd() && IsIdentifierChar(Peek()))
        {
            sb.Append(Advance());
        }

        string identifier = sb.ToString();

        if (Keywords.TryGetValue(identifier, out var keyword))
        {
            return new Token(keyword, identifier, line, col);
        }

        return new Token(TokenType.Identifier, identifier, line, col);
    }

    private void SkipWhitespaceAndComments()
    {
        while (!IsAtEnd())
        {
            char c = Peek();

            if (char.IsWhiteSpace(c))
            {
                Advance();
            }
            else if (c == '#')
            {
                // Line comment
                while (!IsAtEnd() && Peek() != '\n')
                {
                    Advance();
                }
            }
            else if (c == '/' && _position + 1 < _source.Length && _source[_position + 1] == '*')
            {
                // Block comment
                Advance(); // consume /
                Advance(); // consume *
                while (!IsAtEnd())
                {
                    if (Peek() == '*' && _position + 1 < _source.Length && _source[_position + 1] == '/')
                    {
                        Advance(); // consume *
                        Advance(); // consume /
                        break;
                    }
                    Advance();
                }
            }
            else
            {
                break;
            }
        }
    }

    private bool IsAtEnd() => _position >= _source.Length;

    private char Peek() => _position < _source.Length ? _source[_position] : '\0';

    private char Advance()
    {
        char c = _source[_position++];
        if (c == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }
        return c;
    }

    private bool Match(char expected)
    {
        if (IsAtEnd() || _source[_position] != expected)
        {
            return false;
        }
        Advance();
        return true;
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
