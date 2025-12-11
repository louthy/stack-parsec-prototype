namespace LanguageExt.RefParsec;

/// <summary>
/// Value to push on the stack after a process is run
/// </summary>
public enum StackReply : byte
{
    NoReply         = 0x0,
    OK              = 0x1,
    Error           = 0x2
}
