namespace StackParsecPrototype;

public readonly ref struct ParsecCore(ByteSeq instructions, Stack constants)
{
    public readonly ByteSeq Instructions = instructions;
    public readonly Stack Constants = constants;
}
