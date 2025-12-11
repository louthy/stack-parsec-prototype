using System.Runtime.CompilerServices;

namespace LanguageExt.RefParsec;

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