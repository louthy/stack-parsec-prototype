using System.Numerics;

namespace StackParsecPrototype;

public readonly ref struct RefSeq<T> : Stream<RefSeq<T>, T>
    where T : IEqualityOperators<T, T, bool>
{
    const int InitialSize = 32;
    
    readonly Span<T> values;
    readonly int start;
    readonly int count;
    readonly ConsAdd state;
    
    internal RefSeq(Span<T> values, int start, int count)
    {
        this.values = values;
        this.start = start;
        this.count = count;
        this.state = new ConsAdd(0, 0);
    }

    public T this[int offset] =>
        offset < count
            ? values[start + offset]
            : throw new IndexOutOfRangeException();
    
    public int Count => 
        count;

    public T[] ToArray() =>
        values.Slice(start, count).ToArray();

    public ReadOnlySpan<T> ToSpan() =>
        values.Slice(start, count);
    
    public RefSeq<T> Add(T value)
    {
        if (start + count >= values.Length)
        {
            // Doubling event
            var nsize = values.Length * 2;
            Span<T> nvalues = new T[nsize];
            values.Slice(start, count).CopyTo(nvalues.Slice(start, count));            
            nvalues[start + count] = value;
            return new RefSeq<T>(nvalues, start, count + 1);
        }
        else
        {
            if (Interlocked.CompareExchange(ref state.CanAdd, 1, 0) == 0)
            {
                // Adding to a sequence that's never been added to before.
                // This is the fastest path because we can grow into the existing buffer
                values[start + count] = value;
                return new RefSeq<T>(values, start, count + 1);
            }
            else
            {
                // Adding to a sequence that's been added to before.
                // That means we can't use the existing buffer as it's been written to previously.
                // So we must clone.
                Span<T> nvalues = new T[values.Length];
                values.Slice(start, count).CopyTo(nvalues.Slice(start, count));
                nvalues[start + count] = value;
                return new RefSeq<T>(nvalues, 0, count + 1);
            }
        }
    }

    public RefSeq<T> Cons(T value)
    {
        if (start <= 0)
        {
            // Doubling event
            var osize = values.Length;
            var nsize = osize * 2;
            var nstart = osize;
            Span<T> nvalues = new T[nsize];
            values.Slice(start, count).CopyTo(nvalues.Slice(nstart, count));
            nstart--;
            nvalues[nstart] = value;
            return new RefSeq<T>(nvalues, nstart, count + 1);
        }
        else
        {
            if (Interlocked.CompareExchange(ref state.CanCons, 1, 0) == 0)
            {
                // Adding to a sequence that's never been added to before.
                // This is the fastest path because we can grow into the existing buffer
                values[start - 1] = value;
                return new RefSeq<T>(values, start - 1, count + 1);
            }
            else
            {
                // Adding to a sequence that's been added to before.
                // That means we can't use the existing buffer as it's been written to previously.
                // So we must clone.
                Span<T> nvalues = new T[values.Length];
                values.Slice(start, count).CopyTo(nvalues.Slice(start, count));
                nvalues[start + count] = value;
                return new RefSeq<T>(nvalues, 0, count + 1);
            }
        }
    }
    
    public static RefSeq<T> Empty => 
        new(new T[InitialSize], InitialSize >> 1, 0);

    public static RefSeq<T> singleton(T value)
    {
        Span<T> mem = new T[InitialSize];
        mem[InitialSize >> 1] = value;
        return new RefSeq<T>(mem, InitialSize >> 1, 1);
    }

    public static RefSeq<T> create(Span<T> values) =>
        new(values, 0, values.Length);

    public static RefSeq<T> create(Span<T> values, int start, int count) =>
        start + count <= values.Length 
            ? new(values, start, count)
            : throw new ArgumentException("Invalid range");
    
    public ref struct Enumerator
    {
        readonly Span<T> values;
        readonly int start;
        readonly int end;
        int current;

        internal Enumerator(Span<T> values, int start, int count)
        {
            this.values = values;
            this.start = start;
            end = start + count;
            current = start - 1;
        }

        public T Current => 
            values[current];

        public bool MoveNext()
        {
            current++;
            return current < end;
        }

        public void Reset() =>
            current = start - 1;
    }

    public Enumerator GetEnumerator() => 
        new (values, start, count);

    public ReadOnlySpan<T> Tokens => 
        values.Slice(start, count);
}
