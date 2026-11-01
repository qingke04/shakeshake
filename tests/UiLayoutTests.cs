using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace ShakeShake.Tests
{
    /// <summary>
    /// Checks that the settings window adapts its height to the content
    /// (advanced section expand / collapse) instead of using fixed pixel heights.
    /// </summary>
    internal static class UiLayoutTests
    {
        private static int _failed;

        private static void Check(string name, bool ok, string detail)
        {
            Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok ? "" : "  " + detail));
            if (!ok) _failed++;
        }

        private static void Toggle(SettingsForm f)
        {
            MethodInfo m = typeof(SettingsForm).GetMethod("ToggleAdvanced", BindingFlags.NonPublic | BindingFlags.Instance);
            m.Invoke(f, null);
        }

        [STAThread]
        public static int Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Settings s = new Settings();
            L.SetLanguage("zh-CN");

            SettingsForm f = new SettingsForm(s, delegate { }, delegate { });
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(-3000, -3000);
            f.Show();
            Application.DoEvents();

            Size collapsed = f.ClientSize;
            Console.WriteLine("collapsed client = " + collapsed.Width + "x" + collapsed.Height);

            Toggle(f);
            Application.DoEvents();
            Size expanded = f.ClientSize;
            Console.WriteLine("expanded  client = " + expanded.Width + "x" + expanded.Height);

            Toggle(f);
            Application.DoEvents();
            Size collapsed2 = f.ClientSize;
            Console.WriteLine("collapsed client = " + collapsed2.Width + "x" + collapsed2.Height);

            Check("window is reasonably tall", collapsed.Height > 400, "height=" + collapsed.Height);
            Check("expanding grows the window", expanded.Height > collapsed.Height + 80,
                collapsed.Height + " -> " + expanded.Height);
            Check("collapsing restores the window", Math.Abs(collapsed2.Height - collapsed.Height) <= 2,
                collapsed.Height + " -> " + collapsed2.Height);

            // English must not shrink or overflow the content.
            s.Language = "en-US";
            L.SetLanguage("en-US");
            f.SyncFromSettings();
            MethodInfo apply = typeof(SettingsForm).GetMethod("ApplyTexts", BindingFlags.NonPublic | BindingFlags.Instance);
            apply.Invoke(f, null);
            Application.DoEvents();
            Size en = f.ClientSize;
            Console.WriteLine("english   client = " + en.Width + "x" + en.Height);
            Check("english layout is not smaller", en.Width >= collapsed.Width - 2 && en.Height >= collapsed.Height - 2,
                en.Width + "x" + en.Height);

            f.AllowClose();
            f.Close();
            f.Dispose();

            Console.WriteLine();
            Console.WriteLine(_failed == 0 ? "UI layout: ALL PASS" : "UI layout: " + _failed + " FAILED");
            return _failed == 0 ? 0 : 1;
        }
    }
}
