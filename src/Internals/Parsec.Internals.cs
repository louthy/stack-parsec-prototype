using System.Numerics;
using System.Runtime.CompilerServices;

namespace LanguageExt.RefParsec;

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
        var initial = state.Position.Offset;
        ParseUntyped(instructions, constants, constantOffset, ref state, ref stack);
        var taken = state.Position.Offset - initial;

        switch (stack.PeekReply())
        {
            case StackReply.OK:
            {
                stack = stack.Pop();
                if (stack.Peek<A>(out var val))
                {
                    stack = stack.Pop();

                    #if DEBUG
                    if(stack.Top > 0) throw new Exception("Stack hasn't been properly unwound.  Suggests a bug somewhere.");
                    #endif
                    
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
                if (stack.PopError<E, T>(out var err))
                {
                    #if DEBUG
                    if(stack.Top > 0) throw new Exception("Stack hasn't been properly unwound.  Suggests a bug somewhere.");
                    #endif
    
                    return taken == 0
                               ? emptyErr(err, state)
                               : consumedErr(err, state);
                }
                else
                {
                    throw new Exception("The value at the top of stack is a ParseError as expected");
                }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void ParseUntyped(Bytes instructions, Stack constants, int constantOffset, ref State<E, T> state, ref Stack stack)
    {
        var pc = 0; // program counter
        ParseUntyped(instructions, constants, constantOffset, ref state, ref stack, ref pc);
    }

    static void ParseUntyped(Bytes instructions, Stack constants, int constantOffset, ref State<E, T> state, ref Stack stack, ref int pc)
    {
        var constantOffsetReset = constantOffset;
        var initialOffset       = state.Position.Offset;
        
        while(true)
        {
            // No more instructions?
            if (pc >= instructions.Count)
            {
                stack = stack.PushOK();
                return;
            }
            
            // Next instruction
            var instruction = instructions[pc++];
            
            switch ((OpCode)instruction)
            {
                case OpCode.Pure:
                    // Read the pure constant and push it onto the stack.
                    stack = stack.PopError()
                                 .ReadFromAndPush(constants, instructions.GetConstantId(ref pc, constantOffset))
                                 .PushOK();
                    break;

                case OpCode.Error:
                    // Read the error constant and push it onto the stack.
                    ProcessError(instructions, constants, constantOffset, state, ref stack, ref pc);
                    break;
                
                case OpCode.OrLeft:
                    // Find the size of the lhs
                    var lhsSize = BitConverter.ToInt32(instructions.Span().Slice(pc, 4));
                    
                    // Get just the lhs instructions
                    var lhs     = instructions.Slice(pc + 4, lhsSize);
                    
                    // Run the lhs
                    ParseUntyped(lhs, constants, constantOffset, ref state, ref stack);
                    
                    // Skip to the end of the lhs
                    pc += 4 + lhsSize;
                    break;
                
                case OpCode.Token:
                    ProcessToken(instructions, constants, constantOffset, ref state, ref stack, ref pc);
                    break;

                case OpCode.Tokens:
                    ProcessTokens(instructions, constants, constantOffset, ref state, ref stack, ref pc);
                    break;
                
                case OpCode.Invoke:
                    ProcessInvoke(instructions, constants, constantOffset, ref stack, ref pc);
                    break;

                case OpCode.InvokeM:
                    ProcessInvokeM(instructions, constants, constantOffset, ref state, ref stack, ref pc);
                    break;

                case OpCode.Take1:
                    ProcessTake1(ref state, ref stack);
                    break;

                case OpCode.TakeN:
                    ProcessTakeN(instructions, ref state, ref stack, ref pc);
                    break;

                case OpCode.TakeWhile1:
                    ProcessTakeWhile1(instructions, constants, constantOffset, ref state, ref stack, ref pc);
                    break;

                case OpCode.TakeWhile:
                    ProcessTakeWhile(instructions, constants, constantOffset, ref state, ref stack, ref pc);
                    break;

                case OpCode.Satisfy:
                    ProcessSatisfy(instructions, constants, constantOffset, ref state, ref stack, ref pc);
                    break;

                case OpCode.OneOf:
                    ProcessOneOf(instructions, constants, constantOffset, ref state, ref stack, ref pc);
                    break;
                
                case OpCode.Try:
                    ProcessTry(instructions, constants, constantOffset, ref state, ref stack, ref pc);
                    break;
                
                default:
                    throw new NotImplementedException($"OpCode {(OpCode)instruction} not implemented");
            }

            var loop = true;
            while (loop)
            {
                switch (stack.PeekReply())
                {
                    case StackReply.OK:
                        
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
                            ParseUntyped(p.Instructions, p.Constants, 0, ref state, ref stack);
                        }
                        else
                        {
                            while (true)
                            {
                                if (pc >= instructions.Count)
                                {
                                    // No more instructions, so skip to the end of the section
                                    stack = stack.PushOK();
                                    return;
                                }

                                // No more instructions, or, look ahead, if there's an OR instruction,
                                // then we're done, because we succeeded here.
                                if (instructions[pc] == (byte)OpCode.OrRight)
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
                    
                    case StackReply.Error:
                        var taken = state.Position.Offset - initialOffset;
                        if (taken > 0)
                        {
                            // We've consumed, which makes an error fatal, so early-out
                            return;
                        }
                        
                        // No more instructions?
                        if (pc >= instructions.Count)
                        {
                            return;
                        }
                        
                        // Look ahead, we need an OR instruction to continue
                        if (instructions[pc] == (byte)OpCode.OrRight)
                        {
                            // Skip the OR and the int32 offset value (that is only needed if we had succeeded)
                            pc += 5;
                            
                            // Constants offset
                            constantOffset = BitConverter.ToInt16(instructions.Span().Slice(pc, Bytes.ConstantIdSize));
                            pc += Bytes.ConstantIdSize;

                            loop = false;
                            break;
                        }
                        else
                        {
                            // We've got a failure that hasn't been caught by an OR instruction, so we early-out
                            // with the failure value remaining on the stack
                            return;
                        }

                    default:
                        // We've got a failure, so we early-out with the failure value remaining on the stack
                        return;
                }
            }
        }
    }
}