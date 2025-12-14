using System.Numerics;

namespace LanguageExt.RefParsec;

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
    const int defaultStackSize = 4096;
    
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
    /// <param name="f">Function to map away from a possible ref type to something concrete</param>
    /// <returns>Result of the parsing operation</returns>
    public ParserResult<E, T, B> Parse<B>(
        ReadOnlySpan<T> stream, 
        Func<A, B> f)
    {
        Span<byte> stackMem = stackalloc byte[defaultStackSize];
        return Parse(stream, stackMem, "", f);
    }

    /// <summary>
    /// Parse a stream of tokens
    /// </summary>
    /// <param name="stream">Stream of tokens</param>
    /// <param name="sourceName">Name of the source, usually a source-file name</param>
    /// <param name="f">Function to map away from a possible ref type to something concrete</param>
    /// <returns>Result of the parsing operation</returns>
    public ParserResult<E, T, B> Parse<B>(
        ReadOnlySpan<T> stream, 
        string sourceName, 
        Func<A, B> f)
    {
        Span<byte> stackMem = stackalloc byte[defaultStackSize];
        return Parse(stream, stackMem, sourceName, f);
    }

    /// <summary>
    /// Parse a stream of tokens
    /// </summary>
    /// <param name="stream">Stream of tokens</param>
    /// <param name="stackMem">Memory to use for the stack</param>
    /// <param name="f">Function to map away from a possible ref type to something concrete</param>
    /// <returns>Result of the parsing operation</returns>
    public ParserResult<E, T, B> Parse<B>(
        ReadOnlySpan<T> stream, 
        Span<byte> stackMem, 
        Func<A, B> f) =>
        Parse(stream, stackMem, "", f);

    /// <summary>
    /// Parse a stream of tokens
    /// </summary>
    /// <param name="stream">Stream of tokens</param>
    /// <param name="stackMem">Memory to use for the stack</param>
    /// <param name="sourceName">Name of the source, usually a source-file name</param>
    /// <param name="f">Function to map away from a possible ref type to something concrete</param>
    /// <returns>Result of the parsing operation</returns>
    public ParserResult<E, T, B> Parse<B>(
        ReadOnlySpan<T> stream, 
        Span<byte> stackMem, 
        string sourceName, 
        Func<A, B> f) =>
        Parse(stream,
              stackMem,
              sourceName,
              (x, s) => ParserResult.ConsumedOK<E, T, B>(f(x), s),
              (x, s) => ParserResult.EmptyOK<E, T, B>(f(x), s),
              ParserResult.ConsumedErr<E, T, B>,
              ParserResult.EmptyErr<E, T, B>);

    /// <summary>
    /// Parse a stream of tokens
    /// </summary>
    /// <param name="stream">Stream of tokens</param>
    /// <param name="cok">Consumed OK handler (parsed input tokens into a value)</param>
    /// <param name="eok">Empty OK handler (didn't parse any tokens but was able to yield a successful result)</param>
    /// <param name="cerr">Consumed error handler (usually fatal)</param>
    /// <param name="eerr">Empty error handler (often recoverable)</param>
    /// <returns>Result of the parsing operation</returns>
    public B Parse<B>(
        ReadOnlySpan<T> stream,
        Func<A, State<T>, B> cok,
        Func<A, State<T>, B> eok,
        Func<ParseError<E, T>, State<T>, B> cerr,
        Func<ParseError<E, T>, State<T>, B> eerr)
    {
        Span<byte> stackMem = stackalloc byte[defaultStackSize];
        return Parse(stream, stackMem, "", cok, eok, cerr, eerr);
    }

    /// <summary>
    /// Parse a stream of tokens
    /// </summary>
    /// <param name="stream">Stream of tokens</param>
    /// <param name="sourceName">Name of the source, usually a source-file name</param>
    /// <param name="cok">Consumed OK handler (parsed input tokens into a value)</param>
    /// <param name="eok">Empty OK handler (didn't parse any tokens but was able to yield a successful result)</param>
    /// <param name="cerr">Consumed error handler (usually fatal)</param>
    /// <param name="eerr">Empty error handler (often recoverable)</param>
    /// <returns>Result of the parsing operation</returns>
    public B Parse<B>(
        ReadOnlySpan<T> stream,
        string sourceName,
        Func<A, State<T>, B> cok,
        Func<A, State<T>, B> eok,
        Func<ParseError<E, T>, State<T>, B> cerr,
        Func<ParseError<E, T>, State<T>, B> eerr)
    {
        Span<byte> stackMem = stackalloc byte[defaultStackSize];
        return Parse(stream, stackMem, sourceName, cok, eok, cerr, eerr);
    }

    /// <summary>
    /// Parse a stream of tokens
    /// </summary>
    /// <param name="stream">Stream of tokens</param>
    /// <param name="stackMem">Memory to use for the stack</param>
    /// <param name="sourceName">Name of the source, usually a source-file name</param>
    /// <param name="cok">Consumed OK handler (parsed input tokens into a value)</param>
    /// <param name="eok">Empty OK handler (didn't parse any tokens but was able to yield a successful result)</param>
    /// <param name="cerr">Consumed error handler (usually fatal)</param>
    /// <param name="eerr">Empty error handler (often recoverable)</param>
    /// <returns>Result of the parsing operation</returns>
    public B Parse<B>(
        ReadOnlySpan<T> stream, 
        Span<byte> stackMem, 
        string sourceName,
        Func<A, State<T>, B> cok,
        Func<A, State<T>, B> eok,
        Func<ParseError<E, T>, State<T>, B> cerr,
        Func<ParseError<E, T>, State<T>, B> eerr)
    {
        var stack        = new Stack(stackMem);
        var instructions = Instructions;
        var constants    = Constants;
        var state        = new State<T>(stream, SourcePos.FromName(sourceName));

        return ParsecInternals<E, T, A>.Parse(instructions, constants, 0, state, stack, cok, eok, cerr, eerr);
    }

    /// <summary>
    /// Turns a parser that fails on partial consumption into one that backtracks and raises an
    /// 'empty error' that can be caught by the `|` operator. 
    /// </summary>
    /// <returns>Parser</returns>
    public Parsec<E, T, A> Try() =>
        new (Instructions.Prepend(OpCode.Try), Constants);    

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
        var constIx = Constants.Count;
        var instrs = Instructions.Add(OpCode.Invoke)
                                 .AddConstantId(constIx)
                                 .AddConstantId(constIx + 1);

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
                    return stack.PopError()
                                .Push(v)
                                .PushOK();
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
        var constIx = Constants.Count;
        var instrs = Instructions.Add(OpCode.Invoke)
                                 .AddConstantId(constIx)
                                 .AddConstantId(constIx + 1);

        var constants = Constants.PushStackOp(go)
                                 .Push(bind);

        return new Parsec<E, T, B>(instrs, constants);
        
        static Stack go(Stack stack, Stack constants)
        {
            if (stack.Peek<Func<A, Parsec<E, T, B>>>(out var f))
            {
                stack = stack.Pop();
                if (stack.Peek<A>(out var ma))
                {
                    stack = stack.Pop();
                    var mb = f(ma).Core;
                    return stack.PopError()
                                .Push(mb)
                                .PushOK();
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
        var constIx = Constants.Count;
        var instrs = Instructions.Add(OpCode.InvokeM)
                                 .AddConstantId(constIx)
                                 .AddConstantId(constIx + 1)
                                 .Add(OpCode.Invoke)
                                 .AddConstantId(constIx + 2)
                                 .AddConstantId(constIx + 3);

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
                    return stack.PopError()
                                .Push(mb)
                                .PushOK();
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
                        return stack.PopError()
                                    .Push(z)
                                    .PushOK();
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
