using System.Runtime.CompilerServices;
using static LanguageExt.Prelude;

namespace LanguageExt.RefParsec;

public static class ErrorStack
{
    extension(ref Stack stack)
    {
        public bool PopError<E, T>(out ParseError<E, T> error)
        {
            var reply = stack.PeekReply();
            switch (reply)
            {
                case StackReply.Error:
                    stack = stack.Pop();
                    if (stack.Peek<SourcePos>(out var pos))
                    {
                        stack = stack.Pop();
                        ErrorItem<T>?      unexpectedTrivialItem = null;
                        List<ErrorItem<T>>? expectedTrivialItems = null;
                        var                fancyItems            = new List<ErrorFancy<E>>();

                        while (PopErrorItem(ref fancyItems, ref expectedTrivialItems, ref unexpectedTrivialItem, ref stack))
                        {
                        }

                        if (fancyItems is not null && fancyItems.Count > 0)
                        {
                            // Ignore trivial errors
                            error = ParseError.Fancy<E, T>(pos, toSet(fancyItems));
                            return true;
                        }

                        if (expectedTrivialItems is not null && expectedTrivialItems.Count > 0)
                        {
                            expectedTrivialItems = expectedTrivialItems.Where(e => e is not ErrorItem<T>.Label { Value: "" }).ToList();
                        }
                        
                        if (unexpectedTrivialItem is null && (expectedTrivialItems is null || expectedTrivialItems.Count == 0))
                        {
                            // No errors found
                            error = null!;
                            return false;
                        }

                        error = ParseError.Trivial<E, T>(
                            pos, 
                            Optional(unexpectedTrivialItem), 
                            expectedTrivialItems is null 
                                ? [] 
                                : toSet(expectedTrivialItems));
                        
                        return true;
                    }
                    else
                    {
                        throw new Exception("Stack corrupted: expected a SourcePos at the top of stack");
                    }

                case StackReply.OK:
                    stack = stack.Pop();
                    error = null!;
                    return false;

                case StackReply.NoReply:
                    stack = stack.Pop();
                    error = null!;
                    return false;

                default:
                    throw new Exception("Stack corrupted: expected a StackReply at the top of stack");
            }
        }
    }

