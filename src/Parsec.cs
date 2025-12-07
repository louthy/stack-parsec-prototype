using System.Numerics;

namespace StackParsecPrototype;

/// <summary>
/// Parser combinator ref-struct
/// </summary>
/// <remarks>
/// This runs an internal byte-code to process the parsing steps against a `ReadOnlySpan` of tokens.
/// </remarks>
/// <typeparam name="E">Error type</typeparam>
/// <typeparam name="T">Token type</typeparam>
/// <typeparam name="A">Parsed value type</typeparam>
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

    /// <summary>
    /// Parse a stream of tokens
    /// </summary>
    /// <param name="stream">Stream of tokens</param>
    /// <param name="stackMem">Memory to use for the stack</param>
    /// <param name="sourceName">Name of the source, usually a source-file name</param>
    /// <returns>Result of the parsing operation</returns>
    public ParserResult<E, T, A> Parse(ReadOnlySpan<T> stream, Span<byte> stackMem, string sourceName = "")
    {
        var stack        = new Stack(stackMem);
        var instructions = Instructions;
        var constants    = Constants;
        var errors       = RefSeq<ParseError<T, E>>.Empty;
        var state        = new State<T, E>(stream, SourcePosRef.FromName(sourceName), errors);
        return ParsecInternals<E, T, A>.Parse(instructions, constants, 0, state, stack);
    }

    /// <summary>
    /// Turns a parser that fails on partial consumption into one that backtracks and raises an
    /// 'empty error' that can be caught by the `|` operator. 
    /// </summary>
    /// <returns>Parser</returns>
    public Parsec<E, T, A> Try() =>
        new (Instructions.Cons(OpCode.Try), Constants);    

    /// <summary>
    /// Functor map operation
    /// </summary>
    /// <param name="f">Mapping function</param>
    /// <typeparam name="B">Resulting value type</typeparam>
    /// <returns>Parser</returns>
    public Parsec<E, T, B> Select<B>(Func<A, B> f) 
        where B : allows ref struct =>
        Map(f);
    
    /// <summary>
    /// Functor map operation
    /// </summary>
    /// <param name="f">Mapping function</param>
    /// <typeparam name="B">Resulting value type</typeparam>
    /// <returns>Parser</returns>
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
    
    /// <summary>
    /// Monad bind operation
    /// </summary>
    /// <param name="bind">Binding function</param>
    /// <typeparam name="B">Resulting value type</typeparam>
    /// <returns>Parser</returns>
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
    }    

    /// <summary>
    /// Monad bind operation followed by a projection 
    /// </summary>
    /// <param name="bind">Binding function</param>
    /// <param name="project">Projection function</param>
    /// <typeparam name="B">Bind function value type</typeparam>
    /// <typeparam name="C">Resulting value type</typeparam>
    /// <returns>Parser</returns>
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
