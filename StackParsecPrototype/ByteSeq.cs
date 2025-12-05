namespace StackParsecPrototype;

public readonly ref struct ByteSeq
{
    const int InitialSize = 32;
    
    readonly Span<byte> values;
    readonly int start;
    readonly int count;
    readonly ConsAdd state;

    internal ByteSeq(Span<byte> values, int start, int count)
    {
        this.values = values;
        this.start = start;
        this.count = count;
        this.state = new ConsAdd(0, 0);
    }
    
    public int Count => 
        count;

    public bool IsEmpty =>
        count == 0;
    
    public byte this[int offset] =>
        offset < count
            ? values[start + offset]
            : throw new IndexOutOfRangeException();
    
    public ByteSeq Slice(int start) =>
        new (values, this.start + Math.Min(start, count), count - Math.Min(start, count));
    
    public ByteSeq Slice(int start, int count) =>
        new (values, this.start + Math.Min(start, count), Math.Min(this.count - start, count));
    
    public ReadOnlySpan<byte> Span() =>
        values.Slice(start, count);
    
    public ReadOnlySpan<byte> Span(int start) =>
        Slice(start).Span();
    
    public ReadOnlySpan<byte> Span(int start, int count) =>
        Slice(start, count).Span();

    public ByteSeq Add(uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BitConverter.TryWriteBytes(buffer, value);
        return Add(buffer[0]).Add(buffer[1]).Add(buffer[2]).Add(buffer[3]);
    }

    public ByteSeq Add(int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BitConverter.TryWriteBytes(buffer, value);
        return Add(buffer[0]).Add(buffer[1]).Add(buffer[2]).Add(buffer[3]);
    }

    public ByteSeq Add(ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BitConverter.TryWriteBytes(buffer, value);
        return Add(buffer[0]).Add(buffer[1]);
    }

    public ByteSeq Add(short value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BitConverter.TryWriteBytes(buffer, value);
        return Add(buffer[0]).Add(buffer[1]);
    }

    public ByteSeq Add(byte value)
    {
        if (start + count >= values.Length)
        {
            // Doubling event
            var nsize = values.Length * 2;
            Span<byte> nvalues = new byte[nsize];
            values.Slice(start, count).CopyTo(nvalues.Slice(start, count));            
            nvalues[start + count] = value;
            return new ByteSeq(nvalues, start, count + 1);
        }
        else
        {
            if (Interlocked.CompareExchange(ref state.CanAdd, 1, 0) == 0)
            {
                // Adding to a sequence that's never been added to before.
                // This is the fastest path because we can grow into the existing buffer
                values[start + count] = value;
                return new ByteSeq(values, start, count + 1);
            }
            else
            {
                // Adding to a sequence that's been added to before.
                // That means we can't use the existing buffer as it's been written to previously.
                // So we must clone.
                Span<byte> nvalues = new byte[values.Length];
                values.Slice(start, count).CopyTo(nvalues.Slice(start, count));
                nvalues[start + count] = value;
                return new ByteSeq(nvalues, 0, count + 1);
            }
        }
    }

    public ByteSeq Cons(byte value)
    {
        if (start <= 0)
        {
            // Doubling event
            var osize = values.Length;
            var nsize = osize * 2;
            var nstart = osize;
            Span<byte> nvalues = new byte[nsize];
            values.Slice(start, count).CopyTo(nvalues.Slice(nstart, count));
            nstart--;
            nvalues[nstart] = value;
            return new ByteSeq(nvalues, nstart, count + 1);
        }
        else
        {
            if (Interlocked.CompareExchange(ref state.CanCons, 1, 0) == 0)
            {
                // Adding to a sequence that's never been added to before.
                // This is the fastest path because we can grow into the existing buffer
                values[start - 1] = value;
                return new ByteSeq(values, start - 1, count + 1);
            }
            else
            {
                // Adding to a sequence that's been added to before.
                // That means we can't use the existing buffer as it's been written to previously.
                // So we must clone.
                Span<byte> nvalues = new byte[values.Length];
                values.Slice(start, count).CopyTo(nvalues.Slice(start, count));
                nvalues[start + count] = value;
                return new ByteSeq(nvalues, 0, count + 1);
            }
        }
    }
    
    public static ByteSeq Empty => 
        new(new byte[InitialSize], InitialSize >> 1, 0);

    public static ByteSeq singleton(OpCode value) =>
        singleton((byte)value);

    public static ByteSeq singleton(byte value)
    {
        Span<byte> mem = new byte[InitialSize];
        mem[InitialSize >> 1] = value;
        return new ByteSeq(mem, InitialSize >> 1, 1);
    }

    public static ByteSeq create(Span<byte> values) =>
        new(values, 0, values.Length);

    public static ByteSeq create(Span<byte> values, int start, int count) =>
        start + count <= values.Length 
            ? new(values, start, count)
            : throw new ArgumentException("Invalid range");
    
    public ref struct Enumerator
    {
        readonly Span<byte> values;
        readonly int start;
        readonly int end;
        int current;

        internal Enumerator(Span<byte> values, int start, int count)
        {
            this.values = values;
            this.start = start;
            end = start + count;
            current = start - 1;
        }

        public byte Current => 
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
