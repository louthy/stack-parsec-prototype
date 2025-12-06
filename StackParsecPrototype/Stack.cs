using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace StackParsecPrototype;

public readonly ref struct Stack
{
    readonly ObjSeq objects;
    readonly Span<byte> memory;
    readonly int top;
    readonly int bottom;
    readonly int count;
    const int headerSize = 8;
    const int defaultStackSize = 32;

    public Stack(Span<byte> memory)
    {
        this.memory = memory;
        this.objects = ObjSeq.EmptyNoCons;
        this.top = 0;
        this.bottom = 0;
        this.count = memory.Length;
    }

    Stack(ObjSeq objects, Span<byte> memory, int top, int bottom, int count)
    {
        this.objects = objects;
        this.memory = memory;
        this.top = top;
        this.bottom = bottom;
        this.count = count;
    }

    public static Stack Empty => 
        new (new byte[defaultStackSize]);            // TODO: Decided on a good default and implement doubling
    
    public static Stack singleton<A>(A value) 
        where A : allows ref struct =>
        Empty.Push(value);
    
    public int Count => 
        bottom >> 2;

    public bool Initialised =>
        count > 0;

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
    
    public Stack Expand(int spaceNeeded)
    {
        if (!Initialised) return Initialise(spaceNeeded);
        if (count - top - bottom >= spaceNeeded) return this;
        var ncount = count;
        while (ncount - top - bottom < spaceNeeded) ncount = count << 1;
        Span<byte> nmemory = new byte[ncount];
        var ot = memory.Slice(0, top);
        var ob = memory.Slice(count - bottom, bottom);
        var nt = nmemory.Slice(0, top);
        var nb = nmemory.Slice(ncount - bottom, bottom);
        ot.CopyTo(nt);
        ob.CopyTo(nb);
        return new Stack(objects, nmemory, top, bottom, ncount);
    }

    public bool At<A>(int ix, out A returnValue)
        where A : allows ref struct
    {
        if (!Initialised)
        {
            returnValue = default!;
            return false;
        }
        var fourIx = ix + 1 << 2;
        if (fourIx > bottom)
        {
            returnValue = default!;
            return false;
        }
        var index = memory.Slice(count - fourIx, 4);
        var hdrix = BitConverter.ToInt32(index);

        var header        = memory.Slice(hdrix, 4);
        var metadataToken = BitConverter.ToInt32(memory.Slice(hdrix + 4, 4));
        if (metadataToken != typeof(A).MetadataToken)
        {
            returnValue = default!;
            return false;
        }
        
        switch (BitConverter.ToInt32(header))
        {
            case var size when (size & 0xF0000000) == 0:
                var structure = memory.Slice(hdrix - size, size);
                returnValue = Unsafe.ReadUnaligned<A>(in structure.GetPinnableReference());
                return true;
            
            case var objix when (objix & 0xF0000000) == 0x10000000:
                objix &= 0xFFFFFFF;
                if (objects[objix] is A x)
                {
                    returnValue = x;
                    return true;
                }
                else
                {
                    returnValue = default!;
                    return false;
                }
            
            default:
                throw new InvalidOperationException("Stack corrupted");
        }        
    }
    
    public Stack Push<A>(A value)
        where A : allows ref struct 
    {
        if (!Initialised) return Initialise().Push(value);
        
        if (typeof(A).IsClass)
        {
            // Reference types are always stored as objects
            return PushObj(Unsafe.As<A, object>(ref value), typeof(A).MetadataToken);
        }
        
        // Get the size of the value
        var size = Unsafe.SizeOf<A>();
        var stack = Expand(size + headerSize + 4);

        return PushInternal(value, stack, size);
    }
    
    static Stack PushInternal<A>(A value, Stack stack, int size)
        where A : allows ref struct 
    {
        // Clamp its maximum size
        if ((size & 0xfffffff) != size) throw new ArgumentException("Value too large");
        
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
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(header1), size);                     // Size of the value  
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(header2), typeof(A).MetadataToken);  // Allows for simple type-checking

        return new Stack(stack.objects, stack.memory, ntop, nbottom, stack.count);
    }

    public Stack PushStackOp(Func<Stack, Stack> op) =>
        Initialised
            ? PushObj(op, typeof(Func<Stack, Stack>).MetadataToken)
            : Initialise().PushObj(op, typeof(Func<Stack, Stack>).MetadataToken);

    Stack PushObj<A>(A value, int metadataToken)
    {
        var stack = Expand(headerSize + 4);
        return PushObjInternal(value, metadataToken, stack);
    }

    static Stack PushObjInternal<A>(A value, int metadataToken, Stack stack)
    {
        var ntop = stack.top + headerSize;
        var nbottom = stack.bottom + 4;
        if (ntop > stack.count - nbottom) throw new InvalidOperationException("Stack overflow");

        // Write the index at the end of the memory range
        var index = stack.memory.Slice(stack.count - nbottom, 4);
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(index), stack.top);

        var objix = stack.objects.Count;
        var header1 = stack.memory.Slice(stack.top, 4);
        var header2 = stack.memory.Slice(stack.top + 4, 4);
        objix |= 0x10000000;
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(header1), objix); // Index into the objects array
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(header2),
            metadataToken); // Allows for simple type-checking

        return new Stack(stack.objects.Add(value), stack.memory, ntop, nbottom, stack.count);
    }

    public bool Peek<A>(out A returnValue)
        where A : allows ref struct
    {
        if (!Initialised)
        {
            returnValue = default!;
            return false;
        }
        if (top < headerSize) throw new InvalidOperationException("Stack underflow");
        var header = memory.Slice(top - headerSize, 4);
        var metadataToken = BitConverter.ToInt32(memory.Slice(top - headerSize + 4, 4));
        if (metadataToken != typeof(A).MetadataToken)
        {
            returnValue = default!;
            return false;
        }
        
        switch (BitConverter.ToInt32(header))
        {
            case var size when (size & 0xF0000000) == 0:
                var structure = memory.Slice(top - size - headerSize, size);
                returnValue = Unsafe.ReadUnaligned<A>(in structure.GetPinnableReference());
                return true;
            
            case var objix when (objix & 0xF0000000) == 0x10000000:
                var ix = objix & 0xFFFFFFF;
                if (ix == objects.Count - 1)
                {
                    if (objects[ix] is A x)
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
                else
                {
                    returnValue = default!;
                    return false;
                }
            
            default:
                throw new InvalidOperationException("Stack corrupted");
        }
    }

    public Stack Pop()
    {
        if (!Initialised) throw new InvalidOperationException("Stack underflow");
        if (top < headerSize) throw new InvalidOperationException("Stack underflow");
        var header = memory.Slice(top - headerSize, 4);
        switch (BitConverter.ToInt32(header))
        {
            case var size when (size & 0xF0000000) == 0:
                var ntop = top - size - headerSize;
                return new Stack(objects, memory, ntop, bottom - 4, count);
            
            case var objix when (objix & 0xF0000000) == 0x10000000:
                var ix = objix & 0xFFFFFFF;
                if (ix == objects.Count - 1)
                {
                    return new Stack(objects, memory, top - headerSize, bottom - 4, count);
                }
                else
                {
                    throw new InvalidOperationException("Object not at the top of the stack where it should be");
                }
            
            default:
                throw new InvalidOperationException("Stack corrupted");
        }
    }

    public Stack ReadFromAndPush(Stack other, int ix)
    {
        if(!Initialised) return Initialise().ReadFromAndPush(other, ix);
        var fourIx = (ix + 1) << 2;
        if (fourIx > other.bottom)
        {
            throw new ArgumentOutOfRangeException(nameof(ix));
        }
        var index = other.memory.Slice(other.count - fourIx, 4);
        var hdrix = BitConverter.ToInt32(index); 
        
        var header1       = other.memory.Slice(hdrix, 4);
        var metadataToken = BitConverter.ToInt32(other.memory.Slice(hdrix + 4, 4));
        switch (BitConverter.ToInt32(header1))
        {
            case var size when (size & 0xF0000000) == 0:
                var stack = Expand(size + headerSize + 4);
                var structure = other.memory.Slice(hdrix - size, size + headerSize);
                structure.CopyTo(stack.memory.Slice(stack.top));    // TODO check not out of range for destination
                var tindex = memory.Slice(stack.count - stack.bottom - 4, 4);
                Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(tindex), stack.top + size);
                return new Stack(stack.objects, stack.memory, stack.top + size + headerSize, stack.bottom + 4, stack.count);
            
            case var objix when (objix & 0xF0000000) == 0x10000000:
                objix &= 0xFFFFFFF;
                return PushObj(other.objects[objix], metadataToken);
            
            default:
                throw new InvalidOperationException("Stack corrupted");
        }        
    }
}