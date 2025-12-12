namespace LanguageExt.RefParsec;

public readonly ref struct State<E, T>
{
    public readonly ReadOnlySpan<T> Input;
    public readonly SourcePos Position;

    public State(ReadOnlySpan<T> input, SourcePos position)
    {
        Input = input;
        Position = position;
    }

    /// <summary>
    /// Move to the beginning of the next line
    /// </summary>
    /// <returns></returns>
    public State<E, T> NextLine =>
        new (Input, Position.NextLine);

    /// <summary>
    /// Move to the next token
    /// </summary>
    /// <returns></returns>
    public State<E, T> NextToken =>
        new (Input, Position.NextToken);

    /// <summary>
    /// Move to the next token
    /// </summary>
    /// <returns></returns>
    public State<E, T> Next(int amount) =>
        new (Input, Position.Next(amount));
}
