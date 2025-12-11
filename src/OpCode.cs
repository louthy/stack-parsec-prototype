namespace LanguageExt.RefParsec;

public enum OpCode : byte
{
    Pure = 1,
    Error,
    Or,
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

