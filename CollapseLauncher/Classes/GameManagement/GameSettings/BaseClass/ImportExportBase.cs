using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.UABT;
using Hi3Helper.UABT.Binary;
using Hi3Helper.Win32.FileDialogCOM;
using Microsoft.Win32;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Locale;

#nullable enable
namespace CollapseLauncher.GameSettings.Base
{
    internal class ImportExportBase
    {
        private const byte XorKey = 69;
        public async Task<Exception?> ImportSettings(string? gameBasePath = null)
        {
            try
            {
                string path = await FileDialogNative.GetFilePicker(new Dictionary<string, string> { { "Collapse Registry", "*.clreg" } }, Lang._GameSettingsPage.SettingsRegImportTitle);

                if (string.IsNullOrEmpty(path)) throw new OperationCanceledException(Lang._GameSettingsPage.SettingsRegErr1);

                await using FileStream fs   = new FileStream(path, FileMode.Open, FileAccess.Read);
                byte[]           head = new byte[6];
                _ = fs.Read(head, 0, head.Length);

                Logger.LogWriteLine($"Importing registry {RegistryPath}...");

                string header = Encoding.UTF8.GetString(head);
                switch (header)
                {
                    case "clReg\0":
                        ReadLegacyValues(fs);
                        break;
                    case "ColReg":
                        ReadNewerValues(fs, gameBasePath);
                        break;
                    default:
                        throw new FormatException(Lang._GameSettingsPage.SettingsRegErr2);
                }
            }
            catch (Exception ex)
            {
                // Gets caught by the calling method
                return ex;
            }

            return null;
        }

        private void ReadNewerValues(Stream fs, string? gameBasePath)
        {
            byte version = (byte)fs.ReadByte();

            switch (version)
            {
                case 1:
                    using (XORStream xorS = new XORStream(fs, XorKey, true))
                        using (BrotliStream comp = new BrotliStream(xorS, CompressionMode.Decompress, true))
                        {
                            ReadLegacyValues(comp);
                        }
                    break;
                case 2:
                    ReadV2Values(fs);
                    break;
                case 3:
                    ReadV3Values(fs, gameBasePath);
                    break;
                default:
                    throw new FormatException($"Registry version is not supported! Read: {version}");
            }
        }

        private void ReadLegacyValues(Stream fs)
        {
            using EndianBinaryReader reader = new EndianBinaryReader(fs, EndianType.BigEndian, true);
            short                    count  = reader.ReadInt16();
            Logger.LogWriteLine($"File has {count} values.");
            while (count-- > 0)
            {
                string valueName = ReadValueName(reader);
                byte   type      = reader.ReadByte();
                switch (type)
                {
                    case 0:
                        ReadDWord(reader, valueName); break;
                    case 1:
                        ReadQWord(reader, valueName); break;
                    case 2:
                        ReadString(reader, valueName); break;
                    case 3:
                        ReadBinary(reader, valueName); break;
                }
                Logger.LogWriteLine($"Value {valueName} imported!");
            }
        }

        private void ReadV2Values(Stream fs)
        {
            bool isCompressed = fs.ReadByte() > 0;

            Logger.LogWriteLine($"Exporting registry V2 {RegistryPath}...");
            int count = ReadInt32(fs);
            using Stream stream = isCompressed ? new BrotliStream(fs, CompressionMode.Decompress) : fs;
            do
            {
                --count;
                string valueName = ReadValueName(stream);
                EnsureReadImpostorData(stream, valueName);
                Logger.LogWriteLine($"Value V2 {valueName} imported!");
            } while (count > 0);
        }

        private void ReadV3Values(Stream fs, string? gameBasePath)
        {
            bool isCompressed = fs.ReadByte() > 0;
            bool isHasFileToImport = fs.ReadByte() > 0;

            Logger.LogWriteLine($"Exporting registry V3 {RegistryPath}...");
            int count = ReadInt32(fs);
            using Stream stream = isCompressed ? new BrotliStream(fs, CompressionMode.Decompress) : fs;
            do
            {
                --count;
                string valueName = ReadValueName(stream);
                EnsureReadImpostorData(stream, valueName);
                Logger.LogWriteLine($"Value V3 {valueName} imported!");
            } while (count > 0);

            if (isHasFileToImport && !string.IsNullOrEmpty(gameBasePath))
                ImportStreamToFiles(stream, gameBasePath);
        }

