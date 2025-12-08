using System.Runtime.CompilerServices;

namespace StackParsecPrototype;

public static class ArrayExtensions
{
    extension<A>(A[] array)
    {
        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => array.Length == 0;
        }
    }
}