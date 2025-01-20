using Hi3Helper;
using Hi3Helper.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Helper.StreamUtility
{
    internal static class StreamExtension
    {
        internal const int DefaultBufferSize = 64 << 10;

        internal static readonly FileStreamOptions FileStreamOpenReadOpt = new()
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read
        };

        internal static readonly FileStreamOptions FileStreamCreateWriteOpt = new()
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.Write
        };

        internal static readonly FileStreamOptions FileStreamCreateReadWriteOpt = new()
        {
            Mode = FileMode.Create,
            Access = FileAccess.ReadWrite,
            Share = FileShare.ReadWrite
        };

        /*
         * TODO: Create GetFileStream helper
        internal static FileStream GetFileStream(this string? path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare)
        {
            EnsureFilePathExist(path);
            return new FileStream(path, fileMode, fileAccess, fileShare, DefaultBufferSize, true);
        }

        private static void EnsureFilePathExist([NotNull] string? path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path, "path");
            if (File.Exists(path))
                throw new FileNotFoundException($"File: {path} is not found!");
        }
        */

        internal static FileInfo EnsureNoReadOnly(this FileInfo fileInfo)
            => fileInfo.EnsureNoReadOnly(out _);

        internal static FileInfo EnsureNoReadOnly(this FileInfo fileInfo, out bool isFileExist)
        {
            try
            {
                if (!(isFileExist = fileInfo.Exists))
                    return fileInfo;

                fileInfo.IsReadOnly = false;

                return fileInfo;
            }
            finally
            {
                fileInfo.Refresh();
            }
        }

        internal static IEnumerable<FileInfo> EnumerateNoReadOnly(this IEnumerable<FileInfo> enumeratedFileInfo)
            => enumeratedFileInfo.Select(x => x.EnsureNoReadOnly());

        /// <summary>
        /// IDK what Microsoft is smoking but for some reason, the file were throwing IO_SharingViolation_File error,
        /// or sometimes "File is being used by another process" error even though the file HAS NEVER BEEN OPENED LIKE, WTFFF????!>!!!!!!
        /// </summary>
        internal static async ValueTask<FileStream> NaivelyOpenFileStreamAsync(this FileInfo info,
            FileMode fileMode,
            FileAccess fileAccess,
            FileShare fileShare,
            FileOptions fileOption = FileOptions.None)
        {
            const int maxTry = 10;
            int currentTry = 1;
            while (true)
            {
                try
                {
                    return info.Open(new FileStreamOptions
                    {
                        Mode = fileMode,
                        Access = fileAccess,
                        Share = fileShare,
                        Options = fileOption,
                        BufferSize = DefaultBufferSize
                    });
                }
                catch
                {
                    if (currentTry > maxTry)
                    {
                        throw; // Throw this MFs
                    }

                    Logger.LogWriteLine($"Failed while trying to open: {info.FullName}. Retry attempt: {++currentTry} / {maxTry}", LogType.Warning, true);
                    await Task.Delay(50); // Adding 50ms delay
                }
            }
        }

        internal static FileInfo EnsureCreationOfDirectory(string filePath)
            => new FileInfo(filePath).EnsureCreationOfDirectory();

        internal static FileInfo EnsureCreationOfDirectory(this FileInfo filePath)
        {
            ArgumentNullException.ThrowIfNull(filePath, nameof(filePath));
            DirectoryInfo? directoryInfo = filePath.Directory;

            try
            {
                if (directoryInfo is { Exists: false })
                    directoryInfo.Create();

                return filePath;
            }
            finally
            {
                filePath.Refresh();
            }
        }

        internal static bool TryDeleteFile(this FileInfo filePath, bool throwIfFailed = false)
        {
            try
            {
                if (filePath.Exists)
                    filePath.Delete();

                return true;
            }
            catch (Exception ex)
            {
                if (throwIfFailed)
                    throw;

                Logger.LogWriteLine($"Failed to delete file: {filePath.FullName}\r\n{ex}", LogType.Error, true);
                return false;
            }
        }

        internal static string NormalizePath(this string path)
            => ConverterTool.NormalizePath(path);

        internal static bool TryMoveTo(this FileInfo filePath, string toTarget, bool overwrite = true, bool throwIfFailed = false)
            => filePath.TryMoveTo(new FileInfo(toTarget), overwrite, throwIfFailed);

        internal static bool TryMoveTo(this FileInfo filePath, FileInfo toTarget, bool overwrite = true, bool throwIfFailed = false)
        {
            try
            {
                if (filePath.Exists)
                    filePath.MoveTo(toTarget.FullName, overwrite);

                return true;
            }
            catch (Exception ex)
            {
                if (throwIfFailed)
                    throw;

                Logger.LogWriteLine($"Failed to move file: {filePath.FullName} to: {toTarget.FullName}\r\n{ex}", LogType.Error, true);
                return false;
            }
        }
    }
}
