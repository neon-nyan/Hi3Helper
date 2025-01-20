using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Http;
using Hi3Helper.Http.Legacy;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using SharpHDiffPatch.Core;
using SharpHDiffPatch.Core.Event;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Hi3Helper.SentryHelper;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault

namespace CollapseLauncher
{
    public sealed partial class GameConversionManagement : IDisposable
    {
        public event EventHandler<ConvertProgress> ProgressChanged;

        private readonly PresetConfig         _sourceProfile;
        private readonly PresetConfig         _targetProfile;
        private          List<FileProperties> _sourceFileManifest;
        private          List<FileProperties> _targetFileManifest;
        private readonly HttpClient           _client;

        private readonly string            _baseURL;
        private readonly string            _gameVersion;
        private readonly string            _cookbookPath;
        private          Stopwatch         _convertSw;
        private readonly CancellationToken _token;
        private          void              ResetSw() => _convertSw = Stopwatch.StartNew();
        private          string            _convertStatus, _convertDetail;
        private readonly byte              _downloadThread;

        internal GameConversionManagement(PresetConfig sourceProfile, PresetConfig targetProfile,
            string baseURL, string gameVersion, string cookbookPath, CancellationToken token = new())
        {
            // Initialize new proxy-aware HttpClient
            _client = new HttpClientBuilder()
                .UseLauncherConfig()
                .SetAllowedDecompression(DecompressionMethods.None)
                .Create();
            _sourceProfile = sourceProfile;
            _targetProfile = targetProfile;
            _baseURL = baseURL;
            _gameVersion = gameVersion;
            _downloadThread = (byte)AppCurrentDownloadThread;
            _cookbookPath = cookbookPath;
            _token = token;
        }

        ~GameConversionManagement() => Dispose();

        public void Dispose()
        {
            _client?.Dispose();
            GC.SuppressFinalize(this);
        }

        public async Task StartPreparation()
        {
            List<FilePropertiesRemote> sourceFileRemote;
            List<FilePropertiesRemote> targetFileRemote;
            _convertSw = Stopwatch.StartNew();
            _convertStatus = Lang._InstallConvert.Step3Title;

            string ingredientsPath = _targetProfile.ActualGameDataLocation + "_Ingredients";

            DownloadClient downloadClient = DownloadClient.CreateInstance(_client);

            try
            {
                FallbackCDNUtil.DownloadProgress += FetchIngredientsAPI_Progress;

                string url;
                using (MemoryStream buffer = new MemoryStream())
                {
                    url = string.Format(AppGameRepairIndexURLPrefix, _sourceProfile.ProfileName, _gameVersion);
                    _convertDetail = Lang._InstallConvert.Step2Subtitle;
                    await FallbackCDNUtil.DownloadCDNFallbackContent(downloadClient, buffer, url, _token);
                    buffer.Position = 0;
                    sourceFileRemote = await buffer.DeserializeAsListAsync(CoreLibraryJsonContext.Default.FilePropertiesRemote, _token);
                }

                using (MemoryStream buffer = new MemoryStream())
                {
                    url = string.Format(AppGameRepairIndexURLPrefix, _targetProfile.ProfileName, _gameVersion);
                    _convertDetail = Lang._InstallConvert.Step2Subtitle;
                    await FallbackCDNUtil.DownloadCDNFallbackContent(downloadClient, buffer, url, _token);
                    buffer.Position = 0;
                    targetFileRemote = await buffer.DeserializeAsListAsync(CoreLibraryJsonContext.Default.FilePropertiesRemote, _token);
                }
            }
            finally
            {
                FallbackCDNUtil.DownloadProgress -= FetchIngredientsAPI_Progress;
            }

            _sourceFileManifest = BuildManifest(sourceFileRemote);
            _targetFileManifest = BuildManifest(targetFileRemote);
            await Task.Run(() => PrepareIngredients(_sourceFileManifest), _token);
            await RepairIngredients(downloadClient, await VerifyIngredients(_sourceFileManifest, ingredientsPath), ingredientsPath);
        }

