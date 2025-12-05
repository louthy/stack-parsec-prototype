namespace StackParsecPrototype;

public readonly ref struct CharSeq : Stream<CharSeq, char>
{
    const int InitialSize = 32;
    
    readonly Span<char> values;
    readonly int start;
    readonly int count;
    readonly ConsAdd state;
    
    internal CharSeq(Span<char> values, int start, int count)
    {
        switch (values.Length)
        {
            case 0:
                this.values = new char[InitialSize];
                this.start = InitialSize >> 1;
                this.count = 0;
                break;
            
            case < InitialSize >> 2:
                this.values = new Span<char>(new char[InitialSize]);
                this.start = InitialSize >> 1;
                this.count = count;
                values.CopyTo(this.values.Slice(this.start));
                break;
            
            case < InitialSize >> 1:
                this.values = new Span<char>(new char[InitialSize]);
                this.start = InitialSize >> 2;
                this.count = count;
                values.CopyTo(this.values.Slice(this.start));
                break;
            
            default:
                this.values = values;
                this.start = start;
                this.count = count;
                break;
        }

        state = new ConsAdd(0, 0);
    }

    public char this[int offset] =>
        offset < count
            ? values[start + offset]
            : throw new IndexOutOfRangeException();
    
    public int Count => 
        count;
    
    public CharSeq Add(char value)
    {
        if (start + count >= values.Length)
        {
            // Doubling event
            var        nsize   = values.Length * 2;
            Span<char> nvalues = new char[nsize];
            values.Slice(start, count).CopyTo(nvalues.Slice(start, count));            
            nvalues[start + count] = value;
            return new CharSeq(nvalues, start, count + 1);
        }
        else
        {
            if (Interlocked.CompareExchange(ref state.CanAdd, 1, 0) == 0)
            {
                // Adding to a sequence that's never been added to before.
                // This is the fastest path because we can grow into the existing buffer
                values[start + count] = value;
                return new CharSeq(values, start, count + 1);
            }
            else
            {
                // Adding to a sequence that's been added to before.
                // That means we can't use the existing buffer as it's been written to previously.
                // So we must clone.
                Span<char> nvalues = new char[values.Length];
                values.Slice(start, count).CopyTo(nvalues.Slice(start, count));
                nvalues[start + count] = value;
                return new CharSeq(nvalues, 0, count + 1);
            }
        }
    }

    public CharSeq Cons(char value)
    {
        if (start <= 0)
        {
            // Doubling event
            var osize = values.Length;
            var nsize = osize * 2;
            var nstart = osize;
            Span<char> nvalues = new char[nsize];
            values.Slice(start, count).CopyTo(nvalues.Slice(nstart, count));
            nstart--;
            nvalues[nstart] = value;
            return new CharSeq(nvalues, nstart, count + 1);
        }
        else
        {
            if (Interlocked.CompareExchange(ref state.CanCons, 1, 0) == 0)
            {
                // Adding to a sequence that's never been added to before.
                // This is the fastest path because we can grow into the existing buffer
                values[start - 1] = value;
                return new CharSeq(values, start - 1, count + 1);
            }
            else
            {
                // Adding to a sequence that's been added to before.
                // That means we can't use the existing buffer as it's been written to previously.
                // So we must clone.
                Span<char> nvalues = new char[values.Length];
                values.Slice(start, count).CopyTo(nvalues.Slice(start, count));
                nvalues[start + count] = value;
                return new CharSeq(nvalues, 0, count + 1);
            }
        }
    }
    
    public static CharSeq Empty => 
        new(new char[InitialSize], InitialSize >> 1, 0);

    public static CharSeq singleton(char value)
    {
        Span<char> mem = new char[InitialSize];
        mem[InitialSize >> 1] = value;
        return new CharSeq(mem, InitialSize >> 1, 1);
    }

    public static CharSeq create(string value) =>
        new(value.ToCharArray().AsSpan(), 0, value.Length);

    public static CharSeq create(ReadOnlySpan<char> value) =>
        new(value.ToArray().AsSpan(), 0, value.Length);

    public static CharSeq create(Span<char> values) =>
        new(values, 0, values.Length);

    public static CharSeq create(Span<char> values, int start, int count) =>
        start + count <= values.Length 
            ? new(values, start, count)
            : throw new ArgumentException("Invalid range");
    
    public ref struct Enumerator
    {
        readonly Span<char> values;
        readonly int start;
        readonly int end;
        int current;

        internal Enumerator(Span<char> values, int start, int count)
        {
            this.values = values;
            this.start = start;
            end = start + count;
            current = start - 1;
        }

        public char Current => 
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

    public ReadOnlySpan<char> Tokens => 
        values.Slice(start, count);

    public char Zero { get; } = (char)0;
    public char One { get; } = (char)1;
}
