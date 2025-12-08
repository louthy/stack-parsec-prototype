using System.Numerics;
using System.Runtime.CompilerServices;

namespace StackParsecPrototype;

static partial class ParsecInternals<E, T, A>
    where T : IEqualityOperators<T, T, bool>
    where A : allows ref struct
{
    /// <summary>
    /// Process a Try instruction.
    /// </summary>
    /// <param name="instructions">Byte code instructions</param>
    /// <param name="constants">Byte code constants</param>
    /// <param name="constantOffset">Offset into the constants</param>
    /// <param name="state">Parser state</param>
    /// <param name="stack">VM stack</param>
    /// <param name="pc">Program counter</param>
    /// <param name="taken">Tokens read, so far</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void ProcessTry(
        Bytes instructions, 
        Stack constants, 
        int constantOffset, 
        ref State<T, E> state, 
        ref Stack stack, 
        ref int pc, 
        ref int taken)
    {
        var savedState = state;
        var consts     = constants;
        var npc        = pc;
        var ntaken     = ParseUntyped(instructions, consts, constantOffset, ref state, ref stack, ref npc);
        if (ntaken == 0)
        {
            // Not consumed, so we don't care if it succeeded or not. Empty Ok and Empty Error are both fine.
            return;
        }
        
        if (stack.Peek<StackReply>(out var reply))
        {
            switch (reply)
            {
                case StackReply.OK:
                    // Success, so we're done
                    taken += ntaken;
                    pc = npc;
                    return;
                
                case StackReply.EmptyError:
                    // Reset the state back to before we tried parsing
                    state = savedState;
                    break;
                
                case StackReply.ConsumedError:
                    
                    // Reset the state back to before we tried parsing
                    state = savedState;
                    
                    // Return the error as-is but with 'empty error' status
                    stack = stack.Pop().Push(StackReply.EmptyError);
                    break;
                    
            }
        }
        else
        {
            throw new Exception("Try: expected StackReply");
        }
    }
}