using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ShakeShake
{
    /// <summary>HKCU\...\Run entry.</summary>
    internal static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "ShakeShake";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (k == null) return false;
                    object v = k.GetValue(ValueName);
                    string s = v as string;
                    if (string.IsNullOrEmpty(s)) return false;
                    return s.IndexOf("\"" + Application.ExecutablePath + "\"", StringComparison.OrdinalIgnoreCase) >= 0
                        || s.IndexOf(Application.ExecutablePath, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (enabled)
                    {
                        k.SetValue(ValueName, "\"" + Application.ExecutablePath + "\" --autostart", RegistryValueKind.String);
                    }
                    else
                    {
                        if (k.GetValue(ValueName) != null) k.DeleteValue(ValueName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("autostart failed: " + ex.Message);
            }
        }
    }
}
