using System.Numerics;

namespace StackParsecPrototype;

public static class ParserResult
{
    public static ParserResult<E, T, A> ConsumedOK<E, T, A>(A value, State<T, E> state)
        where T : IEqualityOperators<T, T, bool>
        where A : allows ref struct =>
        new(1 /*OK*/ | 4 /*Consumed*/, state, value);

    public static ParserResult<E, T, A> EmptyOK<E, T, A>(A value, State<T, E> state)
        where T : IEqualityOperators<T, T, bool>
        where A : allows ref struct =>
        new(1 /*OK*/ | 8 /*Empty*/, state, value);
    
    public static ParserResult<E, T, A> ConsumedErr<E, T, A>(ParseError<T, E> error, State<T, E> state)
        where T : IEqualityOperators<T, T, bool>
        where A : allows ref struct =>
        new(2 /*Failed*/ | 4 /*Consumed*/, state.AddError(error), default);

    public static ParserResult<E, T, A> EmptyErr<E, T, A>(ParseError<T, E> error, State<T, E> state)
        where T : IEqualityOperators<T, T, bool>
        where A : allows ref struct =>
        new(2 /*Failed*/ | 8 /*Empty*/, state.AddError(error), default);
}

public readonly ref struct ParserResult<E, T, A>
    where T : IEqualityOperators<T, T, bool>
    where A : allows ref struct
{
    readonly int flags;
    readonly State<T, E> state;
    readonly A? value;

    public bool Uninitialised =>
        flags == 0;
    
    public bool Ok =>
        (flags & 1) == 1;

    public bool Failed =>
        (flags & 2) == 2;

    public bool Consumed =>
        (flags & 4) == 4;

    public bool Empty =>
        (flags & 8) == 8;

    public State<T, E> State => 
        state;
    
    public A Value =>
        Ok && value is not null
            ? value
            : throw new InvalidOperationException("Cannot get value from failed result");

    public ReadOnlySpan<ParseError<T, E>> Errors =>
        Failed
            ? state.ParseErrors.Span()
            : ReadOnlySpan<ParseError<T, E>>.Empty;
    
    internal ParserResult(int flags, State<T, E> state, A? value)
    {
        this.flags = flags;
        this.state = state;
        this.value = value;
    }
}