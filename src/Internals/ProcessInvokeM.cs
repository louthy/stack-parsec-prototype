using System.Numerics;
using System.Runtime.CompilerServices;

namespace StackParsecPrototype;

static partial class ParsecInternals<E, T, A>
    where T : IEqualityOperators<T, T, bool>
    where A : allows ref struct
{
    /// <summary>
    /// Process an InvokeM instruction. 
    /// </summary>
    /// <param name="instructions">Byte code instructions</param>
    /// <param name="constants">Byte code constants</param>
    /// <param name="constantOffset">Offset into the constants</param>
    /// <param name="state">Parser state</param>
    /// <param name="stack">VM stack</param>
    /// <param name="pc">Program counter</param>
    /// <param name="taken">Tokens read, so far</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void ProcessInvokeM(
        Bytes instructions, 
        Stack constants, 
        int constantOffset,
        ref State<E, T> state, 
        ref Stack stack, 
        ref int pc,
        ref int taken)
    {
        // Get the delegate to invoke from the constants
        if (constants.At<Func<Stack, Stack, Stack>>(instructions.GetConstantId(ref pc, constantOffset), out var go))
        {
            // Read the function to invoke
            stack = stack.ReadFromAndPush(constants, instructions.GetConstantId(ref pc, constantOffset));
            
            // Invoke the delegate
            stack = go(stack, constants);
        }
        else
        {
            // If there isn't a Func in the constants, then we've got a bug
            throw new Exception("InvokeM: delegate not found");
        }
    }    
}