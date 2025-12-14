using System.Numerics;
using System.Runtime.CompilerServices;

namespace LanguageExt.RefParsec;

static partial class ParsecInternals<E, T, A>
    where T : IEqualityOperators<T, T, bool>
    where A : allows ref struct
{
    /// <summary>
    /// Process a OneOf instruction.
    /// </summary>
    /// <param name="instructions">Byte code instructions</param>
    /// <param name="constants">Byte code constants</param>
    /// <param name="constantOffset">Offset into the constants</param>
    /// <param name="state">Parser state</param>
    /// <param name="stack">VM stack</param>
    /// <param name="pc">Program counter</param>
    /// <param name="taken">Tokens read, so far</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void ProcessOneOf(
        Bytes instructions, 
        Stack constants, 
        int constantOffset, 
        ref State<T> state, 
        ref Stack stack, 
        ref int pc)
    {
        // Get the tokens
        if (constants.At<ReadOnlySpan<T>>(instructions.GetConstantId(ref pc, constantOffset), out var tokens))
        {
            var start  = state.Position.Offset;
            var data   = state.Input.Slice(start, 1);
            if (data.Length < 1)
            {
                stack = stack.PushTerminator(state, out var pos1)
                             .PushEndOfInput(false)
                             .PushErr(pos1);
                return;
            }

            var token = data[0];
            foreach (var t in tokens)
            {
                if (t == token)
                {
                    // Success
                    stack = stack.PopError()
                                 .Push(token)
                                 .PushOK();
                    state = state.NextToken;
                    return;
                }
            }

            // Unexpected token
            stack = stack.PushTerminator(state, out var pos2)
                         .PushToken(token, false)  // unexpected token 
                         .PushTokens(tokens, true) // expected tokens
                         .PushErr(pos2);
        }
        else
        {
            throw new Exception("OneOf: span not found");
        }
    }    
}