    extension(Stack stack)
    {
        public Stack PopError()
        {
            var reply = stack.PeekReply();
            switch (reply)
            {
                case StackReply.Error:
                    stack = stack.Pop()     // StackReply
                                 .Pop();    // SourcePos
                    while (PopErrorItem(ref stack)) /* loop */ ;
                    return stack;

                case StackReply.OK:
                    return stack;

                case StackReply.NoReply:
                    return stack;

                default:
                    throw new Exception("Stack corrupted: expected a StackReply at the top of stack");
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StackReply PeekReply()
        {
            if (stack.Peek<StackReply>(out var r))
            {
                return r;
            }
            else
            {
                return StackReply.NoReply;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsOK() =>
            stack.PeekReply() is StackReply.OK;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsErr() =>
            stack.PeekReply() is StackReply.Error;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Stack PushOK() =>
            stack.Push(StackReply.OK);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Stack PushErr(SourcePos position) =>
            stack.Push(position)
                 .Push(StackReply.Error);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Stack PushFail(ReadOnlySpan<char> text) =>
            stack.Push(text)
                 .Push(ErrorStackType.Fail)
                 .PushFancy();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Stack PushIndentation(int ordering, int reference, int actual) =>
            stack.Push(ordering)
                 .Push(reference)
                 .Push(actual)
                 .Push(ErrorStackType.Indentation)
                 .PushFancy();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Stack PushCustom<E>(E error) =>
            stack.Push(error)
                 .Push(ErrorStackType.Custom)
                 .PushFancy();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Stack PushToken<T>(T token, bool expected) =>
            stack.Push(token)
                 .Push(ErrorStackType.Token)
                 .PushExpectation(expected);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Stack PushTokens<T>(ReadOnlySpan<T> tokens, bool expected) =>
            stack.Push(tokens)
                 .Push(ErrorStackType.Tokens)
                 .PushExpectation(expected);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Stack PushLabel(ReadOnlySpan<char> label, bool expected) =>
            stack.Push(label)
                 .Push(ErrorStackType.Label)
                 .PushExpectation(expected);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Stack PushHidden() =>
            stack.Push(ErrorStackType.Hidden)
                 .PushExpectation(true);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Stack PushEndOfInput(bool expected) =>
            stack.Push(ErrorStackType.EndOfInput)
                 .PushExpectation(expected);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Stack PushFancy() =>
            stack.Push(ErrorStackType.Fancy);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Stack PushExpectation(bool expected) =>
            stack.Push(expected
                           ? ErrorStackType.Expected
                           : ErrorStackType.Unexpected);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Stack PushTerminator<T>(State<T> state, out SourcePos pos)
        {
            if (stack.Peek<StackReply>(out var t) && t == StackReply.Error)
            {
                // Pop StackReply.Error
                stack = stack.Pop();
                
                if (stack.Peek(out pos))
                {
                    // Pop SourcePos and return the old value
                    return stack.Pop();
                }
                else
                {
                    throw new Exception("Stack corrupted: expected a SourcePosRef at the top of stack");
                }
            }
            else
            {
                pos = state.Position;
                return stack.Push(ErrorStackType.Terminator);
            }
        }
    }
    
    public static bool PopErrorItem<E, T>(
        ref List<ErrorFancy<E>>? fancyItems,
        ref List<ErrorItem<T>>? expectedTrivialItems,
        ref ErrorItem<T>? unexpectedTrivialItem,
        ref Stack stack)
    {
        if (stack.Peek<ErrorStackType>(out var type))
        {
            stack = stack.Pop();
            switch (type)
            {
                case ErrorStackType.Terminator:
                    return false;
                
                case ErrorStackType.Expected or ErrorStackType.Unexpected:
                    return PopTrivialErrorItem(ref expectedTrivialItems, ref unexpectedTrivialItem, ref stack, type);

                case ErrorStackType.Fancy:
                {
                    return PopFancyErrorItem(ref fancyItems, ref stack);
                }
                    
                default:
                    throw new Exception("Stack corrupted: expected a ErrorStackType:byte at the top of stack");
            }
        }
        else
        {
            throw new Exception("Stack corrupted: expected a ErrorStackType:byte at the top of stack");
        }
    }
    
    public static bool PopErrorItem(ref Stack stack)
    {
        if (stack.Peek<ErrorStackType>(out var type))
        {
            stack = stack.Pop();
            switch (type)
            {
                case ErrorStackType.Terminator:
                    return false;
                
                case ErrorStackType.Expected or ErrorStackType.Unexpected:
                    return PopTrivialErrorItem(ref stack);

                case ErrorStackType.Fancy:
                    return PopFancyErrorItem(ref stack);
                    
                default:
                    throw new Exception("Stack corrupted: expected a ErrorStackType:byte at the top of stack");
            }
        }
        else
        {
            throw new Exception("Stack corrupted: expected a ErrorStackType:byte at the top of stack");
        }
    }

    static bool PopFancyErrorItem<E>(ref List<ErrorFancy<E>>? fancyItems, ref Stack stack)
    {
        if (stack.Peek<ErrorStackType>(out var subtype))
        {
            stack = stack.Pop();
            switch (subtype)
            {
                case ErrorStackType.Fail:
                    if (stack.Peek<string>(out var txt))
                    {
                        stack = stack.Pop();
                        fancyItems ??= [];
                        fancyItems.Add(ErrorFancy.Fail<E>(txt));
                        return true;
                    }
                    else
                    {
                        throw new Exception("Stack corrupted: expected a string at the top of stack");
                    }

                case ErrorStackType.Indentation:
                    if (stack.Peek<int>(out var actual))
                    {
                        stack = stack.Pop();
                        if (stack.Peek<int>(out var reference))
                        {
                            stack = stack.Pop();

                            if (stack.Peek<int>(out var ordering))
                            {
                                stack = stack.Pop();
                                fancyItems ??= [];
                                fancyItems.Add(ErrorFancy.Indentation<E>(ordering, reference, actual));
                                return true;
                            }
                            else
                            {
                                throw new Exception("Stack corrupted: expected an int at the top of stack");
                            }
                        }
                        else
                        {
                            throw new Exception("Stack corrupted: expected an int at the top of stack");
                        }
                    }
                    else
                    {
                        throw new Exception("Stack corrupted: expected an int at the top of stack");
                    }
                            
                case ErrorStackType.Custom:
                    if (stack.Peek<E>(out var err))
                    {
                        stack = stack.Pop();
                        fancyItems ??= [];
                        fancyItems.Add(ErrorFancy.Custom(err));
                        return true;
                    }
                    else
                    {
                        throw new Exception($"Stack corrupted: expected a {typeof(E).Name} at the top of stack");
                    }
                            
                default:
                    throw new Exception($"Stack corrupted: unexpected ErrorStackType:byte {subtype}");
            }
        }
        else
        {
            throw new Exception("Stack corrupted: expected a byte at the top of stack");
        }
    }

    static bool PopFancyErrorItem(ref Stack stack)
    {
        if (stack.Peek<ErrorStackType>(out var subtype))
        {
            stack = stack.Pop();
            switch (subtype)
            {
                case ErrorStackType.Custom:
                case ErrorStackType.Fail:
                    stack = stack.Pop();
                    return true;

                case ErrorStackType.Indentation:
                    stack = stack.Pop().Pop().Pop();
                    return true;
                            
                default:
                    throw new Exception($"Stack corrupted: unexpected ErrorStackType:byte {subtype}");
            }
        }
        else
        {
            throw new Exception("Stack corrupted: expected a byte at the top of stack");
        }
    }

    static bool PopTrivialErrorItem<T>(
        ref List<ErrorItem<T>>? expectedTrivialItems, 
        ref ErrorItem<T>? unexpectedTrivialItem,
        ref Stack stack, 
        ErrorStackType type)
    {
        if (stack.Peek<ErrorStackType>(out var subtype))
        {
            stack = stack.Pop();
            switch (subtype)
            {
                case ErrorStackType.Hidden:
                {
                    expectedTrivialItems ??= [];
                    expectedTrivialItems.Clear();
                    expectedTrivialItems.Add(ErrorItem<T>.Label.Hidden);
                    return true;
                }
                
                // If we already have a label or an end-of-input, then we can ignore 
                case ErrorStackType.Token or ErrorStackType.Tokens 
                    when type == ErrorStackType.Expected && 
                         expectedTrivialItems is not null && 
                         expectedTrivialItems.Exists(e => e is ErrorItem<T>.Label or ErrorItem<T>.EndfOfInput):
                {
                    stack = stack.Pop();
                    return true;
                }
                
                case ErrorStackType.Token:
                {
                    if (stack.Peek<T>(out var t))
                    {
                        stack = stack.Pop();
                        var item = ErrorItem.Token(t);
                        if (type == ErrorStackType.Expected)
                        {
                            expectedTrivialItems ??= [];
                            expectedTrivialItems.Add(item);
                        }
                        else if(unexpectedTrivialItem is null)
                        {
                            unexpectedTrivialItem = item;
                        }
                        else
                        {
                            unexpectedTrivialItem = item > unexpectedTrivialItem
                                                        ? item
                                                        : unexpectedTrivialItem;
                        }
                        return true;
                    }
                    else
                    {
                        throw new Exception($"Stack corrupted: expected a token-type {typeof(T).Name} at the top of stack");
                    }
                }

                case ErrorStackType.Tokens:
                {
                    if (stack.Peek<ReadOnlySpan<T>>(out var ts))
                    {
                        stack = stack.Pop();
                        var item = ErrorItem.Tokens(ts);
                        if (type == ErrorStackType.Expected)
                        {
                            expectedTrivialItems ??= [];
                            expectedTrivialItems.Add(item);
                        }
                        else if (unexpectedTrivialItem is null)
                        {
                            unexpectedTrivialItem = item;
                        }
                        else
                        {
                            unexpectedTrivialItem = item > unexpectedTrivialItem
                                                        ? item
                                                        : unexpectedTrivialItem;
                        }
                        return true;
                    }
                    else
                    {
                        throw new Exception(
                            $"Stack corrupted: expected a token-type {typeof(ReadOnlySpan<T>).Name} at the top of stack");
                    }
                }

                case ErrorStackType.Label:
                {
                    if (stack.Peek<ReadOnlySpan<char>>(out var l))
                    {
                        stack = stack.Pop();
                        var item = ErrorItem.Label<T>(new string(l));
                        if (type == ErrorStackType.Expected)
                        {
                            expectedTrivialItems ??= [];
                            expectedTrivialItems.Add(item);
                        }
                        else if (unexpectedTrivialItem is null)
                        {
                            unexpectedTrivialItem = item;
                        }
                        else
                        {
                            unexpectedTrivialItem = item > unexpectedTrivialItem
                                                        ? item
                                                        : unexpectedTrivialItem;
                        }

                        return true;
                    }
                    else
                    {
                        throw new Exception(
                            $"Stack corrupted: expected a token-type {typeof(ReadOnlySpan<T>).Name} at the top of stack");
                    }
                }

                case ErrorStackType.EndOfInput:
                {
                    var item = ErrorItem.EndOfInput<T>();
                    if (type == ErrorStackType.Expected)
                    {
                        expectedTrivialItems ??= [];
                        expectedTrivialItems.Add(item);
                    }
                    else if (unexpectedTrivialItem is null)
                    {
                        unexpectedTrivialItem = item;
                    }
                    else
                    {
                        unexpectedTrivialItem = item > unexpectedTrivialItem
                                                    ? item
                                                    : unexpectedTrivialItem;
                    }

                    return true;
                }
                
                default:
                    throw new Exception("Stack corrupted: expected a ErrorStackType:byte at the top of stack [2]");
            }
        }
        else
        {
            throw new Exception("Stack corrupted: expected a ErrorStackType:byte at the top of stack [1]");
        }
    }

    static bool PopTrivialErrorItem(ref Stack stack)
    {
        if (stack.Peek<ErrorStackType>(out var subtype))
        {
            stack = stack.Pop();
            switch (subtype)
            {
                case ErrorStackType.Token:
                case ErrorStackType.Tokens:
                case ErrorStackType.Label:
                {
                    stack = stack.Pop();
                    return true;
                }

                case ErrorStackType.Hidden:
                case ErrorStackType.EndOfInput:
                {
                    return true;
                }
                
                default:
                    throw new Exception("Stack corrupted: expected a ErrorStackType:byte at the top of stack [2]");
            }
        }
        else
        {
            throw new Exception("Stack corrupted: expected a ErrorStackType:byte at the top of stack [1]");
        }
    }
}