        public async Task<Exception?> ExportSettings(bool isCompressed = true, string? parentPathToImport = null, string[]? relativePathToImport = null)
        {
            try
            {
                string path = await FileDialogNative.GetFileSavePicker(new Dictionary<string, string> { { "Collapse Registry", "*.clreg" } }, Lang._GameSettingsPage.SettingsRegExportTitle);
                EnsureFileSaveHasExtension(ref path, ".clreg");

                if (string.IsNullOrEmpty(path)) throw new OperationCanceledException(Lang._GameSettingsPage.SettingsRegErr1);
                Logger.LogWriteLine($"Exporting registry V3 {RegistryPath}...");

                await using FileStream fileStream  = new FileStream(path, FileMode.Create, FileAccess.Write);
                await using Stream     writeStream = isCompressed ? new BrotliStream(fileStream, CompressionLevel.Optimal, true) : fileStream;

                // Magic
                fileStream.Write("ColReg"u8);
                fileStream.WriteByte(3); // Write V3
                // Is compressed
                fileStream.WriteByte((byte)(isCompressed ? 1 : 0));
                // Is has file to import
                bool isHasFileToImport = parentPathToImport != null && (relativePathToImport?.Length ?? 0) != 0;
                fileStream.WriteByte((byte)(isHasFileToImport ? 1 : 0));

                string[] names = RegistryRoot!.GetValueNames();
                Span<byte> lenByte = stackalloc byte[4];
                MemoryMarshal.Write(lenByte, names.Length);
                fileStream.Write(lenByte);

                foreach (string valueName in names)
                {
                    object? val = RegistryRoot.GetValue(valueName);
                    RegistryValueKind valueType = RegistryRoot.GetValueKind(valueName);

                    Logger.LogWriteLine($"Writing value V3 {valueName}...");

                    WriteValueName(writeStream, valueName);
                    EnsureWriteImpostorData(writeStream, valueType, val);
                }

                if (isHasFileToImport && parentPathToImport != null && relativePathToImport != null)
                    ExportFilesToStream(writeStream, parentPathToImport, relativePathToImport);

                Logger.LogWriteLine($"Registry V3 {RegistryPath} has been exported!");
            }
            catch (Exception ex)
            {
                // Gets caught by calling method
                return ex;
            }

            return null;
        }

        private static void EnsureReadImpostorData(Stream stream, string valueName)
        {
            int sizeOf = ReadValueKindAndSize(stream, out RegistryValueKind realValueType);
            object result = realValueType switch
            {
                RegistryValueKind.QWord => ReadTypeNumberUnsafe(stream, sizeOf),
                RegistryValueKind.DWord => ReadTypeNumberUnsafe(stream, sizeOf),
                RegistryValueKind.String => ReadTypeString(stream, sizeOf),
                RegistryValueKind.Binary => WriteTypeBinary(stream, sizeOf),
                _ => throw new InvalidCastException("Cast of the object cannot be determined!")
            };

            if (realValueType == RegistryValueKind.QWord && sizeOf == sizeof(int)
             || realValueType == RegistryValueKind.DWord && sizeOf == sizeof(long))
            {
                // Fix wrong casting of the number. Store the value as a binary type.
                byte[] numberAsBuffer = new byte[sizeOf];
                if (sizeOf == sizeof(int))
                    MemoryMarshal.Write(numberAsBuffer, (int)result);
                else
                    MemoryMarshal.Write(numberAsBuffer, (long)result);

                realValueType = RegistryValueKind.Binary;
                result = numberAsBuffer;
            }

            RegistryRoot?.SetValue(valueName, result, realValueType);
        }

        private static void EnsureWriteImpostorData(Stream stream, RegistryValueKind realValueType, object? value)
        {
            Type valueType = value!.GetType();
            if (valueType == typeof(int) || valueType == typeof(float))
            {
                int castValue = (int)value;
                WriteTypeNumberUnsafe(stream, ref castValue, 4, realValueType);
                return;
            }
            if (valueType == typeof(long) || valueType == typeof(double))
            {
                long castValue = (long)value;
                WriteTypeNumberUnsafe(stream, ref castValue, 8, realValueType);
                return;
            }
            if (valueType == typeof(string))
            {
                WriteTypeString(stream, (string)value, realValueType);
                return;
            }

            if (valueType != typeof(byte[]))
            {
                throw new InvalidCastException("Cast of the object cannot be determined!");
            }

            WriteTypeBinary(stream, (byte[])value, realValueType);
        }

