using System.Numerics;
using System.Runtime.CompilerServices;

namespace StackParsecPrototype;

static partial class ParsecInternals<E, T, A>
    where T : IEqualityOperators<T, T, bool>
    where A : allows ref struct
{
    /// <summary>
    /// Process NoneOf instruction.
    /// </summary>
    /// <param name="instructions">Byte code instructions</param>
    /// <param name="constants">Byte code constants</param>
    /// <param name="constantOffset">Offset into the constants</param>
    /// <param name="state">Parser state</param>
    /// <param name="stack">VM stack</param>
    /// <param name="pc">Program counter</param>
    /// <param name="taken">Tokens read, so far</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void ProcessNoneOf(
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
            var start = state.Position.Offset;
            var data  = state.Input.Slice(start, 1);
            if (data.Length < 1)
            {
                stack = stack.Push(ParseErrorRef<T, E>.UnexpectedEndOfInput(state.Position))
                             .Push(StackReply.EmptyError);
                return;
            }

            var token = data[0];
            foreach (var t in tokens)
            {
                if (t == token)
                {
                    // Unexpected token
                    stack = stack.Push(ParseErrorRef<T, E>.Tokens(state.Position, data))
                                 .Push(StackReply.EmptyError);
                    return;
                }
            }

            // Success
            stack = stack.Push(token)
                         .Push(StackReply.OK);

            state = state.NextToken;
            taken++;
        }
        else
        {
            throw new Exception("OneOf: span not found");
        }
    }    
}