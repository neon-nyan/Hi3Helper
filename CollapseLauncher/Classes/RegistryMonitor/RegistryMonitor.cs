/*
 * Credit
 * =============================================================================================
 * Original code by: Thomas Freudenberg ©2003 - 2005
 * https://www.codeproject.com/Articles/4502/RegistryMonitor-a-NET-wrapper-class-for-RegNotifyC
 *
 * This code might have been modified to adjust the use for Collapse project.
 * Hence, the original code is licensed under The Code Project Open License (CPOL) 1.02
 * https://www.codeproject.com/info/cpol10.aspx
 */

using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Win32.Native.Enums;
using Hi3Helper.Win32.Native.LibraryImport;
using Hi3Helper.Win32.Native.Structs;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using static Hi3Helper.Logger;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable StringLiteralTypo

namespace RegistryUtils
{
    public sealed partial class RegistryMonitor : IDisposable
    {
        #region Event handling

        /// <summary>
        /// Occurs when the specified registry key has changed.
        /// </summary>
        public event EventHandler RegChanged;

        /// <summary>
        /// Raises the <see cref="RegChanged"/> event.
        /// </summary>
        /// <remarks>
        /// <p>
        /// <b>OnRegChanged</b> is called when the specified registry key has changed.
        /// </p>
        /// <note type="inheritinfo">
        /// When overriding <see cref="OnRegChanged"/> in a derived class, be sure to call
        /// the base class's <see cref="OnRegChanged"/> method.
        /// </note>
        /// </remarks>
        private void OnRegChanged()
        {
            EventHandler handler = RegChanged;
            handler?.Invoke(this, null!);
        }

        /// <summary>
        /// Occurs when the access to the registry fails.
        /// </summary>
        public event ErrorEventHandler Error;

        /// <summary>
        /// Raises the <see cref="Error"/> event.
        /// </summary>
        /// <param name="e">The <see cref="Exception"/> which occured while watching the registry.</param>
        /// <remarks>
        /// <p>
        /// <b>OnError</b> is called when an exception occurs while watching the registry.
        /// </p>
        /// <note type="inheritinfo">
        /// When overriding <see cref="OnError"/> in a derived class, be sure to call
        /// the base class's <see cref="OnError"/> method.
        /// </note>
        /// </remarks>
        private void OnError(Exception e)
        {
            SentryHelper.ExceptionHandler(e, SentryHelper.ExceptionType.UnhandledOther);
            ErrorEventHandler handler = Error;
            handler?.Invoke(this, new ErrorEventArgs(e));
        }

        #endregion

        #region Private member variables

        private          HKEYCLASS        _registryHive;
        private          string           _registrySubName;
        private readonly Lock             _threadLock = new();
        private          Thread           _thread;
        private          bool             _disposed;
        private readonly ManualResetEvent _eventTerminate = new(false);

