using System;
using System.Windows.Forms;

namespace ShakeShake
{
    internal static class Hotkey
    {
        public static bool TryParse(string text, out int mods, out int vk)
        {
            mods = 0;
            vk = 0;
            if (string.IsNullOrEmpty(text)) return false;

            string[] parts = text.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;

            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length == 0) continue;

                if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || p.Equals("Control", StringComparison.OrdinalIgnoreCase)) mods |= Native.MOD_CONTROL;
                else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase)) mods |= Native.MOD_ALT;
                else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase)) mods |= Native.MOD_SHIFT;
                else if (p.Equals("Win", StringComparison.OrdinalIgnoreCase) || p.Equals("Windows", StringComparison.OrdinalIgnoreCase)) mods |= Native.MOD_WIN;
                else
                {
                    vk = KeyCode(p);
                    if (vk == 0) return false;
                }
            }
            return vk != 0;
        }

        public static string Describe(int mods, int vk)
        {
            string s = "";
            if ((mods & Native.MOD_CONTROL) != 0) s += "Ctrl+";
            if ((mods & Native.MOD_ALT) != 0) s += "Alt+";
            if ((mods & Native.MOD_SHIFT) != 0) s += "Shift+";
            if ((mods & Native.MOD_WIN) != 0) s += "Win+";
            if (vk >= 0x30 && vk <= 0x39) return s + (char)vk;
            if (vk >= 0x41 && vk <= 0x5A) return s + (char)vk;
            if (vk >= 0x70 && vk <= 0x87) return s + "F" + (vk - 0x70 + 1);
            return s + "0x" + vk.ToString("X2");
        }

        private static int KeyCode(string p)
        {
            if (p.Length == 1)
            {
                char c = char.ToUpperInvariant(p[0]);
                if (c >= 'A' && c <= 'Z') return c;
                if (c >= '0' && c <= '9') return c;
            }

            if (p.Length >= 2 && (p[0] == 'F' || p[0] == 'f'))
            {
                int n;
                if (int.TryParse(p.Substring(1), out n) && n >= 1 && n <= 24) return 0x70 + n - 1;
            }

            switch (p.ToLowerInvariant())
            {
                case "space": return 0x20;
                case "tab": return 0x09;
                case "esc":
                case "escape": return 0x1B;
                case "enter":
                case "return": return 0x0D;
                case "insert": return 0x2D;
                case "delete": return 0x2E;
                case "home": return 0x24;
                case "end": return 0x23;
                case "pageup": return 0x21;
                case "pagedown": return 0x22;
                case "left": return 0x25;
                case "up": return 0x26;
                case "right": return 0x27;
                case "down": return 0x28;
                case "pause": return 0x13;
                case "printscreen": return 0x2C;
            }
            return 0;
        }
    }
}
