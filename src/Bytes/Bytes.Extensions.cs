using System.Runtime.CompilerServices;

namespace LanguageExt.RefParsec;

public static class BytesExtensions
{
    extension(ReadOnlySpan<byte> self)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bytes Cons(Bytes bytes) =>
            bytes.Prepend(self);
    }

    extension(byte self)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bytes Cons(Bytes bytes) =>
            bytes.Prepend(self);
    }

    extension(OpCode self)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bytes Cons(Bytes bytes) =>
             bytes.Prepend(self);
    }

    extension(short self)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bytes Cons(Bytes bytes) =>
            bytes.PrependInt16(self);
    }

    extension(ushort self)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bytes Cons(Bytes bytes) =>
            bytes.PrependUInt16(self);
    }

    extension(int self)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bytes Cons(Bytes bytes) =>
            bytes.PrependInt32(self);
    }

    extension(uint self)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bytes Cons(Bytes bytes) =>
            bytes.PrependUInt32(self);
    }

    extension(long self)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bytes Cons(Bytes bytes) =>
            bytes.PrependInt64(self);
    }

    extension(ulong self)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bytes Cons(Bytes bytes) =>
            bytes.PrependUInt64(self);
    }

    extension(float self)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bytes Cons(Bytes bytes) =>
            bytes.PrependFloat(self);
    }

    extension(double self)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bytes Cons(Bytes bytes) =>
            bytes.PrependDouble(self);
    }

    extension(string self)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bytes Cons(Bytes bytes) =>
            bytes.PrependString(self);
    }    
}