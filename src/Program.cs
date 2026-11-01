using System;
using System.Threading;
using System.Windows.Forms;

namespace ShakeShake
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try { Native.SetProcessDPIAware(); }
            catch { }

            if (HasArg(args, "--selftest"))
            {
                Environment.ExitCode = SelfTest.Run();
                return;
            }

            if (HasArg(args, "--animtest"))
            {
                Environment.ExitCode = AnimTest.Run();
                return;
            }

            bool createdNew;
            using (Mutex mutex = new Mutex(true, @"Local\ShakeShake.SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    SignalExisting();
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    Log.Write("ThreadException: " + e.Exception);
                };

                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    Log.Write("UnhandledException: " + e.ExceptionObject);
                    try { CursorEngine.RestoreLeftovers(); }
                    catch { }
                };

                try { CursorEngine.RestoreLeftovers(); }
                catch (Exception ex) { Log.Write("leftover check failed: " + ex.Message); }

                try
                {
                    Application.Run(new TrayApp(HasArg(args, "--settings")));
                }
                catch (Exception ex)
                {
                    Log.Write("fatal: " + ex);
                    try { CursorEngine.RestoreLeftovers(); }
                    catch { }
                    Environment.ExitCode = 1;
                }
            }
        }

        private static bool HasArg(string[] args, string name)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static void SignalExisting()
        {
            try
            {
                using (EventWaitHandle ev = EventWaitHandle.OpenExisting(@"Local\ShakeShake.Show"))
                {
                    ev.Set();
                }
            }
            catch
            {
                // first instance might still be starting up
            }
        }
    }
}

