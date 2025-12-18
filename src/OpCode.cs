namespace LanguageExt.RefParsec;

public enum OpCode : byte
{
    Pure = 1,
    Error,
    Return,
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
    Hidden,
    
    LookAhead,
    NotFollowedBy,
    EOF,
    Observing
}

