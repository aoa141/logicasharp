namespace LogicaSharp.Ast;

/// <summary>
/// Base interface for all AST nodes.
/// </summary>
public interface IAstNode
{
}

/// <summary>
/// Represents a complete Logica program.
/// </summary>
public record Program(List<IStatement> Statements) : IAstNode;

/// <summary>
/// Base interface for statements.
/// </summary>
public interface IStatement : IAstNode
{
}

/// <summary>
/// Represents an annotation like @Engine("sqlite").
/// </summary>
public record Annotation(string Name, Record? Arguments) : IStatement;

/// <summary>
/// Represents an import statement.
/// </summary>
public record Import(string Path, string? PredicateName, string? Alias) : IStatement;

/// <summary>
/// Represents a logical rule: Head :- Body;
/// </summary>
public record Rule(PredicateCall Head, IBody? Body) : IStatement;

/// <summary>
/// Represents a functor rule: Predicate := Functor(args);
/// </summary>
public record FunctorRule(string PredicateName, string FunctorName, Record Arguments) : IStatement;

/// <summary>
/// Represents a function definition: F(x) = expression;
/// </summary>
public record FunctionRule(PredicateCall Head, IExpression Value) : IStatement;

/// <summary>
/// Base interface for rule bodies.
/// </summary>
public interface IBody : IAstNode
{
}

/// <summary>
/// Represents a conjunction (AND) of body elements.
/// </summary>
public record Conjunction(List<IBody> Conjuncts) : IBody;

/// <summary>
/// Represents a disjunction (OR) of body elements.
/// </summary>
public record Disjunction(List<IBody> Disjuncts) : IBody;

/// <summary>
/// Represents a negation (~).
/// </summary>
public record Negation(IBody Body) : IBody;

/// <summary>
/// Represents a predicate call in the body.
/// </summary>
public record BodyCall(PredicateCall Call) : IBody;

/// <summary>
/// Represents an expression condition in the body.
/// </summary>
public record ExpressionCondition(IExpression Expression) : IBody;

/// <summary>
/// Base interface for expressions.
/// </summary>
public interface IExpression : IAstNode
{
}

/// <summary>
/// Represents a variable reference.
/// </summary>
public record Variable(string Name) : IExpression;

/// <summary>
/// Represents a numeric literal.
/// </summary>
public record NumberLiteral(double Value) : IExpression;

/// <summary>
/// Represents a string literal.
/// </summary>
public record StringLiteral(string Value) : IExpression;

/// <summary>
/// Represents a boolean literal.
/// </summary>
public record BooleanLiteral(bool Value) : IExpression;

/// <summary>
/// Represents a null literal.
/// </summary>
public record NullLiteral() : IExpression;

/// <summary>
/// Represents a predicate call (function call).
/// </summary>
public record PredicateCall(string PredicateName, Record Arguments) : IExpression;

/// <summary>
/// Represents a record (field-value pairs).
/// </summary>
public record Record(List<FieldValue> Fields, IExpression? Spread = null) : IExpression;

/// <summary>
/// Represents a field-value pair in a record.
/// </summary>
public record FieldValue(string Field, IExpression? Value, AggregationType? Aggregation = null);

/// <summary>
/// Aggregation type for field values.
/// </summary>
public enum AggregationType
{
    None,
    Sum,        // +=
    Count,      // ?= (distinct)
    Collect,    // List aggregation
    Min,
    Max,
    Avg
}

/// <summary>
/// Represents a list literal [a, b, c].
/// </summary>
public record ListLiteral(List<IExpression> Elements) : IExpression;

/// <summary>
/// Represents a binary operation.
/// </summary>
public record BinaryOp(IExpression Left, string Operator, IExpression Right) : IExpression;

/// <summary>
/// Represents a unary operation.
/// </summary>
public record UnaryOp(string Operator, IExpression Operand) : IExpression;

/// <summary>
/// Represents a subscript/index operation like array[0] or record.field.
/// </summary>
public record Subscript(IExpression Target, IExpression Index) : IExpression;

/// <summary>
/// Represents a conditional expression.
/// </summary>
public record ConditionalExpr(IExpression Condition, IExpression ThenExpr, IExpression ElseExpr) : IExpression;

/// <summary>
/// Represents an 'in' expression for list membership.
/// </summary>
public record InExpression(IExpression Element, IExpression Collection) : IExpression;

/// <summary>
/// Represents a type cast expression.
/// </summary>
public record CastExpr(IExpression Value, string TargetType) : IExpression;

/// <summary>
/// Represents a raw SQL expression.
/// </summary>
public record SqlExpr(string Template, Record Parameters) : IExpression;

/// <summary>
/// Represents an aggregation expression.
/// </summary>
public record Aggregation(string Operator, IExpression Expression, IExpression? GroupBy = null) : IExpression;

/// <summary>
/// Represents a lambda/anonymous function.
/// </summary>
public record Lambda(List<string> Parameters, IExpression Body) : IExpression;
