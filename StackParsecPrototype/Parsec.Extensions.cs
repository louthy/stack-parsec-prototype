using System.Numerics;
using LanguageExt;

namespace StackParsecPrototype;

public static class ParsecExtensions
{
    extension<E, T, A>(Parsec<E, T, Parsec<E, T, A>> self)
        where T : IEqualityOperators<T, T, bool>    
        where A : allows ref struct
    {
        public Parsec<E, T, A> Flatten() =>
            Parsec<E, T>.flatten(self);
    }
    
    extension<E, T, A, B>(Parsec<E, T, A> self)
        where T : IEqualityOperators<T, T, bool>    
        where A : allows ref struct
        where B : allows ref struct
    {
        public Parsec<E, T, B> Bind(Func<A, Parsec<E, T, B>> f) =>
            Parsec<E, T>.bind(self, f);
    }
    
    extension<E, T, A, B, C>(Parsec<E, T, A> self)
        where T : IEqualityOperators<T, T, bool>    
        where B : allows ref struct
        where C : allows ref struct
    {
        public Parsec<E, T, C> SelectMany(Func<A, Parsec<E, T, B>> bind, Func<A, B, C> project) =>
            Parsec<E, T>.selectMany(self, bind, project);
    }    
}
