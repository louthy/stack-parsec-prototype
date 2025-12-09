using System.Numerics;
using System.Runtime.CompilerServices;

namespace StackParsecPrototype;

static partial class ParsecInternals<E, T, A>
    where T : IEqualityOperators<T, T, bool>
    where A : allows ref struct
{
    public static B Parse<B>(
        Bytes instructions, 
        Stack constants, 
        int constantOffset, 
        State<E, T> state, 
        Stack stack,
        Func<A, State<E, T>, B> consumedOk,
        Func<A, State<E, T>, B> emptyOk,
        Func<ParseError<E, T>, State<E, T>, B> consumedErr,
        Func<ParseError<E, T>, State<E, T>, B> emptyErr)
    {
        var taken = ParseUntyped(instructions, constants, constantOffset, ref state, ref stack);

        switch (stack.PeekReply())
        {
            case (StackReply.OK, _, _):
            {
                stack = stack.Pop();
                if (stack.Peek<A>(out var val))
                {
                    stack = stack.Pop();
                    return taken == 0
                        ? emptyOk(val, state)
                        : consumedOk(val, state);
                }
                else
                {
                    throw new Exception($"The value at the top of stack is not of the type we expected ({typeof(A).Name})");
                }
            }
            
            default:
                if (ParseErrorStack.PopParseError<E, T>(ref stack, out var err))
                {
                    return err.Consumed
                               ? consumedErr(err, state)
                               : emptyErr(err, state);
                }
                else
                {
                    throw new Exception("The value at the top of stack is a ParseError as expected");
                }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int ParseUntyped(Bytes instructions, Stack constants, int constantOffset, ref State<E, T> state, ref Stack stack)
    {
        var pc = 0; // program counter
        return ParseUntyped(instructions, constants, constantOffset, ref state, ref stack, ref pc);
    }

    static int ParseUntyped(Bytes instructions, Stack constants, int constantOffset, ref State<E, T> state, ref Stack stack, ref int pc)
    {
        var constantOffsetReset = constantOffset;
        var taken               = 0;
        while(true)
        {
            // No more instructions?
            if (pc >= instructions.Count)
            {
                stack = stack.PushOK();
                return taken;
            }
            
            // Next instruction
            var instruction = instructions[pc++];
            
            switch ((OpCode)instruction)
            {
                case OpCode.Pure:
                    // Read the pure constant and push it onto the stack.
                    stack = stack.ReadFromAndPush(constants, instructions.GetConstantId(ref pc, constantOffset))
                                 .PushOK();
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
                switch (stack.PeekReply())
                {
                    case (StackReply.OK, _, _):
                        
                        // We're not midway through an OR instruction, so reset the constant offset
                        constantOffset = constantOffsetReset;
                        
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
                            while (true)
                            {
                                if (pc >= instructions.Count)
                                {
                                    // No more instructions, so return a successful result.
                                    stack = stack.PushOK();
                                    return taken;
                                }

                                // No more instructions, or, look ahead, if there's an OR instruction,
                                // then we're done, because we succeeded here.
                                if (instructions[pc] == (byte)OpCode.Or)
                                {
                                    // Skip the OR instruction
                                    pc++;

                                    if (pc + 4 > instructions.Count)
                                    {
                                        // If we get here, then the instructions are corrupt
                                        throw new Exception("Or: expected constant offset");
                                    }
                                    
                                    // Read the offset 
                                    var offset = BitConverter.ToInt32(instructions.Span().Slice(pc, 4));

                                    // Skip the offset, the constant-offset, and the rhs instructions 
                                    pc += offset;
                                }
                                else
                                {
                                    break;
                                }
                            }

                            // If there isn't a ParserCore on the stack, then exit the loop and keep
                            // running the instructions.
                            loop = false;
                        }
                        break;
                    
                    case (StackReply.EmptyError, var errType, var expected):
                        
                        // No more instructions?
                        if (pc >= instructions.Count)
                        {
                            return taken;
                        }
                        
                        // Look ahead, we need an OR instruction to continue
                        if (instructions[pc] == (byte)OpCode.Or)
                        {
                            // Skip the OR and the int32 offset value (that is only needed if we had succeeded)
                            pc += 5;
                            
                            // Constants offset
                            constantOffset = BitConverter.ToInt16(instructions.Span().Slice(pc, Bytes.ConstantIdSize));
                            pc += Bytes.ConstantIdSize;

                            // Pop the error off the stack
                            if (stack.PopError<E, T>(out var err))
                            {
                                // TODO: This is the original strategy before I serialised the errors to the 
                                //       stack, rather than create one large ParseErrorRef.  
                                //
                                //    *  This is now allocating a new ParseError when really we don't
                                //       need it right now.  
                                //
                                //    *  What we know is that the error is on the stack and we may need
                                //       to collect multiple errors.
                                //
                                //    *  So a future strategy is to leave it on the stack and let multiple errors
                                //       accumulate.  We can then collect them at the end of the process and do
                                //       a single round of allocations at that point.
                                //
                                //    *  We could also have a set of ConsumedOK, ConsumedErr, EmptyOK, EmptyErr 
                                //       delegates like Megaparsec and invoke them directly based om the stack
                                //       values.  That might mean no allocation of ParseError types.
                                
                                // Collect the error
                                //collectedErrors = collectedErrors.Combine(err);
                                
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
        }
    }
}