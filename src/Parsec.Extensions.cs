using System.Numerics;

namespace LanguageExt.RefParsec;

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
        public static Parsec<E, T, A> operator |(Parsec<E, T, A> lhs, Parsec<E, T, A> rhs) =>
            Module<E, T>.choose(lhs, rhs);
    }
    
        
    extension<E, T, A>(Parsec<E, T, A> self)
        where T : IEqualityOperators<T, T, bool>
    {
        /// <summary>
        /// Parse a stream of tokens
        /// </summary>
        /// <param name="stream">Stream of tokens</param>
        /// <param name="sourceName">Name of the source, usually a source-file name</param>
        /// <returns>Result of the parsing operation</returns>
        public ParserResult<E, T, A> Parse(ReadOnlySpan<T> stream, string sourceName = "")
        {
            Span<byte> stackMem = stackalloc byte[4096];
            return self.Parse(stream, stackMem, sourceName, static x => x);
        }

        /// <summary>
        /// Parse a stream of tokens
        /// </summary>
        /// <param name="stream">Stream of tokens</param>
        /// <param name="stackMem">Memory to use for the stack</param>
        /// <param name="sourceName">Name of the source, usually a source-file name</param>
        /// <returns>Result of the parsing operation</returns>
        public ParserResult<E, T, A> Parse(ReadOnlySpan<T> stream, Span<byte> stackMem, string sourceName = "") =>
            self.Parse(stream, stackMem, sourceName, static x => x);        
    }
}
