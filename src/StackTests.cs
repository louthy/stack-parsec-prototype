using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace LanguageExt.RefParsec;

public class StackTests
{
    public static void Run()
    {
        Empty_is_empty();
        Push_one_object_contains_one_object();
        Push_two_objects_contains_two_objects();
        Push_one_value_contains_one_value();
    }
    
    public static void Empty_is_empty()
    {
        Span<byte> mem   = stackalloc byte[Stack.defaultStackSize];
        var        stack = new Stack(mem);
        Debug.Assert(stack.Count  == 0);
        Debug.Assert(stack.Top    == 0);
        Debug.Assert(stack.Bottom == 0);
    }

    public static void Push_one_object_contains_one_object()
    {
        Span<byte> mem   = stackalloc byte[Stack.defaultStackSize];
        var        stack = new Stack(mem);
        var        value = "Hello World!";
        
        stack = stack.Push(value);
        
        Debug.Assert(stack.Count  == 1);
        Debug.Assert(stack.Top    == 0);
        Debug.Assert(stack.Bottom == Stack.indexEntrySize);

        var entry = BitConverter.ToInt32(mem.Slice(Stack.defaultStackSize - Stack.indexEntrySize, Stack.indexEntrySize));
        
        Debug.Assert((entry & Stack.objectEntryFlag) == Stack.objectEntryFlag);
        Debug.Assert((entry & Stack.objectEntryMask) == 0);
        Debug.Assert(stack.Objects[0] is "Hello World!");

        // Assert string on the top of the stack
        if (stack.Peek<string>(out var nvalue))
        {
            Debug.Assert(value == nvalue);
        }
        else
        {
            Debug.Fail("Failed to peek value");
        }

        stack = stack.Pop();

        // Assert empty
        Debug.Assert(stack.Count  == 0);
        Debug.Assert(stack.Top    == 0);
        Debug.Assert(stack.Bottom == 0);        
    }

    public static void Push_two_objects_contains_two_objects()
    {
        Span<byte> mem   = stackalloc byte[Stack.defaultStackSize];
        var        stack = new Stack(mem);
        
        stack = stack.Push("Hello").Push("World");
        
        Debug.Assert(stack.Count  == 2);
        Debug.Assert(stack.Top    == 0);
        Debug.Assert(stack.Bottom == Stack.indexEntrySize * 2);

        var entry0 = BitConverter.ToInt32(mem.Slice(Stack.defaultStackSize - Stack.indexEntrySize, Stack.indexEntrySize));
        var entry1 = BitConverter.ToInt32(mem.Slice(Stack.defaultStackSize - Stack.indexEntrySize - Stack.indexEntrySize, Stack.indexEntrySize));
        
        Debug.Assert((entry0 & Stack.objectEntryFlag) == Stack.objectEntryFlag);
        Debug.Assert((entry0 & Stack.objectEntryMask) == 0);
        
        Debug.Assert((entry1 & Stack.objectEntryFlag) == Stack.objectEntryFlag);
        Debug.Assert((entry1 & Stack.objectEntryMask) == 1);
        
        Debug.Assert(stack.Objects[0] is "Hello");
        Debug.Assert(stack.Objects[1] is "World");
    }

    public static void Push_one_value_contains_one_value()
    {
        Span<byte> mem         = stackalloc byte[Stack.defaultStackSize];
        var        stack       = new Stack(mem);
        var        value       = Guid.NewGuid();
        var        valueSizeOf = Unsafe.SizeOf<Guid>();
        
        stack = stack.Push(value);
        
        // Assert the stack is consistent
        Debug.Assert(stack.Count  == 1);
        Debug.Assert(stack.Top    == valueSizeOf + Stack.headerSize);
        Debug.Assert(stack.Bottom == Stack.indexEntrySize);

        // Assert the entry index is correct
        var location = BitConverter.ToInt32(mem.Slice(Stack.defaultStackSize - Stack.indexEntrySize, Stack.indexEntrySize));
        Debug.Assert(location == valueSizeOf);
        
        // Assert the size is correct
        var size = BitConverter.ToInt32(mem.Slice(location, 4));
        Debug.Assert(size == valueSizeOf);
        
        // Assert the type metadata token is correct
        var meta = BitConverter.ToInt32(mem.Slice(location + 4, 4));
        Debug.Assert(meta == typeof(Guid).MetadataToken);

        // Assert the raw bytes are of the Guid
        var structure = mem.Slice(0, location);
        var raw = Unsafe.ReadUnaligned<Guid>(in structure.GetPinnableReference());
        Debug.Assert(raw == value);

        // Assert Guid on the top of the stack
        if (stack.Peek<Guid>(out var nvalue))
        {
            Debug.Assert(value == nvalue);
        }
        else
        {
            Debug.Fail("Failed to peek Guid value");
        }

        stack = stack.Pop();

        // Assert empty
        Debug.Assert(stack.Count  == 0);
        Debug.Assert(stack.Top    == 0);
        Debug.Assert(stack.Bottom == 0);
    }
    
}