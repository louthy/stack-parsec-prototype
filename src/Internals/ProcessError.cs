using System.Numerics;
using System.Runtime.CompilerServices;

namespace LanguageExt.RefParsec;

static partial class ParsecInternals<E, T, A>
    where T : IEqualityOperators<T, T, bool>
    where A : allows ref struct
{
    /// <summary>
    /// Process an Error instruction.
    /// </summary>
    /// <param name="instructions">Byte code instructions</param>
    /// <param name="constants">Byte code constants</param>
    /// <param name="constantOffset">Offset into the constants</param>
    /// <param name="state">Parser state</param>
    /// <param name="stack">VM stack</param>
    /// <param name="pc">Program counter</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void ProcessError(
        Bytes instructions, 
        Stack constants, 
        int constantOffset, 
        in State<E, T> state, 
        ref Stack stack, 
        ref int pc)
    {
        var cid = instructions.GetConstantId(ref pc, constantOffset);
        if (constants.At<E>(cid, out var err))
        {
            stack = stack.PushTerminator(state, out var pos)
                         .PushCustom(err)
                         .PushErr(pos);
        }
        else
        {
            throw new Exception("Error: span not found");
        }
    }
}