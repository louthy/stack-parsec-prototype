namespace StackParsecPrototype;

/// <summary>
/// Value to push on the stack after a process is run
/// </summary>
public enum StackReply
{
    OK,
    EmptyError,
    ConsumedError
}