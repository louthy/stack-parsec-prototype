namespace LanguageExt.RefParsec;

public static class ParserResult
{
    public static ParserResult<E, T, A> ConsumedOK<E, T, A>(A value, State<T> state) =>
        new ParserResult<E, T, A>.ConsumedOK(value, state.Position);

    public static ParserResult<E, T, A> EmptyOK<E, T, A>(A value, State<T> state) =>
        new ParserResult<E, T, A>.EmptyOK(value, state.Position);
    
    public static ParserResult<E, T, A> ConsumedErr<E, T, A>(ParseError<E, T> error, State<T> state) =>
        new ParserResult<E, T, A>.ConsumedErr(error, state.Position);

    public static ParserResult<E, T, A> EmptyErr<E, T, A>(ParseError<E, T> error, State<T> state) =>
        new ParserResult<E, T, A>.EmptyErr(error, state.Position);
}

public abstract record ParserResult<E, T, A>(SourcePos Position)
{
    /// <summary>
    /// Successfully acquired a result
    /// </summary>
    public abstract bool Ok { get; }  
    
    /// <summary>
    /// Failed to acquire a result
    /// </summary>
    public abstract bool Failed { get; }
    
    /// <summary>
    /// Consumed some or all of the input
    /// </summary>
    public abstract bool Consumed { get; }
    
    /// <summary>
    /// No input was consumed
    /// </summary>
    public abstract bool Empty { get; }

    public record ConsumedOK(A Value, SourcePos Position) : ParserResult<E, T, A>(Position)
    {
        public override bool Ok => 
            true;

        public override bool Failed =>
            false;

        public override bool Consumed =>
            true;
        
        public override bool Empty =>
            false;
    }

    public record EmptyOK(A Value, SourcePos Position) : ParserResult<E, T, A>(Position)
    {
        public override bool Ok => 
            true;

        public override bool Failed =>
            false;

        public override bool Consumed =>
            false;
        
        public override bool Empty =>
            true;
    }

    public record ConsumedErr(ParseError<E, T> Value, SourcePos Position) : ParserResult<E, T, A>(Position)
    {
        public override bool Ok => 
            false;

        public override bool Failed =>
            true;

        public override bool Consumed =>
            true;
        
        public override bool Empty =>
            false;
    }

    public record EmptyErr(ParseError<E, T> Value, SourcePos Position) : ParserResult<E, T, A>(Position)
    {
        public override bool Ok => 
            false;

        public override bool Failed =>
            true;

        public override bool Consumed =>
            false;
        
        public override bool Empty =>
            true;
    }    
}

