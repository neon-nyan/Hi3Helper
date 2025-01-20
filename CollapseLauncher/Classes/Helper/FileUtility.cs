using Hi3Helper;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.SentryHelper;
using static Hi3Helper.Logger;

namespace CollapseLauncher.Helper
{
    public static class FileUtility
    {
    #nullable enable
        /// <summary>
        /// Get latest file from a directory given a pattern.
        /// </summary>
        /// <param name="directoryPath">Path to directory you want to get the file from</param>
        /// <param name="searchPattern">Pattern of the file you are looking for (e.g. "*.txt" or "file_*.txt). Default: *.*</param>
        /// <returns>Full path of the new file</returns>
        public static string? GetLatestFile(string directoryPath, string searchPattern = "*.*")
        {
            var directoryInfo = new DirectoryInfo(directoryPath);
            var latestFile = directoryInfo.GetFiles(searchPattern)
                                          .OrderByDescending(f => f.LastWriteTime)
                                          .FirstOrDefault();
            return latestFile?.FullName;
        }
    #nullable restore

        /// <summary>
        /// Wait for a directory to spawn a new file.
        /// </summary>
        /// <param name="directoryPath">Directory of a path you want to monitor</param>
        /// <param name="timeoutMilliseconds">Time you want to keep waiting for a file to spawn</param>
        /// <returns>True if a new file is found, and false if it timed out</returns>
        public static async Task<bool> WaitForNewFileAsync(string directoryPath, int timeoutMilliseconds)
        {
            TaskCompletionSource<string> tcs     = new();
            var                          cts     = new CancellationTokenSource(timeoutMilliseconds);
            var                          watcher = new FileSystemWatcher(directoryPath);

            watcher.Filter              =  "*.*";
            watcher.NotifyFilter        =  NotifyFilters.FileName | NotifyFilters.CreationTime;
            watcher.Created             += (_, e) => tcs.TrySetResult(e.FullPath);
            watcher.EnableRaisingEvents =  true;

            await using (cts.Token.Register(() => tcs.TrySetCanceled()))
            {
                try
                {
                    await tcs.Task;
                    return true;
                }
                catch (TaskCanceledException)
                {
                    return false;
                }
                finally
                {
                    watcher.Dispose();
                }
            }
        }
        
        /// <summary>
        /// Add a prefix to a filename
        /// </summary>
        /// <param name="filePath">File you want to rename</param>
        /// <param name="prefix">Prefix being added to the file</param>
        /// <param name="overwrite">Overwrite if the target filename+prefix already exist</param>
        /// <returns>True if success, false if fail or target exist while overwrite is false</returns>
        public static bool RenameFileWithPrefix(string filePath, string prefix = "-old", bool overwrite = false)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var filename  = Path.GetFileNameWithoutExtension(filePath);
                    var extension = Path.GetExtension(filePath);
                    
                    var directory   = Path.GetDirectoryName(filePath);
                    if (directory == null)
                    {
                        throw new NullReferenceException("[FileUtility::RenameFileWithPrefix] Directory is null!");
                    }

                    var newFilePath = Path.Combine(directory, $"{filename}{prefix}{extension}");
                    
                    if (File.Exists(newFilePath))
                    {
                        if (overwrite) File.Delete(newFilePath);
                        else
                        {
                            LogWriteLine($"[FileUtility::RenameFileWithPrefix] Target file {newFilePath} exist " +
                                         $"while overwrite is disabled!", LogType.Warning, true);
                            return false;
                        }
                    }
                    
                    File.Move(filePath, newFilePath);

                    return true;
                }
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"[FileUtility::RenameFileWithPrefix] Failed to rename file {filePath}!\r\n{ex}",
                             LogType.Error, true);
            }
            return false;
        }
    }
}