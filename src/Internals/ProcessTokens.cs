using System.Numerics;
using System.Runtime.CompilerServices;

namespace LanguageExt.RefParsec;

static partial class ParsecInternals<E, T, A>
    where T : IEqualityOperators<T, T, bool>
    where A : allows ref struct
{
    /// <summary>
    /// Process a Tokens instruction.
    /// </summary>
    /// <param name="instructions">Byte code instructions</param>
    /// <param name="constants">Byte code constants</param>
    /// <param name="constantOffset">Offset into the constants</param>
    /// <param name="state">Parser state</param>
    /// <param name="stack">VM stack</param>
    /// <param name="pc">Program counter</param>
    /// <param name="taken">Tokens read, so far</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void ProcessTokens(
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
            var offset = state.Position.Offset;
            if (offset + tokens.Length > state.Input.Length)
            {
                stack = stack.PushTerminator(state, out var pos)
                             .PushEndOfInput(false)
                             .PushErr(pos);
            }
            else
            {
                var read   = state.Input.Slice(state.Position.Offset, tokens.Length);
                for (var i = 0; i < tokens.Length; i++)
                {
                    if (read[i] == tokens[i])
                    {
                        state = state.NextToken;
                    }
                    else
                    {
                        // Unexpected token
                        stack = stack.PushTerminator(state, out var pos)
                                     .PushToken(read[i], false)         // unexpected token 
                                     .PushToken(tokens[i], true)        // expected token
                                     .PushErr(pos);
                        return;
                    }
                }
                
                stack = stack.PopError()
                             .Push(read)
                             .PushOK();
            }
        }
        else
        {
            throw new Exception("Tokens: span not found");
        }
    }
}