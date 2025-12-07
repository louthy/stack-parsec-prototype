using System.Numerics;
using LanguageExt;

namespace StackParsecPrototype;

/// <summary>
/// Core primitive parsers
/// </summary>
/// <typeparam name="E">Error type</typeparam>
/// <typeparam name="T">Token type</typeparam>
public static class Module<E, T>
    where T : IEqualityOperators<T, T, bool>    
{
    public static Parsec<E, T, A> label<A>(string name, in Parsec<E, T, A> p)
        where A : allows ref struct 
    {
        // We have a byte-code, so we can't have more than 255 objects in our parser.
        var constIx = p.Constants.Count;
        if(constIx >= byte.MaxValue) throw new ArgumentException("Too many objects");

        var instrs = p.Instructions
                      .Cons((byte)constIx)
                      .Cons(OpCode.Label);

        var objs = p.Constants.Push(name);

        return new Parsec<E, T, A>(instrs, objs);
    }

    public static Parsec<E, T, A> error<A>(E error)  
        where A : allows ref struct =>
        new (Bytes.singleton(OpCode.Error).Add((byte)0), Stack.singleton(error));

    public static Parsec<E, T, A> @try<A>(in Parsec<E, T, A> p)  
        where A : allows ref struct =>
        new (p.Instructions.Cons(OpCode.Try), p.Constants);    

    public static Parsec<E, T, A> choose<A>(in Parsec<E, T, A> p1, in Parsec<E, T, A> p2)  
        where A : allows ref struct =>
        new (p1.Instructions
               .Add(OpCode.Or)
               .AddInt32(p1.Constants.Count)
               .Add(p2.Instructions.Span()), 
             p1.Constants.Append(p2.Constants));    

    public static Parsec<E, T, A> lookAhead<A>(in Parsec<E, T, A> p)  
        where A : allows ref struct =>
        new (p.Instructions.Cons(OpCode.LookAhead), p.Constants);    

    public static Parsec<E, T, A> notFollowedBy<A>(in Parsec<E, T, A> p)  
        where A : allows ref struct =>
        new (p.Instructions.Cons(OpCode.NotFollowedBy), p.Constants);

    public static Parsec<E, T, A> observing<A>(in Parsec<E, T, A> p)  
        where A : allows ref struct =>
        new (p.Instructions.Cons(OpCode.Observing), p.Constants);
    
    public static Parsec<E, T, Unit> eof =>
        new (Bytes.singleton(OpCode.EOF), default);
    
    public static Parsec<E, T, T> take1 =>
        new (Bytes.singleton(OpCode.Take1), default);
    
    public static Parsec<E, T, ReadOnlySpan<T>> take(uint amount) =>
        new (Bytes.singleton(OpCode.TakeN).AddUInt32(amount), default);
    
    public static Parsec<E, T, ReadOnlySpan<T>> takeWhile(Func<T, bool> predicate) =>
        new (Bytes.singleton(OpCode.TakeWhile).Add((byte)0), Stack.singleton(predicate));
    
    public static Parsec<E, T, ReadOnlySpan<T>> takeWhile1(Func<T, bool> predicate) =>
        new (Bytes.singleton(OpCode.TakeWhile1).Add((byte)0), Stack.singleton(predicate));

    public static Parsec<E, T, ReadOnlySpan<T>> tokens(ReadOnlySpan<T> tokens) =>
        new (Bytes.singleton(OpCode.Tokens).Add((byte)0), Stack.singleton(tokens));

    public static Parsec<E, T, T> oneOf(ReadOnlySpan<T> tokens) =>
        new (Bytes.singleton(OpCode.OneOf).Add((byte)0), Stack.singleton(tokens));

    public static Parsec<E, T, T> noneOf(ReadOnlySpan<T> tokens) =>
        new (Bytes.singleton(OpCode.NoneOf).Add((byte)0), Stack.singleton(tokens));

    public static Parsec<E, T, T> satisfy(Func<T, bool> test) => 
        new (Bytes.singleton(OpCode.Satisfy).Add((byte)0), Stack.singleton(test));
    
    public static Parsec<E, T, A> pure<A>(A value) 
        where A : allows ref struct =>
        new (Bytes.singleton(OpCode.Pure).Add((byte)0), Stack.singleton(value));

    public static Parsec<E, T, B> bind<A, B>(Parsec<E, T, A> ma, Func<A, Parsec<E, T, B>> f)
        where A : allows ref struct
        where B : allows ref struct =>
        ma.Bind(f);
    
    public static Parsec<E, T, C> selectMany<A, B, C>(Parsec<E, T, A> ma, Func<A, Parsec<E, T, B>> bind, Func<A, B, C> project) 
        where B : allows ref struct 
        where C : allows ref struct =>
        ma.SelectMany(bind, project);
}
