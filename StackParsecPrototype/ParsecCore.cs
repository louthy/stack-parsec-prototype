namespace StackParsecPrototype;

public readonly ref struct ParsecCore(Bytes instructions, Stack constants)
{
    public readonly Bytes Instructions = instructions;
    public readonly Stack Constants = constants;
}
