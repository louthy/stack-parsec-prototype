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
        State<T> state, 
        Stack stack,
        Func<A, State<T>, B> consumedOk,
        Func<A, State<T>, B> emptyOk,
        Func<ParseError<E, T>, State<T>, B> consumedErr,
        Func<ParseError<E, T>, State<T>, B> emptyErr)
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
    static void ParseUntyped(Bytes instructions, Stack constants, int constantOffset, ref State<T> state, ref Stack stack)
    {
        var pc = 0; // program counter
        ParseUntyped(instructions, constants, constantOffset, ref state, ref stack, ref pc);
    }

    static void ParseUntyped(Bytes instructions, Stack constants, int constantOffset, ref State<T> state, ref Stack stack, ref int pc)
    {
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
                
                case OpCode.Return:
                    if (!stack.IsErr())
                    {
                        // Push 'OK' if we're not failing
                        stack = stack.PushOK();
                    }
                    return;
                
                case OpCode.Or:
                    //  1: OR
                    //  4: lhs instructions count (in bytes)
                    //  4: rhs instructions count (in bytes)
                    //  2: offset to second constants set
                    //  n: lhs instructions
                    //  1: RETURN
                    //  n: rhs instructions

                    var span    = instructions.Span();
                    var lhsSize = BitConverter.ToInt32(span.Slice(pc, 4));
                    var rhsSize = BitConverter.ToInt32(span.Slice(pc + 4, 4));
                    var lhs     = instructions.Slice(pc + 10, lhsSize);
                    var so      = state.Position.Offset;
                    
                    ParseUntyped(lhs, constants, constantOffset, ref state, ref stack);
                    if (stack.IsOK())
                    {
                        pc = pc + 10 + lhsSize + rhsSize;
                    }
                    else if(state.Position.Offset > so)
                    {
                        // We've consumed, which makes an error fatal, so early-out
                        return;
                    }
                    else
                    {
                        // We have an empty-error, so we can try the right-hand side
                        var rhs      = instructions.Slice(pc + 10 + lhsSize, rhsSize);
                        var constOff = BitConverter.ToUInt16(span.Slice(pc + 8, 2));
                        ParseUntyped(rhs, constants, constantOffset + constOff, ref state, ref stack);
                        if (stack.IsOK())
                        {
                            pc = pc + 10 + lhsSize + rhsSize;
                        }
                    }
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
                            // If there isn't a ParserCore on the stack, then exit the loop and keep
                            // running the instructions.
                            loop = false;
                        }
                        break;
                    
                    case StackReply.Error:
                        // We've got a failure that hasn't been caught by a choice-operator, so we early-out
                        // with the failure value remaining on the stack
                        return;

                    default:
                        // We've got a failure, so we early-out with the failure value remaining on the stack
                        return;
                }
            }
        }
    }
}