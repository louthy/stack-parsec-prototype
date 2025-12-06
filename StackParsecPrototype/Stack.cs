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
        new (new byte[128]);            // TODO: Decided on a good default and implement doubling
    
    public static Stack singleton<A>(A value) 
        where A : allows ref struct =>
        Empty.Push(value);
    
    public int Count => 
        bottom >> 2;

    public bool At<A>(int ix, out A returnValue)
        where A : allows ref struct
    {
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
        if (typeof(A).IsClass)
        {
            // Reference types are always stored as objects
            return PushObj(Unsafe.As<A, object>(ref value), typeof(A).MetadataToken);
        }
        
        // Get the size of the value
        var size = Unsafe.SizeOf<A>();
        
        // Clamp its maximum size
        if ((size & 0xfffffff) != size) throw new ArgumentException("Value too large");
        
        // Find the new top after the value and header
        var ntop    = top    + size + headerSize;
        var nbottom = bottom + 4;
        
        if (ntop > count - nbottom) throw new InvalidOperationException("Stack overflow");

        // Write the index at the end of the memory range (point to the header)
        var index = memory.Slice(count - nbottom, 4);
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(index), top + size);
        
        // Write the value
        var rest = memory.Slice(top, size);
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(rest), value);

        // Write the size of the value
        var header1 = memory.Slice(top + size, 4);
        var header2 = memory.Slice(top + size + 4, 4);
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(header1), size);                     // Size of the value  
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(header2), typeof(A).MetadataToken);  // Allows for simple type-checking

        return new Stack(objects, memory, ntop, nbottom, count);
    }

    public Stack PushStackOp(Func<Stack, Stack> op) =>
        PushObj(op, typeof(Func<Stack, Stack>).MetadataToken);
    
    Stack PushObj<A>(A value, int metadataToken)
    {
        var ntop = top + headerSize;
        var nbottom = bottom + 4;
        if(ntop > count - nbottom) throw new InvalidOperationException("Stack overflow");
        
        // Write the index at the end of the memory range
        var index = memory.Slice(count - nbottom, 4);
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(index), top);
        
        var objix  = objects.Count;
        var header1 = memory.Slice(top, 4);
        var header2 = memory.Slice(top + 4, 4);
        objix |= 0x10000000;
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(header1), objix);          // Index into the objects array
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(header2), metadataToken);  // Allows for simple type-checking
        
        return new Stack(objects.Add(value), memory, ntop, nbottom, count);
    }

    public bool Peek<A>(out A returnValue)
        where A : allows ref struct
    {
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
        if (top < 4) throw new InvalidOperationException("Stack underflow");
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
                var structure = other.memory.Slice(hdrix - size, size + headerSize);
                structure.CopyTo(memory.Slice(top));    // TODO check not out of range for destination
                var tindex = memory.Slice(count - bottom - 4, 4);
                Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(tindex), top + size);
                return new Stack(objects, memory, top + size + headerSize, bottom + 4, count);
            
            case var objix when (objix & 0xF0000000) == 0x10000000:
                objix &= 0xFFFFFFF;
                return PushObj(other.objects[objix], metadataToken);
            
            default:
                throw new InvalidOperationException("Stack corrupted");
        }        
    }
}