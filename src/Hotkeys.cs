using System;
using System.Collections.Generic;
using System.Windows.Interop;

namespace FloatPrompter
{
    /// <summary>System-wide hotkeys (work even when another app, e.g. Zoom or OBS, is focused).</summary>
    public class Hotkeys : IDisposable
    {
        public class Entry
        {
            public string Keys;
            public string Action;
            public bool Registered;
        }

        private readonly HwndSource source;
        private readonly Dictionary<int, Action> actions = new Dictionary<int, Action>();
        public readonly List<Entry> Entries = new List<Entry>();
        private int nextId = 0x5100;

        public Hotkeys()
        {
            var p = new HwndSourceParameters("FloatPrompterHotkeys")
            {
                Width = 0,
                Height = 0,
                WindowStyle = 0,
                ParentWindow = new IntPtr(-3) // HWND_MESSAGE: invisible message-only window
            };
            source = new HwndSource(p);
            source.AddHook(WndProc);
        }

        public void Add(uint mods, uint vk, bool allowRepeat, string keys, string action, Action handler)
        {
            int id = nextId++;
            uint m = mods | (allowRepeat ? 0 : Native.MOD_NOREPEAT);
            bool ok = Native.RegisterHotKey(source.Handle, id, m, vk);
            if (ok) actions[id] = handler;
            else App.Log("Hotkey " + keys + " is already used by another app");
            Entries.Add(new Entry { Keys = keys, Action = action, Registered = ok });
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Native.WM_HOTKEY)
            {
                Action a;
                if (actions.TryGetValue(wParam.ToInt32(), out a))
                {
                    handled = true;
                    try { a(); } catch (Exception ex) { App.Log("Hotkey action failed: " + ex); }
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            foreach (var id in actions.Keys) Native.UnregisterHotKey(source.Handle, id);
            actions.Clear();
            source.Dispose();
        }
    }
}
