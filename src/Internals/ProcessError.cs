using System.Numerics;
using System.Runtime.CompilerServices;

namespace StackParsecPrototype;

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
    /// <param name="taken">Tokens read, so far</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void ProcessError(
        Bytes instructions, 
        Stack constants, 
        int constantOffset, 
        in State<E, T> state, 
        ref Stack stack, 
        ref int pc, 
        int taken)
    {
        if (constants.At<E>(instructions[pc++] + constantOffset, out var err))
        {
            stack = ParseErrorStack.Custom(err, taken > 0, true, state.Position, stack);
            
            // stack = stack.Push(ParseErrorRef<E, T>.Custom(state.Position, errs))
            //              .Push(taken == 0 ? StackReply.EmptyError : StackReply.ConsumedError);
        }
        else
        {
            throw new Exception("Error: span not found");
        }
    }
}