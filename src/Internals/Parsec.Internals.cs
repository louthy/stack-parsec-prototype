using System.Numerics;

namespace StackParsecPrototype;

static partial class ParsecInternals<E, T, A>
    where T : IEqualityOperators<T, T, bool>
    where A : allows ref struct
{
    public static ParserResult<E, T, A> Parse(Bytes instructions, Stack constants, int constantOffset, State<T, E> state, Stack stack)
    {
        var taken = ParseUntyped(instructions, constants, constantOffset, ref state, ref stack);

        if(stack.Peek<StackReply>(out var reply))
        {
            switch (reply)
            {
                case StackReply.OK:
                {
                    stack = stack.Pop();
                    if (stack.Peek<A>(out var val))
                    {
                        stack = stack.Pop();
                        return taken == 0
                            ? ParserResult.EmptyOK(val, state)
                            : ParserResult.ConsumedOK(val, state);
                    }
                    else
                    {
                        throw new Exception("Top of stack is not of a value we expected");
                    }
                }
                
                case StackReply.EmptyError:
                case StackReply.ConsumedError:
                    stack = stack.Pop();
                    if (stack.Peek<ParseErrorRef<T, E>>(out var err2))
                    {
                        stack = stack.Pop();
                        return taken == 0
                            ? ParserResult.EmptyErr<E, T, A>(err2.UnRef(), state)
                            : ParserResult.ConsumedErr<E, T, A>(err2.UnRef(), state);
                    }
                    else if (stack.Peek<ParseError<T, E>>(out var err3))
                    {
                        stack = stack.Pop();
                        return taken == 0
                            ? ParserResult.EmptyErr<E, T, A>(err3, state)
                            : ParserResult.ConsumedErr<E, T, A>(err3, state);
                    }
                    else if (stack.Peek<E>(out var err1))
                    {
                        stack = stack.Pop();
                        return taken == 0
                            ? ParserResult.EmptyErr<E, T, A>(ParseError<T, E>.Custom(state.Position.UnRef(), [err1]), state)
                            : ParserResult.ConsumedErr<E, T, A>(ParseError<T, E>.Custom(state.Position.UnRef(), [err1]),
                                state);
                    }
                    else
                    {
                        throw new Exception("Top of stack is not a known error type");
                    }
                    break;
                
                default:
                    throw new Exception("Top of stack is not a known StackReply");
            }
        }
        else
        {
            throw new Exception("Top of stack is not of a StackReply");
        }
    }

    static int ParseUntyped(Bytes instructions, Stack constants, int constantOffset, ref State<T, E> state, ref Stack stack)
    {
        var pc = 0; // program counter
        return ParseUntyped(instructions, constants, constantOffset, ref state, ref stack, ref pc);
    }

    static int ParseUntyped(Bytes instructions, Stack constants, int constantOffset, ref State<T, E> state, ref Stack stack, ref int pc)
    {
        var collectedErrors = new ParseErrorRef<T, E>();
        var taken           = 0;
        while(true)
        {
            // No more instructions?
            if (pc >= instructions.Count)
            {
                stack = stack.Push(StackReply.OK);
                return taken;
            }
            
            // Next instruction
            var instruction = instructions[pc++];
            
            switch ((OpCode)instruction)
            {
                case OpCode.Pure:
                    // Read the pure constant and push it onto the stack.
                    stack = stack.ReadFromAndPush(constants, instructions[pc] + constantOffset)
                                 .Push(StackReply.OK);
                    pc++;
                    break;

                case OpCode.Error:
                    // Read the error constant and push it onto the stack.
                    ProcessError(instructions, constants, constantOffset, state, ref stack, ref pc, taken);
                    break;
                
                case OpCode.Token:
                    ProcessToken(instructions, constants, constantOffset, ref state, ref stack, ref pc, ref taken);
                    break;

                case OpCode.Tokens:
                    ProcessTokens(instructions, constants, constantOffset, ref state, ref stack, ref pc, ref taken);
                    break;
                
                case OpCode.Invoke:
                    ProcessInvoke(instructions, constants, constantOffset, ref stack, ref pc);
                    break;

                case OpCode.InvokeM:
                    ProcessInvokeM(instructions, constants, constantOffset, ref state, ref stack, ref pc, ref taken);
                    break;

                case OpCode.Take1:
                    ProcessTake1(ref state, ref stack, ref taken);
                    break;

                case OpCode.TakeN:
                    ProcessTakeN(instructions, ref state, ref stack, ref pc, ref taken);
                    break;

                case OpCode.TakeWhile1:
                    ProcessTakeWhile1(instructions, constants, constantOffset, ref state, ref stack, ref pc, ref taken);
                    break;

                case OpCode.TakeWhile:
                    ProcessTakeWhile(instructions, constants, constantOffset, ref state, ref stack, ref pc, ref taken);
                    break;

                case OpCode.Satisfy:
                    ProcessSatisfy(instructions, constants, constantOffset, ref state, ref stack, ref pc, ref taken);
                    break;

                case OpCode.OneOf:
                    ProcessOneOf(instructions, constants, constantOffset, ref state, ref stack, ref pc, ref taken);
                    break;
                
                case OpCode.Try:
                    ProcessTry(instructions, constants, constantOffset, ref state, ref stack, ref pc, ref taken);
                    break;
                
                default:
                    throw new NotImplementedException($"OpCode {(OpCode)instruction} not implemented");
            }

            var loop = true;
            while (loop)
            {
                if (stack.Peek<StackReply>(out var reply))
                {
                    switch (reply)
                    {
                        case StackReply.OK:
                            // Remove the OK from the stack, leaving just the success value
                            stack = stack.Pop();

                            // If the top of the stack is a ParserCore, then run it.  This means any of the 
                            // op-code processes can return more code to run.
                            if (stack.Peek<ParsecCore>(out var p))
                            {
                                // We have a parser, so pop it
                                stack = stack.Pop();

                                // Run the parser
                                taken += ParseUntyped(p.Instructions, p.Constants, 0, ref state, ref stack);
                            }
                            else
                            {
                                // No more instructions, or, look ahead, if there's an OR instruction,
                                // then we're done, because we succeeded here.
                                if (pc >= instructions.Count || instructions[pc] == (byte)OpCode.Or)
                                {
                                    stack = stack.Push(StackReply.OK);
                                    return taken;
                                }
                                else
                                {
                                    // If there isn't a ParserCore on the stack, then exit the loop and keep
                                    // running the instructions.
                                    loop = false;
                                }
                            }
                            break;
                        
                        case StackReply.EmptyError:
                            
                            // No more instructions?
                            if (pc >= instructions.Count)
                            {
                                return taken;
                            }
                            
                            // Look ahead, we need an OR instruction to continue
                            if (instructions[pc] == (byte)OpCode.Or)
                            {
                                // Pop the StackReply
                                stack = stack.Pop();
                                
                                // Skip the OR
                                pc++;
                                
                                // Constants offset
                                constantOffset = BitConverter.ToInt32(instructions.Span().Slice(pc, 4));
                                pc += 4;

                                if (stack.Peek<ParseErrorRef<T, E>>(out var err))
                                {
                                    // Collect the error
                                    collectedErrors = collectedErrors.Combine(err);
                                    
                                    // Pop the error off the stack
                                    stack = stack.Pop();
                                    
                                    // Continue parsing
                                    loop = false;
                                    break;
                                }
                                else
                                {
                                    throw new Exception("Or: expected ParseErrorRef on the stack");
                                }
                            }
                            else
                            {
                                // We've got a failure that hasn't been caught by an OR instruction, so we early-out
                                // with the failure value remaining on the stack
                                return taken;
                            }

                        default:
                            // We've got a failure, so we early-out with the failure value remaining on the stack
                            return taken;
                    }
                }
                else
                {
                    throw new Exception("Invoke: expected StackReply");
                }
            }
        }
    }
}