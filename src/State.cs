namespace StackParsecPrototype;

public readonly ref struct State<E, T>
{
    public readonly ReadOnlySpan<T> Input;
    public readonly SourcePosRef Position;
    public readonly RefSeq<ParseError<E, T>> ParseErrors;

    public State(ReadOnlySpan<T> input, SourcePosRef position, RefSeq<ParseError<E, T>> parseErrors)
    {
        Input = input;
        Position = position;
        ParseErrors = parseErrors;
    }

    public State<E, T> AddError(ParseError<E, T> error) => 
        new(Input, Position, ParseErrors.Add(error));
    
    /// <summary>
    /// Move to the beginning of the next line
    /// </summary>
    /// <returns></returns>
    public State<E, T> NextLine =>
        new (Input, Position.NextLine, ParseErrors);

    /// <summary>
    /// Move to the next token
    /// </summary>
    /// <returns></returns>
    public State<E, T> NextToken =>
        new (Input, Position.NextToken, ParseErrors);

    /// <summary>
    /// Move to the next token
    /// </summary>
    /// <returns></returns>
    public State<E, T> Next(int amount) =>
        new (Input, Position.Next(amount), ParseErrors);
}
