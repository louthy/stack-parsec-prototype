namespace LanguageExt.RefParsec;

public enum ErrorStackType : byte
{
    // Fancy errors
    Fail                = 0x01,
    Indentation         = 0x02,
    Custom              = 0x03,
    
    // Trivial errors
    Token               = 0x04,
    Tokens              = 0x05 ,
    Label               = 0x06,
    Hidden              = 0x07,
    EndOfInput          = 0x08,
    
    // Expectations
    Expected            = 0x10,
    Unexpected          = 0x20,
    Fancy               = 0x40,
    Terminator          = 0x80      // This byte signifies the end of the sequence of ErrorItems
}