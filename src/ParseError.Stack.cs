namespace StackParsecPrototype;

public static class ParseErrorStack
{
    public static Stack Token<T>(T token, bool consumed, bool expected, SourcePosRef position, Stack stack) =>
        stack.Push(token)
             .Push(position)
             .Push(MakeReplyCode(consumed, expected, StackReplyErrorType.Token));

    public static Stack Tokens<T>(ReadOnlySpan<T> tokens, bool consumed, bool expected, SourcePosRef position, Stack stack) =>
        stack.Push(tokens)
             .Push(position)
             .Push(MakeReplyCode(consumed, expected, StackReplyErrorType.Token));

    public static Stack Label(ReadOnlySpan<char> label, bool consumed, bool expected, SourcePosRef position, Stack stack) =>
        stack.Push(label)
             .Push(position)
             .Push(MakeReplyCode(consumed, expected, StackReplyErrorType.Label));

    public static Stack EndOfInput(bool consumed, bool expected, SourcePosRef position, Stack stack) =>
        stack.Push(position)
             .Push(MakeReplyCode(consumed, expected, StackReplyErrorType.EndOfInput));

    public static Stack Custom<E>(
        E error,
        bool consumed,
        bool expected,
        SourcePosRef position,
        Stack stack) =>
        stack.Push(error)
             .Push(position)
             .Push(MakeReplyCode(consumed, expected, StackReplyErrorType.Custom));

    public static int MakeReplyCode(bool consumed, bool expected, StackReplyErrorType type) =>
        (consumed
             ? (int)StackReply.ConsumedError
             : (int)StackReply.EmptyError)
      | (expected
             ? (int)StackReplyExpectation.Expected
             : 0)
      | (int)type;

    public static (StackReply Reply, StackReplyErrorType Type, bool Expected) GetReplyCode(int code) =>
        (code switch
         {
             _ when (code & (int)StackReply.ConsumedError) == (int)StackReply.ConsumedError => StackReply.ConsumedError,
             _ when (code & (int)StackReply.EmptyError)    == (int)StackReply.EmptyError    => StackReply.EmptyError,
             _                                                                              => StackReply.OK
         },
         (StackReplyErrorType)(code & (int)StackReplyErrorType.Mask),
         (code                      & (int)StackReplyExpectation.Expected) == (int)StackReplyExpectation.Expected);

    public static bool PopParseError<E, T>(ref Stack stack, out ParseError<E, T> result)
    {
        if (stack.Peek<int>(out var code))
        {
            switch (GetReplyCode(code))
            {
                case (StackReply.OK, _, _):
                    result = null!;
                    return false;

                case (var reply, StackReplyErrorType.Custom, var expected):
                {
                    stack = stack.Pop(); // Pop reply
                    if (stack.Peek<SourcePosRef>(out var pos))
                    {
                        stack = stack.Pop(); // Pop SourcePos
                        if (stack.Peek<E>(out var err))
                        {
                            stack = stack.Pop(); // Pop err
                            result = ParseError.Custom<E, T>(err, reply == StackReply.ConsumedError, expected, pos.UnRef());
                            return true;
                        }
                        else
                        {
                            throw new Exception("Can't read error from stack");
                        }
                    }
                    else
                    {
                        throw new Exception("Can't read SourcePos from stack");
                    }
                }

                case (var reply, StackReplyErrorType.Token, var expected):
                {
                    stack = stack.Pop(); // Pop reply
                    if (stack.Peek<SourcePosRef>(out var pos))
                    {
                        stack = stack.Pop(); // Pop SourcePos
                        if (stack.Peek<T>(out var token))
                        {
                            stack = stack.Pop(); // Pop err
                            result = ParseError.Token<E, T>(token, reply == StackReply.ConsumedError, expected, pos.UnRef());
                            return true;
                        }
                        else
                        {
                            throw new Exception("Can't read error from stack");
                        }
                    }
                    else
                    {
                        throw new Exception("Can't read SourcePos from stack");
                    }
                }

                case (var reply, StackReplyErrorType.Tokens, var expected):
                {
                    stack = stack.Pop(); // Pop reply
                    if (stack.Peek<SourcePosRef>(out var pos))
                    {
                        stack = stack.Pop(); // Pop SourcePos
                        if (stack.Peek<ReadOnlySpan<T>>(out var tokens))
                        {
                            stack = stack.Pop(); // Pop err
                            result = ParseError.Tokens<E, T>([..tokens], reply == StackReply.ConsumedError, expected, pos.UnRef());
                            return true;
                        }
                        else
                        {
                            throw new Exception("Can't read error from stack");
                        }
                    }
                    else
                    {
                        throw new Exception("Can't read SourcePos from stack");
                    }
                }

                case (var reply, StackReplyErrorType.Label, var expected):
                {
                    stack = stack.Pop(); // Pop reply
                    if (stack.Peek<SourcePosRef>(out var pos))
                    {
                        stack = stack.Pop(); // Pop SourcePos
                        if (stack.Peek<ReadOnlySpan<char>>(out var tokens))
                        {
                            stack = stack.Pop(); // Pop err
                            result = ParseError.Label<E, T>(new string(tokens), reply == StackReply.ConsumedError, expected, pos.UnRef());
                            return true;
                        }
                        else
                        {
                            throw new Exception("Can't read error from stack");
                        }
                    }
                    else
                    {
                        throw new Exception("Can't read SourcePos from stack");
                    }
                }

                case (var reply, StackReplyErrorType.EndOfInput, var expected):
                {
                    stack = stack.Pop(); // Pop reply
                    if (stack.Peek<SourcePosRef>(out var pos))
                    {
                        stack = stack.Pop(); // Pop SourcePos
                        result = ParseError.EndOfInput<E, T>(reply == StackReply.ConsumedError, expected, pos.UnRef());
                        return true;
                    }
                    else
                    {
                        throw new Exception("Can't read SourcePos from stack");
                    }
                }
                
                default:
                    result = null!;
                    return false;
            }
        }
        else
        {
            result = null!;
            return false;
        }
    }
}
