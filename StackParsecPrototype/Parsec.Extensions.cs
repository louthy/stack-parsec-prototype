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
            self.Bind(static mx => mx);
    }
    
    extension<E, T, A>(Parsec<E, T, A> self)
        where T : IEqualityOperators<T, T, bool>    
        where A : allows ref struct
    {
        public static Parsec<E, T, A> operator |(in Parsec<E, T, A> lhs, in Parsec<E, T, A> rhs) =>
            Module<E, T>.choose(lhs, rhs);
    }
}
