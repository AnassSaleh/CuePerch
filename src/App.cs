using System;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;

[assembly: AssemblyTitle("FloatPrompter")]
[assembly: AssemblyProduct("FloatPrompter")]
[assembly: AssemblyDescription("Floating teleprompter for Windows, hidden from screen sharing")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]
// Opt in to modern WPF behaviour (per-monitor DPI etc.). Runs on the .NET Framework 4.x built into Windows 10/11.
[assembly: TargetFramework(".NETFramework,Version=v4.7.2", FrameworkDisplayName = ".NET Framework 4.7.2")]

namespace FloatPrompter
{
    public static class App
    {
        public const string EditorTitle = "FloatPrompter";

        public static AppModel Model;
        private static Application app;
        private static ControlWindow editor;
        private static PrompterWindow prompter;
        private static Hotkeys hotkeys;

        // Virtual-key codes
        private const uint VK_P = 0x50, VK_LEFT = 0x25, VK_UP = 0x26, VK_RIGHT = 0x27, VK_DOWN = 0x28;
        private const uint VK_H = 0x48, VK_R = 0x52, VK_V = 0x56;

        [STAThread]
        public static int Main(string[] args)
        {
            bool created;
            using (var mutex = new Mutex(true, "FloatPrompter.SingleInstance.v1", out created))
            {
                if (!created)
                {
                    // Already running: bring the existing editor to the front.
                    IntPtr h = Native.FindWindow(null, EditorTitle);
                    if (h != IntPtr.Zero)
                    {
                        if (Native.IsIconic(h)) Native.ShowWindow(h, 9 /* SW_RESTORE */);
                        Native.SetForegroundWindow(h);
                    }
                    return 0;
                }

                app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.DispatcherUnhandledException += OnUnhandled;
                AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
                {
                    Log("Fatal: " + e.ExceptionObject);
                };

                Model = new AppModel(Settings.Load());
                Model.EditorRequested += ShowEditor;

                prompter = new PrompterWindow(Model);
                editor = new ControlWindow(Model);

                hotkeys = new Hotkeys();
                const uint CA = Native.MOD_CONTROL | Native.MOD_ALT;
                hotkeys.Add(CA, VK_P, false, "Ctrl + Alt + P", "Play / pause", delegate { Model.TogglePlay(); });
                hotkeys.Add(CA, VK_UP, true, "Ctrl + Alt + Up", "Faster", delegate { Model.ChangeSpeed(+1); });
                hotkeys.Add(CA, VK_DOWN, true, "Ctrl + Alt + Down", "Slower", delegate { Model.ChangeSpeed(-1); });
                hotkeys.Add(CA, VK_LEFT, true, "Ctrl + Alt + Left", "Jump back", delegate { Model.Nudge(-2); });
                hotkeys.Add(CA, VK_RIGHT, true, "Ctrl + Alt + Right", "Jump forward", delegate { Model.Nudge(+2); });
                hotkeys.Add(CA, VK_R, false, "Ctrl + Alt + R", "Back to start", delegate { Model.Restart(); });
                hotkeys.Add(CA, VK_H, false, "Ctrl + Alt + H", "Show / hide prompter", delegate { Model.SetPrompterVisible(!Model.PrompterVisible); });
                hotkeys.Add(CA, VK_V, false, "Ctrl + Alt + V", "Voice activation on / off", delegate { Model.ToggleVoice(); });
                editor.SetHotkeys(hotkeys.Entries);

                prompter.Show();
                editor.Show();
                editor.Activate();

                app.Run();
                return 0;
            }
        }

        public static void ShowEditor()
        {
            if (editor == null) return;
            if (!editor.IsVisible) editor.Show();
            if (editor.WindowState == WindowState.Minimized) editor.WindowState = WindowState.Normal;
            editor.Activate();
        }

        public static void Quit()
        {
            try
            {
                Model.SetPlaying(false);
                Model.SaveNow();
                Model.Mic.Stop();
                if (hotkeys != null) hotkeys.Dispose();
                if (prompter != null) prompter.Close();
            }
            catch (Exception ex) { Log("Quit: " + ex); }
            app.Shutdown();
        }

        public static object LoadXaml(string name)
        {
            using (Stream s = typeof(App).Assembly.GetManifestResourceStream("FloatPrompter." + name))
            {
                if (s == null) throw new InvalidOperationException("Missing resource " + name);
                return XamlReader.Load(s);
            }
        }

        public static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(Settings.Dir);
                string path = Path.Combine(Settings.Dir, "log.txt");
                if (File.Exists(path) && new FileInfo(path).Length > 512 * 1024) File.Delete(path);
                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
            }
            catch (Exception) { }
        }

        private static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log("Unhandled: " + e.Exception);
            e.Handled = true;
        }
    }
}
