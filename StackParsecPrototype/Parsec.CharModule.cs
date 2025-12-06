using System.Numerics;
using LanguageExt;

namespace StackParsecPrototype;

public static class CharModule<E>
{
    public static Parsec<E, char, string> @string(ReadOnlySpan<char> tokens) =>
        Module<E, char>.tokens(tokens).Map(s => s.ToString());
}
