namespace LogicaSharp.Parsing;

/// <summary>
/// Represents token types in the Logica language.
/// </summary>
public enum TokenType
{
    // Literals
    Identifier,
    Number,
    String,

    // Keywords/Special identifiers
    True,
    False,
    In,
    Is,
    Null,

    // Delimiters
    LeftParen,      // (
    RightParen,     // )
    LeftBrace,      // {
    RightBrace,     // }
    LeftBracket,    // [
    RightBracket,   // ]

    // Punctuation
    Comma,          // ,
    Colon,          // :
    Semicolon,      // ;
    Dot,            // .

    // Operators
    Assign,         // =
    ColonDash,      // :-
    ColonEquals,    // :=
    Arrow,          // ->
    Pipe,           // |
    At,             // @
    Question,       // ?
    Ellipsis,       // ...

    // Comparison operators
    Equal,          // ==
    NotEqual,       // !=
    LessThan,       // <
    LessOrEqual,    // <=
    GreaterThan,    // >
    GreaterOrEqual, // >=

    // Logical operators
    And,            // &&
    Or,             // ||
    Not,            // !

    // Arithmetic operators
    Plus,           // +
    Minus,          // -
    Star,           // *
    Slash,          // /
    Percent,        // %
    Caret,          // ^

    // Compound operators
    PlusEquals,     // +=
    PlusPlus,       // ++

    // Special
    Eof,
    Error
}

/// <summary>
/// Represents a token in the Logica source code.
/// </summary>
/// <param name="Type">The type of the token.</param>
/// <param name="Value">The string value of the token.</param>
/// <param name="Line">The line number where the token appears.</param>
/// <param name="Column">The column number where the token starts.</param>
public record Token(TokenType Type, string Value, int Line, int Column)
{
    public override string ToString() => $"{Type}({Value}) at {Line}:{Column}";
}
