using System;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace ShakeShake
{
    /// <summary>
    /// Custom entry point (the XAML generated Main is disabled via DISABLE_XAML_GENERATED_MAIN)
    /// so the console-style self tests can run without creating any windows.
    /// </summary>
    public static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
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

            try { CursorEngine.RestoreLeftovers(); }
            catch (Exception ex) { Log.Write("leftover check failed: " + ex.Message); }

            AppDomain.CurrentDomain.ProcessExit += delegate
            {
                try { CursorEngine.RestoreLeftovers(); } catch { }
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, System.UnhandledExceptionEventArgs e)
            {
                Log.Write("UnhandledException: " + e.ExceptionObject);
                try { CursorEngine.RestoreLeftovers(); } catch { }
            };

            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(delegate(ApplicationInitializationCallbackParams p)
            {
                DispatcherQueue dispatcher = DispatcherQueue.GetForCurrentThread();
                SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(dispatcher));
                new App();
            });
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
    }
}