        private RegChangeNotifyFilter _regFilter = RegChangeNotifyFilter.Key | RegChangeNotifyFilter.Attribute |
                                                   RegChangeNotifyFilter.Value | RegChangeNotifyFilter.Security;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistryMonitor"/> class.
        /// </summary>
        /// <param name="registryKey">The registry key to monitor.</param>
        public RegistryMonitor(RegistryKey registryKey)
        {
            InitRegistryKey(registryKey.Name);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistryMonitor"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        public RegistryMonitor(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            InitRegistryKey(name);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistryMonitor"/> class.
        /// </summary>
        /// <param name="registryHive">The registry hive.</param>
        /// <param name="subKey">The sub key.</param>
        public RegistryMonitor(RegistryHive registryHive, string subKey)
        {
            InitRegistryKey(registryHive, subKey);
#if DEBUG
            LogWriteLine($"RegistryMonitor Initialized!\r\n" +
                $"  Hive: {registryHive}\r\n" +
                $"  subKey: {subKey}", LogType.Debug, true);
#endif
        }

        ~RegistryMonitor() => Dispose();

        /// <summary>
        /// Disposes this object.
        /// </summary>
        public void Dispose()
        {
            try
            {
                Stop();
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error at stopping RegistryWatcher!\r\n{ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                throw new Exception($"Error in RegistryMonitor Dispose routine!\r\n{ex}");
            }
            _disposed = true;
#if DEBUG
            LogWriteLine("RegistryMonitor Disposed!", LogType.Debug, true);
#endif
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Gets or sets the <see cref="RegChangeNotifyFilter">RegChangeNotifyFilter</see>.
        /// </summary>
        public RegChangeNotifyFilter RegChangeNotifyType
        {
            get { return _regFilter; }
            set
            {
                lock (_threadLock)
                {
                    if (IsMonitoring)
                        throw new InvalidOperationException("Monitoring thread is already running");

                    _regFilter = value;
                }
            }
        }

        #region Initialization

        private void InitRegistryKey(RegistryHive hive, string name)
        {
            _registryHive = hive switch
                            {
                                RegistryHive.ClassesRoot => HKEYCLASS.HKEY_CLASSES_ROOT,
                                RegistryHive.CurrentConfig => HKEYCLASS.HKEY_CURRENT_CONFIG,
                                RegistryHive.CurrentUser => HKEYCLASS.HKEY_CURRENT_USER,
                                RegistryHive.LocalMachine => HKEYCLASS.HKEY_LOCAL_MACHINE,
                                RegistryHive.PerformanceData => HKEYCLASS.HKEY_PERFORMANCE_DATA,
                                RegistryHive.Users => HKEYCLASS.HKEY_USERS,
                                _ => throw new InvalidEnumArgumentException("hive", (int)hive, typeof(RegistryHive))
                            };
            _registrySubName = name;
        }

        private void InitRegistryKey(string name)
        {
            string[] nameParts = name.Split('\\');

            switch (nameParts[0])
            {
                case "HKEY_CLASSES_ROOT":
                case "HKCR":
                    _registryHive = HKEYCLASS.HKEY_CLASSES_ROOT;
                    break;

                case "HKEY_CURRENT_USER":
                case "HKCU":
                    _registryHive = HKEYCLASS.HKEY_CURRENT_USER;
                    break;

                case "HKEY_LOCAL_MACHINE":
                case "HKLM":
                    _registryHive = HKEYCLASS.HKEY_LOCAL_MACHINE;
                    break;

                case "HKEY_USERS":
                    _registryHive = HKEYCLASS.HKEY_USERS;
                    break;

                case "HKEY_CURRENT_CONFIG":
                    _registryHive = HKEYCLASS.HKEY_CURRENT_CONFIG;
                    break;

                default:
                    _registryHive = HKEYCLASS.None;
                    throw new ArgumentException("The registry hive '" + nameParts[0] + "' is not supported", nameof(name));
            }

            _registrySubName = string.Join("\\", nameParts, 1, nameParts.Length - 1);
        }

        #endregion

        /// <summary>
        /// <b>true</b> if this <see cref="RegistryMonitor"/> object is currently monitoring;
        /// otherwise, <b>false</b>.
        /// </summary>
        public bool IsMonitoring
        {
            get { return _thread != null; }
        }

        /// <summary>
        /// Start monitoring.
        /// </summary>
        public void Start()
        {
            if (_disposed)
                throw new ObjectDisposedException(null, "This instance is already disposed");

            lock (_threadLock)
            {
                if (IsMonitoring)
                {
                    return;
                }

                _eventTerminate.Reset();
                _thread              = new Thread(MonitorThread)
                {
                    IsBackground = true
                };
                _thread.Start();
            }
        }

        /// <summary>
        /// Stops the monitoring thread.
        /// </summary>
        public void Stop()
        {
            if (_disposed) return;

            lock (_threadLock)
            {
                Thread thread = _thread;
                if (thread == null)
                {
                    return;
                }

                _eventTerminate.Set();
                thread.Join();
            }
        }

        private void MonitorThread()
        {
            try
            {
                ThreadLoop();
            }
            catch (Exception e)
            {
                OnError(e);
            }
            _thread = null;
        }

        private void ThreadLoop()
        {
            int result = PInvoke.RegOpenKeyEx(_registryHive, _registrySubName, 0,
                                              (uint)ACCESS_MASK.STANDARD_RIGHTS_READ | (uint)RegKeyAccess.KEY_QUERY_VALUE | (uint)RegKeyAccess.KEY_NOTIFY,
                                              out var registryKey);
            if (result != 0)
                throw new Win32Exception(result);

            WaitHandle[] waitHandles = null;
            try
            {
                AutoResetEvent eventNotify = new AutoResetEvent(false);
                waitHandles = [eventNotify, _eventTerminate];
                while (!_eventTerminate.WaitOne(0, true))
                {
                    if (_disposed) break;
#pragma warning disable CS0618 // Type or member is obsolete
                    result = PInvoke.RegNotifyChangeKeyValue(registryKey, true, _regFilter, eventNotify.Handle, true);
#pragma warning restore CS0618 // Type or member is obsolete
                    if (result != 0)
                        throw new Win32Exception(result);

                    if (WaitHandle.WaitAny(waitHandles) != 0
                        || _regFilter != RegChangeNotifyFilter.Value)
                    {
                        continue;
                    }
                #if DEBUG
                    LogWriteLine($"[RegistryMonitor] Found change(s) in registry!\r\n" +
                                 $"  Hive: {_registryHive}\r\n" +
                                 $"  subName: {_registrySubName}", LogType.Debug, true);
                #endif
                    OnRegChanged();
                }
            }
            finally
            {
                if (registryKey != IntPtr.Zero)
                {
                    ((HResult)PInvoke.RegCloseKey(registryKey)).ThrowOnFailure();
                }

                for (int i = 0; i < waitHandles?.Length; i++) waitHandles[i]?.Dispose();
            }
        }
    }
}
