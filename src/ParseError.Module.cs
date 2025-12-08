using LanguageExt;

namespace StackParsecPrototype;

public static class ParseError
{
    /// <summary>
    /// Custom error
    /// </summary>
    /// <param name="customError">Custom error value</param>
    /// <param name="expected"></param>
    /// <param name="consumed"></param>
    /// <param name="position">Position of the error</param>
    public static ParseError<E, T> Custom<E, T>(E customError, bool consumed, bool expected, SourcePos position)  =>
        new ParseError<E, T>.Custom(customError, consumed, expected, position);
    
    /// <summary>
    /// Token error
    /// </summary>
    /// <param name="TokenValue">Token that is the subject of the error</param>
    /// <param name="expected"></param>
    /// <param name="consumed"></param>
    /// <param name="position">Position of the error</param>
    public static ParseError<E, T> Token<E, T>(T TokenValue, bool consumed, bool expected, SourcePos position) =>
        new ParseError<E, T>.Token(TokenValue, consumed, expected, position);
    
    /// <summary>
    /// Tokens error
    /// </summary>
    /// <param name="tokenValues">Tokens that are the subject of the error</param>
    /// <param name="expected"></param>
    /// <param name="consumed"></param>
    /// <param name="position">Position of the error</param>
    public static ParseError<E, T> Tokens<E, T>(Seq<T> tokenValues, bool consumed, bool expected, SourcePos position) =>
        new ParseError<E, T>.Tokens(tokenValues, consumed, expected, position);
    
    /// <summary>
    /// Label error
    /// </summary>
    /// <param name="labelValue">Bespoke label for the error</param>
    /// <param name="expected"></param>
    /// <param name="consumed"></param>
    /// <param name="position">Position of the error</param>
    public static ParseError<E, T> Label<E, T>(string labelValue, bool consumed, bool expected, SourcePos position) =>
        new ParseError<E, T>.Label(labelValue, consumed, expected, position);
    
    /// <summary>
    /// End of input error
    /// </summary>
    /// <param name="expected"></param>
    /// <param name="consumed"></param>
    /// <param name="position">Position of the error</param>
    public static ParseError<E, T> EndOfInput<E, T>(bool consumed, bool expected, SourcePos position) =>
        new ParseError<E, T>.EndOfInput(consumed, expected, position);
}
