namespace StackParsecPrototype;

public interface Stream<S, T>
    where S : Stream<S, T>, allows ref struct
{
    public abstract ReadOnlySpan<T> Tokens { get; }
    public abstract int Count { get; }
}