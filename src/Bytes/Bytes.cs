using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LanguageExt.RefParsec;

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
    public const int ConstantIdSize = 2;
    const int InitialSize = 32;
    
    readonly Span<byte> values;
    readonly int start;
    readonly int count;
    readonly ConsAdd state;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Bytes(Span<byte> values, int start, int count)
    {
        if(values.Length == 0) throw new ArgumentException("Cannot create a Bytes without a byte buffer with size > 0");
        this.values = values;
        this.start = start;
        this.count = count;
        this.state = new ConsAdd(0, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Bytes(Span<byte> values)
    {
        if(values.Length == 0) throw new ArgumentException("Cannot create a Bytes without a byte buffer with size > 0");
        this.values = values;
        this.start = values.Length >> 1;
        this.count = 0;
        this.state = new ConsAdd(0, 0);
    }
    
    public bool Initialised
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => !values.IsEmpty;
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => count;
    }

    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => count == 0;
    }


    public byte this[int offset]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => offset < count
                   ? values[start + offset]
                   : throw new IndexOutOfRangeException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes Clone()
    {
        if(!Initialised) return Initialise();
        Span<byte> nvalues = new byte[values.Length];
        values.Slice(start, count).CopyTo(nvalues.Slice(start, count));
        return new Bytes(nvalues, start, count);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes Slice(int start) =>
        Initialised
            ? new (values, this.start + Math.Min(start, count), count - Math.Min(start, count))
            : Initialise().Slice(start);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes Slice(int start, int count) =>
        Initialised
            ? new (values, this.start + Math.Min(start, this.count), Math.Min(this.count - start, count))
            : Initialise().Slice(start, count);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> Span() =>
        Initialised
            ? values.Slice(start, count)
            : Initialise().Span();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> Span(int start) =>
        Slice(start).Span();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> Span(int start, int count) =>
        Slice(start, count).Span();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes Add(OpCode opCode) =>
        Add((byte)opCode);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes Prepend(OpCode opCode) =>
        Prepend((byte)opCode);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes AddConstantId(int value)
    {
        if(value < 0) throw new IndexOutOfRangeException("ConstantId must be >= 0");
        if(value > ushort.MaxValue) throw new IndexOutOfRangeException("ConstantId must be less than 65536");
        var v = (ushort)value;
        Span<byte> buffer = stackalloc byte[ConstantIdSize];
        BitConverter.TryWriteBytes(buffer, v);
        return Add(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes PrependConstantId(int value)
    {
        if(value < 0) throw new IndexOutOfRangeException("ConstantId must be >= 0");
        if(value > ushort.MaxValue) throw new IndexOutOfRangeException("ConstantId must be less than 65536");
        var        v      = (ushort)value;
        Span<byte> buffer = stackalloc byte[ConstantIdSize];
        BitConverter.TryWriteBytes(buffer, v);
        return Prepend(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetConstantId(ref int pc, int offset)
    {
        ReadOnlySpan<byte> instrs = Span(pc, ConstantIdSize);
        pc += 2;
        return BitConverter.ToUInt16(instrs) + offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes AddString(string text)
    {
        var charSpan = text.AsSpan();
        var byteSpan = MemoryMarshal.AsBytes(charSpan);
        return AddInt32(text.Length).Add(byteSpan);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes PrependString(string text)
    {
        var charSpan = text.AsSpan();
        var byteSpan = MemoryMarshal.AsBytes(charSpan);
        return Prepend(byteSpan).PrependInt32(text.Length);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes AddInt16(short value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BitConverter.TryWriteBytes(buffer, value);
        return Add(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes PrependInt16(short value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BitConverter.TryWriteBytes(buffer, value);
        return Prepend(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes AddUInt16(ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BitConverter.TryWriteBytes(buffer, value);
        return Add(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes PrependUInt16(ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BitConverter.TryWriteBytes(buffer, value);
        return Prepend(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes AddInt32(int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BitConverter.TryWriteBytes(buffer, value);
        return Add(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes PrependInt32(int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BitConverter.TryWriteBytes(buffer, value);
        return Prepend(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes AddUInt32(uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BitConverter.TryWriteBytes(buffer, value);
        return Add(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes PrependUInt32(uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BitConverter.TryWriteBytes(buffer, value);
        return Prepend(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes AddFloat(float value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BitConverter.TryWriteBytes(buffer, value);
        return Add(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes PrependFloat(float value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BitConverter.TryWriteBytes(buffer, value);
        return Prepend(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes AddInt64(long value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BitConverter.TryWriteBytes(buffer, value);
        return Add(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes PrependInt64(long value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BitConverter.TryWriteBytes(buffer, value);
        return Prepend(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes AddUInt64(ulong value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BitConverter.TryWriteBytes(buffer, value);
        return Add(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes PrependUInt64(ulong value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BitConverter.TryWriteBytes(buffer, value);
        return Prepend(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes AddDouble(double value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BitConverter.TryWriteBytes(buffer, value);
        return Add(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes PrependDouble(double value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BitConverter.TryWriteBytes(buffer, value);
        return Prepend(buffer);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes Add(params ReadOnlySpan<byte> values)
    {
        if(!Initialised) return Initialise(Math.Max(values.Length, InitialSize)).Add(values);

        var size  = values.Length;
        var bytes = ExpandUp(size);

        if (Interlocked.CompareExchange(ref bytes.state.CanAdd, 1, 0) == 0)
        {
            // Adding to a sequence that's never been added to before.
            // This is the fastest path because we can grow into the existing buffer
            var d = bytes.values.Slice(bytes.start + bytes.count, size);
            values.CopyTo(d);
            return new Bytes(bytes.values, bytes.start, bytes.count + size);
        }
        else
        {
            // Adding to a sequence that's been added to before.
            // That means we can't use the existing buffer as it's been written to previously.
            // So we must clone.
            return Clone().Add(values);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes Prepend(params ReadOnlySpan<byte> values)
    {
        if(!Initialised) return Initialise(Math.Max(values.Length, InitialSize)).Add(values);

        var size  = values.Length;
        var bytes = ExpandDown(size);

        if (Interlocked.CompareExchange(ref bytes.state.CanCons, 1, 0) == 0)
        {
            // Adding to a sequence that's never been added to before.
            // This is the fastest path because we can grow into the existing buffer
            values.CopyTo(bytes.values.Slice(bytes.start - size, size));
            return new Bytes(bytes.values, bytes.start - size, bytes.count + size);
        }
        else
        {
            // Adding to a sequence that's been added to before.
            // That means we can't use the existing buffer as it's been written to previously.
            // So we must clone.
            return Clone().Prepend(values);
        }       
    }

    public ReadOnlySpan<char> ReadString(ref int offset)
    {
        var span     = Span().Slice(offset);
        var charSize = BitConverter.ToInt32(span[..4]);
        var byteSize = charSize * sizeof(char);
        var bytes    = span.Slice(4, byteSize);
        var chars    = MemoryMarshal.Cast<byte, char>(bytes);
        offset += 4 + byteSize;
        return chars;
    }

    public short ReadInt16(ref int offset)
    {
        var span  = Span().Slice(offset);
        var value = BitConverter.ToInt16(span[..2]);
        offset += 2;
        return value;
    }

    public ushort ReadUInt16(ref int offset)
    {
        var span  = Span().Slice(offset);
        var value = BitConverter.ToUInt16(span[..2]);
        offset += 2;
        return value;
    }

    public int ReadInt32(ref int offset)
    {
        var span  = Span().Slice(offset);
        var value = BitConverter.ToInt32(span[..4]);
        offset += 4;
        return value;
    }

    public uint ReadUInt32(ref int offset)
    {
        var span  = Span().Slice(offset);
        var value = BitConverter.ToUInt32(span[..4]);
        offset += 4;
        return value;
    }

    public long ReadLong(ref int offset)
    {
        var span  = Span().Slice(offset);
        var value = BitConverter.ToInt64(span[..8]);
        offset += 8;
        return value;
    }

    public ulong ReadULong(ref int offset)
    {
        var span  = Span().Slice(offset);
        var value = BitConverter.ToUInt64(span[..8]);
        offset += 8;
        return value;
    }

    public static Bytes Empty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(new byte[InitialSize], InitialSize >> 1, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bytes singleton(OpCode value) =>
        singleton((byte)value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bytes singleton(byte value)
    {
        Span<byte> mem = new byte[InitialSize];
        mem[InitialSize >> 1] = value;
        return new Bytes(mem, InitialSize >> 1, 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bytes create(Span<byte> values) =>
        new(values, 0, values.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(Span<byte> values, int start, int count)
        {
            this.values = values;
            this.start = start;
            end = start + count;
            current = start - 1;
        }

        public byte Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => values[current];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            current++;
            return current < end;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset() =>
            current = start - 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => 
        new (values, start, count);
}
