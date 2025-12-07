using System.Runtime.CompilerServices;

namespace StackParsecPrototype;

/// <summary>
/// A collection of values that's managed as a ref-struct (entirely on the heap).  It is backed by a Span&lt;T&gt;
/// which you can provide yourself (so, you can stack allocate it). If you don't provide one, it will be allocated on
/// the heap. The collection works like an lock-free immutable-type, even if behind the scenes there's mutational logic.
///
/// This type allows you to have a stack-allocated structure for the most common situations in your application (which
/// you can tweak by providing your own stack-allocated `Span`); but for exceptional usage it doesn't fail, it simply
/// falls back to the heap.  It makes this type exceptionally robust, but also exceptionally fast for the most common
/// use-cases. 
/// </summary>
/// <remarks>
/// If you 'branch' the collection by Adding or Consing from the same `RefSeq` struct, then a new clone will be created
/// for the second operation. This preserves the immutability of the collection.
/// </remarks>
/// <remarks>
/// If your Adding or Consing uses all the available space in the backing `Span`, then a new backing array will be
/// allocated on the heap that is double the size. This continues as the collection grows.  That gives List&lt;T&gt;
/// like performance and behaviour once the type falls back to a heap allocated backing span.
/// </remarks>
/// <remarks>
/// Each new operation that returns a new `RefSeq` ref-struct will also allocate a `ConsAdd` type on the heap. I realise
/// this goes against all of what's written above, but this type allows for the lock-free mutation of the backing span,
/// in the most common use-case, and the cloning at other times.  It is 4 bytes in total, so shouldn't be a GC threat.
/// </remarks>
public readonly ref struct RefSeq<T> : Stream<RefSeq<T>, T>
{
    const int InitialSize = 32;
    
    readonly Span<T> values;
    readonly int start;
    readonly int count;
    readonly ConsAdd state;

    internal RefSeq(Span<T> values, int start, int count)
    {
        if(values.Length == 0) throw new ArgumentException("Cannot create a Bytes without a byte buffer with size > 0");
        this.values = values;
        this.start = start;
        this.count = count;
        this.state = new ConsAdd(0, 0);
    }

    internal RefSeq(Span<T> values)
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
    
    public static RefSeq<T> Empty => 
        new(new T[InitialSize], InitialSize >> 1, 0);

    public static RefSeq<T> EmptyNoCons =>
        new(new T[InitialSize >> 1], 0, 0);

    public static RefSeq<T> EmptyNoAdd =>
        new(new T[InitialSize >> 1], InitialSize >> 1, 0);
    
    public T this[int offset] =>
        offset < count
            ? values[start + offset]
            : throw new IndexOutOfRangeException();

    public static RefSeq<T> Initialise(int size = InitialSize)
    {
        size--;
        size |= size >> 1;
        size |= size >> 2;
        size |= size >> 4;
        size |= size >> 8;
        size |= size >> 16;
        size++;
        return new RefSeq<T>(new T[size]);
    }

    public RefSeq<T> ExpandUp(int spaceNeeded)
    {
        if (!Initialised) return Initialise(spaceNeeded);
        if (values.Length - start - count >= spaceNeeded) return this;
        
        var ncount = values.Length << 1;
        while (ncount - count < spaceNeeded) ncount <<= 1;

        Span<T> nvalues = new T[ncount];
        var     ov      = values.Slice(start, count);
        var     nv      = nvalues.Slice(start, count);
        ov.CopyTo(nv);
        
        return new RefSeq<T>(nvalues, start, count);
    }    

    public RefSeq<T> ExpandDown(int spaceNeeded)
    {
        if (!Initialised) return Initialise(spaceNeeded);
        if (start >= spaceNeeded) return this;
        
        var ncount = values.Length << 1;
        while (ncount - count < spaceNeeded) ncount <<= 1;

        Span<T> nvalues = new T[ncount];
        var     nstart  = ncount >> 1;
        var     ov      = values.Slice(start, count);
        var     nv      = nvalues.Slice(nstart , count);
        ov.CopyTo(nv);
        
        return new RefSeq<T>(nvalues, nstart, count);
    }    
    
    public RefSeq<T> Clone()
    {
        if(!Initialised) return Initialise();
        Span<T> nvalues = new T[values.Length];
        values.Slice(start, count).CopyTo(nvalues.Slice(start, count));
        return new RefSeq<T>(nvalues, start, count);
    }
    
    public RefSeq<T> Slice(int start) =>
        Initialised
            ? new (values, this.start + Math.Min(start, count), count - Math.Min(start, count))
            : Initialise().Slice(start);
    
    public RefSeq<T> Slice(int start, int count) =>
        Initialised
            ? new (values, this.start + Math.Min(start, this.count), Math.Min(this.count - Math.Min(start, this.count), count))
            : Initialise().Slice(start, count);
    
    public ReadOnlySpan<T> Span() =>
        Initialised
            ? values.Slice(start, count)
            : Initialise().Span();
    
    public ReadOnlySpan<T> Span(int start) =>
        Slice(start).Span();
    
    public ReadOnlySpan<T> Span(int start, int count) =>
        Slice(start, count).Span();
    
    public RefSeq<T> Add(params ReadOnlySpan<T> valuesToAdd)
    {
        if(!Initialised) return Initialise(Math.Max(valuesToAdd.Length, InitialSize)).Add(valuesToAdd);

        var size   = valuesToAdd.Length;
        var total  = count + valuesToAdd.Length;
        var refseq = ExpandUp(total);

        if (Interlocked.CompareExchange(ref refseq.state.CanAdd, 1, 0) == 0)
        {
            // Adding to a sequence that's never been added to before.
            // This is the fastest path because we can grow into the existing buffer
            var d = refseq.values.Slice(refseq.start + refseq.count, size);
            valuesToAdd.CopyTo(d);
            return new RefSeq<T>(refseq.values, refseq.start, refseq.count + size);
        }
        else
        {
            // Adding to a sequence that's been added to before.
            // That means we can't use the existing buffer as it's been written to previously.
            // So we must clone.
            return Clone().Add(valuesToAdd);
        }
    }

    public RefSeq<T> Cons(params ReadOnlySpan<T> valuesToCons)
    {
        if(!Initialised) return Initialise(Math.Max(valuesToCons.Length, InitialSize)).Add(valuesToCons);

        var size   = valuesToCons.Length;
        var total  = count + valuesToCons.Length;
        var refseq = ExpandDown(total);
        
        if (Interlocked.CompareExchange(ref refseq.state.CanCons, 1, 0) == 0)
        {
            // Adding to a sequence that's never been added to before.
            // This is the fastest path because we can grow into the existing buffer
            valuesToCons.CopyTo(refseq.values.Slice(refseq.start - size, size));
            return new RefSeq<T>(refseq.values, refseq.start - size, refseq.count + size);
        }
        else
        {
            // Adding to a sequence that's been added to before.
            // That means we can't use the existing buffer as it's been written to previously.
            // So we must clone.
            return Clone().Cons(valuesToCons);
        }       
    }

    public RefSeq<T> Pop()
    {
        if( count == 0 ) throw new InvalidOperationException("Cannot pop from an empty sequence");
        return Slice(0, count - 1);
    }

    public static RefSeq<T> singleton(T value)
    {
        Span<T> mem = new T[InitialSize];
        mem[InitialSize >> 1] = value;
        return new RefSeq<T>(mem, InitialSize >> 1, 1);
    }

    public static RefSeq<T> create(Span<T> values) =>
        values.Length <= 0
            ? Empty
            : new(values, 0, values.Length);

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

    public override string ToString() =>
        Span().ToString(); 
}
