using Hi3Helper;
using System;
using System.Buffers;
using System.Buffers.Text;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

// ReSharper disable IdentifierTypo
using ZstdDecompressStream = ZstdNet.DecompressionStream;

namespace CollapseLauncher.Helper.Metadata
{
    internal enum CompressionType : byte
    {
        None,
        Brotli,
        Zstd
    }

    internal static class DataCooker
    {
        private const long CollapseSignature     = 7310310183885631299;
        private const int  AllowedBufferPoolSize = 1 << 20; // 1 MiB

        internal static RSA        RsaInstance;

        internal static string ServeV3Data(string data)
        {
            if (!Base64.IsValid(data))
            {
                return data;
            }

            byte[] dataBytes = Convert.FromBase64String(data);
            if (!IsServeV3Data(dataBytes))
            {
                return data;
            }

            GetServeV3DataSize(dataBytes, out long compressedSize, out long decompressedSize);
            byte[] outBuffer = new byte[decompressedSize];
            ServeV3Data(dataBytes, outBuffer, (int)compressedSize, (int)decompressedSize, out int dataWritten);
            return Encoding.UTF8.GetString(outBuffer.AsSpan(0, dataWritten));

        }

        internal static bool IsServeV3Data(ReadOnlySpan<byte> data)
        {
            if (data.Length < 8)
            {
                return false;
            }

            long signature = MemoryMarshal.Read<long>(data);
            return signature == CollapseSignature;
        }

        internal static void GetServeV3DataSize(ReadOnlySpan<byte> data, out long compressedSize,
                                                out long           decompressedSize)
        {
            if (data.Length < 32)
            {
                throw new FormatException("The MetadataV3 data format is corrupted!");
            }

            compressedSize   = MemoryMarshal.Read<long>(data.Slice(16));
            decompressedSize = MemoryMarshal.Read<long>(data.Slice(24));
        }

        private static void GetServeV3Attribute(ReadOnlySpan<byte> data, out CompressionType compressionType,
                                                out bool           isUseEncryption)
        {
            long attribNumber = MemoryMarshal.Read<long>(data.Slice(sizeof(long)));

            compressionType = (CompressionType)(byte)attribNumber;
            isUseEncryption = (byte)(attribNumber >> 8) == 1;
        }


