using System.Numerics;
using LanguageExt;

namespace StackParsecPrototype;

public readonly ref struct Parsec<E, T, A>
    where T : IEqualityOperators<T, T, bool>
    where A : allows ref struct
{
    internal readonly ParsecCore Core; 
    
    internal Bytes Instructions => 
        Core.Instructions;
    
    internal Stack Constants => 
        Core.Constants;
 
    internal Parsec(ParsecCore core) =>
        Core = core;
    
    internal Parsec(Bytes instructions, Stack constants) : 
        this(new ParsecCore(instructions, constants)) { }

    public ParserResult<E, T, A> Parse(ReadOnlySpan<T> stream, Span<byte> stackMem, string sourceName = "<unknown source>")
    {
        var stack        = new Stack(stackMem);
        var instructions = Instructions;
        var constants    = Constants;
        var errors       = RefSeq<ParseError<T, E>>.Empty;
        var state        = new State<T, E>(stream, SourcePosRef.FromName(sourceName), errors);
        return Parse(instructions, constants, state, stack);
    }

    static ParserResult<E, T, A> Parse(Bytes instructions, Stack constants, State<T, E> state, Stack stack)
    {
        var taken = ParseUntyped(instructions, constants, ref state, ref stack);

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
                
                case StackReply.ParseError:
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

    static int ParseUntyped(Bytes instructions, Stack constants, ref State<T, E> state, ref Stack stack)
    {
        var pc     = 0;  // program counter
        var taken  = 0;
        
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
                    stack = stack.ReadFromAndPush(constants, instructions[pc++])
                                 .Push(StackReply.OK);
                    break;

                case OpCode.Token:
                    //if (ProcessTaken(ref state, ref stack, ref taken)) return taken;
                    throw new NotImplementedException();
                    break;
                
                case OpCode.Invoke:
                    ProcessInvoke(instructions, constants, ref stack, ref pc);
                    break;

                case OpCode.InvokeM:
                    ProcessInvokeM(instructions, constants, ref state, ref stack, ref pc, ref taken);
                    break;

                case OpCode.Take1:
                    ProcessTake1(ref state, ref stack, ref taken);
                    break;

                case OpCode.TakeN:
                    ProcessTakeN(instructions, ref state, ref stack, ref pc, ref taken);
                    break;
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
                                taken += ParseUntyped(p.Instructions, p.Constants, ref state, ref stack);
                            }
                            else
                            {
                                // If there isn't a ParserCore on the stack, then exit the loop and keep
                                // running the instructions.
                                loop = false;
                            }
                            break;

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

    static void ProcessTakeN(Bytes instructions, ref State<T, E> state, ref Stack stack, ref int pc, ref int taken)
    {
        var offset = state.Position.Offset;
        var n      = BitConverter.ToInt32(instructions.Slice(pc, 4).Span());
        pc += 4;
        if (offset + n > state.Input.Length)
        {
            stack = stack.Push(ParseErrorRef<T, E>.UnexpectedEndOfInput(state.Position))
                         .Push(StackReply.ParseError);
        }
        else
        {
            var ts = state.Input.Slice(offset, n);
            stack = stack.Push(ts)
                         .Push(StackReply.OK);
            state = state.Next(n);
            taken += n;
        }
    }

    static void ProcessTake1(ref State<T, E> state, ref Stack stack, ref int taken)
    {
        var offset = state.Position.Offset;
        if (offset + 1 > state.Input.Length)
        {
            stack = stack.Push(ParseErrorRef<T, E>.UnexpectedEndOfInput(state.Position))
                         .Push(StackReply.ParseError);
        }
        else
        {
            stack = stack.Push(state.Input[state.Position.Offset])
                         .Push(StackReply.OK);
            state = state.NextToken;
            taken++;
        }
    }

    static void ProcessInvoke(Bytes instructions, Stack constants, ref Stack stack, ref int pc)
    {
        // Get the wrapper function
        if (constants.At<Func<Stack, Stack, Stack>>(instructions[pc++], out var go))
        {
            // Read the function to invoke
            stack = stack.ReadFromAndPush(constants, instructions[pc++]);
            
            // Invoke the wrapper function that calls the real function
            stack = go(stack, constants);
        }
        else
        {
            throw new Exception("Invoke: delegate not found");
        }
    }

    static void ProcessInvokeM(
        Bytes instructions, 
        Stack constants, 
        ref State<T, E> state, 
        ref Stack stack, 
        ref int pc,
        ref int taken)
    {
        // Get the delegate to invoke from the constants
        if (constants.At<Func<Stack, Stack, Stack>>(instructions[pc++], out var go))
        {
            // Read the function to invoke
            stack = stack.ReadFromAndPush(constants, instructions[pc++]);
            
            // Invoke the delegate
            stack = go(stack, constants);
        }
        else
        {
            // If there isn't a Func in the constants, then we've got a bug
            throw new Exception("InvokeM: delegate not found");
        }
    }

    public Parsec<E, T, B> Select<B>(Func<A, B> f) =>
        Map(f);
    
    public Parsec<E, T, B> Map<B>(Func<A, B> f)
        where B : allows ref struct 
    {
        // We have a byte-code, so we can't have more than 255 objects in our parser.
        var constIx = Constants.Count;
        if(constIx >= byte.MaxValue) throw new ArgumentException("Too many objects");

        var instrs = Instructions.Add((byte)OpCode.Invoke)
                                 .Add((byte)constIx)
                                 .Add((byte)(constIx + 1));

        var constants = Constants.PushStackOp(go)
                                 .Push(f);

        return new Parsec<E, T, B>(instrs, constants);

        static Stack go(Stack stack, Stack constants)
        {
            if (stack.Peek<Func<A, B>>(out var f))
            {
                stack = stack.Pop();
                if (stack.Peek<A>(out var x))
                {
                    stack = stack.Pop();
                    var v = f(x);
                    return stack.Push(v)
                                .Push(StackReply.OK);
                }
                else
                {
                    throw new Exception("Stack underflow");
                }
            }
            else
            {
                throw new Exception("Expected Func<A, B> on stack");
            }
        }
    }
    
    public Parsec<E, T, B> Bind<B>(Func<A, Parsec<E, T, B>> bind)
        where B : allows ref struct 
    {
        // We have a byte-code, so we can't have more than 255 objects in our parser.
        var constIx = Constants.Count;
        if(constIx >= byte.MaxValue) throw new ArgumentException("Too many objects");
        
        var instrs = Instructions.Add((byte)OpCode.Invoke)
                                 .Add((byte)constIx)
                                 .Add((byte)(constIx + 1));

        var constants = Constants.PushStackOp(go)
                                 .Push(bind);

        return new Parsec<E, T, B>(instrs, constants);
        
        static Stack go(Stack stack, Stack constants)
        {
            if (stack.Peek<Func<A, Parsec<E, T, B>>>(out var f))
            {
                stack = stack.Pop();
                if (stack.Peek<A>(out var x))
                {
                    // We don't pop the top stack value here as we need it for the project function
                    var mb = f(x);
                    return stack.Push(mb)
                                .Push(StackReply.OK);
                }
                else
                {
                    throw new Exception("Stack underflow");
                }
            }
            else
            {
                throw new Exception("Expected Func<A, Parsec<E, T, B>> on stack");
            }
        }
    }    

    public Parsec<E, T, C> SelectMany<B, C>(Func<A, Parsec<E, T, B>> bind, Func<A, B, C> project)
        where B : allows ref struct 
        where C : allows ref struct 
    {
        // We have a byte-code, so we can't have more than 255 objects in our parser.
        var constIx = Constants.Count;
        if(constIx >= byte.MaxValue) throw new ArgumentException("Too many objects");
        
        var instrs = Instructions.Add((byte)OpCode.InvokeM)
                                 .Add((byte)constIx)
                                 .Add((byte)(constIx + 1))
                                 .Add((byte)OpCode.Invoke)
                                 .Add((byte)(constIx + 2))
                                 .Add((byte)(constIx + 3));

        var constants = Constants.PushStackOp(go1)
                                 .Push(bind) 
                                 .PushStackOp(go2)
                                 .Push(project);

        return new Parsec<E, T, C>(instrs, constants);
        
        static Stack go1(Stack stack, Stack constants)
        {
            if (stack.Peek<Func<A, Parsec<E, T, B>>>(out var f))
            {
                stack = stack.Pop();
                if (stack.Peek<A>(out var x))
                {
                    // We don't pop the top stack value here as we need it for the project function
                    var mb = f(x).Core;
                    return stack.Push(mb)
                                .Push(StackReply.OK);

                }
                else
                {
                    throw new Exception("Stack underflow");
                }
            }
            else
            {
                throw new Exception("Expected Func<A, Parsec<E, T, B>> on stack");
            }
        }
        
        static Stack go2(Stack stack, Stack constants)
        {
            if (stack.Peek<Func<A, B, C>>(out var f))
            {
                stack = stack.Pop();
                if (stack.Peek<B>(out var y))
                {
                    stack = stack.Pop();
                    if (stack.Peek<A>(out var x))
                    {
                        stack = stack.Pop();
                        var z = f(x, y);
                        return stack.Push(z)
                                    .Push(StackReply.OK);
                    }
                }

                throw new Exception("Stack underflow");
            }
            else
            {
                throw new Exception("Expected Func<A, B, C> on stack");
            }
        }
    }
}
