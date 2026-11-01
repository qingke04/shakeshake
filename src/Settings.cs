using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ShakeShake
{
    /// <summary>User settings, stored as a small human-editable INI file.</summary>
    internal sealed class Settings
    {
        public string Language = "auto";     // auto | zh-CN | en-US
        public bool Enabled = true;
        public double Zoom = 3.0;            // 2.0 .. 6.0
        public bool Autostart;
        public int Sensitivity = 5;          // 1 .. 10
        public int RestoreMs = 100;          // 50 .. 500
        public string Hotkey = "Ctrl+Alt+S";
        public List<string> Exclude = new List<string>();

        public static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShakeShake"); }
        }

        public static string FilePath
        {
            get { return Path.Combine(Dir, "settings.ini"); }
        }

        public Settings Clone()
        {
            Settings s = (Settings)MemberwiseClone();
            s.Exclude = new List<string>(Exclude);
            return s;
        }

        public void Sanitize()
        {
            if (Zoom < 2.0) Zoom = 2.0;
            if (Zoom > 6.0) Zoom = 6.0;
            if (Sensitivity < 1) Sensitivity = 1;
            if (Sensitivity > 10) Sensitivity = 10;
            if (RestoreMs < 50) RestoreMs = 50;
            if (RestoreMs > 500) RestoreMs = 500;
            if (string.IsNullOrEmpty(Hotkey)) Hotkey = "Ctrl+Alt+S";
            if (Language != "zh-CN" && Language != "en-US") Language = "auto";
            Exclude.RemoveAll(delegate(string s) { return string.IsNullOrEmpty(s) || string.IsNullOrEmpty(s.Trim()); });
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                if (!File.Exists(FilePath))
                {
                    s.Autostart = AutoStart.IsEnabled();
                    return s;
                }

                string section = "";
                foreach (string raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0) continue;
                    if (line[0] == '#' || line[0] == ';') continue;
                    if (line[0] == '[' && line[line.Length - 1] == ']')
                    {
                        section = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant();
                        continue;
                    }
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string val = line.Substring(eq + 1).Trim();

                    if (section == "general")
                    {
                        if (key == "language") s.Language = val;
                        else if (key == "enabled") s.Enabled = ParseBool(val, true);
                        else if (key == "zoom") s.Zoom = ParseDouble(val, s.Zoom);
                        else if (key == "autostart") s.Autostart = ParseBool(val, s.Autostart);
                    }
                    else if (section == "advanced")
                    {
                        if (key == "sensitivity") s.Sensitivity = ParseInt(val, s.Sensitivity);
                        else if (key == "restore_ms") s.RestoreMs = ParseInt(val, s.RestoreMs);
                        else if (key == "hotkey") s.Hotkey = val;
                    }
                    else if (section == "exclude")
                    {
                        if (key == "app" && val.Length > 0 && !s.Exclude.Contains(val)) s.Exclude.Add(val);
                    }
                }

                s.Sanitize();
                s.Autostart = AutoStart.IsEnabled();
                return s;
            }
            catch (Exception ex)
            {
                Log.Write("settings load failed: " + ex.Message);
                s.Sanitize();
                return s;
            }
        }

        public void Save()
        {
            try
            {
                Sanitize();
                Directory.CreateDirectory(Dir);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# ShakeShake / 晃晃  settings -- edit with care / 可直接修改，但请保持格式");
                sb.AppendLine("[general]");
                sb.AppendLine("language=" + Language);
                sb.AppendLine("enabled=" + (Enabled ? "1" : "0"));
                sb.AppendLine("zoom=" + Zoom.ToString("0.0", CultureInfo.InvariantCulture));
                sb.AppendLine("autostart=" + (Autostart ? "1" : "0"));
                sb.AppendLine();
                sb.AppendLine("[advanced]");
                sb.AppendLine("sensitivity=" + Sensitivity);
                sb.AppendLine("restore_ms=" + RestoreMs);
                sb.AppendLine("hotkey=" + Hotkey);
                sb.AppendLine();
                sb.AppendLine("[exclude]");
                for (int i = 0; i < Exclude.Count; i++) sb.AppendLine("app=" + Exclude[i]);

                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(true));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception ex)
            {
                Log.Write("settings save failed: " + ex.Message);
            }
        }

        private static bool ParseBool(string s, bool fallback)
        {
            if (s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase) || s.Equals("yes", StringComparison.OrdinalIgnoreCase)) return true;
            if (s == "0" || s.Equals("false", StringComparison.OrdinalIgnoreCase) || s.Equals("no", StringComparison.OrdinalIgnoreCase)) return false;
            return fallback;
        }

        private static int ParseInt(string s, int fallback)
        {
            int v;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        private static double ParseDouble(string s, double fallback)
        {
            double v;
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }
    }
}
