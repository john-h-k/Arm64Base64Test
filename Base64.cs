﻿using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Diagnostics;

namespace System.Diagnostics
{
    /// <summary>
    /// Indicates a class, method, constructor, or struct should be not shown in a <see cref="StackTrace"/> even if the stack trace was created
    /// inside the given type or method. This class cannot be inherited.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Struct, Inherited = false)]
    public sealed class StackTraceHiddenAttribute : Attribute
    {
        public StackTraceHiddenAttribute() { }
    }
}


public static class Base64
{
    private const uint EncodingPad = '='; // '=', for padding

    public static int GetMaxDecodedFromUtf8Length(int length)
    {
        if (length < 0)
            ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.length);

        return (length >> 2) * 3;
    }

    public static unsafe OperationStatus EncodeToUtf8(ReadOnlySpan<byte> bytes, Span<byte> utf8, out int bytesConsumed, out int bytesWritten, bool isFinalBlock = true)
        {
            if (bytes.IsEmpty)
            {
                bytesConsumed = 0;
                bytesWritten = 0;
                return OperationStatus.Done;
            }
 
            fixed (byte* srcBytes = &MemoryMarshal.GetReference(bytes))
            fixed (byte* destBytes = &MemoryMarshal.GetReference(utf8))
            {
                int srcLength = bytes.Length;
                int destLength = utf8.Length;
                int maxSrcLength;
 
                if (srcLength <= 1610612733 && destLength >= GetMaxEncodedToUtf8Length(srcLength))
                {
                    maxSrcLength = srcLength;
                }
                else
                {
                    maxSrcLength = (destLength >> 2) * 3;
                }
 
                byte* src = srcBytes;
                byte* dest = destBytes;
                byte* srcEnd = srcBytes + (uint)srcLength;
                byte* srcMax = srcBytes + (uint)maxSrcLength;
 
                if (maxSrcLength >= 16)
                {
                    byte* end = srcMax - 48;
                    if (AdvSimd.Arm64.IsSupported && (end >= src))
                    {
                        AdvSimd64Encode(ref src, ref dest, end, maxSrcLength, destLength, srcBytes, destBytes);
 
                        if (src == srcEnd)
                            goto DoneExit;
                    }

                    end = srcMax - 32;
                    if (Avx2.IsSupported && (end >= src))
                    {
                        throw null!; //Avx2Encode(ref src, ref dest, end, maxSrcLength, destLength, srcBytes, destBytes);
 
                        if (src == srcEnd)
                            goto DoneExit;
                    }
 
                    end = srcMax - 16;
                    if (Ssse3.IsSupported && (end >= src))
                    {
                        throw null!; //Ssse3Encode(ref src, ref dest, end, maxSrcLength, destLength, srcBytes, destBytes);
 
                        if (src == srcEnd)
                            goto DoneExit;
                    }
                }
 
                ref byte encodingMap = ref MemoryMarshal.GetReference(s_encodingMap);
                uint result = 0;
 
                srcMax -= 2;
                while (src < srcMax)
                {
                    result = Encode(src, ref encodingMap);
                    Unsafe.WriteUnaligned(dest, result);
                    src += 3;
                    dest += 4;
                }
 
                if (srcMax + 2 != srcEnd)
                    goto DestinationTooSmallExit;
 
                if (!isFinalBlock)
                {
                    if (src == srcEnd)
                        goto DoneExit;
 
                    goto NeedMoreData;
                }
 
                if (src + 1 == srcEnd)
                {
                    result = EncodeAndPadTwo(src, ref encodingMap);
                    Unsafe.WriteUnaligned(dest, result);
                    src += 1;
                    dest += 4;
                }
                else if (src + 2 == srcEnd)
                {
                    result = EncodeAndPadOne(src, ref encodingMap);
                    Unsafe.WriteUnaligned(dest, result);
                    src += 2;
                    dest += 4;
                }
 
            DoneExit:
                bytesConsumed = (int)(src - srcBytes);
                bytesWritten = (int)(dest - destBytes);
                return OperationStatus.Done;
 
            DestinationTooSmallExit:
                bytesConsumed = (int)(src - srcBytes);
                bytesWritten = (int)(dest - destBytes);
                return OperationStatus.DestinationTooSmall;
 
            NeedMoreData:
                bytesConsumed = (int)(src - srcBytes);
                bytesWritten = (int)(dest - destBytes);
                return OperationStatus.NeedMoreData;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe uint Encode(byte* threeBytes, ref byte encodingMap)
        {
            uint t0 = threeBytes[0];
            uint t1 = threeBytes[1];
            uint t2 = threeBytes[2];
 
            uint i = (t0 << 16) | (t1 << 8) | t2;
 
            uint i0 = Unsafe.Add(ref encodingMap, (IntPtr)(i >> 18));
            uint i1 = Unsafe.Add(ref encodingMap, (IntPtr)((i >> 12) & 0x3F));
            uint i2 = Unsafe.Add(ref encodingMap, (IntPtr)((i >> 6) & 0x3F));
            uint i3 = Unsafe.Add(ref encodingMap, (IntPtr)(i & 0x3F));
 
            return i0 | (i1 << 8) | (i2 << 16) | (i3 << 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe uint EncodeAndPadOne(byte* twoBytes, ref byte encodingMap)
        {
            uint t0 = twoBytes[0];
            uint t1 = twoBytes[1];
 
            uint i = (t0 << 16) | (t1 << 8);
 
            uint i0 = Unsafe.Add(ref encodingMap, (IntPtr)(i >> 18));
            uint i1 = Unsafe.Add(ref encodingMap, (IntPtr)((i >> 12) & 0x3F));
            uint i2 = Unsafe.Add(ref encodingMap, (IntPtr)((i >> 6) & 0x3F));
 
            return i0 | (i1 << 8) | (i2 << 16) | (EncodingPad << 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe uint EncodeAndPadTwo(byte* oneByte, ref byte encodingMap)
        {
            uint t0 = oneByte[0];
 
            uint i = t0 << 8;
 
            uint i0 = Unsafe.Add(ref encodingMap, (IntPtr)(i >> 10));
            uint i1 = Unsafe.Add(ref encodingMap, (IntPtr)((i >> 4) & 0x3F));
 
            return i0 | (i1 << 8) | (EncodingPad << 16) | (EncodingPad << 24);
        }
 
        /// <summary>
        /// Returns the maximum length (in bytes) of the result if you were to encode binary data within a byte span of size "length".
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown when the specified <paramref name="length"/> is less than 0 or larger than 1610612733 (since encode inflates the data by 4/3).
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetMaxEncodedToUtf8Length(int length)
        {
            if ((uint)length > 1610612733)
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.length);
 
            return ((length + 2) / 3) * 4;
        }

    public static unsafe OperationStatus DecodeFromUtf8(ReadOnlySpan<byte> utf8, Span<byte> bytes, out int bytesConsumed, out int bytesWritten, bool isFinalBlock = true)
    {
        if (utf8.IsEmpty)
        {
            bytesConsumed = 0;
            bytesWritten = 0;
            return OperationStatus.Done;
        }

        fixed (byte* srcBytes = &MemoryMarshal.GetReference(utf8))
        fixed (byte* destBytes = &MemoryMarshal.GetReference(bytes))
        {
            int srcLength = utf8.Length & ~0x3;  // only decode input up to the closest multiple of 4.
            int destLength = bytes.Length;
            int maxSrcLength = srcLength;
            int decodedLength = GetMaxDecodedFromUtf8Length(srcLength);

            // max. 2 padding chars
            if (destLength < decodedLength - 2)
            {
                // For overflow see comment below
                maxSrcLength = destLength / 3 * 4;
            }

            byte* src = srcBytes;
            byte* dest = destBytes;
            byte* srcEnd = srcBytes + (uint)srcLength;
            byte* srcMax = srcBytes + (uint)maxSrcLength;

            if (maxSrcLength >= 24)
            {
                byte* end = srcMax - 45;
                if (false && Avx2.IsSupported && (end >= src))
                {
                    Ssse3Decode(ref src, ref dest, end, maxSrcLength, destLength, srcBytes, destBytes);

                    if (src == srcEnd)
                        goto DoneExit;
                }

                end = srcMax - 24;
                if (Ssse3.IsSupported && (end >= src))
                {
                    Ssse3Decode(ref src, ref dest, end, maxSrcLength, destLength, srcBytes, destBytes);

                    if (src == srcEnd)
                        goto DoneExit;
                }

                if (AdvSimd.Arm64.IsSupported && (end >= src))
                {
                    AvdSimd64Decode(ref src, ref dest, end, maxSrcLength, destLength, srcBytes, destBytes);

                    if (src == srcEnd)
                        goto DoneExit;
                }
            }

            // Last bytes could have padding characters, so process them separately and treat them as valid only if isFinalBlock is true
            // if isFinalBlock is false, padding characters are considered invalid
            int skipLastChunk = isFinalBlock ? 4 : 0;

            if (destLength >= decodedLength)
            {
                maxSrcLength = srcLength - skipLastChunk;
            }
            else
            {
                // This should never overflow since destLength here is less than int.MaxValue / 4 * 3 (i.e. 1610612733)
                // Therefore, (destLength / 3) * 4 will always be less than 2147483641
                Debug.Assert(destLength < (int.MaxValue / 4 * 3));
                maxSrcLength = (destLength / 3) * 4;
            }

            ref sbyte decodingMap = ref MemoryMarshal.GetReference(s_decodingMap);
            srcMax = srcBytes + (uint)maxSrcLength;

            while (src < srcMax)
            {
                int result = Decode(src, ref decodingMap);

                if (result < 0)
                    goto InvalidDataExit;

                WriteThreeLowOrderBytes(dest, result);
                src += 4;
                dest += 3;
            }

            if (maxSrcLength != srcLength - skipLastChunk)
                goto DestinationTooSmallExit;

            // If input is less than 4 bytes, srcLength == sourceIndex == 0
            // If input is not a multiple of 4, sourceIndex == srcLength != 0
            if (src == srcEnd)
            {
                if (isFinalBlock)
                    goto InvalidDataExit;

                if (src == srcBytes + utf8.Length)
                    goto DoneExit;

                goto NeedMoreDataExit;
            }

            // if isFinalBlock is false, we will never reach this point

            // Handle last four bytes. There are 0, 1, 2 padding chars.
            uint t0 = srcEnd[-4];
            uint t1 = srcEnd[-3];
            uint t2 = srcEnd[-2];
            uint t3 = srcEnd[-1];

            int i0 = Unsafe.Add(ref decodingMap, (IntPtr)t0);
            int i1 = Unsafe.Add(ref decodingMap, (IntPtr)t1);

            i0 <<= 18;
            i1 <<= 12;

            i0 |= i1;

            byte* destMax = destBytes + (uint)destLength;

            if (t3 != EncodingPad)
            {
                int i2 = Unsafe.Add(ref decodingMap, (IntPtr)t2);
                int i3 = Unsafe.Add(ref decodingMap, (IntPtr)t3);

                i2 <<= 6;

                i0 |= i3;
                i0 |= i2;

                if (i0 < 0)
                    goto InvalidDataExit;
                if (dest + 3 > destMax)
                    goto DestinationTooSmallExit;

                WriteThreeLowOrderBytes(dest, i0);
                dest += 3;
            }
            else if (t2 != EncodingPad)
            {
                int i2 = Unsafe.Add(ref decodingMap, (IntPtr)t2);

                i2 <<= 6;

                i0 |= i2;

                if (i0 < 0)
                    goto InvalidDataExit;
                if (dest + 2 > destMax)
                    goto DestinationTooSmallExit;

                dest[0] = (byte)(i0 >> 16);
                dest[1] = (byte)(i0 >> 8);
                dest += 2;
            }
            else
            {
                if (i0 < 0)
                    goto InvalidDataExit;
                if (dest + 1 > destMax)
                    goto DestinationTooSmallExit;

                dest[0] = (byte)(i0 >> 16);
                dest += 1;
            }

            src += 4;

            if (srcLength != utf8.Length)
                goto InvalidDataExit;

            DoneExit:
            bytesConsumed = (int)(src - srcBytes);
            bytesWritten = (int)(dest - destBytes);
            return OperationStatus.Done;

        DestinationTooSmallExit:
            if (srcLength != utf8.Length && isFinalBlock)
                goto InvalidDataExit; // if input is not a multiple of 4, and there is no more data, return invalid data instead

            bytesConsumed = (int)(src - srcBytes);
            bytesWritten = (int)(dest - destBytes);
            return OperationStatus.DestinationTooSmall;

        NeedMoreDataExit:
            bytesConsumed = (int)(src - srcBytes);
            bytesWritten = (int)(dest - destBytes);
            return OperationStatus.NeedMoreData;

        InvalidDataExit:
            bytesConsumed = (int)(src - srcBytes);
            bytesWritten = (int)(dest - destBytes);
            return OperationStatus.InvalidData;
        }
    }

    public static unsafe OperationStatus DecodeFromUtf8_VectorLookup(ReadOnlySpan<byte> utf8, Span<byte> bytes, out int bytesConsumed, out int bytesWritten, bool isFinalBlock = true)
    {
        if (utf8.IsEmpty)
        {
            bytesConsumed = 0;
            bytesWritten = 0;
            return OperationStatus.Done;
        }

        fixed (byte* srcBytes = &MemoryMarshal.GetReference(utf8))
        fixed (byte* destBytes = &MemoryMarshal.GetReference(bytes))
        {
            int srcLength = utf8.Length & ~0x3;  // only decode input up to the closest multiple of 4.
            int destLength = bytes.Length;
            int maxSrcLength = srcLength;
            int decodedLength = GetMaxDecodedFromUtf8Length(srcLength);

            // max. 2 padding chars
            if (destLength < decodedLength - 2)
            {
                // For overflow see comment below
                maxSrcLength = destLength / 3 * 4;
            }

            byte* src = srcBytes;
            byte* dest = destBytes;
            byte* srcEnd = srcBytes + (uint)srcLength;
            byte* srcMax = srcBytes + (uint)maxSrcLength;

            if (maxSrcLength > 64)
            {
                byte* end = srcMax - 64;
                if (AdvSimd.Arm64.IsSupported && (end >= src))
                {
                    AvdSimd64Decode_VectorLookup(ref src, ref dest, end, maxSrcLength, destLength, srcBytes, destBytes);

                    if (src == srcEnd)
                        goto DoneExit;
                }
            }

            if (maxSrcLength >= 24)
            {
                byte* end = srcMax - 45;

                if (false && Avx2.IsSupported && (end >= src))
                {
                    Ssse3Decode(ref src, ref dest, end, maxSrcLength, destLength, srcBytes, destBytes);

                    if (src == srcEnd)
                        goto DoneExit;
                }

                end = srcMax - 24;
                if (Ssse3.IsSupported && (end >= src))
                {
                    Ssse3Decode(ref src, ref dest, end, maxSrcLength, destLength, srcBytes, destBytes);

                    if (src == srcEnd)
                        goto DoneExit;
                }
            }

            // Last bytes could have padding characters, so process them separately and treat them as valid only if isFinalBlock is true
            // if isFinalBlock is false, padding characters are considered invalid
            int skipLastChunk = isFinalBlock ? 4 : 0;

            if (destLength >= decodedLength)
            {
                maxSrcLength = srcLength - skipLastChunk;
            }
            else
            {
                // This should never overflow since destLength here is less than int.MaxValue / 4 * 3 (i.e. 1610612733)
                // Therefore, (destLength / 3) * 4 will always be less than 2147483641
                Debug.Assert(destLength < (int.MaxValue / 4 * 3));
                maxSrcLength = (destLength / 3) * 4;
            }

            ref sbyte decodingMap = ref MemoryMarshal.GetReference(s_decodingMap);
            srcMax = srcBytes + (uint)maxSrcLength;

            while (src < srcMax)
            {
                int result = Decode(src, ref decodingMap);

                if (result < 0)
                    goto InvalidDataExit;

                WriteThreeLowOrderBytes(dest, result);
                src += 4;
                dest += 3;
            }

            if (maxSrcLength != srcLength - skipLastChunk)
                goto DestinationTooSmallExit;

            // If input is less than 4 bytes, srcLength == sourceIndex == 0
            // If input is not a multiple of 4, sourceIndex == srcLength != 0
            if (src == srcEnd)
            {
                if (isFinalBlock)
                    goto InvalidDataExit;

                if (src == srcBytes + utf8.Length)
                    goto DoneExit;

                goto NeedMoreDataExit;
            }

            // if isFinalBlock is false, we will never reach this point

            // Handle last four bytes. There are 0, 1, 2 padding chars.
            uint t0 = srcEnd[-4];
            uint t1 = srcEnd[-3];
            uint t2 = srcEnd[-2];
            uint t3 = srcEnd[-1];

            int i0 = Unsafe.Add(ref decodingMap, (IntPtr)t0);
            int i1 = Unsafe.Add(ref decodingMap, (IntPtr)t1);

            i0 <<= 18;
            i1 <<= 12;

            i0 |= i1;

            byte* destMax = destBytes + (uint)destLength;

            if (t3 != EncodingPad)
            {
                int i2 = Unsafe.Add(ref decodingMap, (IntPtr)t2);
                int i3 = Unsafe.Add(ref decodingMap, (IntPtr)t3);

                i2 <<= 6;

                i0 |= i3;
                i0 |= i2;

                if (i0 < 0)
                    goto InvalidDataExit;
                if (dest + 3 > destMax)
                    goto DestinationTooSmallExit;

                WriteThreeLowOrderBytes(dest, i0);
                dest += 3;
            }
            else if (t2 != EncodingPad)
            {
                int i2 = Unsafe.Add(ref decodingMap, (IntPtr)t2);

                i2 <<= 6;

                i0 |= i2;

                if (i0 < 0)
                    goto InvalidDataExit;
                if (dest + 2 > destMax)
                    goto DestinationTooSmallExit;

                dest[0] = (byte)(i0 >> 16);
                dest[1] = (byte)(i0 >> 8);
                dest += 2;
            }
            else
            {
                if (i0 < 0)
                    goto InvalidDataExit;
                if (dest + 1 > destMax)
                    goto DestinationTooSmallExit;

                dest[0] = (byte)(i0 >> 16);
                dest += 1;
            }

            src += 4;

            if (srcLength != utf8.Length)
                goto InvalidDataExit;

            DoneExit:
            bytesConsumed = (int)(src - srcBytes);
            bytesWritten = (int)(dest - destBytes);
            return OperationStatus.Done;

        DestinationTooSmallExit:
            if (srcLength != utf8.Length && isFinalBlock)
                goto InvalidDataExit; // if input is not a multiple of 4, and there is no more data, return invalid data instead

            bytesConsumed = (int)(src - srcBytes);
            bytesWritten = (int)(dest - destBytes);
            return OperationStatus.DestinationTooSmall;

        NeedMoreDataExit:
            bytesConsumed = (int)(src - srcBytes);
            bytesWritten = (int)(dest - destBytes);
            return OperationStatus.NeedMoreData;

        InvalidDataExit:
            bytesConsumed = (int)(src - srcBytes);
            bytesWritten = (int)(dest - destBytes);
            return OperationStatus.InvalidData;
        }
    }

    private static TVector ReadVector<TVector>(ReadOnlySpan<sbyte> data)
    {
        ref sbyte tmp = ref MemoryMarshal.GetReference(data);
        return Unsafe.As<sbyte, TVector>(ref tmp);
    }

    private static TVector ReadVector<TVector>(ReadOnlySpan<byte> data)
    {
        ref byte tmp = ref MemoryMarshal.GetReference(data);
        return Unsafe.As<byte, TVector>(ref tmp);
    }

    [Conditional("DEBUG")]
    private static unsafe void AssertRead<TVector>(byte* src, byte* srcStart, int srcLength)
    {
        int vectorElements = Unsafe.SizeOf<TVector>();
        byte* readEnd = src + vectorElements;
        byte* srcEnd = srcStart + srcLength;

        if (readEnd > srcEnd)
        {
            int srcIndex = (int)(src - srcStart);
            Debug.Fail($"Read for {typeof(TVector)} is not within safe bounds. srcIndex: {srcIndex}, srcLength: {srcLength}");
        }
    }

    [Conditional("DEBUG")]
    private static unsafe void AssertWrite<TVector>(byte* dest, byte* destStart, int destLength)
    {
        int vectorElements = Unsafe.SizeOf<TVector>();
        byte* writeEnd = dest + vectorElements;
        byte* destEnd = destStart + destLength;

        if (writeEnd > destEnd)
        {
            int destIndex = (int)(dest - destStart);
            Debug.Fail($"Write for {typeof(TVector)} is not within safe bounds. destIndex: {destIndex}, destLength: {destLength}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void Ssse3Decode(ref byte* srcBytes, ref byte* destBytes, byte* srcEnd, int sourceLength, int destLength, byte* srcStart, byte* destStart)
    {
        // If we have SSSE3 support, pick off 16 bytes at a time for as long as we can,
        // but make sure that we quit before seeing any == markers at the end of the
        // string. Also, because we write four zeroes at the end of the output, ensure
        // that there are at least 6 valid bytes of input data remaining to close the
        // gap. 16 + 2 + 6 = 24 bytes.

        // The input consists of six character sets in the Base64 alphabet,
        // which we need to map back to the 6-bit values they represent.
        // There are three ranges, two singles, and then there's the rest.
        //
        //  #  From       To        Add  Characters
        //  1  [43]       [62]      +19  +
        //  2  [47]       [63]      +16  /
        //  3  [48..57]   [52..61]   +4  0..9
        //  4  [65..90]   [0..25]   -65  A..Z
        //  5  [97..122]  [26..51]  -71  a..z
        // (6) Everything else => invalid input

        // We will use LUTS for character validation & offset computation
        // Remember that 0x2X and 0x0X are the same index for _mm_shuffle_epi8,
        // this allows to mask with 0x2F instead of 0x0F and thus save one constant declaration (register and/or memory access)

        // For offsets:
        // Perfect hash for lut = ((src>>4)&0x2F)+((src==0x2F)?0xFF:0x00)
        // 0000 = garbage
        // 0001 = /
        // 0010 = +
        // 0011 = 0-9
        // 0100 = A-Z
        // 0101 = A-Z
        // 0110 = a-z
        // 0111 = a-z
        // 1000 >= garbage

        // For validation, here's the table.
        // A character is valid if and only if the AND of the 2 lookups equals 0:

        // hi \ lo              0000 0001 0010 0011 0100 0101 0110 0111 1000 1001 1010 1011 1100 1101 1110 1111
        //      LUT             0x15 0x11 0x11 0x11 0x11 0x11 0x11 0x11 0x11 0x11 0x13 0x1A 0x1B 0x1B 0x1B 0x1A

        // 0000 0X10 char        NUL  SOH  STX  ETX  EOT  ENQ  ACK  BEL   BS   HT   LF   VT   FF   CR   SO   SI
        //           andlut     0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10

        // 0001 0x10 char        DLE  DC1  DC2  DC3  DC4  NAK  SYN  ETB  CAN   EM  SUB  ESC   FS   GS   RS   US
        //           andlut     0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10

        // 0010 0x01 char               !    "    #    $    %    &    '    (    )    *    +    ,    -    .    /
        //           andlut     0x01 0x01 0x01 0x01 0x01 0x01 0x01 0x01 0x01 0x01 0x01 0x00 0x01 0x01 0x01 0x00

        // 0011 0x02 char          0    1    2    3    4    5    6    7    8    9    :    ;    <    =    >    ?
        //           andlut     0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x02 0x02 0x02 0x02 0x02 0x02

        // 0100 0x04 char          @    A    B    C    D    E    F    G    H    I    J    K    L    M    N    0
        //           andlut     0x04 0x00 0x00 0x00 0X00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00

        // 0101 0x08 char          P    Q    R    S    T    U    V    W    X    Y    Z    [    \    ]    ^    _
        //           andlut     0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x08 0x08 0x08 0x08 0x08

        // 0110 0x04 char          `    a    b    c    d    e    f    g    h    i    j    k    l    m    n    o
        //           andlut     0x04 0x00 0x00 0x00 0X00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00
        // 0111 0X08 char          p    q    r    s    t    u    v    w    x    y    z    {    |    }    ~
        //           andlut     0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x08 0x08 0x08 0x08 0x08

        // 1000 0x10 andlut     0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10
        // 1001 0x10 andlut     0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10
        // 1010 0x10 andlut     0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10
        // 1011 0x10 andlut     0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10
        // 1100 0x10 andlut     0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10
        // 1101 0x10 andlut     0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10
        // 1110 0x10 andlut     0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10
        // 1111 0x10 andlut     0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10 0x10

        // The JIT won't hoist these "constants", so help it
        Vector128<sbyte> lutHi = ReadVector<Vector128<sbyte>>(s_sseDecodeLutHi);
        Vector128<sbyte> lutLo = ReadVector<Vector128<sbyte>>(s_sseDecodeLutLo);
        Vector128<sbyte> lutShift = ReadVector<Vector128<sbyte>>(s_sseDecodeLutShift);
        Vector128<sbyte> mask2F = Vector128.Create((sbyte)'/');
        Vector128<sbyte> mergeConstant0 = Vector128.Create(0x01400140).AsSByte();
        Vector128<short> mergeConstant1 = Vector128.Create(0x00011000).AsInt16();
        Vector128<sbyte> packBytesMask = ReadVector<Vector128<sbyte>>(s_sseDecodePackBytesMask);
        Vector128<sbyte> zero = Vector128<sbyte>.Zero;

        byte* src = srcBytes;
        byte* dest = destBytes;

        //while (remaining >= 24)
        do
        {
            AssertRead<Vector128<sbyte>>(src, srcStart, sourceLength);
            Vector128<sbyte> str = Sse2.LoadVector128(src).AsSByte();

            // lookup
            Vector128<sbyte> hiNibbles = Sse2.And(Sse2.ShiftRightLogical(str.AsInt32(), 4).AsSByte(), mask2F);
            Vector128<sbyte> loNibbles = Sse2.And(str, mask2F);
            Vector128<sbyte> hi = Ssse3.Shuffle(lutHi, hiNibbles);
            Vector128<sbyte> lo = Ssse3.Shuffle(lutLo, loNibbles);

            Console.WriteLine(hi);
            Console.WriteLine(lo);

            // Check for invalid input: if any "and" values from lo and hi are not zero,
            // fall back on bytewise code to do error checking and reporting:
            if (Sse2.MoveMask(Sse2.CompareGreaterThan(Sse2.And(lo, hi), zero)) != 0)
                break;

            Console.WriteLine(hi);
            Console.WriteLine(lo);

            Vector128<sbyte> eq2F = Sse2.CompareEqual(str, mask2F);
            Vector128<sbyte> shift = Ssse3.Shuffle(lutShift, Sse2.Add(eq2F, hiNibbles));

            // Now simply add the delta values to the input:
            str = Sse2.Add(str, shift);

            Console.WriteLine(str);

            // in, bits, upper case are most significant bits, lower case are least significant bits
            // 00llllll 00kkkkLL 00jjKKKK 00JJJJJJ
            // 00iiiiii 00hhhhII 00ggHHHH 00GGGGGG
            // 00ffffff 00eeeeFF 00ddEEEE 00DDDDDD
            // 00cccccc 00bbbbCC 00aaBBBB 00AAAAAA

            Vector128<short> merge_ab_and_bc = Ssse3.MultiplyAddAdjacent(str.AsByte(), mergeConstant0);
            // 0000kkkk LLllllll 0000JJJJ JJjjKKKK
            // 0000hhhh IIiiiiii 0000GGGG GGggHHHH
            // 0000eeee FFffffff 0000DDDD DDddEEEE
            // 0000bbbb CCcccccc 0000AAAA AAaaBBBB

            Console.WriteLine(merge_ab_and_bc);

            Vector128<int> output = Sse2.MultiplyAddAdjacent(merge_ab_and_bc, mergeConstant1);
            // 00000000 JJJJJJjj KKKKkkkk LLllllll
            // 00000000 GGGGGGgg HHHHhhhh IIiiiiii
            // 00000000 DDDDDDdd EEEEeeee FFffffff
            // 00000000 AAAAAAaa BBBBbbbb CCcccccc
            Console.WriteLine(output);

            // Pack bytes together:
            str = Ssse3.Shuffle(output.AsSByte(), packBytesMask);
            // 00000000 00000000 00000000 00000000
            // LLllllll KKKKkkkk JJJJJJjj IIiiiiii
            // HHHHhhhh GGGGGGgg FFffffff EEEEeeee
            // DDDDDDdd CCcccccc BBBBbbbb AAAAAAaa

            Console.WriteLine(str);

            AssertWrite<Vector128<sbyte>>(dest, destStart, destLength);
            Sse2.Store(dest, str.AsByte());

            src += 16;
            dest += 12;
        }
        while (src <= srcEnd);

        srcBytes = src;
        destBytes = dest;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void AvdSimd64Decode(ref byte* srcBytes, ref byte* destBytes, byte* srcEnd, int sourceLength, int destLength, byte* srcStart, byte* destStart)
    {
        // The JIT won't hoist these "constants", so help it
        Vector128<sbyte> lutHi = ReadVector<Vector128<sbyte>>(s_sseDecodeLutHi);
        Vector128<sbyte> lutLo = ReadVector<Vector128<sbyte>>(s_sseDecodeLutLo);
        Vector128<sbyte> lutShift = ReadVector<Vector128<sbyte>>(s_sseDecodeLutShift);
        Vector128<sbyte> mask2F = Vector128.Create((sbyte)'/');
        Vector128<sbyte> mask0F = Vector128.Create((sbyte)0x0F);
        Vector128<sbyte> mergeConstant0 = Vector128.Create(0x01400140).AsSByte();
        Vector128<short> mergeConstant1 = Vector128.Create(0x00011000).AsInt16();
        Vector128<sbyte> packBytesMask = ReadVector<Vector128<sbyte>>(s_sseDecodePackBytesMask);
        Vector128<sbyte> zero = Vector128<sbyte>.Zero;

        byte* src = srcBytes;
        byte* dest = destBytes;

        //while (remaining >= 24)
        do
        {
            AssertRead<Vector128<sbyte>>(src, srcStart, sourceLength);
            Vector128<sbyte> str = AdvSimd.LoadVector128(src).AsSByte();

            // lookup
            Vector128<sbyte> hiNibbles = AdvSimd.And(AdvSimd.ShiftRightLogical(str.AsInt32(), 4).AsSByte(), mask2F);
            Vector128<sbyte> loNibbles = AdvSimd.And(str, mask2F);
            Vector128<sbyte> hi = AdvSimd.Arm64.VectorTableLookup(lutHi, AdvSimd.And(hiNibbles, mask0F));
            Vector128<sbyte> lo = AdvSimd.Arm64.VectorTableLookup(lutLo, AdvSimd.And(loNibbles, mask0F));

            Console.WriteLine(hi);
            Console.WriteLine(lo);

            // Check for invalid input: if any "and" values from lo and hi are not zero,
            // fall back on bytewise code to do error checking and reporting:
            if (AdvSimd.Arm64.MaxAcross(AdvSimd.And(lo, hi)).ToScalar() != 0)
                break;

            Console.WriteLine(hi);
            Console.WriteLine(lo);

            Vector128<sbyte> eq2F = AdvSimd.CompareEqual(str, mask2F);
            Vector128<sbyte> shift = AdvSimd.Arm64.VectorTableLookup(lutShift, AdvSimd.And(AdvSimd.Add(eq2F, hiNibbles), mask0F));

            // Now simply add the delta values to the input:
            str = AdvSimd.Add(str, shift);

            Console.WriteLine(str);

            // in, bits, upper case are most significant bits, lower case are least significant bits
            // 00llllll 00kkkkLL 00jjKKKK 00JJJJJJ
            // 00iiiiii 00hhhhII 00ggHHHH 00GGGGGG
            // 00ffffff 00eeeeFF 00ddEEEE 00DDDDDD
            // 00cccccc 00bbbbCC 00aaBBBB 00AAAAAA

            // Vector128<short> wideLo = AdvSimd.MultiplyWideningLower(str.GetLower(), mergeConstant0.GetLower());
            // Vector128<short> wideHi = AdvSimd.MultiplyWideningUpper(str, mergeConstant0);

            // wideLo = AdvSimd.Arm64.UnzipEven(wideLo, wideHi);
            // wideHi = AdvSimd.Arm64.UnzipOdd(wideLo, wideHi);

            // Vector128<short> merge_ab_and_bc = AdvSimd.AddSaturate(wideLo, wideHi);

            Vector128<short> wideLo = AdvSimd.ZeroExtendWideningLower(str.GetLower());
            Vector128<short> wideHi = AdvSimd.ZeroExtendWideningLower(str.GetUpper());

            Vector128<short> constLo = AdvSimd.SignExtendWideningLower(mergeConstant0.GetLower());
            Vector128<short> constHi = AdvSimd.SignExtendWideningLower(mergeConstant0.GetUpper());

            var mulLo = AdvSimd.Multiply(wideLo, constLo);
            var mulHi = AdvSimd.Multiply(wideHi, constHi);

            mulLo = AdvSimd.Arm64.UnzipEven(mulLo, mulHi);
            mulHi = AdvSimd.Arm64.UnzipOdd(mulLo, mulHi);

            Vector128<short> merge_ab_and_bc = AdvSimd.Add(mulLo, mulHi);

            // 0000kkkk LLllllll 0000JJJJ JJjjKKKK
            // 0000hhhh IIiiiiii 0000GGGG GGggHHHH
            // 0000eeee FFffffff 0000DDDD DDddEEEE
            // 0000bbbb CCcccccc 0000AAAA AAaaBBBB

            Console.WriteLine(merge_ab_and_bc);

            Vector128<int> outputLo = AdvSimd.MultiplyWideningLower(merge_ab_and_bc.GetLower(), mergeConstant1.GetLower());
            Vector128<int> outputHi = AdvSimd.MultiplyWideningUpper(merge_ab_and_bc, mergeConstant1);

            outputLo = AdvSimd.Arm64.UnzipEven(outputLo, outputHi);
            outputHi = AdvSimd.Arm64.UnzipOdd(outputLo, outputHi);

            Vector128<int> output = AdvSimd.AddSaturate(outputLo, outputHi);
            // 00000000 JJJJJJjj KKKKkkkk LLllllll
            // 00000000 GGGGGGgg HHHHhhhh IIiiiiii
            // 00000000 DDDDDDdd EEEEeeee FFffffff
            // 00000000 AAAAAAaa BBBBbbbb CCcccccc
            Console.WriteLine(output);

            // Pack bytes together:
            str = AdvSimd.Arm64.VectorTableLookup(output.AsSByte(), AdvSimd.And(packBytesMask, mask0F));
            // 00000000 00000000 00000000 00000000
            // LLllllll KKKKkkkk JJJJJJjj IIiiiiii
            // HHHHhhhh GGGGGGgg FFffffff EEEEeeee
            // DDDDDDdd CCcccccc BBBBbbbb AAAAAAaa

            Console.WriteLine(str);

            AssertWrite<Vector128<sbyte>>(dest, destStart, destLength);
            AdvSimd.Store(dest, str.AsByte());

            src += 16;
            dest += 12;
        }
        while (src <= srcEnd);

        srcBytes = src;
        destBytes = dest;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void AdvSimd64Encode(ref byte* srcBytes, ref byte* destBytes, byte* srcEnd, int sourceLength, int destLength, byte* srcStart, byte* destStart)
    {
        byte* src = srcBytes;
        byte* dest = destBytes;

        var maskTopBits = Vector128.Create((byte)0x3F);
        var sixteen = Vector128.Create((byte)16);
        var thirtytwo = Vector128.Create((byte)32);
        var fourtyeight = Vector128.Create((byte)48);

        Vector128<byte> lookup0, lookup1, lookup2, lookup3;
        lookup0 = ReadVector<Vector128<byte>>(s_encodingMap);
        lookup1 = ReadVector<Vector128<byte>>(s_encodingMap.Slice(16));
        lookup2 = ReadVector<Vector128<byte>>(s_encodingMap.Slice(32));
        lookup3 = ReadVector<Vector128<byte>>(s_encodingMap.Slice(48));

        //while (remaining >= 48)
        do
        {
            AssertRead<Vector128<byte>>(src + 32, srcStart, sourceLength);
            // TODO translate this triple VLD1 to a single VLD3
            var str0 = AdvSimd.LoadVector128(src);
            var str1 = AdvSimd.LoadVector128(src + 16);
            var str2 = AdvSimd.LoadVector128(src + 32);

            Vector128<byte> res0, res1, res2, res3;
            Vector128<byte> int0, int1, int2, int3;

            // reshuffle

            int0 = AdvSimd.ShiftRightLogical(str0, 2);
            int1 = AdvSimd.Or(AdvSimd.ShiftRightLogical(str1, 4), AdvSimd.ShiftLeftLogical(str0, 4));
            int2 = AdvSimd.Or(AdvSimd.ShiftRightLogical(str2, 6), AdvSimd.ShiftLeftLogical(str1, 2));
            int3 = str2;

            int0 = AdvSimd.And(int0, maskTopBits);
            int1 = AdvSimd.And(int1, maskTopBits);
            int2 = AdvSimd.And(int2, maskTopBits);
            int3 = AdvSimd.And(int3, maskTopBits);

            // translate

            // The bits have now been shifted to the right locations;
            // translate their values 0..63 to the Base64 alphabet.
            // Use a 64-byte table lookup:

            Console.WriteLine(int0);
            Console.WriteLine(int1);
            Console.WriteLine(int2);
            Console.WriteLine(int3);

            Console.WriteLine(lookup0);
            Console.WriteLine(lookup1);
            Console.WriteLine(lookup2);
            Console.WriteLine(lookup3);

            res0 = AdvSimd.Arm64.VectorTableLookup(lookup0, int0);
            res0 = AdvSimd.Or(res0, AdvSimd.Arm64.VectorTableLookup(lookup1, AdvSimd.Subtract(int0, sixteen)));
            res0 = AdvSimd.Or(res0, AdvSimd.Arm64.VectorTableLookup(lookup2, AdvSimd.Subtract(int0, thirtytwo)));
            res0 = AdvSimd.Or(res0, AdvSimd.Arm64.VectorTableLookup(lookup3, AdvSimd.Subtract(int0, fourtyeight)));

            res1 = AdvSimd.Arm64.VectorTableLookup(lookup0, int1);
            res1 = AdvSimd.Or(res1, AdvSimd.Arm64.VectorTableLookup(lookup1, AdvSimd.Subtract(int1, sixteen)));
            res1 = AdvSimd.Or(res1, AdvSimd.Arm64.VectorTableLookup(lookup2, AdvSimd.Subtract(int1, thirtytwo)));
            res1 = AdvSimd.Or(res1, AdvSimd.Arm64.VectorTableLookup(lookup3, AdvSimd.Subtract(int1, fourtyeight)));

            res2 = AdvSimd.Arm64.VectorTableLookup(lookup0, int2);
            res2 = AdvSimd.Or(res2, AdvSimd.Arm64.VectorTableLookup(lookup1, AdvSimd.Subtract(int2, sixteen)));
            res2 = AdvSimd.Or(res2, AdvSimd.Arm64.VectorTableLookup(lookup2, AdvSimd.Subtract(int2, thirtytwo)));
            res2 = AdvSimd.Or(res2, AdvSimd.Arm64.VectorTableLookup(lookup3, AdvSimd.Subtract(int2, fourtyeight)));

            res3 = AdvSimd.Arm64.VectorTableLookup(lookup0, int3);
            res3 = AdvSimd.Or(res3, AdvSimd.Arm64.VectorTableLookup(lookup1, AdvSimd.Subtract(int3, sixteen)));
            res3 = AdvSimd.Or(res3, AdvSimd.Arm64.VectorTableLookup(lookup2, AdvSimd.Subtract(int3, thirtytwo)));
            res3 = AdvSimd.Or(res3, AdvSimd.Arm64.VectorTableLookup(lookup3, AdvSimd.Subtract(int3, fourtyeight)));

            Console.WriteLine(res0);
            Console.WriteLine(res1);
            Console.WriteLine(res2);
            Console.WriteLine(res3);

            // TODO translate this quadruple VST1 to a single VST4
            // Only assert last write
            AssertWrite<Vector128<sbyte>>(dest + 48, destStart, destLength);
            AdvSimd.Store(dest, res0);
            AdvSimd.Store(dest + 16, res1);
            AdvSimd.Store(dest + 32, res2);
            AdvSimd.Store(dest + 48, res3);

            src += 48;
            dest += 64;
        }
        while (src <= srcEnd);

        srcBytes = src;
        destBytes = dest;
    }

    private static ReadOnlySpan<byte> s_encodingMap => new byte[]
    {
        65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83,
        84, 85, 86, 87, 88, 89, 90, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106,
        107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120, 121,
        122, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 43, 47
    };


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void AvdSimd64Decode_VectorLookup(ref byte* srcBytes, ref byte* destBytes, byte* srcEnd, int sourceLength, int destLength, byte* srcStart, byte* destStart)
    {
        Console.WriteLine("AvdSimd64Decode_VectorLookup");

        byte* src = srcBytes;
        byte* dest = destBytes;

        var offset = Vector128.Create((byte)63);

        Vector128<byte> lookup0_0, lookup0_1, lookup0_2, lookup0_3;
        lookup0_0 = ReadVector<Vector128<byte>>(s_advSimd64DecodeLut1.Slice(16 * 0));
        lookup0_1 = ReadVector<Vector128<byte>>(s_advSimd64DecodeLut1.Slice(16 * 1));
        lookup0_2 = ReadVector<Vector128<byte>>(s_advSimd64DecodeLut1.Slice(16 * 2));
        lookup0_3 = ReadVector<Vector128<byte>>(s_advSimd64DecodeLut1.Slice(16 * 3));

        Vector128<byte> lookup1_0, lookup1_1, lookup1_2, lookup1_3;
        lookup1_0 = ReadVector<Vector128<byte>>(s_advSimd64DecodeLut2.Slice(16 * 0));
        lookup1_1 = ReadVector<Vector128<byte>>(s_advSimd64DecodeLut2.Slice(16 * 1));
        lookup1_2 = ReadVector<Vector128<byte>>(s_advSimd64DecodeLut2.Slice(16 * 2));
        lookup1_3 = ReadVector<Vector128<byte>>(s_advSimd64DecodeLut2.Slice(16 * 3));

        do
        {
            Console.WriteLine($"hi loop (src is {(IntPtr)src:X8}, srcEnd is {(IntPtr)srcEnd:X8}");
            Vector128<byte> int0, int1, int2, int3;
            Vector128<byte> tmp0, tmp1, tmp2, tmp3;
            Vector128<byte> res0, res1, res2;

            // Load 64 bytes and deinterleave:
            Vector128<byte> str0, str1, str2, str3;

            AssertRead<Vector128<byte>>(src + 48, srcStart, sourceLength);
            str0 = AdvSimd.LoadVector128(src);
            str1 = AdvSimd.LoadVector128(src + 16);
            str2 = AdvSimd.LoadVector128(src + 32);
            str3 = AdvSimd.LoadVector128(src + 48);

            // Get indices for 2nd LUT
            tmp0 = AdvSimd.SubtractSaturate(str0, offset);
            tmp1 = AdvSimd.SubtractSaturate(str1, offset);
            tmp2 = AdvSimd.SubtractSaturate(str2, offset);
            tmp3 = AdvSimd.SubtractSaturate(str3, offset);

            Console.WriteLine(tmp0);
            Console.WriteLine(tmp1);
            Console.WriteLine(tmp2);
            Console.WriteLine(tmp2);

            // Get values from 1st LUT
            int0 = AdvSimd.Arm64.VectorTableLookup(lookup0_0, str0);
            int0 = AdvSimd.Or(int0, AdvSimd.Arm64.VectorTableLookup(lookup0_1, str0));
            int0 = AdvSimd.Or(int0, AdvSimd.Arm64.VectorTableLookup(lookup0_2, str0));
            int0 = AdvSimd.Or(int0, AdvSimd.Arm64.VectorTableLookup(lookup0_3, str0));

            int1 = AdvSimd.Arm64.VectorTableLookup(lookup0_0, str1);
            int1 = AdvSimd.Or(int1, AdvSimd.Arm64.VectorTableLookup(lookup0_1, str1));
            int1 = AdvSimd.Or(int1, AdvSimd.Arm64.VectorTableLookup(lookup0_2, str1));
            int1 = AdvSimd.Or(int1, AdvSimd.Arm64.VectorTableLookup(lookup0_3, str1));

            int2 = AdvSimd.Arm64.VectorTableLookup(lookup0_0, str2);
            int2 = AdvSimd.Or(int2, AdvSimd.Arm64.VectorTableLookup(lookup0_1, str2));
            int2 = AdvSimd.Or(int2, AdvSimd.Arm64.VectorTableLookup(lookup0_2, str2));
            int2 = AdvSimd.Or(int2, AdvSimd.Arm64.VectorTableLookup(lookup0_3, str2));

            int3 = AdvSimd.Arm64.VectorTableLookup(lookup0_0, str3);
            int3 = AdvSimd.Or(int3, AdvSimd.Arm64.VectorTableLookup(lookup0_1, str3));
            int3 = AdvSimd.Or(int3, AdvSimd.Arm64.VectorTableLookup(lookup0_2, str3));
            int3 = AdvSimd.Or(int3, AdvSimd.Arm64.VectorTableLookup(lookup0_3, str3));

            Console.WriteLine(int0);
            Console.WriteLine(int1);
            Console.WriteLine(int2);
            Console.WriteLine(int3);

            // Get values from 2nd LUT
            var cp0 = tmp0;
            var cp1 = tmp1;
            var cp2 = tmp2;
            var cp3 = tmp3;

            tmp0 = AdvSimd.Arm64.VectorTableLookupExtension(cp0, lookup1_0, cp0);
            tmp0 = AdvSimd.Or(tmp0, AdvSimd.Arm64.VectorTableLookupExtension(cp0, lookup1_1, cp0));
            tmp0 = AdvSimd.Or(tmp0, AdvSimd.Arm64.VectorTableLookupExtension(cp0, lookup1_2, cp0));
            tmp0 = AdvSimd.Or(tmp0, AdvSimd.Arm64.VectorTableLookupExtension(cp0, lookup1_3, cp0));

            tmp1 = AdvSimd.Arm64.VectorTableLookupExtension(cp1, lookup1_0, cp1);
            tmp1 = AdvSimd.Or(tmp1, AdvSimd.Arm64.VectorTableLookupExtension(cp1, lookup1_1, cp1));
            tmp1 = AdvSimd.Or(tmp1, AdvSimd.Arm64.VectorTableLookupExtension(cp1, lookup1_2, cp1));
            tmp1 = AdvSimd.Or(tmp1, AdvSimd.Arm64.VectorTableLookupExtension(cp1, lookup1_3, cp1));

            tmp2 = AdvSimd.Arm64.VectorTableLookupExtension(cp2, lookup1_0, cp2);
            tmp2 = AdvSimd.Or(tmp2, AdvSimd.Arm64.VectorTableLookupExtension(cp2, lookup1_1, cp2));
            tmp2 = AdvSimd.Or(tmp2, AdvSimd.Arm64.VectorTableLookupExtension(cp2, lookup1_2, cp2));
            tmp2 = AdvSimd.Or(tmp2, AdvSimd.Arm64.VectorTableLookupExtension(cp2, lookup1_3, cp2));

            tmp3 = AdvSimd.Arm64.VectorTableLookupExtension(cp3, lookup1_0, cp3);
            tmp3 = AdvSimd.Or(tmp3, AdvSimd.Arm64.VectorTableLookupExtension(cp3, lookup1_1, cp3));
            tmp3 = AdvSimd.Or(tmp3, AdvSimd.Arm64.VectorTableLookupExtension(cp3, lookup1_2, cp3));
            tmp3 = AdvSimd.Or(tmp3, AdvSimd.Arm64.VectorTableLookupExtension(cp3, lookup1_3, cp3));

            // Get final values
            str0 = AdvSimd.Or(int0, tmp0);
            str1 = AdvSimd.Or(int1, tmp1);
            str2 = AdvSimd.Or(int2, tmp2);
            str3 = AdvSimd.Or(int3, tmp3);

            // Check for invalid input, any value larger than 63:
            Vector128<byte> classified = AdvSimd.CompareGreaterThan(str0, offset);
            classified = AdvSimd.Or(classified, AdvSimd.CompareGreaterThan(str1, offset));
            classified = AdvSimd.Or(classified, AdvSimd.CompareGreaterThan(str2, offset));
            classified = AdvSimd.Or(classified, AdvSimd.CompareGreaterThan(str3, offset));

            // check that all bits are zero:
            if (AdvSimd.Arm64.MaxAcross(classified).ToScalar() != 0U)
            {
                Console.WriteLine(str0);
                Console.WriteLine(str1);
                Console.WriteLine(str2);
                Console.WriteLine(str3);
                Console.WriteLine("Bad data in _Vl loop");
                break;
            }

            // Compress four bytes into three:
            res0 = AdvSimd.Or(AdvSimd.ShiftLeftLogical(str0, 2), AdvSimd.ShiftRightLogical(str1, 4));
            res1 = AdvSimd.Or(AdvSimd.ShiftLeftLogical(str1, 4), AdvSimd.ShiftRightLogical(str2, 2));
            res2 = AdvSimd.Or(AdvSimd.ShiftLeftLogical(str2, 6), str3);

            // TODO translate this triple VST1 to a single VST3
            // Only assert last write
            AssertWrite<Vector128<sbyte>>(dest + 32, destStart, destLength);
            AdvSimd.Store(dest, res0);
            AdvSimd.Store(dest + 16, res1);
            AdvSimd.Store(dest + 32, res2);

            src += 64;
            dest += 48;
        }
        while (src <= srcEnd);

        srcBytes = src;
        destBytes = dest;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe int Decode(byte* encodedBytes, ref sbyte decodingMap)
    {
        uint t0 = encodedBytes[0];
        uint t1 = encodedBytes[1];
        uint t2 = encodedBytes[2];
        uint t3 = encodedBytes[3];

        int i0 = Unsafe.Add(ref decodingMap, (IntPtr)t0);
        int i1 = Unsafe.Add(ref decodingMap, (IntPtr)t1);
        int i2 = Unsafe.Add(ref decodingMap, (IntPtr)t2);
        int i3 = Unsafe.Add(ref decodingMap, (IntPtr)t3);

        i0 <<= 18;
        i1 <<= 12;
        i2 <<= 6;

        i0 |= i3;
        i1 |= i2;

        i0 |= i1;
        return i0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void WriteThreeLowOrderBytes(byte* destination, int value)
    {
        destination[0] = (byte)(value >> 16);
        destination[1] = (byte)(value >> 8);
        destination[2] = (byte)(value);
    }

    private static ReadOnlySpan<sbyte> s_decodingMap => new sbyte[] {
                -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
                -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
                -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 62, -1, -1, -1, 63,         //62 is placed at index 43 (for +), 63 at index 47 (for /)
                52, 53, 54, 55, 56, 57, 58, 59, 60, 61, -1, -1, -1, -1, -1, -1,         //52-61 are placed at index 48-57 (for 0-9), 64 at index 61 (for =)
                -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14,
                15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, -1, -1, -1, -1, -1,         //0-25 are placed at index 65-90 (for A-Z)
                -1, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40,
                41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, -1, -1, -1, -1, -1,         //26-51 are placed at index 97-122 (for a-z)
                -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,         // Bytes over 122 ('z') are invalid and cannot be decoded
                -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,         // Hence, padding the map with 255, which indicates invalid input
                -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
                -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
                -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
                -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
                -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
                -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            };

    private static ReadOnlySpan<sbyte> s_sseDecodePackBytesMask => new sbyte[] {
                2, 1, 0, 6,
                5, 4, 10, 9,
                8, 14, 13, 12,
                -1, -1, -1, -1
            };

    private static ReadOnlySpan<sbyte> s_sseDecodeLutLo => new sbyte[] {
                0x15, 0x11, 0x11, 0x11,
                0x11, 0x11, 0x11, 0x11,
                0x11, 0x11, 0x13, 0x1A,
                0x1B, 0x1B, 0x1B, 0x1A
            };

    private static ReadOnlySpan<sbyte> s_sseDecodeLutHi => new sbyte[] {
                0x10, 0x10, 0x01, 0x02,
                0x04, 0x08, 0x04, 0x08,
                0x10, 0x10, 0x10, 0x10,
                0x10, 0x10, 0x10, 0x10
            };

    private static ReadOnlySpan<sbyte> s_sseDecodeLutShift => new sbyte[] {
                0, 16, 19, 4,
                -65, -65, -71, -71,
                0, 0, 0, 0,
                0, 0, 0, 0
            };

    private static ReadOnlySpan<sbyte> s_avxDecodePackBytesInLaneMask => new sbyte[] {
                2, 1, 0, 6,
                5, 4, 10, 9,
                8, 14, 13, 12,
                -1, -1, -1, -1,
                2, 1, 0, 6,
                5, 4, 10, 9,
                8, 14, 13, 12,
                -1, -1, -1, -1
            };

    private static ReadOnlySpan<sbyte> s_avxDecodePackLanesControl => new sbyte[] {
                0, 0, 0, 0,
                1, 0, 0, 0,
                2, 0, 0, 0,
                4, 0, 0, 0,
                5, 0, 0, 0,
                6, 0, 0, 0,
                -1, -1, -1, -1,
                -1, -1, -1, -1
            };

    private static ReadOnlySpan<sbyte> s_avxDecodeLutLo => new sbyte[] {
                0x15, 0x11, 0x11, 0x11,
                0x11, 0x11, 0x11, 0x11,
                0x11, 0x11, 0x13, 0x1A,
                0x1B, 0x1B, 0x1B, 0x1A,
                0x15, 0x11, 0x11, 0x11,
                0x11, 0x11, 0x11, 0x11,
                0x11, 0x11, 0x13, 0x1A,
                0x1B, 0x1B, 0x1B, 0x1A
            };

    private static ReadOnlySpan<sbyte> s_avxDecodeLutHi => new sbyte[] {
                0x10, 0x10, 0x01, 0x02,
                0x04, 0x08, 0x04, 0x08,
                0x10, 0x10, 0x10, 0x10,
                0x10, 0x10, 0x10, 0x10,
                0x10, 0x10, 0x01, 0x02,
                0x04, 0x08, 0x04, 0x08,
                0x10, 0x10, 0x10, 0x10,
                0x10, 0x10, 0x10, 0x10
            };

    private static ReadOnlySpan<sbyte> s_avxDecodeLutShift => new sbyte[] {
                0, 16, 19, 4,
                -65, -65, -71, -71,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 16, 19, 4,
                -65, -65, -71, -71,
                0, 0, 0, 0,
                0, 0, 0, 0
            };

    private static ReadOnlySpan<byte> s_advSimd64DecodeLut1 => new byte[]
    {
                255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
                255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
                255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 62, 255, 255, 255, 63,
                52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 255, 255, 255, 255, 255, 255
    };

    private static ReadOnlySpan<byte> s_advSimd64DecodeLut2 => new byte[]
    {
                0, 255,  0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13,
                14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 255, 255, 255, 255,
                255, 255, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39,
                40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 255, 255, 255, 255
    };
}
