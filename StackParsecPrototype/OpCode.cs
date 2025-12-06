namespace StackParsecPrototype;

public enum OpCode : byte
{
    Pure = 1,
    Error,
    Token,
    Invoke,
    InvokeM,
    Take1,
    TakeN,
    
    Label,
    Try,
    LookAhead,
    NotFollowedBy,
    EOF,
    Observing
}

