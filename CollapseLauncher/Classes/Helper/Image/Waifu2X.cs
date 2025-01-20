using Hi3Helper;
using Hi3Helper.Shared.Region;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hi3Helper.SentryHelper;
using static CollapseLauncher.Helper.Image.Waifu2X;
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable StringLiteralTypo
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault

namespace CollapseLauncher.Helper.Image
{
    #region PInvokes
    internal static partial class Waifu2XPInvoke
    {
        private const string DllName = "Lib\\waifu2x-ncnn-vulkan.dll";

#nullable enable
        private static string? appDirPath;
        private static string? waifu2xLibPath;
#nullable restore

        static Waifu2XPInvoke()
        {
            // Use custom Dll import resolver
            NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), DllImportResolver);
        }

        private static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            appDirPath ??= LauncherConfig.AppExecutableDir;

            if (DllImportSearchPath.AssemblyDirectory != searchPath
             && DllImportSearchPath.ApplicationDirectory != searchPath)
            {
                return LoadInternal(libraryName, assembly, searchPath);
            }

            waifu2xLibPath ??= Path.Combine(appDirPath, DllName);
            return LoadInternal(waifu2xLibPath, assembly, null);

        }

        private static IntPtr LoadInternal(string path, Assembly assembly, DllImportSearchPath? searchPath)
        {
            bool isLoadSuccessful = NativeLibrary.TryLoad(path, assembly, null, out IntPtr pResult);
            if (!isLoadSuccessful || pResult == IntPtr.Zero)
                throw new FileLoadException($"Failed while loading library from this path: {path} with Search Path: {searchPath}\r\nMake sure that the library/.dll is a valid Win32 library and not corrupted!");

            return pResult;
        }

        #region ncnn PInvokes
        [LibraryImport(DllName)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
        internal static partial int ncnn_get_default_gpu_index();

        [LibraryImport(DllName)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
        internal static partial IntPtr ncnn_get_gpu_name(int gpuId);
        #endregion

        #region Waifu2X PInvokes
        [LibraryImport(DllName)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
        internal static partial IntPtr waifu2x_create(int gpuId, [MarshalAs(UnmanagedType.Bool)] bool ttaMode, int numThreads);

        [LibraryImport(DllName)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
        internal static partial void waifu2x_destroy(IntPtr context);

        [LibraryImport(DllName)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
        internal static unsafe partial int waifu2x_load(IntPtr context, byte* param, byte* model);

        [LibraryImport(DllName)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
        internal static unsafe partial int waifu2x_process(IntPtr context, int w, int h, int c, byte* inData, byte* outData);

        [LibraryImport(DllName)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
        internal static unsafe partial int waifu2x_process_cpu(IntPtr context, int w, int h, int c, byte* inData, byte* outData);

        [LibraryImport(DllName)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
        internal static partial void waifu2x_set_param(IntPtr context, Param param, int value);

        [LibraryImport(DllName)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
        internal static partial int waifu2x_get_param(IntPtr context, Param param);

        [LibraryImport(DllName)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
        internal static partial Waifu2XStatus waifu2x_self_test(IntPtr context);

        [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static unsafe partial long GetPackagesByPackageFamily(string packageFamilyName, ref uint count, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 1)] out string[] packageFullNames, ref uint bufferLength, void* buffer);
        #endregion
    }
    #endregion

    #region ncnn Defines
    public static class Ncnn
    {
        public static int DefaultGpuIndex => Waifu2XPInvoke.ncnn_get_default_gpu_index();

        public static string GetGpuName(int gpuId) => Marshal.PtrToStringUTF8(Waifu2XPInvoke.ncnn_get_gpu_name(gpuId));
    }
    #endregion

    public sealed partial class Waifu2X : IDisposable
    {
        #region Enums
        public enum Param
        {
            Noise,
            Scale,
            TileSize
        }

        public enum Waifu2XStatus
        {
            Ok,

            Warning,
            CpuMode = Warning,
            D3DMappingLayers,

            Error,
            NotAvailable = Error,
            TestNotPassed,
            NotInitialized
        }
        #endregion

        #region Properties
        private IntPtr _context;

        #endregion

        public Waifu2XStatus Status { get; private set; }

        #region Main Methods
        public Waifu2X()
        {
            try
            {
                var gpuId = -1; // Do not touch Vulkan before VulkanTest.
                Status = VulkanTest();
                if (Status == Waifu2XStatus.Ok)
                    gpuId = Ncnn.DefaultGpuIndex;
                _context = Waifu2XPInvoke.waifu2x_create(gpuId, false, 0);
                Logger.LogWriteLine($"Waifu2X initialized successfully with device: {Ncnn.GetGpuName(gpuId)}", LogType.Default, true);
            }
            catch ( DllNotFoundException ex )
            {
                Status = Waifu2XStatus.NotAvailable;
                Logger.LogWriteLine("Dll file \"waifu2x-ncnn-vulkan.dll\" can not be found. Waifu2X feature will be disabled.", LogType.Error, true);
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
            }
            catch ( Exception ex )
            {
                Status = Waifu2XStatus.Error;
                Logger.LogWriteLine($"There was an error when loading Waifu2X!\r\n{ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
            }
        }

        public void Dispose()
        {
            if (_context == 0)
            {
                return;
            }

            Waifu2XPInvoke.waifu2x_destroy(_context);
            _context = 0;
            Status   = Waifu2XStatus.NotInitialized;
            Logger.LogWriteLine("Waifu2X is destroyed!");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool Load(ReadOnlySpan<byte> param, ReadOnlySpan<byte> model)
        {
            if (_context == 0) throw new NotSupportedException();
            fixed (byte* pParam = &MemoryMarshal.GetReference(param), pModel = &MemoryMarshal.GetReference(model))
            {
                return Waifu2XPInvoke.waifu2x_load(_context, pParam, pModel) == 0 && ProcessTest();
            }
        }

        public bool Load(string paramPath, string modelPath)
        {
            if (_context == 0) throw new NotSupportedException();
            try
            {
                byte[] paramBuffer = File.ReadAllBytes(paramPath);
                byte[] modelBuffer = File.ReadAllBytes(modelPath);

                return Load(paramBuffer, modelBuffer);
            }
            catch (IOException ex)
            {
                Status = Waifu2XStatus.TestNotPassed;
                Logger.LogWriteLine("Waifu2X model file can not be found. Waifu2X feature will be disabled.", LogType.Error, true);
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                return false;
            }
        }
        #endregion

        #region Process Methods
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe int Process(int w, int h, int c, ReadOnlySpan<byte> inData, Span<byte> outData)
        {
            if (_context == 0) throw new NotSupportedException();
            fixed (byte* pInData = &MemoryMarshal.GetReference(inData), pOutData = &MemoryMarshal.GetReference(outData))
            {
                return Waifu2XStatus.CpuMode == Status ?
                    Waifu2XPInvoke.waifu2x_process_cpu(_context, w, h, c, pInData, pOutData) :
                    Waifu2XPInvoke.waifu2x_process(_context, w, h, c, pInData, pOutData);
            }
        }
        #endregion

        #region Parameters
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetParam(Param param, int value)
        {
            if (_context == 0) throw new NotSupportedException();
            Waifu2XPInvoke.waifu2x_set_param(_context, param, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetParam(Param param)
        {
            if (_context == 0) throw new NotSupportedException();
            return Waifu2XPInvoke.waifu2x_get_param(_context, param);
        }
        #endregion

        #region Misc
        public static Waifu2XStatus VulkanTest()
        {
            try
            {
                if (CheckD3DMappingLayersPackageInstalled())
                {
                    Logger.LogWriteLine("D3DMappingLayers package detected. Fallback to CPU mode.", LogType.Warning, true);
                    return Waifu2XStatus.D3DMappingLayers;
                }
                var status = Waifu2XPInvoke.waifu2x_self_test(0);
                switch (status)
                {
                    case Waifu2XStatus.CpuMode:
                        Logger.LogWriteLine("No available Vulkan GPU device was found and CPU mode will be used. This will greatly increase image processing time.", LogType.Warning, true);
                        break;
                    case Waifu2XStatus.NotAvailable:
                        Logger.LogWriteLine("An error occurred while initializing Vulkan. Fallback to CPU mode.", LogType.Warning, true);
                        status = Waifu2XStatus.CpuMode;
                        break;
                    case Waifu2XStatus.Ok:
                        Logger.LogWriteLine("Vulkan test passes and GPU mode can be used.", LogType.Default, true);
                        break;
                    default:
                        Logger.LogWriteLine("Waifu2X: Unknown return value from waifu2x_self_test.", LogType.Error, true);
                        status = Waifu2XStatus.NotAvailable;
                        break;
                }
                return status;
            }
            catch (FileLoadException ex)
            {
                return ReturnAsFailedDllInit(ex);
            }
            catch (DllNotFoundException ex)
            {
                return ReturnAsFailedDllInit(ex);
            }

            Waifu2XStatus ReturnAsFailedDllInit<T>(T ex)
                where T : Exception
            {
                Logger.LogWriteLine($"Cannot load Waifu2X as the library failed to load!\r\n{ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                return Waifu2XStatus.Error;
            }
        }

        private bool ProcessTest()
        {
            if (Status >= Waifu2XStatus.Error)
            {
                return Status == Waifu2XStatus.Ok;
            }

            Status = Waifu2XPInvoke.waifu2x_self_test(_context);
            switch (Status)
            {
                case Waifu2XStatus.TestNotPassed:
                    Logger.LogWriteLine("Waifu2X self-test failed, got an empty output image.", LogType.Error, true);
                    break;
                case Waifu2XStatus.NotAvailable:
                    Logger.LogWriteLine("An error occurred while processing the image.", LogType.Error, true);
                    break;
                case Waifu2XStatus.Ok:
                    Logger.LogWriteLine("Waifu2X self-test passed, you can use Waifu2X function normally.", LogType.Default, true);
                    break;
                default:
                    Logger.LogWriteLine("Waifu2X: Unknown return value from waifu2x_self_test.", LogType.Error, true);
                    Status = Waifu2XStatus.NotAvailable;
                    break;
            }
            return Status == Waifu2XStatus.Ok;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe bool CheckD3DMappingLayersPackageInstalled()
        {
            const string FAMILY_NAME = "Microsoft.D3DMappingLayers_8wekyb3d8bbwe";
            uint count = 0, bufferLength = 0;
            Waifu2XPInvoke.GetPackagesByPackageFamily(FAMILY_NAME, ref count, out _, ref bufferLength, null);
            return count != 0;
        }
        #endregion
    }
}