        internal static void ServeV3Data(ReadOnlySpan<byte> data,             Span<byte> outData, int compressedSize,
                                         int                decompressedSize, out int    dataWritten)
        {
            GetServeV3Attribute(data, out CompressionType compressionType, out bool isUseEncryption);
            const int readOffset = sizeof(long) * 4;

            ReadOnlySpan<byte> dataRawBuffer     = data[readOffset..];
            byte[]             decryptedDataSpan = null;
            int                encBitLength      = LauncherMetadataHelper.CurrentMasterKey?.BitSize ?? 0;

            bool isDecryptPoolAllowed = dataRawBuffer.Length <= AllowedBufferPoolSize;
            bool isDecryptPoolUsed    = false;

            try
            {
                if (isUseEncryption)
                {
                    if (LauncherMetadataHelper.CurrentMasterKey == null)
                        throw new NullReferenceException("Master key is null or empty!");

                    decryptedDataSpan = isDecryptPoolAllowed
                        ? ArrayPool<byte>.Shared.Rent(dataRawBuffer.Length)
                        : new byte[dataRawBuffer.Length];
                    isDecryptPoolUsed = isDecryptPoolAllowed;

                    if (RsaInstance == null)
                    {
                        RsaInstance = RSA.Create();
                        byte[] key;
                        if (IsServeV3Data(LauncherMetadataHelper.CurrentMasterKey?.Key))
                        {
                            GetServeV3DataSize(LauncherMetadataHelper.CurrentMasterKey?.Key, out long keyCompSize,
                                               out long keyDecompSize);
                            key = new byte[keyCompSize];
                            ServeV3Data(LauncherMetadataHelper.CurrentMasterKey?.Key, key, (int)keyCompSize,
                                        (int)keyDecompSize,                          out _);
                        }
                        else
                        {
                            key = LauncherMetadataHelper.CurrentMasterKey?.Key;
                        }

                        RsaInstance.ImportRSAPrivateKey(key, out _);
                    }

                    int offset    = 0;
                    int offsetOut = 0;
                    while (offset < dataRawBuffer.Length)
                    {
                        int decryptWritten = RsaInstance.Decrypt(dataRawBuffer.Slice(offset, encBitLength),
                                                                 decryptedDataSpan.AsSpan(offsetOut),
                                                                 RSAEncryptionPadding.Pkcs1);
                        offsetOut += decryptWritten;
                        offset    += encBitLength;
                    }

                    dataRawBuffer = decryptedDataSpan.AsSpan(0, offsetOut);
                }

                if (dataRawBuffer.Length != compressedSize)
                {
                    throw new DataMisalignedException("RAW data is misaligned!");
                }

                switch (compressionType)
                {
                    case CompressionType.None:
                        if (!isUseEncryption)
                        {
                            data.Slice(readOffset, decompressedSize).CopyTo(outData);
                        }

                        dataWritten = decompressedSize;
                        break;
                    case CompressionType.Brotli:
                        dataWritten = DecompressDataFromBrotli(outData, compressedSize, decompressedSize, dataRawBuffer);
                        break;
                    case CompressionType.Zstd:
                        dataWritten = DecompressDataFromZstd(outData, decompressedSize, dataRawBuffer);
                        break;
                    default:
                        throw new FormatException($"Decompression format is not supported! ({compressionType})");
                }

            #if DEBUG
                Logger.LogWriteLine($"[DataCooker::ServeV3Data()] Loaded ServeV3 data [IsPooled: {isDecryptPoolUsed}][TCompress: {compressionType} | IsEncrypt: {isUseEncryption}][CompSize: {compressedSize} | UncompSize: {decompressedSize}]",
                                    LogType.Debug, true);
            #endif
            }
            finally
            {
                if (isDecryptPoolAllowed && isDecryptPoolUsed && decryptedDataSpan != null)
                {
                    ArrayPool<byte>.Shared.Return(decryptedDataSpan, true);
                }
            }
        }

        private static int DecompressDataFromBrotli(Span<byte> outData, int compressedSize, int decompressedSize, ReadOnlySpan<byte> dataRawBuffer)
        {
            BrotliDecoder decoder = new BrotliDecoder();

            int offset = 0;
            int decompressedWritten = 0;
            while (offset < compressedSize)
            {
                decoder.Decompress(dataRawBuffer.Slice(offset), outData.Slice(decompressedWritten),
                                   out int dataConsumedWritten, out int dataDecodedWritten);
                decompressedWritten += dataDecodedWritten;
                offset += dataConsumedWritten;
            }

            if (decompressedSize != decompressedWritten)
            {
                throw new DataMisalignedException("Decompressed data is misaligned!");
            }

            return decompressedWritten;
        }

        private static unsafe int DecompressDataFromZstd(Span<byte> outData, int decompressedSize, ReadOnlySpan<byte> dataRawBuffer)
        {
            fixed (byte* inputBuffer = &dataRawBuffer[0])
                fixed (byte* outputBuffer = &outData[0])
                {
                    int decompressedWritten = 0;

                    byte[] buffer = new byte[4 << 10];

                    using UnmanagedMemoryStream inputStream = new UnmanagedMemoryStream(inputBuffer, dataRawBuffer.Length);
                    using UnmanagedMemoryStream outputStream = new UnmanagedMemoryStream(outputBuffer, outData.Length);
                    using ZstdDecompressStream decompStream = new ZstdDecompressStream(inputStream);

                    int read;
                    while ((read = decompStream.Read(buffer)) > 0)
                    {
                        outputStream.Write(buffer, 0, read);
                        decompressedWritten += read;
                    }

                    if (decompressedSize != decompressedWritten)
                    {
                        throw new DataMisalignedException("Decompressed data is misaligned!");
                    }

                    return decompressedWritten;
                }
        }
    }
}