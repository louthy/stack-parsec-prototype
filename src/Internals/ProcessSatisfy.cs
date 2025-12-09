using System.Numerics;
using System.Runtime.CompilerServices;

namespace StackParsecPrototype;

static partial class ParsecInternals<E, T, A>
    where T : IEqualityOperators<T, T, bool>
    where A : allows ref struct
{
    /// <summary>
    /// Process a Satisfy instruction.  
    /// </summary>
    /// <param name="instructions">Byte code instructions</param>
    /// <param name="constants">Byte code constants</param>
    /// <param name="constantOffset">Offset into the constants</param>
    /// <param name="state">Parser state</param>
    /// <param name="stack">VM stack</param>
    /// <param name="pc">Program counter</param>
    /// <param name="taken">Tokens read, so far</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void ProcessSatisfy(Bytes instructions, Stack constants, int constantOffset, ref State<E, T> state, ref Stack stack, ref int pc, ref int taken)
    {
        var start = state.Position.Offset;
        var data  = state.Input.Slice(start, 1);
        if (data.Length < 1)
        {
            stack = ParseErrorStack.EndOfInput(false, false, state.Position, stack);
            return;
        }
        
        if (constants.At<Func<T, bool>>(instructions[pc++] + constantOffset, out var predicate))
        {
            var token = data[0];
            if (predicate(token))
            {
                // Success
                stack = stack.Push(token).PushOK();

                state = state.NextToken;
                taken ++;
            }
            else
            {
                // Unexpected token
                stack = ParseErrorStack.Token(token, false, false, state.Position, stack);
            }
        }
        else
        {
            throw new Exception("Satisfy: predicate not found");
        }
    }    
}