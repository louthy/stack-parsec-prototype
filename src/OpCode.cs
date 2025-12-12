namespace LanguageExt.RefParsec;

public enum OpCode : byte
{
    Pure = 1,
    Error,
    OrLeft,  // Left-hand side of a choice parser
    OrRight, // Right-hand side of a choice parser
    Token,
    Tokens,
    Invoke,
    InvokeM,
    Take1,
    TakeN,
    TakeWhile1,
    TakeWhile,
    Satisfy,
    OneOf,
    NoneOf,
    Try,
    
    Label,
    LookAhead,
    NotFollowedBy,
    EOF,
    Observing
}

