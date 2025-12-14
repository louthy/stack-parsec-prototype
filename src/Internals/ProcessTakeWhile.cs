using System.Numerics;
using System.Runtime.CompilerServices;

namespace LanguageExt.RefParsec;

static partial class ParsecInternals<E, T, A>
    where T : IEqualityOperators<T, T, bool>
    where A : allows ref struct
{
    /// <summary>
    /// Process a TakeWhile instruction.
    /// </summary>
    /// <param name="instructions">Byte code instructions</param>
    /// <param name="constants">Byte code constants</param>
    /// <param name="constantOffset">Offset into the constants</param>
    /// <param name="state">Parser state</param>
    /// <param name="stack">VM stack</param>
    /// <param name="pc">Program counter</param>
    /// <param name="taken">Tokens read, so far</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void ProcessTakeWhile(
        Bytes instructions, 
        Stack constants, 
        int constantOffset, 
        ref State<T> state, 
        ref Stack stack, 
        ref int pc)
    {
        var count = 0;
        var start = state.Position.Offset;
        var data  = state.Input.Slice(start);
        
        if (data.Length < 1)
        {
            stack = stack.PopError()
                         .Push(ReadOnlySpan<T>.Empty)
                         .PushOK();
            return;
        }
        
        if (constants.At<Func<T, bool>>(instructions.GetConstantId(ref pc, constantOffset), out var predicate))
        {
            while (true)
            {
                if (count >= state.Input.Length || !predicate(data[count]))
                {
                    stack = stack.PopError()
                                 .Push(data.Slice(0, count))
                                 .PushOK();
                    return;
                }
                else
                {
                    count++;
                    state = state.Next(count);
                }
            }
        }
        else
        {
            throw new Exception("TakeWhile: predicate not found");
        }
    }
}