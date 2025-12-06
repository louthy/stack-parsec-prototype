namespace StackParsecPrototype;

public enum OpCode : byte
{
    Pure = 1,
    Error,
    Label,
    Try,
    LookAhead,
    NotFollowedBy,
    EOF,
    Observing,
    Take1,
    TakeN,
    Bind,
    Flatten,
    Invoke,
    InvokeM
}

