/*
 * Initial Implementation Credit by: @Shatyuka
 */

using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedVariable

namespace CollapseLauncher.GameSettings.Zenless;

#nullable enable
internal static class Sleepy
{
    // https://github.com/dotnet/runtime/blob/a7efcd9ca9255dc9faa8b4a2761cdfdb62619610/src/libraries/System.Runtime.Serialization.Formatters/src/System/Runtime/Serialization/Formatters/Binary/BinaryEnums.cs#L7C1-L32C6
    private enum BinaryHeaderEnum
    {
        SerializedStreamHeader    = 0,
        Object                    = 1,
        ObjectWithMap             = 2,
        ObjectWithMapAssemId      = 3,
        ObjectWithMapTyped        = 4,
        ObjectWithMapTypedAssemId = 5,
        ObjectString              = 6,
        Array                     = 7,
        MemberPrimitiveTyped      = 8,
        MemberReference           = 9,
        ObjectNull                = 10,
        MessageEnd                = 11,
        Assembly                  = 12,
        ObjectNullMultiple256     = 13,
        ObjectNullMultiple        = 14,
        ArraySinglePrimitive      = 15,
        ArraySingleObject         = 16,
        ArraySingleString         = 17,
        CrossAppDomainMap         = 18,
        CrossAppDomainString      = 19,
        CrossAppDomainAssembly    = 20,
        MethodCall                = 21,
        MethodReturn              = 22,
        BinaryReference           = -1
    }

    // https://github.com/dotnet/runtime/blob/a7efcd9ca9255dc9faa8b4a2761cdfdb62619610/src/libraries/System.Runtime.Serialization.Formatters/src/System/Runtime/Serialization/Formatters/Binary/BinaryEnums.cs#L35
    private enum BinaryTypeEnum
    {
        Primitive      = 0,
        String         = 1,
        Object         = 2,
        ObjectUrt      = 3,
        ObjectUser     = 4,
        ObjectArray    = 5,
        StringArray    = 6,
        PrimitiveArray = 7
    }

    // https://github.com/dotnet/runtime/blob/a7efcd9ca9255dc9faa8b4a2761cdfdb62619610/src/libraries/System.Runtime.Serialization.Formatters/src/System/Runtime/Serialization/Formatters/Binary/BinaryEnums.cs#L47
    private enum BinaryArrayTypeEnum
    {
        Single            = 0,
        Jagged            = 1,
        Rectangular       = 2,
        SingleOffset      = 3,
        JaggedOffset      = 4,
        RectangularOffset = 5
    }

    // https://github.com/dotnet/runtime/blob/a7efcd9ca9255dc9faa8b4a2761cdfdb62619610/src/libraries/System.Runtime.Serialization.Formatters/src/System/Runtime/Serialization/Formatters/Binary/BinaryEnums.cs#L99
    private enum InternalArrayTypeE
    {
        Empty       = 0,
        Single      = 1,
        Jagged      = 2,
        Rectangular = 3,
        Base64      = 4
    }

    internal static string ReadString(string filePath, ReadOnlySpan<byte> magic)
    {
        // Get the FileInfo
        FileInfo fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
            throw new FileNotFoundException("[Sleepy::ReadString] File does not exist!");

        // Open the stream and get the thing
        using FileStream stream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        return ReadString(stream, magic);
    }

    internal static unsafe string ReadString(Stream stream, ReadOnlySpan<byte> magic)
    {
        // Stream assertion
        if (!stream.CanRead) throw new ArgumentException("[Sleepy::ReadString] Stream must be readable!", nameof(stream));

        // Assign the reader
        using BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);

        // Emulate and Assert the BinaryFormatter header info
        reader.EmulateSleepyBinaryFormatterHeaderAssertion();

        // Get the data length
        int length      = reader.GetBinaryFormatterDataLength();
        int magicLength = magic.Length;

        // Alloc temporary buffers
        bool   isRent      = length <= 1 << 17; // Check if length <= 128 KiB
        char[] bufferChars = isRent ? ArrayPool<char>.Shared.Rent(length) : new char[length];

