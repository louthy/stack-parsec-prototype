using System.Numerics;
using System.Text;
using LanguageExt.Common;

namespace StackParsecPrototype;

public readonly struct ParseError<T, E> : 
    IEqualityOperators<ParseError<T, E>, ParseError<T, E>, bool>
    where T : IEqualityOperators<T, T, bool>
{
    readonly SourcePos Position;
    readonly E[] CustomErrors;
    readonly T[] UnexpectedTokens;
    readonly string[] UnexpectedLabels;
    readonly T[] ExpectedTokens;
    readonly string[] ExpectedLabels;
    readonly byte EndOfInput;

    internal ParseError(
        SourcePos position,
        ReadOnlySpan<E> customErrors,
        ReadOnlySpan<T> unexpectedTokens,
        ReadOnlySpan<string> unexpectedLabels,
        ReadOnlySpan<T> expectedTokens, 
        ReadOnlySpan<string> expectedLabels,
        byte endOfInput)
    {
        Position = position;
        CustomErrors = customErrors.ToArray();
        UnexpectedTokens = unexpectedTokens.ToArray();
        UnexpectedLabels = unexpectedLabels.ToArray();
        ExpectedTokens = expectedTokens.ToArray();
        ExpectedLabels = expectedLabels.ToArray();
        EndOfInput = endOfInput;
    }

    ParseError(
        SourcePos position,
        ReadOnlySpan<T> unexpectedTokens,
        ReadOnlySpan<T> expectedTokens)
    {
        Position = position;
        CustomErrors = [];
        UnexpectedTokens = unexpectedTokens.ToArray();
        UnexpectedLabels = [];
        ExpectedTokens = expectedTokens.ToArray();
        ExpectedLabels = [];
        EndOfInput = 0;
    }

    ParseError(
        SourcePos position,
        ReadOnlySpan<string> unexpectedLabels,
        ReadOnlySpan<string> expectedLabels)
    {
        Position = position;
        CustomErrors = [];
        UnexpectedTokens = [];
        UnexpectedLabels = unexpectedLabels.ToArray();
        ExpectedTokens = [];
        ExpectedLabels = expectedLabels.ToArray();
        EndOfInput = 0;
    }

    ParseError(
        SourcePos position,
        byte endOfInput)
    {
        Position = position;
        CustomErrors = [];
        UnexpectedTokens = [];
        UnexpectedLabels = [];
        ExpectedTokens = [];
        ExpectedLabels = [];
        EndOfInput = endOfInput;
    }

    ParseError(
        SourcePos position,
        ReadOnlySpan<E> customErrors)
    {
        Position = position;
        CustomErrors = customErrors.ToArray();
        UnexpectedTokens = [];
        UnexpectedLabels = [];
        ExpectedTokens = [];
        ExpectedLabels = [];
        EndOfInput = 0;
    }
    
    public static ParseError<T, E> Tokens(SourcePos position, ReadOnlySpan<T> unexpectedTokens, ReadOnlySpan<T> expectedTokens) =>
        new (position, unexpectedTokens, expectedTokens);
    
    public static ParseError<T, E> Tokens(SourcePos position, ReadOnlySpan<T> unexpectedTokens) =>
        new (position, unexpectedTokens, ReadOnlySpan<T>.Empty);
    
    public static ParseError<T, E> Label(SourcePos position, ReadOnlySpan<string> expectedLabels, ReadOnlySpan<string> unexpectedLabels) =>
        new (position, expectedLabels, unexpectedLabels);
    
    public static ParseError<T, E> ExpectedEndOfInput(SourcePos position) =>
        new (position, 1);
    
    public static ParseError<T, E> UnexpectedEndOfInput(SourcePos position) =>
        new (position, 2);

    public static ParseError<T, E> Custom(SourcePos position, ReadOnlySpan<E> errors) =>
        new (position, errors);

    public static ParseError<T, E> operator +(ParseError<T, E> lhs, ParseError<T, E> rhs) =>
        lhs.Combine(rhs);
    
    public ParseError<T, E> Combine(ParseError<T, E> rhs)
    {
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
                return new ParseError<T, E>(Position, Concat(ce1, ce2));
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
        
        return new ParseError<T, E>(Position, ReadOnlySpan<E>.Empty, ut, ul, et, el, ei);
    }

    ReadOnlySpan<X> Concat<X>(ReadOnlySpan<X> lhs, ReadOnlySpan<X> rhs)
    {
        var xs = new X[lhs.Length + rhs.Length];
        lhs.CopyTo(xs);
        rhs.CopyTo(xs.AsSpan(lhs.Length));
        return xs;
    }

    public static bool operator ==(ParseError<T, E> left, ParseError<T, E> right) =>
        left.Position == right.Position;

    public static bool operator !=(ParseError<T, E> left, ParseError<T, E> right) =>
        left.Position != right.Position;

    internal void CollectErrorDisplay(List<string> texts)
    {
        var sb = new StringBuilder();
        if (CustomErrors.Length > 0)
        {
            foreach (var ce in CustomErrors)
            {
                sb.Append(Position);
                sb.Append(" ");
                sb.AppendLine(ce?.ToString() ?? "<no error display text>");
            }
            texts.Add(sb.ToString());
            return;
        }

        if(UnexpectedLabels.Length > 0)
        {
            sb.Clear();
            sb.Append(Position);
            sb.Append(" unexpected ");
            sb.AppendLine(string.Join(", ", UnexpectedLabels));
            texts.Add(sb.ToString());
        }

        if (UnexpectedTokens.Length > 0)
        {
            sb.Clear();
            sb.Append(Position);
            sb.Append(" unexpected ");
            sb.AppendLine(string.Join(", ", UnexpectedTokens));
            texts.Add(sb.ToString());
        }

        if(ExpectedLabels.Length > 0)
        {
            sb.Clear();
            sb.Append(Position);
            sb.Append(" expected ");
            sb.AppendLine(string.Join(", ", ExpectedLabels));
            texts.Add(sb.ToString());
        }

        if (ExpectedTokens.Length > 0)
        {
            sb.Clear();
            sb.Append(Position);
            sb.Append(" expected ");
            sb.AppendLine(string.Join(", ", ExpectedTokens));
            texts.Add(sb.ToString());
        }

        if ((EndOfInput & 1) == 1)
        {
            sb.Clear();
            sb.Append(Position);
            sb.AppendLine(" expected end of input");
            texts.Add(sb.ToString());
        }

        if ((EndOfInput & 2) == 2)
        {
            sb.Clear();
            sb.Append(Position);
            sb.AppendLine(" unexpected end of input");
            texts.Add(sb.ToString());
        }
    }

    public override string ToString()
    {
        List<string> ts = [];
        CollectErrorDisplay(ts);
        return string.Concat(ts);
    }    
}
