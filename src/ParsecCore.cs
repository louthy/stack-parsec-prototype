namespace StackParsecPrototype;

/// <summary>
/// This is the inner-structure of a parser, It contains the parsing instructions byte-code
/// and the constants used by the byte-code interpreter.
/// </summary>
/// <param name="instructions">Byte code instructions</param>
/// <param name="constants">Constant values</param>
public readonly ref struct ParsecCore(Bytes instructions, Stack constants)
{
    public readonly Bytes Instructions = instructions;
    public readonly Stack Constants = constants;
}