        public static unsafe object ReadTypeNumberUnsafe(Stream stream, int sizeOf)
        {
            Span<byte> buffer = stackalloc byte[sizeOf];
            stream.ReadExactly(buffer);

            return sizeOf switch
                   {
                       sizeof(int) => MemoryMarshal.Read<int>(buffer),
                       sizeof(long) => MemoryMarshal.Read<long>(buffer),
                       _ => throw new
                           InvalidCastException($"The type of the number type is unknown! Expecting size: {sizeOf}")
                   };
        }

        private static unsafe void WriteTypeNumberUnsafe<T>(Stream stream, ref T value, int sizeOf, RegistryValueKind realValueType)
            where T : struct
        {
            ReadOnlySpan<byte> byteNumberAsSpan = new((byte*)Unsafe.AsPointer(ref value), sizeOf);
            WriteValueKindAndSize(stream, sizeOf, realValueType);
            WriteToStream(stream, byteNumberAsSpan);
        }

        private static void ExportFilesToStream(Stream writeStream, string parentPath, string[] relativePaths)
        {
            using BinaryWriter writer = new BinaryWriter(writeStream, Encoding.UTF8, true);
            writer.Write7BitEncodedInt(relativePaths.Length);

            Logger.LogWriteLine($"Writing exported {relativePaths.Length} file(s) to V3 data...");
            foreach (string relativePath in relativePaths)
            {
                string pathCombined = Path.Combine(parentPath, relativePath);
                FileInfo fileInfo = new FileInfo(pathCombined);
                if (!fileInfo.Exists) continue;

                Logger.LogWriteLine($"Writing file: {relativePath} -> {fileInfo.Length} bytes to V3 data...");
                writer.WriteStringToNull(relativePath);
                writer.Write7BitEncodedInt64(fileInfo.Length);

                // Write files
                using FileStream exportFileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                exportFileStream.CopyTo(writeStream);
            }
            Logger.LogWriteLine($"{relativePaths.Length} file(s) has been succesfully exported to V3 data!");
        }

        private static void ImportStreamToFiles(Stream readStream, string? gamePath)
        {
            if (string.IsNullOrEmpty(gamePath)) return;

            using BinaryReader reader = new BinaryReader(readStream, Encoding.UTF8, true);
            int filesCount = reader.Read7BitEncodedInt();

            byte[] buffer = new byte[16 << 10];

            Logger.LogWriteLine($"Importing {filesCount} file(s) from V3 data to: {gamePath}...");
            for (; filesCount > 0; filesCount--)
            {
                string relativePath = reader.ReadStringToNull();
                string pathCombined = Path.Combine(gamePath, relativePath);
                string? pathCombinedDir = Path.GetDirectoryName(pathCombined);

                if (!string.IsNullOrEmpty(pathCombinedDir) && !Directory.Exists(pathCombinedDir))
                    Directory.CreateDirectory(pathCombinedDir);

                long fileSize = reader.Read7BitEncodedInt64();
                Logger.LogWriteLine($"Writing V3 data to file: {relativePath} -> {fileSize} bytes...");
                using FileStream fileStream = File.Create(pathCombined);

                while (fileSize > 0)
                {
                    int toRead = Math.Min(buffer.Length, (int)fileSize);
                    int read = readStream.Read(buffer, 0, toRead);
                    fileStream.Write(buffer, 0, read);
                    fileSize -= read;
                }
            }
        }

