namespace StackParsecPrototype;

public static class ArrayExtensions
{
    extension<A>(A[] array)
    {
        public bool IsEmpty => 
            array.Length == 0;
    }
}