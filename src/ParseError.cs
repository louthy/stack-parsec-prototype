using System.Text;
using LanguageExt;

namespace StackParsecPrototype;

public abstract record ParseError<E, T>(bool Consumed, bool Expected, SourcePos Position)
{
    /// <summary>
    /// Custom error
    /// </summary>
    /// <param name="CustomError">Custom error value</param>
    /// <param name="Expected"></param>
    /// <param name="Consumed"></param>
    /// <param name="Position">Position of the error</param>
    public record Custom(E CustomError, bool Consumed, bool Expected, SourcePos Position) :
        ParseError<E, T>(Consumed, Expected, Position)
    {
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Position);
            sb.Append(Expected ? " expected " : " unexpected ");
            sb.Append(CustomError?.ToString() ?? "<no custom-error display text>");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Token error
    /// </summary>
    /// <param name="TokenValue">Token that is the subject of the error</param>
    /// <param name="Expected"></param>
    /// <param name="Consumed"></param>
    /// <param name="Position">Position of the error</param>
    public record Token(T TokenValue, bool Consumed, bool Expected, SourcePos Position) :
        ParseError<E, T>(Consumed, Expected, Position)
    {
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Position);
            sb.Append(Expected ? " expected '" : " unexpected '");
            sb.Append(TokenValue);
            sb.Append("'");
            return sb.ToString();
        }
    }
    
    /// <summary>
    /// Tokens error
    /// </summary>
    /// <param name="TokenValues">Tokens that are the subject of the error</param>
    /// <param name="Expected"></param>
    /// <param name="Consumed"></param>
    /// <param name="Position">Position of the error</param>
    public record Tokens(Seq<T> TokenValues, bool Consumed, bool Expected, SourcePos Position) :
        ParseError<E, T>(Consumed, Expected, Position)
    {
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Position);
            sb.Append(Expected ? " expected '" : " unexpected '");
            foreach (var token in TokenValues) sb.Append(token);
            sb.Append("'");
            return sb.ToString();
        }
    }
    
    /// <summary>
    /// Label error
    /// </summary>
    /// <param name="LabelValue">Bespoke label for the error</param>
    /// <param name="Expected"></param>
    /// <param name="Consumed"></param>
    /// <param name="Position">Position of the error</param>
    public record Label(string LabelValue, bool Consumed, bool Expected, SourcePos Position) :
        ParseError<E, T>(Consumed, Expected, Position)
    {
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Position);
            sb.Append(Expected ? " expected " : " unexpected ");
            sb.Append(LabelValue);
            return sb.ToString();
        }
    }
    
    /// <summary>
    /// End of input error
    /// </summary>
    /// <param name="Expected"></param>
    /// <param name="Consumed"></param>
    /// <param name="Position">Position of the error</param>
    public record EndOfInput(bool Consumed, bool Expected, SourcePos Position) :
        ParseError<E, T>(Consumed, Expected, Position)
    {
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Position);
            sb.Append(Expected ? " expected end of input " : " unexpected end of input");
            return sb.ToString();
        }
    }
}
