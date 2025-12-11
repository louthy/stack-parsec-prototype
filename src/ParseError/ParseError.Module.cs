using static LanguageExt.Prelude;

namespace LanguageExt.RefParsec;

public static class ParseError
{
    public static ParseError<E, T> Trivial<E, T>(
        SourcePos position, 
        Option<ErrorItem<T>> unexpected, 
        Set<ErrorItem<T>> expected) =>
        new ParseError<E, T>.Trivial(position, unexpected, expected);
    
    public static ParseError<E, T> Trivial<E, T>(
        SourcePos position, 
        Option<ErrorItem<T>> unexpected, 
        ErrorItem<T> expected) =>
        new ParseError<E, T>.Trivial(position, unexpected, Set.singleton(expected));

    public static ParseError<E, T> Trivial<E, T>(
        SourcePos position,
        Option<ErrorItem<T>> unexpected,
        Option<ErrorItem<T>> expected) =>
        new ParseError<E, T>.Trivial(position, unexpected, expected.IsSome ? Set.singleton((ErrorItem<T>)expected) : default);

    public static ParseError<E, T> Trivial<E, T>(
        SourcePos position, 
        Option<ErrorItem<T>> unexpected) =>
        new ParseError<E, T>.Trivial(position, unexpected, default);
    
    public static ParseError<E, T> Fancy<E, T>(SourcePos position, Set<ErrorFancy<E>> errors) => 
        new ParseError<E, T>.Fancy(position, errors);
    
    public static ParseError<E, T> Fancy<E, T>(SourcePos position, ErrorFancy<E> errors) => 
        new ParseError<E, T>.Fancy(position, Set.singleton(errors));

    public static ParseError<E, T> mergeError<E, T>(ParseError<E, T> e1, ParseError<E, T> e2)
    {
        return (e1.Position.CompareTo(e2.Position)) switch
               {
                   < 0 => e2,
                   0 => (e1, e2) switch
                        {
                            (ParseError<E, T>.Trivial (var s1, var u1, var p1), ParseError<E, T>.Trivial (_, var u2, var p2)) =>
                                Trivial<E, T>(s1, n(u1, u2), p1 + p2),
                            
                            (ParseError<E, T>.Fancy, ParseError<E, T>.Trivial) =>
                                e1,
                            
                            (ParseError<E, T>.Trivial, ParseError<E, T>.Fancy) =>
                                e2,
                            
                            (ParseError<E, T>.Fancy (var s1, var x1), ParseError<E, T>.Fancy(_, var x2) ) =>
                                Fancy<E, T>(s1, x1 + x2),
                            
                            _ => e1
                        },
                   > 0 => e1
               };
        
        // NOTE The logic behind this merging is that since we only combine
        // parse errors that happen at exactly the same position, all the
        // unexpected items will be prefixes of input stream at that position or
        // labels referring to the same thing. Our aim here is to choose the
        // longest prefix (merging with labels and end of input is somewhat
        // arbitrary, but is necessary because otherwise we can't make
        // ParseError lawful Monoid and have nice parse errors at the same
        // time).
        static Option<ErrorItem<T>> n(Option<ErrorItem<T>> mx, Option<ErrorItem<T>> my) =>
            (mx, my) switch
            {
                ({ IsNone: true }, { IsNone: true }) => None,
                ({ IsSome: true }, { IsNone: true }) => mx,
                ({ IsNone: true }, { IsSome: true }) => my,
                (_, _)                               => Some((ErrorItem<T>)mx > (ErrorItem<T>)my 
                                                                 ? (ErrorItem<T>)mx 
                                                                 : (ErrorItem<T>)my)
            };
    }

}
