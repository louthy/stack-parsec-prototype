using LanguageExt;
using LanguageExt.Traits;

namespace LanguageExt.RefParsec;

/// <summary>
///  A data type that is used to represent “unexpected or expected” items in
/// 'ParseError'. It is parametrised over the token type `T`.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public abstract record ErrorItem<T> : ErrorItemBase, K<ErrorItem, T>, IComparable<ErrorItem<T>>
{
    public ErrorItem<U> Map<U>(Func<T, U> f) =>
        this.Kind().Map(f).As();

    public ErrorItem<U> Select<U>(Func<T, U> f) =>
        this.Kind().Map(f).As();

    public override bool IsFancy =>
        false;

    /// <summary>
    /// Non-empty stream of tokens
    /// </summary>
    /// <param name="Tokens">Tokens</param>
    public record Tokens(in Seq<T> Items) : ErrorItem<T>
    {
        public override int CompareTo(ErrorItem<T>? other) =>
            other switch
            {
                Tokens tokens => Items.CompareTo(tokens.Items),
                _             => 1
            };

        public override string ToString() =>
            string.Join(", ", Items.Map(t => $"'{t}'"));
    }

    /// <summary>
    /// Label (should not be empty)
    /// </summary>
    /// <param name="Value">Label value</param>
    public record Label(string Value) : ErrorItem<T>
    {
        internal static readonly ErrorItem<T> Hidden = new Label("");
        
        public override int CompareTo(ErrorItem<T>? other) =>
            other switch
            {
                Label label => string.Compare(Value, label.Value, StringComparison.Ordinal),
                Tokens      => -1,
                _           => 1
            };

        public override string ToString() =>
            Value;
    }

    /// <summary>
    /// End of input
    /// </summary>
    public record EndfOfInput : ErrorItem<T>
    {
        public override int CompareTo(ErrorItem<T>? other) =>
            other switch
            {
                EndfOfInput => 0,
                _           => -1
            };

        public override string ToString() =>
            "end-of-input";
    }

    public abstract int CompareTo(ErrorItem<T>? other);

    public static bool operator >(ErrorItem<T> l, ErrorItem<T> r) =>
        l.CompareTo(r) > 0;

    public static bool operator >=(ErrorItem<T> l, ErrorItem<T> r) =>
        l.CompareTo(r) >= 0;

    public static bool operator <(ErrorItem<T> l, ErrorItem<T> r) =>
        l.CompareTo(r) < 0;

    public static bool operator <=(ErrorItem<T> l, ErrorItem<T> r) =>
        l.CompareTo(r) <= 0;
}
