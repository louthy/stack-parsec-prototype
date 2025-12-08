using System.Numerics;

namespace StackParsecPrototype;

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
    static void ProcessTokens(
        Bytes instructions, 
        Stack constants, 
        int constantOffset, 
        ref State<T, E> state, 
        ref Stack stack, 
        ref int pc, 
        ref int taken)
    {
        // Get the tokens
        if (constants.At<ReadOnlySpan<T>>(instructions[pc++] + constantOffset, out var tokens))
        {
            var offset = state.Position.Offset;
            if (offset + tokens.Length > state.Input.Length)
            {
                stack = stack.Push(ParseErrorRef<T, E>.UnexpectedEndOfInput(state.Position))
                             .Push(StackReply.EmptyError);
            }
            else
            {
                var read = state.Input.Slice(state.Position.Offset, tokens.Length);
                for (var i = 0; i < tokens.Length; i++)
                {
                    if (read[i] == tokens[i])
                    {
                        state = state.NextToken;
                        taken++;
                    }
                    else
                    {
                        stack = stack.Push(ParseErrorRef<T, E>.Tokens(state.Position, read, tokens))
                                     .Push(StackReply.ConsumedError);
                        return;
                    }
                }
                
                stack = stack.Push(read)
                             .Push(StackReply.OK);
            }
        }
        else
        {
            throw new Exception("Tokens: span not found");
        }
    }
}