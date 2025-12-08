using System.Numerics;
using System.Runtime.CompilerServices;

namespace StackParsecPrototype;

static partial class ParsecInternals<E, T, A>
    where T : IEqualityOperators<T, T, bool>
    where A : allows ref struct
{
    /// <summary>
    /// Process a Take1 instruction.  
    /// </summary>
    /// <param name="state">Parser state</param>
    /// <param name="stack">VM stack</param>
    /// <param name="taken">Tokens read, so far</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void ProcessTake1(ref State<T, E> state, ref Stack stack, ref int taken)
    {
        var offset = state.Position.Offset;
        if (offset + 1 > state.Input.Length)
        {
            stack = stack.Push(ParseErrorRef<T, E>.UnexpectedEndOfInput(state.Position))
                         .Push(StackReply.EmptyError);
        }
        else
        {
            stack = stack.Push(state.Input[state.Position.Offset])
                         .Push(StackReply.OK);
            state = state.NextToken;
            taken++;
        }
    }
}