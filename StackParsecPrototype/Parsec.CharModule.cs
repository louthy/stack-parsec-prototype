namespace StackParsecPrototype;

public static class CharModule<E>
{
    public static Parsec<E, char, string> @string(ReadOnlySpan<char> tokens) =>
        asString(Module<E, char>.tokens(tokens));
    
    public static Parsec<E, char, string> asString(Parsec<E, char, ReadOnlySpan<char>> p) => 
        p.Map(static s => s.ToString());
}
