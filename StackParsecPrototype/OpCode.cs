namespace StackParsecPrototype;

public enum OpCode : byte
{
    Pure = 1,
    Error,
    Tokens,
    Invoke,
    InvokeM,
    Take1,
    TakeN,
    TakeWhile1,
    TakeWhile,
    
    Label,
    Try,
    LookAhead,
    NotFollowedBy,
    EOF,
    Observing
}