        private static string ReadTypeString(Stream stream, int length)
        {
            bool isUsePool = length <= 64 << 10;
            byte[] buffer = isUsePool ? ArrayPool<byte>.Shared.Rent(length) : new byte[length];
            try
            {
                stream.ReadExactly(buffer, 0, length);
                string result = Encoding.UTF8.GetString(buffer, 0, length);
                return result;
            }
            finally
            {
                if (isUsePool) ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static void WriteTypeString(Stream stream, ReadOnlySpan<char> stringSpan, RegistryValueKind realValueType)
        {
            bool isUsePool = stringSpan.Length <= 64 << 10;
            byte[] buffer = isUsePool ? ArrayPool<byte>.Shared.Rent(stringSpan.Length) : new byte[stringSpan.Length];
            try
            {
                int writtenLen = Encoding.UTF8.GetBytes(stringSpan, buffer);
                WriteValueKindAndSize(stream, writtenLen, realValueType);
                WriteToStream(stream, buffer.AsSpan(0, writtenLen));
            }
            finally
            {
                if (isUsePool) ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static byte[] WriteTypeBinary(Stream stream, int length)
        {
            byte[] buffer = new byte[length];
            stream.ReadExactly(buffer);
            return buffer;
        }

        private static void WriteTypeBinary(Stream stream, ReadOnlySpan<byte> buffer, RegistryValueKind realValueType)
        {
            WriteValueKindAndSize(stream, buffer.Length, realValueType);
            WriteToStream(stream, buffer);
        }

        private static void WriteValueKindAndSize(Stream stream, int sizeOf, RegistryValueKind kind)
        {
            Span<byte> lenBuffer = stackalloc byte[5];
            lenBuffer[0] = (byte)kind;
            BinaryPrimitives.WriteInt32LittleEndian(lenBuffer.Slice(1), sizeOf);
            stream.Write(lenBuffer);
        }

        private static int ReadValueKindAndSize(Stream stream, out RegistryValueKind kind)
        {
            Span<byte> lenBuffer = stackalloc byte[5];
            stream.ReadExactly(lenBuffer);
            kind = (RegistryValueKind)lenBuffer[0];
            int sizeOf = MemoryMarshal.Read<int>(lenBuffer.Slice(1));
            return sizeOf;
        }

        private static void WriteToStream(Stream stream, ReadOnlySpan<byte> buffer) => stream.Write(buffer);

        private static void EnsureFileSaveHasExtension(ref string path, string exte)
        {
            if (string.IsNullOrEmpty(path)) return;
            string ext = Path.GetExtension(path);

            if (string.IsNullOrEmpty(ext))
            {
                path += exte;
            }
        }

        private static void WriteValueName(Stream stream, string name)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(4 << 10);
            try
            {
                int writtenData = Encoding.UTF8.GetBytes(name, buffer.AsSpan(2));
                BinaryPrimitives.WriteUInt16LittleEndian(buffer, (ushort)writtenData);
                stream.Write(buffer, 0, writtenData + 2);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private string ReadValueName(Stream stream)
        {
            short length = ReadInt16(stream);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                _ = stream.Read(buffer, 0, length);
                return Encoding.UTF8.GetString(buffer, 0, length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        // ReSharper disable once UnusedMember.Local
        // just in case, i aint wanna dig around commit history if somehow this needed in the future
        private long ReadInt64(Stream stream)
        {
            Span<byte> buffer = stackalloc byte[4];
            _ = stream.Read(buffer);
            return MemoryMarshal.Read<long>(buffer);
        }

        private int ReadInt32(Stream stream)
        {
            Span<byte> buffer = stackalloc byte[4];
            _ = stream.Read(buffer);
            return MemoryMarshal.Read<int>(buffer);
        }

        private short ReadInt16(Stream stream)
        {
            Span<byte> buffer = stackalloc byte[2];
            _ = stream.Read(buffer);
            return MemoryMarshal.Read<short>(buffer);
        }

        private static string ReadValueName(EndianBinaryReader reader) => reader.ReadString8BitLength();

        private static void ReadDWord(EndianBinaryReader reader, string valueName)
        {
            int val = reader.ReadInt32();
            RegistryRoot?.SetValue(valueName, val, RegistryValueKind.DWord);
        }

        private static void ReadQWord(EndianBinaryReader reader, string valueName)
        {
            long val = reader.ReadInt64();
            RegistryRoot?.SetValue(valueName, val, RegistryValueKind.QWord);
        }

        private static void ReadString(EndianBinaryReader reader, string valueName)
        {
            string val = reader.ReadString8BitLength();
            RegistryRoot?.SetValue(valueName, val, RegistryValueKind.String);
        }

        protected virtual void ReadBinary(EndianBinaryReader reader, string valueName)
        {
            int len = reader.ReadInt32();
            byte[] val = new byte[len];
            _ = reader.Read(val, 0, len);
            RegistryRoot?.SetValue(valueName, val, RegistryValueKind.Binary);
        }
    }
}
