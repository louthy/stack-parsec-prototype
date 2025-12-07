using System.Numerics;

namespace StackParsecPrototype;

public readonly struct SourcePos(string name, int offset, int line, int column) :
    IEqualityOperators<SourcePos, SourcePos, bool>
{
    /// <summary>
    /// Create a source position from a name
    /// </summary>
    /// <param name="name">Name</param>
    /// <returns>SourcePos</returns>
    public static SourcePos FromName(string name) =>
        new (name, 0, 1, 1);
    
    /// <summary>
    /// Name of the source
    /// </summary>
    public string Name { get; } = 
        name;
    
    /// <summary>
    /// Raw offset
    /// </summary>
    public int Offset { get; } = 
        offset;
    
    /// <summary>
    /// Line number
    /// </summary>
    public int Line { get; } = 
        line;
    
    /// <summary>
    /// Column number
    /// </summary>
    public int Column { get; } = 
        column;
    
    /// <summary>
    /// Convert to string
    /// </summary>
    /// <returns>String representation of the structure</returns>
    public override string ToString() => 
        $"{Name}({Line},{Column})";

    /// <summary>
    /// Move to the beginning of the next line
    /// </summary>
    /// <returns></returns>
    public SourcePos NextLine =>
        new (Name, Offset + 1, Line + 1, 1);

    /// <summary>
    /// Move to the next token
    /// </summary>
    /// <returns></returns>
    public SourcePos NextToken =>
        new (Name, Offset + 1, Line, Column + 1);

    /// <summary>
    /// Move to the next token
    /// </summary>
    /// <returns></returns>
    public SourcePos Next(int amount) =>
        new (Name, Offset + amount, Line, Column + amount);

    public static bool operator ==(SourcePos left, SourcePos right) =>
        left.Offset == right.Offset &&
        left.Name   == right.Name;

    public static bool operator !=(SourcePos left, SourcePos right) =>
        left.Offset != right.Offset ||
        left.Name   != right.Name;
}