        private long _makeIngredientsRead;
        private long _makeIngredientsTotalSize;
        private void PrepareIngredients(List<FileProperties> fileManifest)
        {
            ResetSw();
            _makeIngredientsRead = 0;
            _makeIngredientsTotalSize = fileManifest.Sum(x => x.FileSize);

            _convertStatus = Lang._InstallConvert.Step3Title;

            foreach (FileProperties entry in fileManifest)
            {
                var inputPath = Path.Combine(_sourceProfile.ActualGameDataLocation!, entry.FileName);
                var outputPath = Path.Combine(_targetProfile.ActualGameDataLocation + "_Ingredients", entry.FileName);

                if (!Directory.Exists(Path.GetDirectoryName(outputPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

                if (File.Exists(inputPath))
                {
                    _convertDetail = $"Moving: {entry.FileName} ({entry.FileSizeStr})";
                    _makeIngredientsRead += entry.FileSize;
                    File.Move(inputPath, outputPath, true);
                    UpdateProgress(_makeIngredientsRead, _makeIngredientsTotalSize, 1, 1, _convertSw.Elapsed, _convertStatus, _convertDetail);
                }
                else
                {
                    _makeIngredientsRead += entry.FileSize;
                    UpdateProgress(_makeIngredientsRead, _makeIngredientsTotalSize, 1, 1, _convertSw.Elapsed, _convertStatus, _convertDetail);
                }
            }
        }

        public async Task PostConversionVerify()
        {
            DownloadClient downloadClient = DownloadClient.CreateInstance(_client);

            string targetPath = _targetProfile.ActualGameDataLocation;
            await RepairIngredients(downloadClient, await VerifyIngredients(_targetFileManifest, targetPath), targetPath);
        }

        private async Task<List<FileProperties>> VerifyIngredients(List<FileProperties> fileManifest, string gamePath)
        {
            ResetSw();
            List<FileProperties> brokenManifest = [];
            long                 curRead        = 0;
            long                 totalSize      = fileManifest.Sum(x => x.FileSize);

            _convertStatus = Lang._InstallConvert.Step3Title2;
            foreach (FileProperties entry in fileManifest)
            {
                var               outputPath = Path.Combine(gamePath, entry.FileName);
                _convertDetail =
                    $"{Lang._Misc.CheckingFile}: {string.Format(Lang._Misc.PerFromTo, entry.FileName, entry.FileSizeStr)}";
                UpdateProgress(curRead, totalSize, 1, 1, _convertSw.Elapsed, _convertStatus, _convertDetail);
                if (File.Exists(outputPath))
                {
                    await using FileStream fs = new FileStream(outputPath, FileMode.Open, FileAccess.Read);
                    byte[] hashBytes = entry.CurrCRC.Length > 8 ?
                        await Hash.GetCryptoHashAsync<MD5>(fs, null, null, _token) :
                        await Hash.GetHashAsync<Crc32>(fs, null, _token);
                    var               localHash = Convert.ToHexStringLower(hashBytes);

                    _token.ThrowIfCancellationRequested();
                    if (localHash != entry.CurrCRC)
                    {
                        LogWriteLine($"File {entry.FileName} has unmatched hash. Local: {localHash} Remote: {entry.CurrCRC}", LogType.Warning, true);
                        brokenManifest.Add(entry);
                    }
                }
                else
                    brokenManifest.Add(entry);
                curRead += entry.FileSize;
            }

            return brokenManifest;
        }

        private static List<FileProperties> BuildManifest(List<FilePropertiesRemote> fileRemote)
        {
            List<FileProperties> @out = [];

            foreach (FilePropertiesRemote entry in fileRemote)
            {
                switch (entry.FT)
                {
                    case FileType.Generic:
                        {
                            @out.Add(new FileProperties
                            {
                                FileName = entry.N,
                                FileSize = entry.S,
                                CurrCRC = entry.CRC,
                                DataType = FileType.Generic
                            });
                        }
                        break;
                    case FileType.Block:
                        {
                            @out.AddRange(BuildBlockManifest(entry.BlkC, entry.N));
                        }
                        break;
                }
            }

            return @out;
        }

        private static List<FileProperties> BuildBlockManifest(List<XMFBlockList> blockC, string baseName)
        {
            List<FileProperties> @out = [];

            for (var index = blockC.Count - 1; index >= 0; index--)
            {
                var block = blockC[index];
                var name  = baseName + '/' + block.BlockHash + ".wmv";
                @out.Add(new FileProperties
                {
                    FileName = name,
                    FileSize = block.BlockSize,
                    CurrCRC  = block.BlockHash
                });
            }

            return @out;
        }

        private long _repairRead;
        private long _repairTotalSize;
        private async Task RepairIngredients(DownloadClient downloadClient, List<FileProperties> brokenFile, string gamePath)
        {
            if (brokenFile.Count == 0) return;

            ResetSw();
            _repairTotalSize = brokenFile.Sum(x => x.FileSize);

            _convertStatus = Lang._InstallConvert.Step3Title1;
            await Parallel.ForEachAsync(brokenFile, new ParallelOptions
            {
                MaxDegreeOfParallelism = _downloadThread,
                CancellationToken = _token
            }, async (entry, coopToken) =>
            {
                _token.ThrowIfCancellationRequested();

                string outputPath = Path.Combine(gamePath, entry.FileName);
                string outputPathDir = Path.GetDirectoryName(outputPath);
                string inputURL = CombineURLFromString(_baseURL, entry.FileName);

                _convertDetail =
                    $"{Lang._Misc.Downloading}: {string.Format(Lang._Misc.PerFromTo, entry.FileName, entry.FileSizeStr)}";
                if (!Directory.Exists(outputPathDir))
                    Directory.CreateDirectory(outputPathDir!);

                await downloadClient.DownloadAsync(inputURL, outputPath, true, progressDelegateAsync: RepairIngredients_Progress, maxConnectionSessions: _downloadThread, cancelToken: coopToken);
            });
        }

        private void RepairIngredients_Progress(int read, DownloadProgress downloadProgress)
        {
            Interlocked.Add(ref _repairRead, read);

            UpdateProgress(_repairRead, _repairTotalSize, 1, 1, _convertSw.Elapsed,
                _convertStatus, _convertDetail);
        }

        private void FetchIngredientsAPI_Progress(object sender, DownloadEvent e)
        {
            UpdateProgress(e.SizeDownloaded, e.SizeToBeDownloaded, 1, 1, _convertSw.Elapsed,
                _convertStatus, _convertDetail);
        }

        public async Task StartConversion()
        {
            ResetSw();
            string ingredientsPath = _targetProfile.ActualGameDataLocation + "_Ingredients";
            string outputPath = _targetProfile.ActualGameDataLocation;

            try
            {
                if (Directory.Exists(outputPath))
                    TryDirectoryDelete(outputPath, true);

                Directory.CreateDirectory(outputPath!);

                HDiffPatch.LogVerbosity = Verbosity.Verbose;
                EventListener.LoggerEvent += EventListener_PatchLogEvent;
                EventListener.PatchEvent += EventListener_PatchEvent;

                await Task.Run(() =>
                {
                    HDiffPatch patch = new HDiffPatch();
                    patch.Initialize(_cookbookPath);
                    patch.Patch(ingredientsPath, outputPath, true, _token, false, true);
                }, _token);

                TryDirectoryDelete(ingredientsPath, true);
                TryFileDelete(_cookbookPath);
                MoveMiscSourceFiles(_sourceProfile.ActualGameDataLocation, outputPath);
                TryDirectoryDelete(_sourceProfile.ActualGameDataLocation, true);
            }
            catch (Exception ex)
            {
                try
                {
                    RevertBackIngredients(_sourceFileManifest, ingredientsPath, outputPath);
                }
                catch (Exception exf)
                {
                    LogWriteLine($"Conversion process has failed and sorry, the files can't be reverted back :(\r\nInner Exception: {ex}\r\nReverting Exception: {exf}", LogType.Error, true);
                    await SentryHelper.ExceptionHandlerAsync(exf, SentryHelper.ExceptionType.UnhandledOther);
                    throw new Exception($"Conversion process has failed and sorry, the files can't be reverted back :(\r\nInner Exception: {ex}\r\nReverting Exception: {exf}", new Exception($"Inner exception: {ex}", ex));
                }
                LogWriteLine($"Conversion process has failed! But don't worry, the files have been reverted :D\r\n{ex}", LogType.Error, true);
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                throw new Exception($"Conversion process has failed! But don't worry, the file have been reverted :D\r\n{ex}", ex);
            }
            finally
            {
                EventListener.PatchEvent -= EventListener_PatchEvent;
                EventListener.LoggerEvent -= EventListener_PatchLogEvent;
            }
        }

        private static void EventListener_PatchLogEvent(object sender, LoggerEvent e)
        {
            if (HDiffPatch.LogVerbosity == Verbosity.Quiet
            || (HDiffPatch.LogVerbosity == Verbosity.Debug
            && e.LogLevel is not (Verbosity.Debug or Verbosity.Verbose or Verbosity.Info))
            || (HDiffPatch.LogVerbosity == Verbosity.Verbose
            && e.LogLevel is not (Verbosity.Verbose or Verbosity.Info))
            || (HDiffPatch.LogVerbosity == Verbosity.Info
            && e.LogLevel != Verbosity.Info)) return;

            LogType type = e.LogLevel switch
            {
                Verbosity.Verbose => LogType.Debug,
                Verbosity.Debug => LogType.Debug,
                _ => LogType.Default
            };

            LogWriteLine(e.Message, type, true);
        }

        private void EventListener_PatchEvent(object sender, PatchEvent e)
        {
            _convertDetail = string.Format(Lang._Misc.Converting, "");
            UpdateProgress(e.CurrentSizePatched, e.TotalSizeToBePatched, 1, 1, e.TimeLeft, _convertStatus, _convertDetail);
        }

        private static void TryFileDelete(string input)
        {
            try
            {
                File.Delete(input);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error while trying to delete file \"{input}\"\r\n{ex}");
            }
        }

        private static void TryDirectoryDelete(string input, bool recursive)
        {
            try
            {
                Directory.Delete(input, recursive);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error while trying to delete directory \"{input}\"{(recursive ? " recursively!" : "!")}\r\n{ex}");
            }
        }

        private void RevertBackIngredients(List<FileProperties> fileManifest, string ingredientPath, string output)
        {
            foreach (FileProperties entry in fileManifest)
            {
                var outputPath = Path.Combine(_sourceProfile.ActualGameDataLocation!,                 entry.FileName);
                var inputPath  = Path.Combine(_targetProfile.ActualGameDataLocation + "_Ingredients", entry.FileName);

                if (!Directory.Exists(Path.GetDirectoryName(outputPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

                if (File.Exists(inputPath))
                    File.Move(inputPath, outputPath, true);
            }

            Directory.Delete(ingredientPath, true);
            Directory.Delete(output, true);
        }

        private static void MoveMiscSourceFiles(string inputPath, string outputPath)
        {
            IEnumerable<string> files = Directory.EnumerateFiles(inputPath, "*", SearchOption.AllDirectories);
            foreach (string entry in files)
            {
                var outputFile = Path.Combine(outputPath, entry[(inputPath.Length + 1)..]);
                if (File.Exists(outputFile))
                {
                    continue;
                }

                if (!Directory.Exists(Path.GetDirectoryName(outputFile)))
                    Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
                File.Move(entry, outputFile);
            }
        }

        public void UpdateProgress(long startSize, long endSize, int startCount, int endCount,
                TimeSpan timeSpan, string statusMsg = "", string detailMsg = "", bool useCountUnit = false)
        {
            ProgressChanged?.Invoke(this, new ConvertProgress(startSize, endSize, startCount, endCount,
                timeSpan, statusMsg, detailMsg, useCountUnit));
        }
    }

    public class ConvertProgress(
        long     startSize,
        long     endSize,
        int      startCount,
        int      endCount,
        TimeSpan timeSpan,
        string   statusMsg    = "",
        string   detailMsg    = "",
        bool     useCountUnit = false)
    {
        public bool UseCountUnit { get; } = useCountUnit;
        public long StartSize    { get; } = startSize;
        public long EndSize      { get; } = endSize;
        public int  StartCount   { get; } = startCount;
        public int  EndCount     { get; } = endCount;

        public double Percentage => UseCountUnit ? ToPercentage(EndCount, StartCount) :
                                                   ToPercentage(EndSize, StartSize);
        public double ProgressSpeed
        {
            get => StartSize / field;
        } = timeSpan.TotalSeconds;

        public TimeSpan RemainingTime => UseCountUnit ? TimeSpan.Zero :
                                                        ToTimeSpanRemain(EndSize, StartSize, ProgressSpeed);

        public string ProgressStatus => statusMsg;
        public string ProgressDetail => $"[{(UseCountUnit ? string.Format(Lang._Misc.PerFromTo, StartCount, EndCount) :
            string.Format(Lang._Misc.PerFromTo, SummarizeSizeSimple(StartSize), SummarizeSizeSimple(EndSize)))}] ({(UseCountUnit ? $"{Percentage}%" :
            $"{Percentage}% {string.Format(Lang._Misc.SpeedPerSec, SummarizeSizeSimple(ProgressSpeed))} - {string.Format(Lang._Misc.TimeRemainHMSFormat, RemainingTime)}")})\r\n{detailMsg}...";
    }
}
