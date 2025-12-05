using System.Numerics;
using LanguageExt;

namespace StackParsecPrototype;

public readonly ref struct ParsecCore(ByteSeq instructions, Stack constants)
{
    public readonly ByteSeq Instructions = instructions;
    public readonly Stack Constants = constants;
}

/*
public readonly ref struct ParsecReply(ByteSeq instructions, ObjSeq constants, Stack stack)
{
    public readonly ByteSeq Instructions = instructions;
    public readonly ObjSeq Constants = constants;
    public readonly Stack Stack = stack;
}
*/

public readonly ref struct Parsec<E, T, A>
    where T : IEqualityOperators<T, T, bool>
    where A : allows ref struct
{
    internal readonly ParsecCore Core; 
    
    internal ByteSeq Instructions => 
        Core.Instructions;
    
    internal Stack Constants => 
        Core.Constants;
 
    internal Parsec(ParsecCore core) =>
        Core = core;
    
    internal Parsec(ByteSeq instructions, Stack constants) : 
        this(new ParsecCore(instructions, constants)) { }

    /*public ParserResult<E, T, A> Parse(in S stream, string sourceName, ref A parsedValue)
    {
        Span<byte> stackMem = stackalloc byte[1024];
        var result = Parse(stream, sourceName, stackMem);
        if (result.Ok)
        {
            var val = result.Value;
            parsedValue = val;
        }
    }*/

    public ParserResult<E, T, A> Parse(ReadOnlySpan<T> stream, Span<byte> stackMem, string sourceName = "<unknown source>")
    {
        var stack        = new Stack(stackMem);
        var instructions = Instructions;
        var constants    = Constants;
        var errors       = RefSeq<ParseError<T, E>>.Empty;
        var state        = new State<T, E>(stream, SourcePosRef.FromName(sourceName), errors);
        return Parse(instructions, constants, state, stack);
    }

    static ParserResult<E, T, A> Parse(ByteSeq instructions, Stack constants, State<T, E> state, Stack stack)
    {
        var taken = ParseUntyped(instructions, constants, ref state, ref stack);

        // Return the top of the stack as the result.
        if (stack.Peek<A>(out var val))
        {
            return taken == 0
                       ? ParserResult.EmptyOK(val, state)
                       : ParserResult.ConsumedOK(val, state);
        }
        else if (stack.Peek<ParseErrorRef<T, E>>(out var err2))
        {
            return taken == 0
                       ? ParserResult.EmptyErr<E, T, A>(err2.UnRef(), state)
                       : ParserResult.ConsumedErr<E, T, A>(err2.UnRef(), state);
        }
        else if (stack.Peek<ParseError<T, E>>(out var err3))
        {
            return taken == 0
                       ? ParserResult.EmptyErr<E, T, A>(err3, state)
                       : ParserResult.ConsumedErr<E, T, A>(err3, state);
        }
        else if (stack.Peek<E>(out var err1))
        {
            return taken == 0
                       ? ParserResult.EmptyErr<E, T, A>(ParseError<T, E>.Custom(state.Position.UnRef(), [err1]), state)
                       : ParserResult.ConsumedErr<E, T, A>(ParseError<T, E>.Custom(state.Position.UnRef(), [err1]),
                                                           state);
        }
        else
        {
            throw new Exception("Top of stack is not of a value we expected");
        }
    }

    static int ParseUntyped(ByteSeq instructions, Stack constants, ref State<T, E> state, ref Stack stack)
    {
        var pc     = 0;  // program counter
        var taken  = 0;
        
        while(true)
        {
            // No more instructions?
            if (pc >= instructions.Count)
            {
                return taken;
            }
            
            // Next instruction
            var instruction = instructions[pc++];
            
            switch ((OpCode)instruction)
            {
                case OpCode.Pure:
                    // Read the pure constant and push it onto the stack.
                    stack = stack.ReadFromAndPush(constants, instructions[pc++]);
                    break;

                case OpCode.Map:
                {
                    if (constants.At<Func<Stack, Stack>>(instructions[pc++], out var f))
                    {
                        stack = f(stack);
                    }
                    else
                    {
                        throw new Exception("Map: null delegate");
                    }
                    break; 
                }

                case OpCode.Take1:
                {
                    var offset = state.Position.Offset;
                    if (offset + 1 > state.Input.Length)
                    {
                        state = state.NextToken;
                        stack = stack.Push(ParseErrorRef<T, E>.UnexpectedEndOfInput(state.Position));
                    }
                    else
                    {
                        stack = stack.Push(state.Input[state.Position.Offset]);
                        state = state.NextToken;
                        taken++;
                    }

                    break;
                }

                case OpCode.TakeN:
                {
                    var offset = state.Position.Offset;
                    var n      = BitConverter.ToInt32(instructions.Slice(pc, 4).Span());
                    pc += 4;
                    if (offset + n > state.Input.Length)
                    {
                        state = state.Next(state.Input.Length - offset);
                        stack = stack.Push(ParseErrorRef<T, E>.UnexpectedEndOfInput(state.Position));
                    }
                    else
                    {
                        var ts = state.Input.Slice(offset, n);
                        stack = stack.Push(ts);
                        state = state.Next(n);
                        taken += n;
                    }

                    break;
                }

                case OpCode.Flatten:
                {
                    if (stack.Peek<ParsecCore>(out var p))
                    {
                        stack = stack.Pop();
                        taken += ParseUntyped(p.Instructions, p.Constants, ref state, ref stack);
                    }
                    else
                    {
                        throw new Exception("Flatten: invalid stack state");
                    }
                    break;
                }

                /*case OpCode.Bind:
                {
                    var f = (Func<ObjSeq, ObjSeq>?)constants[instructions[pc++]] ?? throw new Exception("Bind: null delegate");
                    var a = stack.Peek();
                    stack = stack.Pop();
                    var p = f(a);
                    
                    break;
                }*/
            }
        }
    }    

    public Parsec<E, T, B> Map<B>(Func<A, B> f)
        where B : allows ref struct 
    {
        // We have a byte-code, so we can't have more than 255 objects in our parser.
        var constIx = Constants.Count;
        if(constIx >= byte.MaxValue) throw new ArgumentException("Too many objects");

        var instrs = Instructions.Add((byte)OpCode.Map)
                                 .Add((byte)constIx);

        var constants = Constants.Push((Func<Stack, Stack>)map);
//////
        return new Parsec<E, T, B>(instrs, constants);

        Stack map(Stack stack)
        {
            if (stack.Peek<A>(out var x))
            {
                stack = stack.Pop();
                var v = f(x);
                return stack.Push(v);
            }
            else
            {
                throw new Exception("Stack underflow in map");
            }
        }
    }

    /*public Parsec<E, T, B> Bind<B>(Func<A, Parsec<E, T, B>> f) 
        where B : allows ref struct =>
        this.Map(f).Flatten();*/

    /*
    public Parsec<E, T, B> Bind<B>(Func<A, Parsec<E, T, B>> f)
    {
        // We have a byte-code, so we can't have more than 255 objects in our parser.
        var constIx = Constants.Count;
        if(constIx >= byte.MaxValue) throw new ArgumentException("Too many objects");

        var instrs = Instructions.Add((byte)OpCode.Bind)
                                 .Add((byte)constIx);

        var of = (object? x) => x is A a
                                    ? f(a) // TODO --- THIS RETURNS A Parsec, which needs expanding and running too
                                    : throw new Exception("Bind: expected `A`");

        var constants = Constants.Add(of);

        return new Parsec<E, T, B>(instrs, constants);
    }
    */

}
