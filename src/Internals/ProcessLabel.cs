using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LanguageExt.RefParsec;

static partial class ParsecInternals<E, T, A>
    where T : IEqualityOperators<T, T, bool>
    where A : allows ref struct
{
    /// <summary>
    /// Process a label instruction.
    /// </summary>
    /// <param name="instructions">Byte code instructions</param>
    /// <param name="constants">Byte code constants</param>
    /// <param name="constantOffset">Offset into the constants</param>
    /// <param name="state">Parser state</param>
    /// <param name="stack">VM stack</param>
    /// <param name="pc">Program counter</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void ProcessLabel(
        Bytes instructions,
        Stack constants, 
        int constantOffset, 
        ref State<T> state, 
        ref Stack stack,
        ref int pc)
    {
        var span     = instructions.Span().Slice(pc);
        var charSize = BitConverter.ToInt32(span[..4]);
        var byteSize = charSize * sizeof(char);
        var bytes    = span.Slice(4, byteSize);
        var label    = MemoryMarshal.Cast<byte, char>(bytes);
        var lsize    = 4 + byteSize;

        // Skip past the label header and text
        pc += lsize;
        var so = state.Position.Offset;
        
        ParseUntyped(instructions, constants, constantOffset, ref state, ref stack, ref pc);

        if (stack.IsErr() && state.Position.Offset == so)
        {
            // We have an empty-error, so we can try the label
            stack = stack.PushTerminator(state, out var pos)
                         .PushLabel(label, true)
                         .PushErr(pos);
            
            // TODO: Consider whether we should pop all errors off the stack first
        }
    }
}