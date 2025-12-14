using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LanguageExt.RefParsec;

/// <summary>
/// This is a stack that can store either objects, values, or even ref-struct references.  It works like
/// the other collection-types in that it can start off with stack-allocated backing spans, but if it needs
/// to grow to facilitate a more complex scenario then it will double its storage by allocating new backing
/// spans on the heap.
///
/// That gives this type extreme performance for common use-cases, but also a Get Out of Jail Free card for
/// more complex use-cases.  
/// </summary>
public readonly ref struct Stack
{
#if DEBUG
    const bool ShowDebugMessages = true;
#else    
    const bool ShowDebugMessages = false;
#endif
    public const int headerSize = 8;
    public const int indexEntrySize = 4;
    public const int defaultStackSize = 64;
    public const int objectEntryFlag = 0x10000000;
    public const int objectEntryMask = objectEntryFlag - 1;
    
    readonly RefSeq<object?> objects;
    readonly Span<byte> memory;
    readonly int top;
    readonly int bottom;
    readonly int count;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Stack(Span<byte> memory)
    {
        this.memory = memory;
        this.objects = RefSeq<object?>.EmptyNoCons;
        this.top = 0;
        this.bottom = 0;
        this.count = memory.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    Stack(RefSeq<object?> objects, Span<byte> memory, int top, int bottom, int count)
    {
        this.objects = objects;
        this.memory = memory;
        this.top = top;
        this.bottom = bottom;
        this.count = count;
    }

    public static Stack Empty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        get => new(new byte[defaultStackSize]); 
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Stack singleton<A>(A value) 
        where A : allows ref struct =>
        Empty.Push(value);
    
    public int Count 
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => bottom >> 2;
    }
    
    public int Top 
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => top;
    }
    
    public int Bottom 
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => bottom;
    }

    public bool Initialised
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => count > 0;
    }

    public RefSeq<object?> Objects
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => objects;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    Stack Initialise(int size = defaultStackSize)
    {
        size--;
        size |= size >> 1;
        size |= size >> 2;
        size |= size >> 4;
        size |= size >> 8;
        size |= size >> 16;
        size++;
        return new Stack(new byte[size]);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Stack Expand(int spaceNeeded)
    {
        if (!Initialised) return Initialise(spaceNeeded);
        if (count - top - bottom >= spaceNeeded) return this;
        var ncount = count << 1;
        while (ncount - top - bottom < spaceNeeded) ncount <<= 1;
        Span<byte> nmemory = new byte[ncount];
        var ot = memory.Slice(0, top);
        var ob = memory.Slice(count - bottom, bottom);
        var nt = nmemory.Slice(0, top);
        var nb = nmemory.Slice(ncount - bottom, bottom);
        ot.CopyTo(nt);
        ob.CopyTo(nb);
        return new Stack(objects, nmemory, top, bottom, ncount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsObject(int ix)
    {
        if (!Initialised || ix < 0 || ix >= Count) return false;
        var entryIndex = ix + 1 << 2;
        if (entryIndex > bottom) return false;
        var index = memory.Slice(count - entryIndex, 4);
        var entry = BitConverter.ToInt32(index);
        return (entry & objectEntryFlag) == objectEntryFlag;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValueType(int ix)
    {
        if (!Initialised || ix < 0 || ix >= Count) return false;
        var entryIndex = ix + 1 << 2;
        if (entryIndex > bottom) return false;
        var index = memory.Slice(count - entryIndex, 4);
        var entry = BitConverter.ToInt32(index);
        return (entry & objectEntryFlag) == 0;
    }
    
    public bool At<A>(int ix, out A returnValue)
        where A : allows ref struct
    {
        if (!Initialised || ix < 0 || ix >= Count)
        {
            returnValue = default!;
            return false;
        }

        var entryIndex = ix + 1 << 2;
        if (entryIndex > bottom)
        {
            returnValue = default!;
            return false;
        }
        var index = memory.Slice(count - entryIndex, 4);
        var entry = BitConverter.ToInt32(index);
        
        if ((entry & objectEntryFlag) == objectEntryFlag)
        {
            // This is an object index, so return the object
            entry &= objectEntryMask;

            if (objects[entry] is A x)
            {
                returnValue = x;
                return true;
            }
            else
            {
                returnValue = default!;
                return false;
            }
        }

        var header        = memory.Slice(entry, 4);
        var metadataToken = BitConverter.ToInt32(memory.Slice(entry + 4, 4));
        if (metadataToken != typeof(A).MetadataToken)
        {
            returnValue = default!;
            return false;
        }

        var size = BitConverter.ToInt32(header);
        var structure = memory.Slice(entry - size, size);
        returnValue = Unsafe.ReadUnaligned<A>(in structure.GetPinnableReference());
        return true;
    }
    
    public ReadOnlySpan<byte> AtBytes(int index)
    {
        var span = AtBytesAndHeader(index);
        return span.Slice(0, span.Length - headerSize);
    }

    public ReadOnlySpan<byte> AtBytesAndHeader(int index)
    {
        if (!Initialised ||  index < 0 || index >= Count) return ReadOnlySpan<byte>.Empty;
        var bottomIndex = index + 1 << 2;
        var entry = BitConverter.ToInt32(memory.Slice(count - bottomIndex, indexEntrySize));

        if ((entry & objectEntryFlag) == objectEntryFlag)
        {
            // This is an object index, so return empty
            return ReadOnlySpan<byte>.Empty;
        }

        var size = BitConverter.ToInt32(memory.Slice(entry, 4));
        return memory.Slice(entry - size, size + headerSize);
    }

    public A? AtObject<A>(int index)
    {
        if (!Initialised || index < 0 || index >= Count) return default;
        var bottomIndex = index + 1 << 2;
        if (bottomIndex > bottom) return default;
        var entry = BitConverter.ToInt32(memory.Slice(count - bottomIndex, indexEntrySize));
        
        if ((entry & objectEntryFlag) == 0)
        {
            // This is not an object index, so return null
            return default;
        }

        entry &= objectEntryMask;
        return objects[entry] is A x 
                   ? x 
                   : default;
    }

    public Stack Push<A>(A value)
        where A : allows ref struct 
    {
        if (!Initialised) return Initialise().Push(value);
        
        if (typeof(A).IsClass)
        {
            // Reference types are always stored as objects
            return PushObj(Unsafe.As<A, object>(ref value));
        }
        
        // Get the size of the value
        var size = Unsafe.SizeOf<A>();
        var stack = Expand(size + headerSize + 4);

        return PushInternal(value, stack, size);
    }
    
    static Stack PushInternal<A>(A value, Stack stack, int size)
        where A : allows ref struct 
    {
        // Find the new top after the value and header
        var ntop    = stack.top    + size + headerSize;
        var nbottom = stack.bottom + 4;
        
        if (ntop > stack.count - nbottom) throw new InvalidOperationException("Stack overflow");

        // Write the index at the end of the memory range (point to the header)
        var index = stack.memory.Slice(stack.count - nbottom, 4);
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(index), stack.top + size);
        
        // Write the value
        var rest = stack.memory.Slice(stack.top, size);
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(rest), value);

        // Write the size of the value
        var header1 = stack.memory.Slice(stack.top + size, 4);
        var header2 = stack.memory.Slice(stack.top + size + 4, 4);
        var metadataToken = typeof(A).MetadataToken;
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(header1), size);           // Size of the value  
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(header2), metadataToken);  // Allows for simple type-checking
        if (ShowDebugMessages)
        {
            Console.WriteLine($"PUSH ({metadataToken}): {typeof(A).Name}");
        }
        
        return new Stack(stack.objects, stack.memory, ntop, nbottom, stack.count);
    }

    public Stack PushStackOp(Func<Stack, Stack, Stack> op) =>
        Initialised
            ? PushObj(op)
            : Initialise().PushStackOp(op);

    Stack PushObj<A>(A value)
    {
        var stack = Expand(headerSize + 4);
        return PushObjInternal(value, stack);
    }

    static Stack PushObjInternal<A>(A value, Stack stack)
    {
        var nbottom = stack.bottom + 4;
        if (stack.top > stack.count - nbottom) throw new InvalidOperationException("Stack overflow");

        // Write the index at the end of the memory range
        var index = stack.memory.Slice(stack.count - nbottom, 4);
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(index), stack.objects.Count | objectEntryFlag);

        if (ShowDebugMessages)
        {
            Console.WriteLine($"PUSH (obj): {value?.GetType().Name ?? typeof(A).Name}");
        }
        return new Stack(stack.objects.Add(value), stack.memory, stack.top, nbottom, stack.count);
    }

    public bool Peek<A>(out A returnValue)
        where A : allows ref struct =>
        At(Count - 1, out returnValue);

    public Stack Pop()
    {
        if (!Initialised || Count == 0) throw new InvalidOperationException("Stack underflow");
        var entryIndex = Count << 2;
        if (entryIndex > bottom)
        {
            return this;
        }
        var index = memory.Slice(count - entryIndex, 4);
        var entry = BitConverter.ToInt32(index);

        if ((entry & objectEntryFlag) == objectEntryFlag)
        {
            return new Stack(objects.Pop(), memory, top, bottom - 4, count);
        }
        else
        {
            var size = BitConverter.ToInt32(memory.Slice(entry, 4));
            var ntop = top - size - headerSize;
            return new Stack(objects, memory, ntop, bottom - 4, count);
        }
    }

    public Stack ReadFromAndPush(Stack other, int ix)
    {
        if (!Initialised) return ReadFromAndPush(Initialise(), ix);
        if (other.IsObject(ix))
        {
            return PushObj(other.AtObject<object?>(ix));
        }
        else
        {
            var bytes = other.AtBytesAndHeader(ix);
            var size  = bytes.Length;
            var stack = Expand(size + indexEntrySize);
            var ntop  = stack.top + size;
            bytes.CopyTo(stack.memory.Slice(stack.top));
            var index = memory.Slice(stack.count - stack.bottom - indexEntrySize, indexEntrySize);
            
            Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(index), ntop - headerSize);
            
            return new Stack(stack.objects, stack.memory, ntop, stack.bottom + 4, stack.count);
        }
    }

    public Stack Append(Stack other)
    {
        if (!other.Initialised) return this;
        if (!Initialised) return other;

        var stack = Expand(other.top + other.bottom);

        // Copy other's top section to this stack's top section
        var otherTop = other.memory.Slice(0, other.top);
        var thisTop = stack.memory.Slice(stack.top, other.top);
        otherTop.CopyTo(thisTop);

        // Copy other's bottom section to this stack's bottom section
        var objtop = stack.objects.Count;
        var bottom = stack.count - stack.bottom;
        var valtop = stack.top;
        var max    = other.count;
        for (var i = 4; i <= other.bottom; i+=4)
        {
            // Get the other index value and offset it by this stack's top value to
            // make sure we're pointing at the right item
            var entry = BitConverter.ToInt32(other.memory.Slice(max - i, 4));
            if ((entry & objectEntryFlag) == objectEntryFlag)
            {
                // It's an object; which has an index that needs shifting.
                entry += objtop;
                Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(stack.memory.Slice(bottom - i, 4)), entry);
            }
            else
            {
                // It's a value, so it has now been shifted by `stack.top` bytes. That means we should
                // update the index value.  
                entry += valtop;
                Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(stack.memory.Slice(bottom - i, 4)), entry);
            }
        }

        // Merge the object sequences
        var newObjects = stack.objects;
        for (var i = 0; i < other.objects.Count; i++)
        {
            newObjects = newObjects.Add(other.objects[i]);
        }

        return new Stack(newObjects, stack.memory, stack.top + other.top, stack.bottom + other.bottom, stack.count);
    }
}