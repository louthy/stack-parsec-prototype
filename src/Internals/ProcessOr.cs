using System.Numerics;

namespace LanguageExt.RefParsec;

static partial class ParsecInternals<E, T, A>
    where T : IEqualityOperators<T, T, bool>
    where A : allows ref struct
{
    /// <summary>
    /// Process choice operator
    /// </summary>
    /// <param name="instructions">Byte code instructions</param>
    /// <param name="constants">Byte code constants</param>
    /// <param name="constantOffset">Offset into the constants</param>
    /// <param name="state">Parser state</param>
    /// <param name="stack">VM stack</param>
    /// <param name="pc">Program counter</param>
    static void ProcessOr(
        Bytes instructions, 
        Stack constants, 
        int constantOffset, 
        ref State<T> state, 
        ref Stack stack,
        ref int pc)
    {
        //  1: OR
        //  4: lhs instructions count (in bytes)
        //  4: rhs instructions count (in bytes)
        //  2: offset to second constants set
        //  n: lhs instructions
        //  1: RETURN
        //  n: rhs instructions

        var span    = instructions.Span();
        var lhsSize = BitConverter.ToInt32(span.Slice(pc, 4));
        var rhsSize = BitConverter.ToInt32(span.Slice(pc + 4, 4));
        var lhs     = instructions.Slice(pc + 10, lhsSize);
        var so      = state.Position.Offset;
                    
        ParseUntyped(lhs, constants, constantOffset, ref state, ref stack);
        if (stack.IsOK())
        {
            // Skip the whole OR block (left and right hand side) if we succeeded
            pc = pc + 10 + lhsSize + rhsSize;
        }
        else if(state.Position.Offset > so)
        {
            // We've consumed, which makes an error fatal, so early-out
            return;
        }
        else
        {
            // We have an empty-error, so we can try the right-hand side
            var rhs      = instructions.Slice(pc + 10 + lhsSize, rhsSize);
            var constOff = BitConverter.ToUInt16(span.Slice(pc + 8, 2));
            ParseUntyped(rhs, constants, constantOffset + constOff, ref state, ref stack);
            if (stack.IsOK())
            {
                // Skip the whole OR block (left and right hand side) if we succeeded
                pc = pc + 10 + lhsSize + rhsSize;
            }
        }
    }    
}