        // Do the do
        CreateEvil(magic, out bool[] evil, out int evilsCount);
        fixed (bool* evp = &evil[0])
            fixed (char* bp = &bufferChars[0])
            {
                try
                {
                    // Do the do (pt. 2)
                    int j = InternalDecode(magic, evp, reader, length, magicLength, bp);

                    // Emulate and Assert the BinaryFormatter footer
                    reader.EmulateSleepyBinaryFormatterFooterAssertion();

                    // Return
                    return new string(bp, 0, j);
                }
                finally
                {
                    // Return and clear the buffer, to only returns the return string.
                    if (isRent) ArrayPool<char>.Shared.Return(bufferChars, true);
                    else Array.Clear(bufferChars);
                }
            }
    }

    private static unsafe int InternalDecode(ReadOnlySpan<byte> magic, bool* evil, BinaryReader reader, int length, int magicLength, char* bp)
    {
        bool eepy = false;

        int j = 0;
        int i = 0;

        amimir:
        var  n  = i % magicLength;
        byte c  = reader.ReadByte();
        byte ch = (byte)(c ^ magic[n]);

        if (*(evil + n))
        {
            eepy = ch != 0;
        }
        else
        {
            if (eepy)
            {
                ch   += 0x40;
                eepy =  false;
            }
            *(bp + j++) = (char)ch;
        }

        if (++i < length) goto amimir;
        return j;
    }

    internal static void WriteString(string filePath, ReadOnlySpan<char> content, ReadOnlySpan<byte> magic)
    {
        // Ensure the folder always exist
        string? fileDir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(fileDir) && !Directory.Exists(fileDir))
        {
            Directory.CreateDirectory(fileDir);
        }

        // Get the FileInfo
        FileInfo fileInfo = new FileInfo(filePath);

        // Create the stream and write the thing
        using FileStream stream = fileInfo.Open(FileMode.Create, FileAccess.Write, FileShare.Write);
        WriteString(stream, content, magic);
    }

    internal static unsafe void WriteString(Stream stream, ReadOnlySpan<char> content, ReadOnlySpan<byte> magic)
    {
        // Stream assertion
        if (!stream.CanWrite) throw new ArgumentException("[Sleepy::WriteString] Stream must be writable!", nameof(stream));

        // Magic assertion
        if (magic.Length == 0) throw new ArgumentException("[Sleepy::WriteString] Magic cannot be empty!", nameof(magic));

        // Assign the writer
        using BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);

        // Emulate to write the BinaryFormatter header
        writer.EmulateSleepyBinaryFormatterHeaderWrite();

        // Do the do
        int  contentLen = content.Length;
        int  bufferLen  = contentLen * 2;
        bool isRent     = bufferLen <= 2 << 17;

        // Alloc temporary buffers
        byte[] contentBytes = isRent ? ArrayPool<byte>.Shared.Rent(bufferLen) : new byte[bufferLen];
        byte[] encodedBytes = isRent ? ArrayPool<byte>.Shared.Rent(bufferLen) : new byte[bufferLen];

        // Do the do
        CreateEvil(magic, out bool[] evil, out int evilsCount);

        fixed (char* cp = &content[0])
            fixed (byte* bp = &contentBytes[0])
                fixed (byte* ep = &encodedBytes[0])
                    fixed (bool* evp = &evil[0])
                    {
                        try
                        {
                            // Get the string bytes
                            _ = Encoding.UTF8.GetBytes(cp, contentLen, bp, bufferLen);

                            // Do the do (pt. 2)
                            int h = InternalWrite(magic, contentLen, bp, ep, evp);

                            writer.Write7BitEncodedInt(h);
                            writer.BaseStream.Write(encodedBytes, 0, h);
                            writer.EmulateSleepyBinaryFormatterFooterWrite();
                        }
                        finally
                        {
                            // Return and clear the buffer.
                            if (isRent) ArrayPool<byte>.Shared.Return(contentBytes, true);
                            else Array.Clear(contentBytes);

                            if (isRent) ArrayPool<byte>.Shared.Return(encodedBytes, true);
                            else Array.Clear(encodedBytes);
                        }
                    }
    }

    private static unsafe int InternalWrite(ReadOnlySpan<byte> magic, int contentLen, byte* bp, byte* ep, bool* evil)
    {
        int h = 0;
        int i = 0;
        int j = 0;

        amimir:
        int  n  = i % magic.Length;
        byte ch = *(bp + j);
        if (*(evil + n))
        {
            byte eepy = 0;
            if (*(bp + j) > 0x40)
            {
                ch   -= 0x40;
                eepy =  1;
            }
            *(ep + h++) = (byte)(eepy ^ magic[n]);

            n = ++i % magic.Length;
        }

        *(ep + h++) = (byte)(ch ^ magic[n]);
        ++i;
        ++j;
        if (j < contentLen) goto amimir;
        return h;
    }

    private static void CreateEvil(ReadOnlySpan<byte> magic, out bool[] evilist, out int evilsCount)
    {
        int magicLength = magic.Length;
        int i           = 0;
        evilist    = new bool[magicLength];
        evilsCount = 0;
        evilist:
        int n = i % magicLength;
        evilist[i] = (magic[n] & 0xC0) == 0xC0;
        if (evilist[i]) ++evilsCount;
        if (++i < magicLength) goto evilist;
    }

    private static void EmulateSleepyBinaryFormatterHeaderAssertion(this BinaryReader reader)
    {
        // Do assert [class] -> [string object]
        // START!
        // Check if the first byte is SerializedStreamHeader
        reader.LogAssertInfoByteEnum(BinaryHeaderEnum.SerializedStreamHeader);

        // Check if the type is an Object
        reader.LogAssertInfoInt32Enum(BinaryHeaderEnum.Object);

        // Check if the type is a BinaryReference
        reader.LogAssertInfoInt32Enum(BinaryHeaderEnum.BinaryReference);

        // Check if the BinaryReference type is a String
        reader.LogAssertInfoInt32Enum(BinaryTypeEnum.String);

        // Check for the binary array type and check if it's Single
        reader.LogAssertInfoInt32Enum(BinaryArrayTypeEnum.Single);

        // Check for the binary type and check if it's StringArray (UTF-8)
        reader.LogAssertInfoByteEnum(BinaryTypeEnum.StringArray);

        // Check for the internal array type and check if it's Single
        reader.LogAssertInfoInt32Enum(InternalArrayTypeE.Single);
    }

    // Do assert [class] -> [EOF mark]
    // START!
    private static void EmulateSleepyBinaryFormatterFooterAssertion(this BinaryReader reader) =>
        reader.LogAssertInfoByteEnum(BinaryHeaderEnum.MessageEnd);

    private static void EmulateSleepyBinaryFormatterHeaderWrite(this BinaryWriter writer)
    {
        // Emulate to write Sleepy BinaryFormatter header information
        writer.WriteEnumAsByte(BinaryHeaderEnum.SerializedStreamHeader);
        writer.WriteEnumAsInt32(BinaryHeaderEnum.Object);
        writer.WriteEnumAsInt32(BinaryHeaderEnum.BinaryReference);
        writer.WriteEnumAsInt32(BinaryTypeEnum.String);
        writer.WriteEnumAsInt32(BinaryArrayTypeEnum.Single);
        writer.WriteEnumAsByte(BinaryTypeEnum.StringArray);
        writer.WriteEnumAsInt32(InternalArrayTypeE.Single);
    }

    // Emulate to write Sleepy BinaryFormatter footer EOF
    private static void EmulateSleepyBinaryFormatterFooterWrite(this BinaryWriter writer) =>
        writer.WriteEnumAsByte(BinaryHeaderEnum.MessageEnd);

    private static void WriteEnumAsByte<T>(this BinaryWriter writer, T headerEnum)
        where T : struct, Enum
    {
        int enumValue = Unsafe.As<T, int>(ref headerEnum);
        writer.Write((byte)enumValue);
    }

    private static void WriteEnumAsInt32<T>(this BinaryWriter writer, T headerEnum)
        where T : struct, Enum
    {
        int enumValue = Unsafe.As<T, int>(ref headerEnum);
        writer.Write(enumValue);
    }

    private static void LogAssertInfoByteEnum<T>(this BinaryReader stream, T assertHeaderEnum)
        where T : struct, Enum
    {
        int currentInt = stream.ReadByte();
        LogAssertInfo(stream, ref assertHeaderEnum, ref currentInt);
    }

    private static void LogAssertInfoInt32Enum<T>(this BinaryReader stream, T assertHeaderEnum)
        where T : struct, Enum
    {
        int currentInt = stream.ReadInt32();
        LogAssertInfo(stream, ref assertHeaderEnum, ref currentInt);
    }

    private static void LogAssertInfo<T>(BinaryReader reader, ref T assertHeaderEnum, ref int currentInt)
        where T : struct, Enum
    {
        int intAssertCasted = Unsafe.As<T, int>(ref assertHeaderEnum);
        if (intAssertCasted != currentInt)
        {
            string? assertHeaderEnumValueName   = Enum.GetName(assertHeaderEnum);
            T       comparedEnumCasted          = Unsafe.As<int, T>(ref currentInt);
            string? comparedHeaderEnumValueName = Enum.GetName(comparedEnumCasted);

            throw new InvalidDataException($"[Sleepy::LogAssertInfo] BinaryFormatter header is not valid at stream pos: {reader.BaseStream.Position:x8}. Expecting object enum: {assertHeaderEnumValueName} but getting: {comparedHeaderEnumValueName} instead!");
        }
    }

    private static int GetBinaryFormatterDataLength(this BinaryReader reader) => reader.Read7BitEncodedInt();
}