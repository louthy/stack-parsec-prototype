namespace StackParsecPrototype;

public readonly ref struct ObjSeq
{
    const int InitialSize = 32;
    
    readonly Span<object?> values;
    readonly int start;
    readonly int count;
    readonly ConsAdd state;

    internal ObjSeq(Span<object?> values, int start, int count)
    {
        this.values = values;
        this.start = start;
        this.count = count;
        this.state = new ConsAdd(0, 0);
    }

    public object? this[int offset] =>
        offset >= 0 && offset < count
            ? values[start + offset]
            : throw new IndexOutOfRangeException();

    public int Count => 
        count;

    public bool IsEmpty =>
        count == 0;

    public object? Peek() =>
        count > 0 
            ? values[start + count - 1]
            : throw new InvalidOperationException("Cannot peek from empty sequence");

    public ObjSeq Pop() =>
        new (values, start, count - 1);
    
    public ObjSeq Add(object? value)
    {
        if (start + count >= values.Length)
        {
            // Doubling event
            var nsize = values.Length * 2;
            Span<object?> nvalues = new object?[nsize];
            values.Slice(start, count).CopyTo(nvalues.Slice(start, count));            
            nvalues[start + count] = value;
            return new ObjSeq(nvalues, start, count + 1);
        }
        else
        {
            if (Interlocked.CompareExchange(ref state.CanAdd, 1, 0) == 0)
            {
                // Adding to a sequence that's never been added to before.
                // This is the fastest path because we can grow into the existing buffer
                values[start + count] = value;
                return new ObjSeq(values, start, count + 1);
            }
            else
            {
                // Adding to a sequence that's been added to before.
                // That means we can't use the existing buffer as it's been written to previously.
                // So we must clone.
                Span<object?> nvalues = new object?[values.Length];
                values.Slice(start, count).CopyTo(nvalues.Slice(start, count));
                nvalues[start + count] = value;
                return new ObjSeq(nvalues, 0, count + 1);
            }
        }
    }

    public ObjSeq Cons(object? value)
    {
        if (start <= 0)
        {
            // Doubling event
            var osize = values.Length;
            var nsize = osize * 2;
            var nstart = osize;
            Span<object?> nvalues = new object?[nsize];
            values.Slice(start, count).CopyTo(nvalues.Slice(nstart, count));
            nstart--;
            nvalues[nstart] = value;
            return new ObjSeq(nvalues, nstart, count + 1);
        }
        else
        {
            if (Interlocked.CompareExchange(ref state.CanCons, 1, 0) == 0)
            {
                // Adding to a sequence that's never been added to before.
                // This is the fastest path because we can grow into the existing buffer
                values[start - 1] = value;
                return new ObjSeq(values, start - 1, count + 1);
            }
            else
            {
                // Adding to a sequence that's been added to before.
                // That means we can't use the existing buffer as it's been written to previously.
                // So we must clone.
                Span<object?> nvalues = new object?[values.Length];
                values.Slice(start, count).CopyTo(nvalues.Slice(start, count));
                nvalues[start + count] = value;
                return new ObjSeq(nvalues, 0, count + 1);
            }
        }
    }
    
    public static ObjSeq Empty => 
        new(new Object?[InitialSize], InitialSize >> 1, 0);
    
    public static ObjSeq EmptyNoCons => 
        new(new Object?[InitialSize], 0, 0);

    public static ObjSeq singleton(object? value)
    {
        Span<object?> mem = new object?[InitialSize];
        mem[InitialSize >> 1] = value;
        return new ObjSeq(mem, InitialSize >> 1, 1);
    }

    public static ObjSeq create(Span<object?> values) =>
        new(values, 0, values.Length);

    public static ObjSeq create(Span<object?> values, int start, int count) =>
        start + count <= values.Length 
            ? new(values, start, count)
            : throw new ArgumentException("Invalid range");
    
    public ref struct Enumerator
    {
        readonly Span<object?> values;
        readonly int start;
        readonly int end;
        int current;

        internal Enumerator(Span<object?> values, int start, int count)
        {
            this.values = values;
            this.start = start;
            end = start + count;
            current = start - 1;
        }

        public object? Current => 
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
}
