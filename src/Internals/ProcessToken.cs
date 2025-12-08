using System.Numerics;
using System.Runtime.CompilerServices;

namespace StackParsecPrototype;

static partial class ParsecInternals<E, T, A>
    where T : IEqualityOperators<T, T, bool>
    where A : allows ref struct
{
    /// <summary>
    /// Process a Token instruction.
    /// </summary>
    /// <param name="instructions">Byte code instructions</param>
    /// <param name="constants">Byte code constants</param>
    /// <param name="constantOffset">Offset into the constants</param>
    /// <param name="state">Parser state</param>
    /// <param name="stack">VM stack</param>
    /// <param name="pc">Program counter</param>
    /// <param name="taken">Tokens read, so far</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void ProcessToken(
        Bytes instructions, 
        Stack constants, 
        int constantOffset, 
        ref State<E, T> state, 
        ref Stack stack, 
        ref int pc, 
        ref int taken)
    {
        // Get the tokens
        if (constants.At<T>(instructions[pc++] + constantOffset, out var token))
        {
            var offset = state.Position.Offset;
            if (offset + 1 > state.Input.Length)
            {
                stack = ParseErrorStack.EndOfInput(false, false, state.Position, stack);
            }
            else
            {
                var read = state.Input.Slice(state.Position.Offset, 1);
                if (read[0] == token)
                {
                    state = state.NextToken;
                    taken++;
                    stack = stack.Push(read[0])
                                 .Push(StackReply.OK);
                }
                else
                {
                    // TODO: Show the expected `token` 
                    stack = ParseErrorStack.Token(read[0], false, false, state.Position, stack);
                }
            }
        }
        else
        {
            throw new Exception("Tokens: span not found");
        }
    }
}