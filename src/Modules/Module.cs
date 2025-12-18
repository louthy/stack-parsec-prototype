using System.Numerics;
using LanguageExt;

namespace LanguageExt.RefParsec;

/// <summary>
/// Core primitive parsers
/// </summary>
/// <typeparam name="E">Error type</typeparam>
/// <typeparam name="T">Token type</typeparam>
public static class Module<E, T>
    where T : IEqualityOperators<T, T, bool>    
{
    /// <summary>
    /// Replace any 'expected' error contexts with a label.  This allows for more meaningful error messages to
    /// be applied at a higher-scope than where more token-level errors are yielded.
    /// </summary>
    /// <param name="name">Label value</param>
    /// <param name="p">Parser to run and to label the failure results of if necessary (only labels empty-errors)</param>
    /// <typeparam name="A">Parsed value type</typeparam>
    /// <returns>Parser</returns>
    public static Parsec<E, T, A> label<A>(string name, Parsec<E, T, A> p)
        where A : allows ref struct => 
        new (p.Instructions.PrependString(name).Prepend(OpCode.Label), p.Constants);
    
    /// <summary>
    /// Clear any 'expected' error contexts, this is like `label` with an empty string, but it more formally clears
    /// any previously expected error-items.
    /// </summary>
    /// <param name="p">Parser to run and to label the failure results of if necessary (only labels empty-errors)</param>
    /// <typeparam name="A">Parsed value type</typeparam>
    /// <returns>Parser</returns>
    public static Parsec<E, T, A> hidden<A>(Parsec<E, T, A> p)
        where A : allows ref struct => 
        new (p.Instructions.Prepend(OpCode.Hidden), p.Constants);

    /// <summary>
    /// Yields a custom-error value.  Custom-error values override all other trivial error types, like token-level
    /// errors, label-errors, or end-of-stream errors. 
    /// </summary>
    /// <param name="error"></param>
    /// <typeparam name="A"></typeparam>
    /// <returns></returns>
    public static Parsec<E, T, A> error<A>(E error)  
        where A : allows ref struct =>
        new (Bytes.singleton(OpCode.Error).AddConstantId(0), Stack.singleton(error));

    /*/// <summary>
    /// Stop parsing and report a trivial `ParseError`.
    /// </summary>
    /// <param name="unexpected">Optional unexpected tokens</param>
    /// <param name="expected">Expected tokens</param>
    /// <typeparam name="A">Value type (never yielded because this is designed to error)</typeparam>
    /// <returns>Parser</returns>
    public static Parsec<E, T, A> failure<A>(Option<ErrorItem<T>> unexpected, Set<ErrorItem<T>> expected) =>
        getOffset >>> (o => error<A>(ParseError.Trivial<T, E>(o, unexpected, expected)));

    /// <summary>
    /// Stop parsing and report a fancy 'ParseError'. To report a single custom parse error
    /// </summary>
    /// <param name="errors">Optional unexpected tokens</param>
    /// <typeparam name="A">Value type (never yielded because this is designed to error)</typeparam>
    /// <returns>Parser</returns>
    public static Parsec<E, T, A> failure<A>(Set<ErrorFancy<E>> errors) =>
        getOffset >>> (o => error<A>(ParseError.Fancy<T, E>(o, errors)));

    /// <summary>
    /// Stop parsing and report a fancy 'ParseError'. To report a single custom parse error
    /// </summary>
    /// <param name="error">Custom error</param>
    /// <typeparam name="A">Value type (never yielded because this is designed to error)</typeparam>
    /// <returns>Parser</returns>
    public static Parsec<E, T, A> failure<A>(E error) =>
        Pure(error) >> ErrorFancy.Custom >> Set.singleton >> (failure<A>) >> lower;

    /// <summary>
    /// The parser `unexpected(item)` fails with an error message telling
    /// about an unexpected `item` without consuming any input.
    /// </summary>
    /// <param name="item">The unexpected item</param>
    /// <typeparam name="A">Value type (never yielded because this is designed to error)</typeparam>
    /// <returns>Parser</returns>
    public static Parsec<E, T, A> unexpected<A>(ErrorItem<T> item) =>
        failure<A>(Some(item), default);*/
    
    public static Parsec<E, T, A> @try<A>(Parsec<E, T, A> p)  
        where A : allows ref struct =>
        new (p.Instructions.Prepend(OpCode.Try), p.Constants);

    public static Parsec<E, T, A> choose<A>(Parsec<E, T, A> p1, Parsec<E, T, A> p2)
        where A : allows ref struct
    {
        //  1: OR
        //  4: lhs instructions count (in bytes)
        //  4: rhs instructions count (in bytes)
        //  2: offset to second constants set
        //  n: lhs instructions
        //  1: RETURN
        //  n: rhs instructions
        //  1: RETURN

        var lhs = p1.Instructions.Add(OpCode.Return);
        var rhs = p2.Instructions.Add(OpCode.Return);

        var instrs = rhs.Prepend(lhs.Span())
                        .PrependConstantId(p1.Constants.Count)
                        .PrependInt32(rhs.Count)
                        .PrependInt32(lhs.Count)
                        .Prepend(OpCode.Or);
        
        // Next set of instruction to run if the first set fails
        //var nspan = p2.Instructions.Span();

        // Total number of bytes in the new instruction set.  This will allow
        // us to jump over the second instruction set if the first succeeds.
        //var next = nspan.Length + Bytes.ConstantIdSize + 4 /* this offset size */; 
        
        // Concatenate the instruction sets with meta-data in-between that
        // delimits the two sets and also gives us enough information to skip
        // the second set if the first succeeds.
        // var ninstrs = p1.Instructions
        //                 .PrependInt32(p1.Instructions.Count)
        //                 .Prepend(OpCode.Or)
        //                 .AddInt32(next)
        //                 .AddConstantId(p1.Constants.Count)
        //              .Add(p2.Instructions.Span());

        // Concatenate the constant sets
        var constants = p1.Constants.Append(p2.Constants);
        
        return new(instrs, constants);
    }

    public static Parsec<E, T, A> lookAhead<A>(Parsec<E, T, A> p)  
        where A : allows ref struct =>
        new (p.Instructions.Prepend(OpCode.LookAhead), p.Constants);    

    public static Parsec<E, T, A> notFollowedBy<A>(Parsec<E, T, A> p)  
        where A : allows ref struct =>
        new (p.Instructions.Prepend(OpCode.NotFollowedBy), p.Constants);

    public static Parsec<E, T, A> observing<A>(Parsec<E, T, A> p)  
        where A : allows ref struct =>
        new (p.Instructions.Prepend(OpCode.Observing), p.Constants);
    
    public static Parsec<E, T, Unit> eof =>
        new (Bytes.singleton(OpCode.EOF), default);
    
    public static Parsec<E, T, T> take1 =>
        new (Bytes.singleton(OpCode.Take1), default);
    
    public static Parsec<E, T, ReadOnlySpan<T>> take(uint amount) =>
        new (Bytes.singleton(OpCode.TakeN).AddUInt32(amount), default);
    
    public static Parsec<E, T, ReadOnlySpan<T>> takeWhile(Func<T, bool> predicate) =>
        new (Bytes.singleton(OpCode.TakeWhile).AddConstantId(0), Stack.singleton(predicate));
    
    public static Parsec<E, T, ReadOnlySpan<T>> takeWhile1(Func<T, bool> predicate) =>
        new (Bytes.singleton(OpCode.TakeWhile1).AddConstantId(0), Stack.singleton(predicate));

    public static Parsec<E, T, T> token(T token) =>
        new (Bytes.singleton(OpCode.Token).AddConstantId(0), Stack.singleton(token));

    public static Parsec<E, T, ReadOnlySpan<T>> tokens(ReadOnlySpan<T> tokens) =>
        new (Bytes.singleton(OpCode.Tokens).AddConstantId(0), Stack.singleton(tokens));

    public static Parsec<E, T, T> oneOf(ReadOnlySpan<T> tokens) =>
        new (Bytes.singleton(OpCode.OneOf).AddConstantId(0), Stack.singleton(tokens));

    public static Parsec<E, T, T> noneOf(ReadOnlySpan<T> tokens) =>
        new (Bytes.singleton(OpCode.NoneOf).AddConstantId(0), Stack.singleton(tokens));

    public static Parsec<E, T, T> satisfy(Func<T, bool> test) => 
        new (Bytes.singleton(OpCode.Satisfy).AddConstantId(0), Stack.singleton(test));
    
    public static Parsec<E, T, A> pure<A>(A value) 
        where A : allows ref struct =>
        new (Bytes.singleton(OpCode.Pure).AddConstantId(0), Stack.singleton(value));

    public static Parsec<E, T, B> map<A, B>(Parsec<E, T, A> ma, Func<A, B> f)
        where A : allows ref struct
        where B : allows ref struct =>
        ma.Map(f);

    public static Parsec<E, T, B> bind<A, B>(Parsec<E, T, A> ma, Func<A, Parsec<E, T, B>> f)
        where A : allows ref struct
        where B : allows ref struct =>
        ma.Bind(f);
    
    public static Parsec<E, T, C> selectMany<A, B, C>(Parsec<E, T, A> ma, Func<A, Parsec<E, T, B>> bind, Func<A, B, C> project) 
        where B : allows ref struct 
        where C : allows ref struct =>
        ma.SelectMany(bind, project);
}
