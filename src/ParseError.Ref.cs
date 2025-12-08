/*
using System.Numerics;

namespace StackParsecPrototype;

public readonly ref struct ParseErrorRef<E, T>
    where T : IEqualityOperators<T, T, bool>
{
    readonly SourcePosRef Position;
    readonly ReadOnlySpan<E> CustomErrors;
    readonly ReadOnlySpan<T> UnexpectedTokens;
    readonly ReadOnlySpan<string> UnexpectedLabels;
    readonly ReadOnlySpan<T> ExpectedTokens;
    readonly ReadOnlySpan<string> ExpectedLabels;
    readonly byte EndOfInput;
    readonly byte HasBeenInitialised;

    ParseErrorRef(
        SourcePosRef position,
        ReadOnlySpan<E> customErrors,
        ReadOnlySpan<T> unexpectedTokens,
        ReadOnlySpan<string> unexpectedLabels,
        ReadOnlySpan<T> expectedTokens, 
        ReadOnlySpan<string> expectedLabels,
        byte endOfInput)
    {
        Position = position;
        CustomErrors = customErrors;
        UnexpectedTokens = unexpectedTokens;
        UnexpectedLabels = unexpectedLabels;
        ExpectedTokens = expectedTokens;
        ExpectedLabels = expectedLabels;
        EndOfInput = endOfInput;
        HasBeenInitialised = 1;
    }

    ParseErrorRef(
        SourcePosRef position,
        ReadOnlySpan<T> unexpectedTokens,
        ReadOnlySpan<T> expectedTokens)
    {
        Position = position;
        CustomErrors = default;
        UnexpectedTokens = unexpectedTokens;
        UnexpectedLabels = ReadOnlySpan<string>.Empty;
        ExpectedTokens = expectedTokens;
        ExpectedLabels = ReadOnlySpan<string>.Empty;
        EndOfInput = 0;
        HasBeenInitialised = 1;
    }

    ParseErrorRef(
        SourcePosRef position,
        ReadOnlySpan<string> unexpectedLabels,
        ReadOnlySpan<string> expectedLabels)
    {
        Position = position;
        CustomErrors = default;
        UnexpectedTokens = ReadOnlySpan<T>.Empty;
        UnexpectedLabels = unexpectedLabels;
        ExpectedTokens = ReadOnlySpan<T>.Empty;
        ExpectedLabels = expectedLabels;
        EndOfInput = 0;
        HasBeenInitialised = 1;
    }

    ParseErrorRef(
        SourcePosRef position,
        byte endOfInput)
    {
        Position = position;
        CustomErrors = default;
        UnexpectedTokens = ReadOnlySpan<T>.Empty;
        UnexpectedLabels = ReadOnlySpan<string>.Empty;
        ExpectedTokens = ReadOnlySpan<T>.Empty;
        ExpectedLabels = ReadOnlySpan<string>.Empty;
        EndOfInput = endOfInput;
        HasBeenInitialised = 1;
    }

    ParseErrorRef(
        SourcePosRef position,
        ReadOnlySpan<E> customErrors)
    {
        Position = position;
        CustomErrors = customErrors;
        UnexpectedTokens = ReadOnlySpan<T>.Empty;
        UnexpectedLabels = ReadOnlySpan<string>.Empty;
        ExpectedTokens = ReadOnlySpan<T>.Empty;
        ExpectedLabels = ReadOnlySpan<string>.Empty;
        EndOfInput = 0;
        HasBeenInitialised = 1;
    }

    public ParseError<E, T> UnRef() =>
        new (Position.UnRef(), CustomErrors, UnexpectedTokens, UnexpectedLabels, ExpectedTokens, ExpectedLabels, EndOfInput);
    
    public static ParseErrorRef<E, T> Tokens(SourcePosRef position, ReadOnlySpan<T> unexpectedTokens, ReadOnlySpan<T> expectedTokens) =>
        new (position, unexpectedTokens, expectedTokens);
    
    public static ParseErrorRef<E, T> Tokens(SourcePosRef position, ReadOnlySpan<T> unexpectedTokens) =>
        new (position, unexpectedTokens, ReadOnlySpan<T>.Empty);
    
    public static ParseErrorRef<E, T> Label(SourcePosRef position, ReadOnlySpan<string> expectedLabels, ReadOnlySpan<string> unexpectedLabels) =>
        new (position, expectedLabels, unexpectedLabels);
    
    public static ParseErrorRef<E, T> ExpectedEndOfInput(SourcePosRef position) =>
        new (position, 1);
    
    public static ParseErrorRef<E, T> UnexpectedEndOfInput(SourcePosRef position) =>
        new (position, 2);

    public static ParseErrorRef<E, T> Custom(SourcePosRef position, ReadOnlySpan<E> errors) =>
        new (position, errors);

    public static ParseErrorRef<E, T> operator +(ParseErrorRef<E, T> lhs, ParseErrorRef<E, T> rhs) =>
        lhs.Combine(rhs);
    
    public ParseErrorRef<E, T> Combine(ParseErrorRef<E, T> rhs)
    {
        if (rhs.HasBeenInitialised == 0) return this;
        if (HasBeenInitialised == 0) return rhs;
        
        // Custom errors take precedence over everything else
        var ce1 = CustomErrors;
        var ce2 = rhs.CustomErrors;
        switch (ce1.IsEmpty, ce2.IsEmpty)
        {
            case (true, true):
                break;

            case (false, true):
                return this;

            case (true, false):
                return rhs;

            default:
                return new ParseErrorRef<E, T>(Position, Concat(ce1, ce2));
        }

        var ut1 = UnexpectedTokens;
        var ut2 = rhs.UnexpectedTokens;
        var ut = (ut1.IsEmpty, ut2.IsEmpty) switch
                 {
                     (true, true) =>
                         ReadOnlySpan<T>.Empty,

                     (false, true) =>
                         ut1,

                     (true, false) =>
                         ut2,
                     
                     _ => Concat(ut1, ut2)
                 };

        var ul1 = UnexpectedLabels;
        var ul2 = rhs.UnexpectedLabels;
        var ul = (ut1.IsEmpty, ut2.IsEmpty) switch
                 {
                     (true, true) =>
                         ReadOnlySpan<string>.Empty,

                     (false, true) =>
                         ul1,

                     (true, false) =>
                         ul2,
                     
                     _ => Concat(ul1, ul2)
                 };
        
        var et1 = ExpectedTokens;
        var et2 = rhs.ExpectedTokens;
        var et = (ut1.IsEmpty, ut2.IsEmpty) switch
                 {
                     (true, true) =>
                         ReadOnlySpan<T>.Empty,

                     (false, true) =>
                         et1,

                     (true, false) =>
                         et2,

                     _ => Concat(et1, et2)
                 };

        var el1 = ExpectedLabels;
        var el2 = rhs.ExpectedLabels;
        var el = (et1.IsEmpty, et2.IsEmpty) switch
                 {
                     (true, true) =>
                         ReadOnlySpan<string>.Empty,

                     (false, true) =>
                         el1,

                     (true, false) =>
                         el2,

                     _ => Concat(el1, el2)
                 };

        var ei = (byte)(EndOfInput | rhs.EndOfInput);
        
        return new ParseErrorRef<E, T>(Position, ReadOnlySpan<E>.Empty, ut, ul, et, el, ei);
    }

    ReadOnlySpan<X> Concat<X>(ReadOnlySpan<X> lhs, ReadOnlySpan<X> rhs)
    {
        var xs = new X[lhs.Length + rhs.Length];
        lhs.CopyTo(xs);
        rhs.CopyTo(xs.AsSpan(lhs.Length));
        return xs;
    }
}
*/
