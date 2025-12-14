using System.Numerics;
using System.Runtime.CompilerServices;

namespace LanguageExt.RefParsec;

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
    static void ProcessTake1(ref State<T> state, ref Stack stack)
    {
        var offset = state.Position.Offset;
        if (offset + 1 > state.Input.Length)
        {
            stack = stack.PushTerminator(state, out var pos)
                         .PushEndOfInput(false)
                         .PushErr(pos);
        }
        else
        {
            stack = stack.PopError()
                         .Push(state.Input[state.Position.Offset])
                         .PushOK();
            state = state.NextToken;
        }
    }
}