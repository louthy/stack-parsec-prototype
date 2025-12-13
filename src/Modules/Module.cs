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
    public static Parsec<E, T, A> label<A>(string name, Parsec<E, T, A> p)
        where A : allows ref struct 
    {
        var instrs = p.Instructions
                      .AddConstantId(p.Constants.Count)
                      .Cons(OpCode.Label);

        var objs = p.Constants.Push(name);

        return new Parsec<E, T, A>(instrs, objs);
    }

    public static Parsec<E, T, A> error<A>(E error)  
        where A : allows ref struct =>
        new (Bytes.singleton(OpCode.Error).AddConstantId(0), Stack.singleton(error));

    public static Parsec<E, T, A> @try<A>(Parsec<E, T, A> p)  
        where A : allows ref struct =>
        new (p.Instructions.Cons(OpCode.Try), p.Constants);

    public static Parsec<E, T, A> choose<A>(Parsec<E, T, A> p1, Parsec<E, T, A> p2)
        where A : allows ref struct
    {
        // Next set of instruction to run if the first set fails
        var nspan = p2.Instructions.Span();

        // Total number of bytes in the new instruction set.  This will allow
        // us to jump over the second instruction set if the first succeeds.
        var next = nspan.Length + Bytes.ConstantIdSize + 4 /* this offset size */; 
        
        // Concatenate the instruction sets with meta-data in-between that
        // delimits the two sets and also gives us enough information to skip
        // the second set if the first succeeds.
        var ninstrs = p1.Instructions
                        .ConsInt32(p1.Instructions.Count)
                        .Cons(OpCode.Or)
                        .AddInt32(next)
                        .AddConstantId(p1.Constants.Count)
                        .Add(p2.Instructions.Span());

        // Concatenate the constant sets
        var nconstants = p1.Constants.Append(p2.Constants);
        
        return new(ninstrs, nconstants);
    }

    public static Parsec<E, T, A> lookAhead<A>(Parsec<E, T, A> p)  
        where A : allows ref struct =>
        new (p.Instructions.Cons(OpCode.LookAhead), p.Constants);    

    public static Parsec<E, T, A> notFollowedBy<A>(Parsec<E, T, A> p)  
        where A : allows ref struct =>
        new (p.Instructions.Cons(OpCode.NotFollowedBy), p.Constants);

    public static Parsec<E, T, A> observing<A>(Parsec<E, T, A> p)  
        where A : allows ref struct =>
        new (p.Instructions.Cons(OpCode.Observing), p.Constants);
    
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
