namespace StackParsecPrototype;

/// <summary>
/// Operations specific to character parsers
/// </summary>
/// <typeparam name="E">Error type</typeparam>
public static class CharModule<E>
{
    public static Parsec<E, char, string> @string(ReadOnlySpan<char> tokens) =>
        asString(Module<E, char>.tokens(tokens));
    
    public static Parsec<E, char, string> asString(Parsec<E, char, ReadOnlySpan<char>> p) => 
        p.Map(static s => s.ToString());
}
