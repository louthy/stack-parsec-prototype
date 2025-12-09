namespace StackParsecPrototype;

/// <summary>
/// Value to push on the stack after a process is run
/// </summary>
public enum StackReply
{
    OK              = 0x10000,
    EmptyError      = 0x20000,
    ConsumedError   = 0x40000
}

public enum StackReplyExpectation
{
    Expected   = 0x1000
}

public enum StackReplyErrorType
{
    EndOfInput = 1,
    Custom     = 2,
    Label      = 3,
    Token      = 4,
    Tokens     = 5,
    
    Mask       = 0xfff
}

public static class StackReplyExtensions
{
    extension(ref Stack self)
    {
        public bool PopError<E, T>(out ParseError<E, T> err) =>
            ParseErrorStack.PopParseError(ref self, out err);
    }

    extension(Stack self)
    {
        public (StackReply Reply, StackReplyErrorType Type, bool Expected) PeekReply()
        {
            if (self.Peek<int>(out var reply))
            {
                return ParseErrorStack.GetReplyCode(reply);
            }
            else
            {
                throw new Exception("Reply not found");
            }
        }
        
        public static bool IsOK(Stack stack) =>
            stack.Peek<int>(out var top) &&
            (top & (int)StackReply.OK) == (int)StackReply.OK;

        public Stack PushOK() =>
            self.Push((int)StackReply.OK);
    }
}