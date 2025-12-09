using System.Numerics;
using System.Runtime.CompilerServices;

namespace StackParsecPrototype;

static partial class ParsecInternals<E, T, A>
    where T : IEqualityOperators<T, T, bool>
    where A : allows ref struct
{
    /// <summary>
    /// Process an Invoke instruction.   
    /// </summary>
    /// <param name="instructions">Byte code instructions</param>
    /// <param name="constants">Byte code constants</param>
    /// <param name="constantOffset">Offset into the constants</param>
    /// <param name="stack">VM stack</param>
    /// <param name="pc">Program counter</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void ProcessInvoke(
        Bytes instructions, 
        Stack constants, 
        int constantOffset, 
        ref Stack stack, 
        ref int pc)
    {
        // Get the wrapper function
        if (constants.At<Func<Stack, Stack, Stack>>(instructions.GetConstantId(ref pc, constantOffset), out var go))
        {
            // Read the function to invoke
            stack = stack.ReadFromAndPush(constants, instructions.GetConstantId(ref pc, constantOffset));
            
            // Invoke the wrapper function that calls the real function
            stack = go(stack, constants);
        }
        else
        {
            throw new Exception("Invoke: delegate not found");
        }
    }
}