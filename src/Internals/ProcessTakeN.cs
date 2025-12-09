using System.Numerics;
using System.Runtime.CompilerServices;

namespace StackParsecPrototype;

static partial class ParsecInternals<E, T, A>
    where T : IEqualityOperators<T, T, bool>
    where A : allows ref struct
{
    /// <summary>
    /// Process a Take(n) instruction.
    /// </summary>
    /// <param name="instructions">Byte code instructions</param>
    /// <param name="state">Parser state</param>
    /// <param name="stack">VM stack</param>
    /// <param name="pc">Program counter</param>
    /// <param name="taken">Tokens read, so far</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void ProcessTakeN(
        Bytes instructions, 
        ref State<E, T> state, 
        ref Stack stack, 
        ref int pc, 
        ref int taken)
    {
        var offset = state.Position.Offset;
        var n      = BitConverter.ToInt32(instructions.Slice(pc, 4).Span());
        pc += 4;
        if (offset + n > state.Input.Length)
        {
            stack = ParseErrorStack.EndOfInput(false, false, state.Position, stack);
        }
        else
        {
            var ts = state.Input.Slice(offset, n);
            stack = stack.Push(ts).PushOK();
            state = state.Next(n);
            taken += n;
        }
    }
}