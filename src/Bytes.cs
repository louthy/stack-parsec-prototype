namespace StackParsecPrototype;

/// <summary>
/// Bytes collection.  It is backed by a Span&lt;byte&gt; which you can provide yourself (so, you can stack allocate it).
/// If you don't provide one, it will be allocated on the heap. The collection works like a lock-free immutable-type,
/// even if behind the scenes there's mutational logic.
///
/// This type allows you to have a stack-allocated structure for the most common situations in your application (which
/// you can tweak by providing your own stack-allocated `Span`); but for exceptional usage it doesn't fail, it simply
/// falls back to the heap.  It makes this type exceptionally robust, but also exceptionally fast for the most common
/// use-cases. 
/// </summary>
/// <remarks>
/// If you 'branch' the collection by Adding or Consing from the same Bytes struct, then a new clone will be created
/// for the second operation. This preserves the immutability of the collection.
/// </remarks>
/// <remarks>
/// If your Adding or Consing uses all the available space in the backing `Span`, then a new backing array will be
/// allocated on the heap that is double the size. This continues as the collection grows.  That gives List&lt;T&gt;
/// like performance and behaviour once the type falls back to a heap allocated backing span.
/// </remarks>
/// <remarks>
/// Each new operation that returns a new `Bytes` ref-struct will also allocate a `ConsAdd` type on the heap.  I realise
/// this goes against all of what's written above, but this type allows for the lock-free mutation of the backing span,
/// in the most common use-case, and the cloning at other times.  It is 4 bytes in total, so shouldn't be a GC threat.
/// </remarks>
public readonly ref struct Bytes
{
    const int InitialSize = 32;
    
    readonly Span<byte> values;
    readonly int start;
    readonly int count;
    readonly ConsAdd state;

    internal Bytes(Span<byte> values, int start, int count)
    {
        if(values.Length == 0) throw new ArgumentException("Cannot create a Bytes without a byte buffer with size > 0");
        this.values = values;
        this.start = start;
        this.count = count;
        this.state = new ConsAdd(0, 0);
    }

    internal Bytes(Span<byte> values)
    {
        if(values.Length == 0) throw new ArgumentException("Cannot create a Bytes without a byte buffer with size > 0");
        this.values = values;
        this.start = values.Length >> 1;
        this.count = 0;
        this.state = new ConsAdd(0, 0);
    }
    
    public bool Initialised =>
        !values.IsEmpty;
    
    public int Count => 
        count;

    public bool IsEmpty =>
        count == 0;
    
    public byte this[int offset] =>
        offset < count
            ? values[start + offset]
            : throw new IndexOutOfRangeException();

    public static Bytes Initialise(int size = InitialSize)
    {
        size--;
        size |= size >> 1;
        size |= size >> 2;
        size |= size >> 4;
        size |= size >> 8;
        size |= size >> 16;
        size++;
        return new Bytes(new byte[size]);
    }

    public Bytes ExpandUp(int spaceNeeded)
    {
        if (!Initialised) return Initialise(spaceNeeded);
        if (values.Length - start - count >= spaceNeeded) return this;
        
        var ncount = values.Length << 1;
        while (ncount - count < spaceNeeded) ncount <<= 1;
        
        Span<byte> nvalues = new byte[ncount];
        var        ov      = values.Slice(start, count);
        var        nv      = nvalues.Slice(start, count);
        ov.CopyTo(nv);
        
        return new Bytes(nvalues, start, count);
    }    

    public Bytes ExpandDown(int spaceNeeded)
    {
        if (!Initialised) return Initialise(spaceNeeded);
        if (start >= spaceNeeded) return this;
        
        var ncount = values.Length << 1;
        while (ncount - count < spaceNeeded) ncount <<= 1;
        
        Span<byte> nvalues = new byte[ncount];
        var        nstart  = ncount >> 1;
        var        ov      = values.Slice(start, count);
        var        nv      = nvalues.Slice(nstart , count);
        ov.CopyTo(nv);
        
        return new Bytes(nvalues, nstart, count);
    }    
    
    public Bytes Clone()
    {
        if(!Initialised) return Initialise();
        Span<byte> nvalues = new byte[values.Length];
        values.Slice(start, count).CopyTo(nvalues.Slice(start, count));
        return new Bytes(nvalues, start, count);
    }
    
    public Bytes Slice(int start) =>
        Initialised
            ? new (values, this.start + Math.Min(start, count), count - Math.Min(start, count))
            : Initialise().Slice(start);
    
    public Bytes Slice(int start, int count) =>
        Initialised
            ? new (values, this.start + Math.Min(start, this.count), Math.Min(this.count - start, count))
            : Initialise().Slice(start, count);
    
    public ReadOnlySpan<byte> Span() =>
        Initialised
            ? values.Slice(start, count)
            : Initialise().Span();
    
    public ReadOnlySpan<byte> Span(int start) =>
        Slice(start).Span();
    
    public ReadOnlySpan<byte> Span(int start, int count) =>
        Slice(start, count).Span();

    public Bytes Add(OpCode opCode) =>
        Add((byte)opCode);

    public Bytes Cons(OpCode opCode) =>
        Cons((byte)opCode);

    public Bytes AddUInt32(uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BitConverter.TryWriteBytes(buffer, value);
        return Add(buffer);
    }

    public Bytes ConsUInt32(uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BitConverter.TryWriteBytes(buffer, value);
        return Cons(buffer);
    }

    public Bytes AddInt32(int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BitConverter.TryWriteBytes(buffer, value);
        return Add(buffer);
    }

    public Bytes ConsInt32(int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BitConverter.TryWriteBytes(buffer, value);
        return Cons(buffer);
    }

    public Bytes AddUInt16(ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BitConverter.TryWriteBytes(buffer, value);
        return Add(buffer);
    }

    public Bytes ConsUInt16(ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BitConverter.TryWriteBytes(buffer, value);
        return Cons(buffer);
    }

    public Bytes AddInt16(short value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BitConverter.TryWriteBytes(buffer, value);
        return Add(buffer);
    }

    public Bytes ConsInt16(short value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BitConverter.TryWriteBytes(buffer, value);
        return Cons(buffer);
    }
    
    public Bytes Add(params ReadOnlySpan<byte> valuesToAdd)
    {
        if(!Initialised) return Initialise(Math.Max(valuesToAdd.Length, InitialSize)).Add(valuesToAdd);

        var size  = valuesToAdd.Length;
        var bytes = ExpandUp(size);

        if (Interlocked.CompareExchange(ref bytes.state.CanAdd, 1, 0) == 0)
        {
            // Adding to a sequence that's never been added to before.
            // This is the fastest path because we can grow into the existing buffer
            var d = bytes.values.Slice(bytes.start + bytes.count, size);
            valuesToAdd.CopyTo(d);
            return new Bytes(bytes.values, bytes.start, bytes.count + size);
        }
        else
        {
            // Adding to a sequence that's been added to before.
            // That means we can't use the existing buffer as it's been written to previously.
            // So we must clone.
            return Clone().Add(valuesToAdd);
        }
    }

    public Bytes Cons(params ReadOnlySpan<byte> valuesToCons)
    {
        if(!Initialised) return Initialise(Math.Max(valuesToCons.Length, InitialSize)).Add(valuesToCons);

        var size  = valuesToCons.Length;
        var bytes = ExpandDown(size);

        if (Interlocked.CompareExchange(ref bytes.state.CanCons, 1, 0) == 0)
        {
            // Adding to a sequence that's never been added to before.
            // This is the fastest path because we can grow into the existing buffer
            valuesToCons.CopyTo(bytes.values.Slice(bytes.start - size, size));
            return new Bytes(bytes.values, bytes.start - size, bytes.count + size);
        }
        else
        {
            // Adding to a sequence that's been added to before.
            // That means we can't use the existing buffer as it's been written to previously.
            // So we must clone.
            return Clone().Cons(valuesToCons);
        }       
    }
    
    public static Bytes Empty => 
        new(new byte[InitialSize], InitialSize >> 1, 0);

    public static Bytes singleton(OpCode value) =>
        singleton((byte)value);

    public static Bytes singleton(byte value)
    {
        Span<byte> mem = new byte[InitialSize];
        mem[InitialSize >> 1] = value;
        return new Bytes(mem, InitialSize >> 1, 1);
    }

    public static Bytes create(Span<byte> values) =>
        new(values, 0, values.Length);

    public static Bytes create(Span<byte> values, int start, int count) =>
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
