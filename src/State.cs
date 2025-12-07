using System.Numerics;

namespace StackParsecPrototype;

public readonly ref struct State<T, E>
    where T : IEqualityOperators<T, T, bool>
{
    public readonly ReadOnlySpan<T> Input;
    public readonly SourcePosRef Position;
    public readonly RefSeq<ParseError<T, E>> ParseErrors;

    public State(ReadOnlySpan<T> input, SourcePosRef position, RefSeq<ParseError<T, E>> parseErrors)
    {
        Input = input;
        Position = position;
        ParseErrors = parseErrors;
    }

    public State<T, E> AddError(ParseErrorRef<T, E> error) => 
        new(Input, Position, ParseErrors.Add(error.UnRef()));

    public State<T, E> AddError(ParseError<T, E> error) => 
        new(Input, Position, ParseErrors.Add(error));
    
    /// <summary>
    /// Move to the beginning of the next line
    /// </summary>
    /// <returns></returns>
    public State<T, E> NextLine =>
        new (Input, Position.NextLine, ParseErrors);

    /// <summary>
    /// Move to the next token
    /// </summary>
    /// <returns></returns>
    public State<T, E> NextToken =>
        new (Input, Position.NextToken, ParseErrors);

    /// <summary>
    /// Move to the next token
    /// </summary>
    /// <returns></returns>
    public State<T, E> Next(int amount) =>
        new (Input, Position.Next(amount), ParseErrors);
}
