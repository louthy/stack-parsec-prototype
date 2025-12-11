using LanguageExt;

namespace LanguageExt.RefParsec;

/// <summary>
/// Precomputed expected errors
/// </summary>
/// <remarks>
/// By precomputing expected errors, we can avoid creating a lot of additional memory allocations for common
/// parser combinators.
/// </remarks>
public static class ExpectedErrors
{
    public static readonly ErrorItem<char> newline =
        ErrorItem.Label<char>("newline");
    
    public static readonly ErrorItem<char> tab =
        ErrorItem.Label<char>("tab");
    
    public static readonly Option<string> whiteSpace =
        "white space";
    
    public static readonly ErrorItem<char> whiteSpaceChar =
        ErrorItem.Label<char>("white space");
    
    public static readonly ErrorItem<char> upperChar =
        ErrorItem.Label<char>("uppercase letter");
    
    public static readonly ErrorItem<char> lowerChar =
        ErrorItem.Label<char>("lowercase letter");
    
    public static readonly ErrorItem<char> letterChar =
        ErrorItem.Label<char>("letter");
    
    public static readonly ErrorItem<char> alphaNumChar =
        ErrorItem.Label<char>("alphanumeric character");
    
    public static readonly ErrorItem<char> digitChar =
        ErrorItem.Label<char>("digit");
    
    public static readonly ErrorItem<char> binaryDigitChar =
        ErrorItem.Label<char>("binary digit");
    
    public static readonly ErrorItem<char> hexDigitChar =
        ErrorItem.Label<char>("hexadecimal digit");
    
    public static readonly ErrorItem<char> numberChar =
        ErrorItem.Label<char>("numeric character");
    
    public static readonly ErrorItem<char> symbolChar =
        ErrorItem.Label<char>("symbol");
    
    public static readonly ErrorItem<char> punctuationChar =
        ErrorItem.Label<char>("punctuation");

    public static readonly ErrorItem<char> control =
        ErrorItem.Label<char>("control character");

    public static readonly ErrorItem<char> separator =
        ErrorItem.Label<char>("separator");
}
